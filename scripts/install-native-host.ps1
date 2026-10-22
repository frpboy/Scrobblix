# Scrobblix native messaging host setup for Chrome, Edge, and optional Firefox.
# Run after publishing Scrobblix.NativeHost and loading the corresponding unpacked extension.
[CmdletBinding()]
param(
    [Parameter()]
    [string[]]$ExtensionId,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$NativeHostPublishDirectory,

    [switch]$RegisterChrome,
    [switch]$RegisterEdge,

    [Parameter()]
    [ValidateLength(1, 255)]
    [string]$FirefoxExtensionId = "scrobblix@frpboy.in",

    [switch]$RegisterFirefox
)

$ErrorActionPreference = 'Stop'

# An unpacked extension can have different IDs in Chrome and Edge. Support multiple IDs
# so the native-host manifest and the desktop allow-list remain consistent.
$extensionIds = @(
    $ExtensionId |
        ForEach-Object { if ($null -ne $_) { $_.Trim() } } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
)

# Backwards-compatible convenience: supplied Chromium IDs with no explicit browser
# switches register Chrome and Edge. Firefox-only mode needs no Chromium ID.
$registerChromium = $RegisterChrome.IsPresent -or $RegisterEdge.IsPresent
if (-not $RegisterChrome.IsPresent -and -not $RegisterEdge.IsPresent -and
    -not $RegisterFirefox.IsPresent -and $extensionIds.Count -gt 0) {
    $RegisterChrome = $true
    $RegisterEdge = $true
    $registerChromium = $true
}

if (-not $registerChromium -and -not $RegisterFirefox.IsPresent) {
    throw 'Choose at least one target: pass -ExtensionId for Chrome/Edge, or use -RegisterFirefox.'
}
if ($registerChromium -and $extensionIds.Count -eq 0) {
    throw 'A valid Chromium extension ID is required. Load the unpacked extension first and copy its actual ID from the browser.'
}
foreach ($id in $extensionIds) {
    if ($id -cnotmatch '^[a-p]{32}\z') {
        throw "Invalid Chromium extension ID '$id'. IDs must be exactly 32 lowercase characters from a through p."
    }
}

$sourceDirectory = (Resolve-Path -LiteralPath $NativeHostPublishDirectory).Path
$sourceExecutable = Join-Path $sourceDirectory 'Scrobblix.NativeHost.exe'
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw "Scrobblix.NativeHost.exe was not found in '$sourceDirectory'. Publish the native host first."
}

$destinationDirectory = Join-Path $env:LOCALAPPDATA 'Scrobblix\NativeHost'
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $destinationDirectory -Recurse -Force
$installedExecutable = Join-Path $destinationDirectory 'Scrobblix.NativeHost.exe'
$trustPath = 'HKCU:\Software\Scrobblix\NativeMessaging'

if ($registerChromium) {
    $manifestPath = Join-Path $destinationDirectory 'com.frpboy.scrobblix.json'
    $manifest = [ordered]@{
        name = 'com.frpboy.scrobblix'
        description = 'Scrobblix local playback metadata bridge'
        path = $installedExecutable
        type = 'stdio'
        allowed_origins = @($extensionIds | ForEach-Object { "chrome-extension://$_/" })
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8

    if ($RegisterChrome) {
        $registryPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.frpboy.scrobblix'
        New-Item -Path $registryPath -Force | Out-Null
        Set-Item -LiteralPath $registryPath -Value $manifestPath
    }
    if ($RegisterEdge) {
        $registryPath = 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.frpboy.scrobblix'
        New-Item -Path $registryPath -Force | Out-Null
        Set-Item -LiteralPath $registryPath -Value $manifestPath
    }

    New-Item -Path $trustPath -Force | Out-Null
    Set-ItemProperty -LiteralPath $trustPath -Name 'AllowedChromeExtensionIds' -Value ($extensionIds -join ';') -Type String
    Write-Host ("Chromium extension ID(s) allow-listed: " + ($extensionIds -join ', '))
}

if ($RegisterFirefox) {
    $firefoxManifestPath = Join-Path $destinationDirectory 'com.frpboy.scrobblix.firefox.json'
    $firefoxManifest = [ordered]@{
        name = 'com.frpboy.scrobblix'
        description = 'Scrobblix local playback metadata bridge for Firefox'
        path = $installedExecutable
        type = 'stdio'
        allowed_extensions = @($FirefoxExtensionId)
    }
    $firefoxManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $firefoxManifestPath -Encoding utf8
    $firefoxRegistryPath = 'HKCU:\Software\Mozilla\NativeMessagingHosts\com.frpboy.scrobblix'
    New-Item -Path $firefoxRegistryPath -Force | Out-Null
    Set-Item -LiteralPath $firefoxRegistryPath -Value $firefoxManifestPath

    New-Item -Path $trustPath -Force | Out-Null
    Set-ItemProperty -LiteralPath $trustPath -Name 'AllowedFirefoxExtensionIds' -Value $FirefoxExtensionId -Type String
    Write-Host "Firefox extension ID allow-listed: $FirefoxExtensionId"
}

Write-Host 'Scrobblix native messaging host registration updated.'
Write-Host "Native host: $installedExecutable"
Write-Host 'Restart any registered browser completely before testing the companion.'
