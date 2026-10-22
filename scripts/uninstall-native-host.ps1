# Unregister Scrobblix native messaging from Chrome, Microsoft Edge, and Firefox.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$registryPaths = @(
    'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.frpboy.scrobblix',
    'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.frpboy.scrobblix',
    'HKCU:\Software\Mozilla\NativeMessagingHosts\com.frpboy.scrobblix'
)
foreach ($registryPath in $registryPaths) {
    if (Test-Path -LiteralPath $registryPath) {
        Remove-Item -LiteralPath $registryPath -Recurse -Force
    }
}

$trustPath = 'HKCU:\Software\Scrobblix\NativeMessaging'
if (Test-Path -LiteralPath $trustPath) {
    Remove-ItemProperty -LiteralPath $trustPath -Name 'AllowedChromeExtensionIds' -ErrorAction SilentlyContinue
    Remove-ItemProperty -LiteralPath $trustPath -Name 'AllowedFirefoxExtensionIds' -ErrorAction SilentlyContinue
}

# This directory is dedicated to the Scrobblix native host. The browser registration
# and trust entries are removed first; if a live host process holds the executable,
# report the remaining directory instead of failing the entire unregistration.
$nativeHostDirectory = Join-Path $env:LOCALAPPDATA 'Scrobblix\NativeHost'
if (Test-Path -LiteralPath $nativeHostDirectory) {
    try {
        Remove-Item -LiteralPath $nativeHostDirectory -Recurse -Force -ErrorAction Stop
        Write-Host 'Per-user native-host files removed.'
    }
    catch {
        Write-Warning "Native-host registration was removed, but some installed files remain in '$nativeHostDirectory'. Close running browsers/host processes and remove that directory if you want a full uninstall."
    }
}

Write-Host 'Scrobblix native messaging registration removed. Local listening history and logs were not touched.'
