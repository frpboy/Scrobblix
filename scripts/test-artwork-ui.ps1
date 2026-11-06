$ErrorActionPreference = 'Stop'
$output = Join-Path $env:RUNNER_TEMP 'scrobblix-artwork-ui'
New-Item -ItemType Directory -Force $output | Out-Null
Write-Output "Interactive=$([Environment]::UserInteractive) Session=$((Get-Process -Id $PID).SessionId)"
$env:SCROBBLIX_UI_OUTPUT = $output
$env:SCROBBLIX_UI_FIXTURES = Join-Path $PWD 'tests/Scrobblix.Windows.Tests/fixtures'
$app = Join-Path $PWD 'artifacts/ui-probe/Scrobblix.Desktop.exe'
$p = Start-Process -FilePath $app -PassThru
if (-not $p.WaitForExit(60000)) { $p.Kill(); throw 'Isolated fixture app timed out60s' }
Write-Output "Exit=$($p.ExitCode)"
$result = Join-Path $output 'result.txt'
if (-not (Test-Path $result)) { throw 'App exited before producing qualification result' }
$text = Get-Content $result -Raw
Write-Output $text
if ($text -notmatch '^PASS') { throw 'Artwork UI qualification failed' }
foreach ($name in @('valid-artwork.png','invalid-fallback.png','recovered-valid.png')) {
    if (-not (Test-Path (Join-Path $output $name))) { throw "Missing actual render $name" }
}
