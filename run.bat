@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo     Project Renamer and Builder
echo ===================================================

:: Navigate to the C# project directory
cd /d "%~dp0office\receiver-agent" || (
    echo Error: Could not find office\receiver-agent directory.
    pause
    exit /b 1
)

echo.
echo [1/3] Clearing old build cache fully...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
dotnet clean >nul 2>&1
echo Cache cleared.
echo.

set /p PROJECT_NAME="Enter new project name (e.g. MyProject): "
set /p APP_NAME="Enter new app name (e.g. MyApp): "
set /p TRAY_NAME="Enter new tray name: "
set /p WINDOW_NAME="Enter normal window name: "

echo.
echo [2/3] Updating project files...

for %%f in (*.csproj) do set "CSPROJ_FILE=%%f"

for /f "usebackq delims=" %%i in (`powershell -Command "$m = [regex]::Match((Get-Content '!CSPROJ_FILE!' -Raw), '<AssemblyName>(.*?)</AssemblyName>'); if ($m.Success) { Write-Output $m.Groups[1].Value }"`) do set "CURRENT_APP=%%i"
if "!CURRENT_APP!"=="" set "CURRENT_APP=HRAS-Receiver-Agent"

:: Create a temporary PowerShell script to handle the replacements safely
set "PS_SCRIPT=%TEMP%\rename_script_%RANDOM%.ps1"
(
echo $projFile = '!CSPROJ_FILE!'
echo $c = Get-Content $projFile -Raw
echo $c = $c -replace '^<AssemblyName^>.*?^</AssemblyName^>', '^<AssemblyName^>%APP_NAME%^</AssemblyName^>'
echo $c = $c -replace '^<RootNamespace^>.*?^</RootNamespace^>', '^<RootNamespace^>%PROJECT_NAME%^</RootNamespace^>'
echo if ^($c -match '^<Product^>.*?^</Product^>'^) {
echo     $c = $c -replace '^<Product^>.*?^</Product^>', '^<Product^>%WINDOW_NAME%^</Product^>'
echo } else {
echo     $c = $c -replace '^</PropertyGroup^>', "    <Product>%WINDOW_NAME%</Product>`r`n  </PropertyGroup>"
echo }
echo if ^($c -match '^<Title^>.*?^</Title^>'^) {
echo     $c = $c -replace '^<Title^>.*?^</Title^>', '^<Title^>%WINDOW_NAME%^</Title^>'
echo } else {
echo     $c = $c -replace '^</PropertyGroup^>', "    <Title>%WINDOW_NAME%</Title>`r`n  </PropertyGroup>"
echo }
echo Set-Content $projFile -Value $c
echo $progFile = 'Program.cs'
echo $c = Get-Content $progFile -Raw
echo $c = $c -replace 'Text = ".*?"', 'Text = "%TRAY_NAME%"'
echo $c = $c -replace 'Local\\[^"]*', 'Local\%APP_NAME%'
echo Set-Content $progFile -Value $c
echo Get-ChildItem -Filter *.ps1 ^| ForEach-Object {
echo     $c = Get-Content $_.FullName -Raw
echo     $c = $c -replace '!CURRENT_APP!', '%APP_NAME%'
echo     Set-Content $_.FullName -Value $c
echo }
) > "!PS_SCRIPT!"

powershell -ExecutionPolicy Bypass -File "!PS_SCRIPT!"
del "!PS_SCRIPT!"

if not "!CSPROJ_FILE!"=="%PROJECT_NAME%.csproj" (
    ren "!CSPROJ_FILE!" "%PROJECT_NAME%.csproj"
)

echo Files updated successfully.

echo.
echo [3/3] Building project...
dotnet publish -c Release -r win-x64 --self-contained false

echo.
echo Build complete! The new executable is in bin\Release\net10.0-windows\win-x64\publish\
pause
