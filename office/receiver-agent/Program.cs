using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using DrawingEncoder = System.Drawing.Imaging.Encoder;

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

var options = AgentOptions.Parse(args);
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

while (!cancellation.IsCancellationRequested)
{
    try
    {
        await RunAgentAsync(options, cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection failed: {DescribeConnectionError(ex)}");
        Console.WriteLine("Retrying in 3 seconds...");
        await Task.Delay(TimeSpan.FromSeconds(3), cancellation.Token);
    }
}

static string DescribeConnectionError(Exception ex)
{
    if (ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase))
    {
        return "Unauthorized (401). The code in hras-agent.json must match HRAS_ACCESS_CODE on the office server.";
    }

    if (ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase))
    {
        return "Not found (404). Make sure the office server is running the latest server.js with the /frames endpoint.";
    }

    return ex.Message;
}

static async Task RunAgentAsync(AgentOptions options, CancellationToken cancellationToken)
{
    using var socket = new ClientWebSocket();
    var endpoint = options.BuildEndpoint();

    Console.WriteLine($"Connecting to {endpoint}");
    await socket.ConnectAsync(endpoint, cancellationToken);
    Console.WriteLine("Connected. Screen sharing is active. Press Ctrl+C to stop.");

    var metadata = JsonSerializer.Serialize(new
    {
        type = "agent-ready",
        machine = Environment.MachineName,
        fps = options.Fps,
        quality = options.Quality,
    });
    await socket.SendAsync(
        Encoding.UTF8.GetBytes(metadata),
        WebSocketMessageType.Text,
        true,
        cancellationToken);

    var frameDelay = TimeSpan.FromMilliseconds(1000.0 / options.Fps);

    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var frame = CaptureJpeg(options);

        await socket.SendAsync(
            frame,
            WebSocketMessageType.Binary,
            true,
            cancellationToken);

        var elapsed = DateTimeOffset.UtcNow - startedAt;
        if (elapsed < frameDelay)
        {
            await Task.Delay(frameDelay - elapsed, cancellationToken);
        }
    }
}

static byte[] CaptureJpeg(AgentOptions options)
{
    var screens = Screen.AllScreens;
    var screenIndex = Math.Clamp(options.Monitor, 0, screens.Length - 1);
    var bounds = screens[screenIndex].Bounds;

    using var source = new Bitmap(bounds.Width, bounds.Height);
    using (var graphics = Graphics.FromImage(source))
    {
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
    }

    using var output = ResizeIfNeeded(source, options.MaxWidth);
    using var stream = new MemoryStream();
    var encoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
    using var encoderParameters = new EncoderParameters(1);
    encoderParameters.Param[0] = new EncoderParameter(DrawingEncoder.Quality, options.Quality);
    output.Save(stream, encoder, encoderParameters);
    return stream.ToArray();
}

static Bitmap ResizeIfNeeded(Bitmap source, int maxWidth)
{
    if (maxWidth <= 0 || source.Width <= maxWidth)
    {
        return new Bitmap(source);
    }

    var ratio = maxWidth / (double)source.Width;
    var width = maxWidth;
    var height = Math.Max(1, (int)Math.Round(source.Height * ratio));
    var resized = new Bitmap(width, height);

    using var graphics = Graphics.FromImage(resized);
    graphics.CompositingQuality = CompositingQuality.HighSpeed;
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.SmoothingMode = SmoothingMode.HighSpeed;
    graphics.DrawImage(source, 0, 0, width, height);
    return resized;
}

sealed record AgentOptions(
    string Server,
    string Room,
    string Code,
    int Fps,
    long Quality,
    int MaxWidth,
    int Monitor)
{
    public static AgentOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var config = AgentConfig.Load();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal)) continue;

            var key = arg[2..];
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = args[++index];
            }
        }

        return new AgentOptions(
            Get(values, "server", config?.Server ?? "ws://127.0.0.1:8080"),
            Get(values, "room", config?.Room ?? "head-office"),
            Get(values, "code", config?.Code ?? "change-me"),
            ClampInt(Get(values, "fps", config?.Fps?.ToString() ?? "5"), 1, 15),
            ClampInt(Get(values, "quality", config?.Quality?.ToString() ?? "55"), 20, 90),
            ClampInt(Get(values, "max-width", config?.MaxWidth?.ToString() ?? "1280"), 640, 3840),
            ClampInt(Get(values, "monitor", config?.Monitor?.ToString() ?? "0"), 0, 8));
    }

    public Uri BuildEndpoint()
    {
        var server = Server.TrimEnd('/');

        if (server.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            server = "ws://" + server["http://".Length..];
        }
        else if (server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            server = "wss://" + server["https://".Length..];
        }

        if (!server.EndsWith("/frames", StringComparison.OrdinalIgnoreCase))
        {
            server += "/frames";
        }

        var separator = server.Contains('?') ? '&' : '?';
        var url = $"{server}{separator}role=agent&room={Uri.EscapeDataString(Room)}&code={Uri.EscapeDataString(Code)}";
        return new Uri(url);
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static int ClampInt(string value, int min, int max)
    {
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : min;
    }
}

sealed class AgentConfig
{
    public string? Server { get; set; }
    public string? Room { get; set; }
    public string? Code { get; set; }
    public int? Fps { get; set; }
    public long? Quality { get; set; }
    public int? MaxWidth { get; set; }
    public int? Monitor { get; set; }

    public static AgentConfig? Load()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "hras-agent.json"),
            Path.Combine(Environment.CurrentDirectory, "hras-agent.json"),
        };

        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path)) continue;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AgentConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        return null;
    }
}
