param(
    [Parameter(Mandatory=$true)][string]$BundleDirectory,
    [string]$OutputDirectory = 'artifacts/installer',
    [string]$BuildVersion = '0.1.0.9',
    [string]$BuildLabel = 'Windows installer preview'
)
$ErrorActionPreference = 'Stop'
./scripts/test-portable-package.ps1 -BundleDirectory $BundleDirectory
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
if (-not (Test-Path $compiler -PathType Leaf)) { throw 'Required runner Inno Setup 6 compiler missing' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$payload = (Resolve-Path $BundleDirectory).Path
$output = (Resolve-Path $OutputDirectory).Path
$compilerOutput = & $compiler "/DPayloadDir=$payload" "/DOutputDir=$output" "/DBuildVersion=$BuildVersion" "/DBuildLabel=$BuildLabel" 'packaging/windows/Scrobblix.iss'
if ($LASTEXITCODE -ne 0) { $compilerOutput | Write-Output; throw 'Installer compile failed' }
# ISCC.exe can have a placeholder 0.0.0.0 PE ProductVersion. The owning compiler
# prints its loaded engine version during compilation; gate before executing/publishing.
$banner = @($compilerOutput | Where-Object { $_ -match '^Compiler engine version: Inno Setup 6\.7\.1$' })
if ($banner.Count -ne 1) { $compilerOutput | Write-Output; throw 'Unreviewed compiler engine banner' }
$version = '6.7.1'
$compilerOutput | Set-Content (Join-Path $output 'compiler-output.txt')
Get-FileHash (Join-Path $output 'Scrobblix-Windows-x64-Setup.exe') -Algorithm SHA256 | Format-List | Out-String | Set-Content (Join-Path $output 'installer-sha256.txt')
"Compiler observed: Inno Setup $version; build $BuildVersion; label $BuildLabel" | Set-Content (Join-Path $output 'build-identity.txt')
