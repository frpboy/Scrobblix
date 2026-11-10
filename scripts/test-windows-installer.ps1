param([Parameter(Mandatory=$true)][string]$Installer,[Parameter(Mandatory=$true)][string]$PayloadDirectory)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') { throw 'Disposable Windows CI only' }
$output = Join-Path $env:RUNNER_TEMP 'scrobblix-installer-qualification'
New-Item -ItemType Directory -Force $output | Out-Null
$install = Join-Path $env:LOCALAPPDATA 'Programs/Scrobblix'
$data = Join-Path $env:LOCALAPPDATA 'Scrobblix'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Scrobblix.lnk'
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrpBoy.Scrobblix.Windows_is1'
if ((Test-Path $install) -or (Test-Path $data) -or (Test-Path $registry) -or (Test-Path $shortcut)) { throw 'Fresh installer runner precondition failed' }
$Installer = (Resolve-Path $Installer).Path
function Run-Setup([string]$Exe,[string]$Name,[switch]$ExpectBlocked) {
    $log = Join-Path $output "$Name.log"
    $p = Start-Process -FilePath $Exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$log`"") -PassThru
    if (-not $p.WaitForExit(120000)) { $p.Kill(); throw 'Installer timed out' }
    if ($ExpectBlocked) { if ($p.ExitCode -eq 0) { throw 'Running-app safety gate failed' } }
    elseif ($p.ExitCode -ne 0) { throw "Installer failed $Name exit=$($p.ExitCode)" }
}
function Check-Payload {
    Get-ChildItem $PayloadDirectory -File -Recurse | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath((Resolve-Path $PayloadDirectory).Path,$_.FullName)
        $actual = Join-Path $install $relative
        if (-not (Test-Path $actual) -or (Get-FileHash $_.FullName).Hash -ne (Get-FileHash $actual).Hash) { throw "Installed byte mismatch: $relative" }
    }
    if (-not (Test-Path $registry) -or -not (Test-Path $shortcut)) { throw 'Registration/Start menu shortcut missing' }
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcut).TargetPath -ine (Join-Path $install 'desktop/Scrobblix.Desktop.exe')) { throw 'Shortcut target incorrect' }
}
# Capture the real setup wizard without completing it, then close only that process.
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class SetupCapture {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@
$wizard = Start-Process $Installer -PassThru
try {
    Start-Sleep -Seconds 4
    $visible = Get-Process | Where-Object { $_.ProcessName -like 'Scrobblix-Windows-x64-Setup*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $visible) { throw 'Actual installer wizard not visible' }
    [SetupCapture]::SetForegroundWindow($visible.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    $rect = New-Object SetupCapture+RECT
    if (-not [SetupCapture]::GetWindowRect($visible.MainWindowHandle,[ref]$rect)) { throw 'Setup bounds missing' }
    $bitmap = New-Object Drawing.Bitmap(($rect.Right-$rect.Left),($rect.Bottom-$rect.Top))
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size);$bitmap.Save((Join-Path $output 'installer-wizard.png')) }
    finally { $graphics.Dispose();$bitmap.Dispose() }
} finally {
    # Only test-owned setup processes, before any install was accepted.
    Get-Process | Where-Object { $_.ProcessName -like 'Scrobblix-Windows-x64-Setup*' } | Stop-Process -Force
}
Run-Setup $Installer 'fresh-install'
Check-Payload
./scripts/test-ordinary-startup.ps1 -AppDirectory (Join-Path $install 'desktop') -RequirePrivateRuntime
Copy-Item "$env:RUNNER_TEMP/scrobblix-ordinary-startup/ordinary-startup.png" "$output/installed-app.png"
Move-Item $data (Join-Path $env:RUNNER_TEMP 'scrobblix-installer-fresh-profile-preserved')
python scripts/test-installer-profile.py
if ($LASTEXITCODE -ne 0) { throw 'Synthetic profile fixture failed' }
# First installer generation is beta8 payload, compiled by the same reviewed installer recipe.
Run-Setup (Join-Path $install 'unins000.exe') 'remove-fresh-program-only'
python scripts/test-legacy-profile.py verify
if ($LASTEXITCODE -ne 0) { throw 'Uninstall changed legacy fixture' }
$baselineZip = Join-Path $env:RUNNER_TEMP 'scrobblix-beta8.zip'
Invoke-WebRequest 'https://github.com/frpboy/Scrobblix/releases/download/windows-v0.1.0-beta.8/Scrobblix-Windows-x64-windows-v0.1.0-beta.8.zip' -OutFile $baselineZip
if ((Get-FileHash $baselineZip).Hash -ine '72f34ca18b8fa7db1671aa1ed04e95bb571136d8f857933c1a21bac93ef418c4') { throw 'Baseline byte identity mismatch' }
$baseline = Join-Path $env:RUNNER_TEMP 'scrobblix-beta8-payload'
Expand-Archive $baselineZip $baseline
./scripts/build-windows-installer.ps1 -BundleDirectory $baseline -OutputDirectory artifacts/installer-baseline -BuildVersion '0.1.0.8' -BuildLabel 'synthetic baseline from verified beta8'
Run-Setup (Resolve-Path 'artifacts/installer-baseline/Scrobblix-Windows-x64-Setup.exe').Path 'baseline-install'
python scripts/test-legacy-profile.py verify
if ($LASTEXITCODE -ne 0) { throw 'Baseline install changed profile' }
# Prove the actual running app blocks an upgrade and uninstall without force-closing it.
$p = Start-Process (Join-Path $install 'desktop/Scrobblix.Desktop.exe') -PassThru
try {
    Start-Sleep -Seconds 5
    $p.Refresh(); if ($p.HasExited -or $p.MainWindowHandle -eq 0) { throw 'Baseline app not running for gate check' }
    Run-Setup $Installer 'blocked-upgrade' -ExpectBlocked
    Run-Setup (Join-Path $install 'unins000.exe') 'blocked-uninstall' -ExpectBlocked
    $p.Refresh(); if ($p.HasExited) { throw 'Installer force-closed running app' }
} finally { if (-not $p.HasExited) { $p.Kill();$p.WaitForExit() } }
Run-Setup $Installer 'upgrade'
Check-Payload
python scripts/test-legacy-profile.py verify
if ($LASTEXITCODE -ne 0) { throw 'Upgrade changed profile/Unknown rows' }
./scripts/test-ordinary-startup.ps1 -AppDirectory (Join-Path $install 'desktop') -RequirePrivateRuntime -SyntheticLegacyProfile
python scripts/test-legacy-profile.py verify
if ($LASTEXITCODE -ne 0) { throw 'Installed upgraded app changed profile' }
Copy-Item "$env:RUNNER_TEMP/scrobblix-legacy-startup/ordinary-startup.png" "$output/upgraded-app.png"
$sentinel = Join-Path $install 'unrelated-synthetic-user-file.txt'
'synthetic keep' | Set-Content $sentinel
Run-Setup (Join-Path $install 'unins000.exe') 'final-uninstall'
if ((Test-Path (Join-Path $install 'desktop/Scrobblix.Desktop.exe')) -or (Test-Path $shortcut) -or (Test-Path $registry) -or -not (Test-Path $sentinel)) { throw 'Uninstall removal/retention policy failed' }
python scripts/test-legacy-profile.py verify
if ($LASTEXITCODE -ne 0) { throw 'Uninstall changed profile or held Unknown rows' }
Get-FileHash $Installer | Format-List | Out-String | Set-Content "$output/tested-installer-sha256.txt"
'PASS actual wizard, exact installed bytes, Start shortcut/registration, fresh private-runtime launch, beta8-payload baseline to current installer upgrade, running-app gate, unchanged synthetic legacy history/account/queue/loved/two Unknown rows, program-only uninstall and unrelated-file retention. Credential Locker API untouched by installer; no live provider or real-user credential test.' | Set-Content "$output/result.txt"
