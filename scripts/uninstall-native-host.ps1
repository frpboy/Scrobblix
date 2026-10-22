# Unregister Scrobblix native messaging from Chrome and Microsoft Edge.
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

Write-Host 'Scrobblix native messaging registration removed. Local listening history and logs were not touched.'
