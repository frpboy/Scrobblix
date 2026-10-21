# Scrobblix native messaging host setup for Chrome and Microsoft Edge.
# Run after building/publishing Scrobblix.NativeHost and loading the extension unpacked.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-p]{32}$')]
    [string]$ExtensionId,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$NativeHostPublishDirectory,

    [switch]$RegisterEdge = $true,
    [switch]$RegisterChrome = $true
)

$ErrorActionPreference = 'Stop'

$sourceDirectory = (Resolve-Path -LiteralPath $NativeHostPublishDirectory).Path
$sourceExecutable = Join-Path $sourceDirectory 'Scrobblix.NativeHost.exe'
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw "Scrobblix.NativeHost.exe was not found in '$sourceDirectory'. Publish the native host first."
}

$destinationDirectory = Join-Path $env:LOCALAPPDATA 'Scrobblix\NativeHost'
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $destinationDirectory -Recurse -Force

$installedExecutable = Join-Path $destinationDirectory 'Scrobblix.NativeHost.exe'
$manifestPath = Join-Path $destinationDirectory 'com.frpboy.scrobblix.json'
$manifest = [ordered]@{
    name = 'com.frpboy.scrobblix'
    description = 'Scrobblix local playback metadata bridge'
    path = $installedExecutable
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8

$registryPaths = [System.Collections.Generic.List[string]]::new()
if ($RegisterChrome) {
    $registryPaths.Add('HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.frpboy.scrobblix')
}
if ($RegisterEdge) {
    $registryPaths.Add('HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.frpboy.scrobblix')
}

foreach ($registryPath in $registryPaths) {
    New-Item -Path $registryPath -Force | Out-Null
    Set-Item -LiteralPath $registryPath -Value $manifestPath
}

$trustPath = 'HKCU:\Software\Scrobblix\NativeMessaging'
New-Item -Path $trustPath -Force | Out-Null
Set-ItemProperty -LiteralPath $trustPath -Name 'AllowedChromeExtensionIds' -Value $ExtensionId -Type String

Write-Host 'Scrobblix native messaging host registered.'
Write-Host "Extension ID allow-listed: $ExtensionId"
Write-Host "Native host: $installedExecutable"
Write-Host "Manifest: $manifestPath"
Write-Host 'Restart your browser completely before testing the companion.'
