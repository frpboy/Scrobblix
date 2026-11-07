param(
    [string]$AppDirectory = 'artifacts/Scrobblix/desktop',
    [switch]$RequirePrivateRuntime
)
$ErrorActionPreference = 'Stop'
$output = Join-Path $env:RUNNER_TEMP 'scrobblix-ordinary-startup'
New-Item -ItemType Directory -Force $output | Out-Null
$data = Join-Path $env:LOCALAPPDATA 'Scrobblix'
if (Test-Path $data) { throw 'Fresh-profile precondition failed: existing Scrobblix data' }
foreach ($name in @('SCROBBLIX_UI_OUTPUT','SCROBBLIX_UI_FIXTURES','SCROBBLIX_LASTFM_API_KEY','SCROBBLIX_LASTFM_API_SECRET')) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}
$appDirectory = (Resolve-Path $AppDirectory).Path
$app = Join-Path $appDirectory 'Scrobblix.Desktop.exe'
$pri = Join-Path $appDirectory 'Scrobblix.Desktop.pri'
if (-not (Test-Path $pri)) { throw 'Ordinary publish app PRI missing' }
Write-Output "Interactive=$([Environment]::UserInteractive) Session=$((Get-Process -Id $PID).SessionId) AppPRI=$((Get-Item $pri).Length)"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class OrdinaryCapture {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool repaint);
}
'@
$p = Start-Process -FilePath $app -WorkingDirectory $appDirectory -PassThru
try {
    Start-Sleep -Seconds 20
    $p.Refresh()
    if ($p.HasExited) { throw "Ordinary app exited before20s, exit=$($p.ExitCode)" }
    if ($p.MainWindowHandle -eq 0 -or -not $p.Responding) { throw 'Ordinary app has no responsive main window' }
    if ($RequirePrivateRuntime) {
        $privateClr = @($p.Modules | Where-Object { $_.ModuleName -ieq 'coreclr.dll' })
        if ($privateClr.Count -ne 1 -or [IO.Path]::GetDirectoryName($privateClr[0].FileName) -ine $appDirectory) { throw 'CLR was not loaded from delivered app folder' }
        $privateXaml = @($p.Modules | Where-Object { $_.ModuleName -ieq 'Microsoft.UI.Xaml.dll' })
        if ($privateXaml.Count -ne 1 -or [IO.Path]::GetDirectoryName($privateXaml[0].FileName) -ine $appDirectory) { throw 'WinUI was not loaded from delivered app folder' }
        'PASS loaded private coreclr.dll and Microsoft.UI.Xaml.dll from exact extracted ZIP' | Set-Content (Join-Path $output 'private-runtime.txt')
    }
    $screen = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $captureWidth = [Math]::Min(1000,$screen.Width-32)
    $captureHeight = [Math]::Min(740,$screen.Height-32)
    [OrdinaryCapture]::MoveWindow($p.MainWindowHandle,$screen.Left+16,$screen.Top+16,$captureWidth,$captureHeight,$true) | Out-Null
    [OrdinaryCapture]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    $rect = New-Object OrdinaryCapture+RECT
    if (-not [OrdinaryCapture]::GetWindowRect($p.MainWindowHandle,[ref]$rect)) { throw 'Window bounds unavailable' }
    $w=$rect.Right-$rect.Left;$h=$rect.Bottom-$rect.Top
    if ($w -lt 100 -or $h -lt 100) { throw 'Window not visibly sized' }
    $bitmap = New-Object Drawing.Bitmap($w,$h)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
        $bitmap.Save((Join-Path $output 'ordinary-startup.png'),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose();$bitmap.Dispose() }
    if ($p.MainWindowTitle -match 'could not be started|Windows App Runtime') { throw 'Bootstrap dependency dialog, not ordinary application window' }
    $logs = Get-ChildItem (Join-Path $data 'logs') -File
    if (-not $logs) { throw 'No ordinary app startup logs' }
    $text = ($logs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
    # Fresh hosted profile, no credentials or listening data. Preserve only known operational categories.
    $safe = $text -split "`n" | Where-Object { $_ -match '\| (STARTUP|DATABASE|MIGRATION|TRAY|SHUTDOWN|STARTUP_FAILED|STARTUP_ERROR) \|' }
    $safe | Set-Content (Join-Path $output 'startup-operations.txt')
    $safe | ForEach-Object { Write-Output $_ }
    if ($text -notmatch '\| STARTUP \|' -or $text -notmatch '\| DATABASE \|.*initialized') { throw 'Startup/database initialization evidence missing' }
    if ($text -match 'STARTUP_FAILED|STARTUP_ERROR|Startup failed|Application startup failed') { throw 'Startup error in ordinary logs' }
    'PASS ordinary publish responsive20s and actual window capture; forced test-process cleanup, not graceful-close/soak proof' | Set-Content (Join-Path $output 'result.txt')
} finally {
    if (-not $p.HasExited) { $p.Kill();$p.WaitForExit() }
}
