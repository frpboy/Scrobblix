param([Parameter(Mandatory)][string]$PackageZip,
      [Parameter(Mandatory)][string]$DriverExe)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$expectedZip = '5191988d22cd2c9c6ee8c6942dec43dd85d1882115790ef4123d8f06de604afc'
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_OS -cne 'Windows' -or
    $env:GITHUB_RUN_ATTEMPT -cne '1' -or -not [Environment]::UserInteractive) {
    throw 'host_guard_failed'
}
$data = Join-Path $env:LOCALAPPDATA 'Scrobblix'
if (Test-Path $data) { throw 'existing_profile_stop_no_reset' }
if (@(Get-Process -Name 'Scrobblix.Desktop' -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'existing_app_process_stop'
}
if ((Get-FileHash $PackageZip -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedZip) {
    throw 'delivered_zip_hash_mismatch'
}
$root = Join-Path $env:RUNNER_TEMP ('scrobblix-local-core-' + [guid]::NewGuid().ToString('N'))
$output = Join-Path $env:RUNNER_TEMP 'scrobblix-local-core-evidence'
if (Test-Path $output) { throw 'evidence_destination_already_exists' }
New-Item -ItemType Directory $root,$output | Out-Null
$extract = Join-Path $root 'package'
# Exact already audited fixed archive. No download URL supplied by ZIP contents.
Expand-Archive -LiteralPath $PackageZip -DestinationPath $extract
$appDirectory = Join-Path $extract 'desktop'
$app = Join-Path $appDirectory 'Scrobblix.Desktop.exe'
if (-not (Test-Path $app) -or -not (Test-Path (Join-Path $appDirectory 'Scrobblix.Desktop.pri'))) {
    throw 'ordinary_package_layout_missing'
}
if (Test-Path (Join-Path $appDirectory '.env')) { throw 'unexpected_local_provider_env_file' }
$initialHashes = @{}
Get-ChildItem $extract -Recurse -File | ForEach-Object {
    $initialHashes[$_.FullName] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
}
$DriverExe = (Resolve-Path $DriverExe).Path
$driverDirectory = Join-Path $root 'driver-controls'
New-Item -ItemType Directory $driverDirectory | Out-Null
foreach ($name in @('SCROBBLIX_UI_OUTPUT','SCROBBLIX_UI_FIXTURES',
    'SCROBBLIX_LASTFM_API_KEY','SCROBBLIX_LASTFM_API_SECRET')) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}
[Environment]::SetEnvironmentVariable('SCROBBLIX_BROWSER_PIPE',('scrobblix.local-core.'+[guid]::NewGuid().ToString('N')),'Process')
# No account secret is provided to either process. Host has no user profile copied in.
# Domain/public/private profiles must already be enabled. Do not weaken any profile.
$profiles = @(Get-NetFirewallProfile -PolicyStore ActiveStore)
if ($profiles.Count -ne 3 -or @($profiles | Where-Object { -not $_.Enabled }).Count -ne 0) {
    throw 'guest_firewall_not_enabled_stop'
}
$ruleName = 'ScrobblixLocalCore-' + [guid]::NewGuid().ToString('N')
$p = $null; $driver = $null; $ruleCreated = $false
$phase = 'firewall'
$success = $false
$db = Join-Path $data 'scrobblix.db'
$parser = Join-Path $PSScriptRoot 'read-state.py'
function Read-State([string]$stage, [string]$file, [string]$previous = '') {
    $arguments = @($parser,'--db',$db,'--stage',$stage,'--output',(Join-Path $output $file))
    if ($previous) { $arguments += @('--previous',(Join-Path $output $previous)) }
    & python @arguments 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}
function Wait-State([string]$stage,[string]$file,[int]$seconds) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $seconds) {
        $p.Refresh()
        if ($p.HasExited -or -not $p.Responding) { throw 'ordinary_app_unresponsive_or_exited' }
        if ($null -ne $driver) { $driver.Refresh(); if ($driver.HasExited) { throw 'synthetic_driver_exited_early' } }
        if (Read-State $stage $file) { return }
        Start-Sleep -Milliseconds 750
    }
    throw ('state_deadline_' + $stage)
}
try {
    New-NetFirewallRule -Name $ruleName -DisplayName $ruleName -Direction Outbound `
        -Program $app -Action Block -Profile Any -Enabled True | Out-Null
    $ruleCreated = $true
    $effective = @(Get-NetFirewallRule -PolicyStore ActiveStore -Name $ruleName)
    if ($effective.Count -ne 1 -or $effective[0].Enabled -ne 'True' -or
        $effective[0].Action -ne 'Block' -or $effective[0].Direction -ne 'Outbound' -or
        [string]$effective[0].Profile -ne 'Any') { throw 'guest_egress_block_not_effective' }
    $filter = @($effective[0] | Get-NetFirewallApplicationFilter)
    if ($filter.Count -ne 1 -or $filter[0].Program -ine $app) { throw 'guest_egress_program_mismatch' }
    $phase = 'baseline'
    $p = Start-Process -FilePath $app -WorkingDirectory $appDirectory -PassThru
    Wait-State 'baseline' 'baseline.json' 30
    # Driver is launched only AFTER readonly DB confirms no account/action/listening rows.
    $phase = 'native_playing_to_eligible'
    $realPlayback = [Diagnostics.Stopwatch]::StartNew()
    $driver = Start-Process -FilePath $DriverExe -ArgumentList ('"' + $driverDirectory + '"') -PassThru
    Wait-State 'eligible' 'eligible.json' 70
    if ($realPlayback.Elapsed.TotalSeconds -lt 45 -or $realPlayback.Elapsed.TotalSeconds -gt 75) {
        throw 'realtime_eligibility_window_invalid'
    }
    $phase = 'actual_playing_ui'
    Add-Type -AssemblyName System.Drawing
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LocalCoreWindow {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool repaint);
}
'@
    function Window-Root {
        $p.Refresh()
        if ($p.MainWindowHandle -eq 0) { throw 'ordinary_window_missing' }
        return [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
    }
    function Assert-SyntheticVisible([bool]$historyPage = $false) {
        $window = Window-Root
        $condition = [Windows.Automation.PropertyCondition]::new(
            [Windows.Automation.AutomationElement]::NameProperty,$(if ($historyPage) { 'Scrobblix synthetic local-core artist · Scrobblix synthetic local-core 90s' } else { 'Scrobblix synthetic local-core 90s' }))
        $title = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
        if ($null -eq $title -or $title.Current.IsOffscreen) { throw 'synthetic_title_not_visible_in_actual_ui' }
    }
    function Capture-Window([string]$name) {
        $p.Refresh()
        if ($p.HasExited -or -not $p.Responding) { throw 'ordinary_window_not_responsive' }
        $screen = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        [LocalCoreWindow]::MoveWindow($p.MainWindowHandle,$screen.Left+16,$screen.Top+16,
            [Math]::Min(1000,$screen.Width-32),[Math]::Min(740,$screen.Height-32),$true) | Out-Null
        [LocalCoreWindow]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
        Start-Sleep -Milliseconds 500
        if ([LocalCoreWindow]::GetForegroundWindow() -ne $p.MainWindowHandle) { throw 'capture_foreground_not_owned' }
        $rect = New-Object LocalCoreWindow+RECT
        if (-not [LocalCoreWindow]::GetWindowRect($p.MainWindowHandle,[ref]$rect)) { throw 'window_bounds_missing' }
        $w=$rect.Right-$rect.Left; $h=$rect.Bottom-$rect.Top
        if ($w -lt 200 -or $h -lt 200 -or $rect.Left -lt $screen.Left -or $rect.Top -lt $screen.Top -or
            $rect.Right -gt $screen.Right -or $rect.Bottom -gt $screen.Bottom) { throw 'window_bounds_not_safe' }
        $bitmap = New-Object Drawing.Bitmap($w,$h)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
            $bitmap.Save((Join-Path $output $name),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    Assert-SyntheticVisible
    Capture-Window 'ordinary-playing.png'
    $phase = 'native_paused'
    New-Item -ItemType File (Join-Path $driverDirectory 'pause') | Out-Null
    Wait-State 'paused' 'paused.json' 12
    Start-Sleep -Seconds 7
    if (-not (Read-State 'stable' 'stable.json' 'paused.json')) { throw 'paused_time_not_stable' }
    $phase = 'actual_history_ui'
    $window = Window-Root
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty,'History')
    $historyItems = $window.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)
    $navigated = $false
    foreach ($item in $historyItems) {
        if ($item.Current.IsOffscreen -or -not $item.Current.IsEnabled) { continue }
        $pattern = $null
        if ($item.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)) {
            $pattern.Select()
            if (-not $pattern.Current.IsSelected) { throw 'history_navigation_not_selected' }
            $navigated = $true
            break
        }
        # Narrow ordinary windows use the app's visible compact button rail.
        if ($item.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
            $item.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)) {
            $pattern.Invoke()
            $navigated = $true
            break
        }
    }
    if (-not $navigated) { throw 'history_navigation_supported_pattern_unavailable' }
    Start-Sleep -Seconds 1
    Assert-SyntheticVisible $true
    Capture-Window 'ordinary-history.png'
    $phase = 'package_identity_after_runtime'
    $afterFiles = @(Get-ChildItem $extract -Recurse -File)
    if ($afterFiles.Count -ne $initialHashes.Count) { throw 'package_inventory_changed' }
    foreach ($file in $afterFiles) {
        if (-not $initialHashes.ContainsKey($file.FullName) -or
            (Get-FileHash $file.FullName -Algorithm SHA256).Hash -cne $initialHashes[$file.FullName]) {
            throw 'package_bytes_changed'
        }
    }
    $success = $true
} catch {
    # Fixed phase only, no unbounded exception, paths, config, DB or logs.
    ('{"status":"FAIL","phase":"' + $phase + '"}') | Set-Content (Join-Path $output 'result.json')
    throw ('local_core_qualification_failed_at_' + $phase)
} finally {
    # Cleanup errors cannot masquerade as a successful proof.
    $cleanupOK = $true
    if ($null -ne $driver) {
        try {
            New-Item -ItemType File (Join-Path $driverDirectory 'stop') -Force | Out-Null
            if (-not $driver.WaitForExit(3000)) { $driver.Kill(); $driver.WaitForExit() }
        } catch { $cleanupOK = $false }
    }
    if ($null -ne $p) {
        try { $p.Refresh(); if (-not $p.HasExited) { $p.Kill(); $p.WaitForExit() } }
        catch { $cleanupOK = $false }
    }
    if ($ruleCreated) {
        try { Remove-NetFirewallRule -Name $ruleName }
        catch { $cleanupOK = $false }
    }
    # No profile reset/deletion. Disposable hosted guest retains synthetic rows until teardown.
    if (-not $cleanupOK) {
        '{"status":"FAIL","phase":"cleanup"}' | Set-Content (Join-Path $output 'result.json')
        throw 'local_core_cleanup_failed'
    }
}
if ($success) {
    [ordered]@{
        status='ASSERTIONS_PASS_VISUAL_REVIEW_PENDING'; source_commit='da37da0caa8edf5653a9b02483a4e37d739179cf';
        package_sha256=$expectedZip; package_bytes=(Get-Item $PackageZip).Length;
        app_sha256=(Get-FileHash $app -Algorithm SHA256).Hash.ToLowerInvariant();
        driver_sha256=(Get-FileHash $DriverExe -Algorithm SHA256).Hash.ToLowerInvariant();
        probe_head=$env:GITHUB_SHA; run_id=$env:GITHUB_RUN_ID; run_attempt=$env:GITHUB_RUN_ATTEMPT;
        playback_wall_seconds=[Math]::Round($realPlayback.Elapsed.TotalSeconds,2);
        scope='ordinary app synthetic local GSMTC capture/history/eligibility/pause only';
        account_count=0; provider_action_count=0; egress='guest firewall application outbound block';
        cleanup='owned processes stopped; guest-only rule removed; no graceful-close/soak claim';
        not_proven=@('provider delivery or acknowledgement','installed browser extension path','all real players','full beta readiness')
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output 'result.json')
}
