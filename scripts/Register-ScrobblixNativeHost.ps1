[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-p]{32}$')]
    [string]$ExtensionId,

    [ValidateSet('Chrome', 'Edge', 'Both')]
    [string]$Browser = 'Both',

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$HostPath
)

$hostName = 'com.frpboy.scrobblix'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Scrobblix\NativeMessaging'
$manifestPath = Join-Path $installDirectory "$hostName.json"
$hostFullPath = [IO.Path]::GetFullPath($HostPath)
$manifest = [ordered]@{
    name = $hostName
    description = 'Scrobblix Browser Companion native messaging host'
    path = $hostFullPath
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}

if ($PSCmdlet.ShouldProcess($manifestPath, 'write native messaging host manifest')) {
    New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))
}

$registryRoots = switch ($Browser) {
    'Chrome' { @('HKCU:\Software\Google\Chrome\NativeMessagingHosts') }
    'Edge' { @('HKCU:\Software\Microsoft\Edge\NativeMessagingHosts') }
    default { @('HKCU:\Software\Google\Chrome\NativeMessagingHosts', 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts') }
}

foreach ($registryRoot in $registryRoots) {
    $registrationPath = Join-Path $registryRoot $hostName
    if ($PSCmdlet.ShouldProcess($registrationPath, 'register native messaging host for the current Windows user')) {
        New-Item -Path $registrationPath -Force | Out-Null
        Set-ItemProperty -Path $registrationPath -Name '(default)' -Value $manifestPath
    }
}

Write-Host "Registered $hostName for $Browser using $manifestPath"
