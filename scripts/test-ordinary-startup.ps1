param(
    [string]$AppDirectory = 'artifacts/Scrobblix/desktop',
    [switch]$RequirePrivateRuntime,
    [switch]$SyntheticLegacyProfile
)
$ErrorActionPreference = 'Stop'
$output = Join-Path $env:RUNNER_TEMP $(if ($SyntheticLegacyProfile) { 'scrobblix-legacy-startup' } else { 'scrobblix-ordinary-startup' })
New-Item -ItemType Directory -Force $output | Out-Null
$data = Join-Path $env:LOCALAPPDATA 'Scrobblix'
if ($SyntheticLegacyProfile) {
    $marker = Join-Path $data 'synthetic-ci-fixture.marker'
    if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or -not (Test-Path $marker) -or (Get-Content $marker -Raw) -cne 'scrobblix-known-lf-legacy-fixture-v1') {
        throw 'Synthetic hosted-CI legacy profile precondition failed'
    }
} elseif (Test-Path $data) { throw 'Fresh-profile precondition failed: existing Scrobblix data' }
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
$priorLogPaths = @(Get-ChildItem (Join-Path $data 'logs') -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
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
    if ($AppDirectory -like '*Programs*Scrobblix*') {
        $bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
        $full = New-Object Drawing.Bitmap($bounds.Width,$bounds.Height)
        $canvas = [Drawing.Graphics]::FromImage($full)
        try { $canvas.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$full.Size); $full.Save((Join-Path $output 'installed-taskbar.png')) }
        finally { $canvas.Dispose();$full.Dispose() }
        [Windows.Forms.SendKeys]::SendWait('^{ESC}')
        Start-Sleep -Milliseconds 800
        [Windows.Forms.SendKeys]::SendWait('Scrobblix')
        Start-Sleep -Seconds 8
        $full = New-Object Drawing.Bitmap($bounds.Width,$bounds.Height)
        $canvas = [Drawing.Graphics]::FromImage($full)
        try { $canvas.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$full.Size); $full.Save((Join-Path $output 'installed-start-search.png')) }
        finally { $canvas.Dispose();$full.Dispose();[Windows.Forms.SendKeys]::SendWait('{ESC}') }
    }
    if ($AppDirectory -like '*Programs*Scrobblix*') {
        # Search can be unavailable on hosted Server desktop images; also show the actual Start shortcut in its owning folder.
        $startLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'Scrobblix.lnk'
        Start-Process explorer.exe -ArgumentList "/select,`"$startLink`""
        Start-Sleep -Seconds 4
        $bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
        $full = New-Object Drawing.Bitmap($bounds.Width,$bounds.Height)
        $canvas = [Drawing.Graphics]::FromImage($full)
        try { $canvas.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$full.Size); $full.Save((Join-Path $output 'installed-start-shortcut.png')) }
        finally { $canvas.Dispose();$full.Dispose() }
    }
    if ($p.MainWindowTitle -match 'could not be started|Windows App Runtime') { throw 'Bootstrap dependency dialog, not ordinary application window' }
    # A retained log from a prior launch must not satisfy this launch's gate.
    $logs = @(Get-ChildItem (Join-Path $data 'logs') -File | Where-Object {
        $_.Name -match "^scrobblix-[0-9]{8}-[0-9]{6}-$($p.Id)\.log$" -and $_.FullName -notin $priorLogPaths
    })
    if ($logs.Count -ne 1) { throw 'Expected exactly one new log for launched process' }
    $text = Get-Content $logs[0].FullName -Raw
    if ($text -notmatch "Starting Scrobblix; process_id=$($p.Id);") { throw 'Startup log process identity mismatch' }
    $readStartupMarkerElapsed = {
        param([string]$LogText, [string]$Code)
        $matchesForCode = [regex]::Matches($LogText, "(?m)^.*\| STARTUP \| reason_code=startup\.$Code;[^\r\n]*\r?$")
        if ($matchesForCode.Count -ne 1) { throw "Startup marker must occur exactly once: $Code" }
        $elapsedMatch = [regex]::Match($matchesForCode[0].Value, "reason_code=startup\.$Code; elapsed_ms=([0-9]{1,8});")
        if (-not $elapsedMatch.Success) { throw "Startup marker elapsed format invalid: $Code" }
        $elapsed = [long]::Parse($elapsedMatch.Groups[1].Value)
        if ($elapsed -gt 3600000) { throw "Startup marker exceeds export bound: $Code" }
        return $elapsed
    }
    # Exercise the exact gate parser, including malformed duplicate markers that
    # must not disappear merely because their elapsed value fails the format.
    $validMarker = '000001 | 2026-01-01T00:00:00Z | INFO | STARTUP | reason_code=startup.root_loaded; elapsed_ms=12;'
    foreach ($newline in @("`n", "`r`n")) {
        if ((& $readStartupMarkerElapsed "$validMarker$newline" 'root_loaded') -ne 12) { throw 'Startup marker positive fixture failed' }
        $otherMarker = $validMarker.Replace('startup.root_loaded;', 'startup.window_created;')
        if ((& $readStartupMarkerElapsed "$otherMarker$newline$validMarker$newline$otherMarker$newline" 'root_loaded') -ne 12) { throw 'Surrounding unrelated markers changed target parsing' }
        $missingElapsed = $validMarker.Replace(' elapsed_ms=12;', '')
        foreach ($fixture in @("$missingElapsed$newline", "$validMarker$newline$missingElapsed$newline")) {
            $rejected = $false
            try { & $readStartupMarkerElapsed $fixture 'root_loaded' | Out-Null }
            catch { $rejected = $true }
            if (-not $rejected) { throw 'Absent elapsed field or duplicate with absent field was accepted' }
        }
        foreach ($badElapsed in @('-1','999999999','3600001','missing')) {
            $badMarker = $validMarker.Replace('elapsed_ms=12;', "elapsed_ms=$badElapsed;")
            foreach ($fixture in @("$badMarker$newline", "$validMarker$newline$badMarker$newline")) {
                $rejected = $false
                try { & $readStartupMarkerElapsed $fixture 'root_loaded' | Out-Null }
                catch { $rejected = $true }
                if (-not $rejected) { throw 'Malformed or duplicate startup marker fixture was accepted' }
            }
        }
        foreach ($fixture in @("", "$validMarker$newline$validMarker$newline")) {
            $rejected = $false
            try { & $readStartupMarkerElapsed $fixture 'root_loaded' | Out-Null }
            catch { $rejected = $true }
            if (-not $rejected) { throw 'Missing or repeated valid startup marker fixture was accepted' }
        }
    }
    $milestones = @{}
    foreach ($code in @('app_constructor','window_created','root_loaded','first_render_tick')) {
        $milestones[$code] = & $readStartupMarkerElapsed $text $code
    }
    if ($milestones.app_constructor -ne 0 -or $milestones.window_created -gt $milestones.root_loaded -or $milestones.root_loaded -gt $milestones.first_render_tick) { throw 'Startup scheduling milestone order invalid' }
    # These are instrumented callbacks including logger overhead, not unmodified first-pixel latency.
    # Fresh hosted profile, no credentials or listening data. Preserve only known operational categories.
    $safe = $text -split "`n" | Where-Object { $_ -match '\| (STARTUP|DATABASE|MIGRATION|TRAY|SHUTDOWN|STARTUP_FAILED|STARTUP_ERROR) \|' }
    $safe | Set-Content (Join-Path $output 'startup-operations.txt')
    $safe | ForEach-Object { Write-Output $_ }
    if ($text -notmatch '\| STARTUP \|' -or $text -notmatch '\| DATABASE \|.*initialized') { throw 'Startup/database initialization evidence missing' }
    if ($text -notmatch 'reason_code=startup.root_loaded;' -or $text -notmatch 'reason_code=startup.first_render_tick;') { throw 'Loaded/render scheduling milestones missing; responsive20s is not first-frame evidence' }
    if ($text -match 'STARTUP_FAILED|STARTUP_ERROR|Startup failed|Application startup failed') { throw 'Startup error in ordinary logs' }
    'PASS ordinary publish responsive20s and actual window capture; forced test-process cleanup, not graceful-close/soak proof' | Set-Content (Join-Path $output 'result.txt')
} finally {
    if (-not $p.HasExited) { $p.Kill();$p.WaitForExit() }
}
