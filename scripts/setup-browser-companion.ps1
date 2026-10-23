[CmdletBinding()]
param(
    [ValidateSet('BuildOnly', 'Chrome', 'Edge', 'ChromeAndEdge', 'Firefox')]
    [string]$Browser = 'BuildOnly',

    [Parameter()]
    [string[]]$ExtensionId = @(),

    [Parameter()]
    [ValidateLength(1, 255)]
    [string]$FirefoxExtensionId = 'scrobblix@frpboy.in',

    [switch]$RegisterOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactRoot = Join-Path $repoRoot 'artifacts\Scrobblix'
$packageRoot = Join-Path $artifactRoot 'BrowserCompanion'
$chromiumRoot = Join-Path $packageRoot 'chromium'
$firefoxRoot = Join-Path $packageRoot 'firefox'
$nativeHostRoot = Join-Path $packageRoot 'native-host'
$zipPath = Join-Path $artifactRoot 'Scrobblix-BrowserCompanion.zip'
$installerPath = Join-Path $repoRoot 'scripts\install-native-host.ps1'
$uninstallerPath = Join-Path $repoRoot 'scripts\uninstall-native-host.ps1'

if ($RegisterOnly -and $Browser -eq 'BuildOnly') {
    throw 'RegisterOnly needs a target browser: Chrome, Edge, ChromeAndEdge, or Firefox.'
}
if (-not $RegisterOnly -and $Browser -ne 'BuildOnly') {
    throw 'Build/package and registration are separate steps. Run BuildOnly first, then rerun with -RegisterOnly and the target browser.'
}
if (-not $RegisterOnly -and $Browser -ne 'BuildOnly') {
    throw 'Build/package and registration are separate steps. Run BuildOnly first, then rerun with -RegisterOnly and the target browser.'
}
if ($Browser -in @('Chrome', 'Edge', 'ChromeAndEdge') -and @($ExtensionId).Count -eq 0) {
    throw 'Build the package first, load the unpacked extension from artifacts\Scrobblix\BrowserCompanion\chromium, copy its real ID from the browser, then rerun with -RegisterOnly -Browser Chrome/Edge/ChromeAndEdge -ExtensionId <ID>.'
}

Push-Location $repoRoot
try {
    if (-not $RegisterOnly) {
        foreach ($commandName in @('dotnet', 'node', 'npm')) {
            if (-not (Get-Command $commandName -ErrorAction SilentlyContinue)) {
                throw "Required command '$commandName' was not found. Install the .NET 10 SDK and Node.js 22 or newer, then retry."
            }
        }
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'browser-extension\package.json') -PathType Leaf)) {
            throw 'Run this script from a Scrobblix source clone; browser-extension/package.json was not found.'
        }

        # Only replace generated package outputs during an explicit build. Registration-only
        # never rebuilds/deletes them, so browser IDs and installed manifest paths remain stable.
        # BuildOnly replaces generated package artifacts. Registration is kept in a separate
        # invocation so it never destroys or silently rebuilds a loaded extension's package.
        if (Test-Path -LiteralPath $packageRoot) {
            Remove-Item -LiteralPath $packageRoot -Recurse -Force
        }
        New-Item -ItemType Directory -Path $chromiumRoot -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $chromiumRoot 'dist') -Force | Out-Null
        New-Item -ItemType Directory -Path $firefoxRoot -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $firefoxRoot 'dist') -Force | Out-Null
        New-Item -ItemType Directory -Path $nativeHostRoot -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $packageRoot 'scripts') -Force | Out-Null

        Write-Host 'Installing locked browser companion build dependencies...'
        npm --prefix browser-extension ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed for browser-extension.' }

        Write-Host 'Type-checking the browser companion...'
        npm --prefix browser-extension run check
        if ($LASTEXITCODE -ne 0) { throw 'Browser companion TypeScript check failed.' }

        Write-Host 'Building the browser companion...'
        npm --prefix browser-extension run build
        if ($LASTEXITCODE -ne 0) { throw 'Browser companion TypeScript build failed.' }

        Write-Host 'Validating Chromium/Firefox manifests and native-host identity hooks...'
        node scripts/validate-browser-manifests.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Browser manifest validation failed.' }

        Write-Host 'Publishing the Windows Native Messaging host...'
        dotnet publish src/Scrobblix.NativeHost/Scrobblix.NativeHost.csproj --configuration Release --output $nativeHostRoot
        if ($LASTEXITCODE -ne 0) { throw 'Publishing Scrobblix.NativeHost failed.' }

        Copy-Item -LiteralPath (Join-Path $repoRoot 'browser-extension\manifest.json') -Destination (Join-Path $chromiumRoot 'manifest.json') -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'browser-extension\manifest.firefox.json') -Destination (Join-Path $firefoxRoot 'manifest.json') -Force
        Copy-Item -Path (Join-Path $repoRoot 'browser-extension\dist\*') -Destination (Join-Path $chromiumRoot 'dist') -Recurse -Force
        Copy-Item -Path (Join-Path $repoRoot 'browser-extension\dist\*') -Destination (Join-Path $firefoxRoot 'dist') -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'browser-extension\README.md') -Destination (Join-Path $chromiumRoot 'README.md') -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'browser-extension\README.md') -Destination (Join-Path $firefoxRoot 'README.md') -Force
        Copy-Item -LiteralPath $installerPath -Destination (Join-Path $packageRoot 'scripts') -Force
        Copy-Item -LiteralPath $uninstallerPath -Destination (Join-Path $packageRoot 'scripts') -Force

        $requiredFiles = @(
            (Join-Path $chromiumRoot 'manifest.json'),
            (Join-Path $chromiumRoot 'dist\service-worker.js'),
            (Join-Path $chromiumRoot 'dist\media-observer.js'),
            (Join-Path $firefoxRoot 'manifest.json'),
            (Join-Path $firefoxRoot 'dist\service-worker.js'),
            (Join-Path $firefoxRoot 'dist\media-observer.js'),
            (Join-Path $nativeHostRoot 'Scrobblix.NativeHost.exe'),
            (Join-Path $packageRoot 'scripts\install-native-host.ps1'),
            (Join-Path $packageRoot 'scripts\uninstall-native-host.ps1')
        )
        foreach ($requiredFile in $requiredFiles) {
            if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
                throw "Browser companion package is missing required output: $requiredFile"
            }
        }

        $instructions = @'
SCROBBLIX BROWSER COMPANION — DEVELOPMENT BUILD

This package adds browser-tab metadata from supported YouTube Music and Spotify Web Player pages.
It does not capture audio and does not send Last.fm requests directly. Last.fm credentials remain
in the Scrobblix desktop application.

CHROME / EDGE
1. Extract this ZIP to a stable folder. Do not move the chromium folder after loading it unpacked.
2. Open chrome://extensions or edge://extensions.
3. Turn on Developer mode and select Load unpacked.
4. Select the chromium folder from this package.
5. Copy the complete extension ID shown by that browser. If you use Chrome and Edge, collect both IDs.
6. In a PowerShell terminal at the Scrobblix source repository, run the setup script in registration-only mode:
   .\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "CHROME_EXTENSION_ID"
   Use -Browser Edge for Edge. For both, use -Browser ChromeAndEdge -ExtensionId "CHROME_ID","EDGE_ID".
   For a package-only install without the source clone, use scripts\install-native-host.ps1 and
   -NativeHostPublishDirectory pointing to this package's native-host folder.
7. Completely restart the browser and Scrobblix. Play supported browser media and confirm a source event.

FIREFOX (DEVELOPMENT / TEMPORARY ADD-ON)
1. Open about:debugging#/runtime/this-firefox.
2. Choose Load Temporary Add-on and select the firefox\manifest.json file.
3. Register the host from the Scrobblix source repository:
   .\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Firefox
4. Restart/reattach the temporary add-on as required after Firefox restarts. Temporary add-ons are not permanent release installs.

UNINSTALL
Run scripts\uninstall-native-host.ps1 from this package or the source repository. Remove the unpacked/temporary
extension separately from the browser's extension UI. Unregistering the host does not erase local listening history.

SECURITY AND SUPPORT
- This is a development package, not a signed or store-approved browser extension or Windows installer.
- Use only the exact browser-provided extension ID. Do not guess it.
- Never put Last.fm API keys/shared secrets/session keys into this package, an extension, a screenshot, or Git.
- The companion is optional; Windows GSMTC detection continues to work for players that expose media metadata.
'@
        Set-Content -LiteralPath (Join-Path $packageRoot 'INSTALL.txt') -Value $instructions -Encoding utf8

        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
        Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -Force
        Write-Host ''
        Write-Host 'Browser companion build and package checks passed.'
        Write-Host "Chromium extension folder: $chromiumRoot"
        Write-Host "Firefox extension folder:  $firefoxRoot"
        Write-Host "Native messaging host:     $nativeHostRoot"
        Write-Host "Combined ZIP:              $zipPath"
    }
    else {
        foreach ($requiredPath in @(
            (Join-Path $chromiumRoot 'manifest.json'),
            (Join-Path $firefoxRoot 'manifest.json'),
            (Join-Path $nativeHostRoot 'Scrobblix.NativeHost.exe')
        )) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Build the package first with .\scripts\setup-browser-companion.ps1. Missing: $requiredPath"
            }
        }
    }

    if ($Browser -eq 'BuildOnly') {
        Write-Host 'BuildOnly selected: no browser extension or registry entry was installed.'
    }
    elseif ($Browser -eq 'Firefox') {
        & $installerPath -NativeHostPublishDirectory $nativeHostRoot -RegisterFirefox -FirefoxExtensionId $FirefoxExtensionId
        Write-Host "Next: load $firefoxRoot\manifest.json through about:debugging#/runtime/this-firefox."
        Write-Host 'Firefox temporary add-ons must be loaded through Firefox; the native host cannot silently install the extension.'
    }
    else {
        $extensionIds = @(
            $ExtensionId |
                ForEach-Object { if ($null -ne $_) { $_.Trim() } } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Select-Object -Unique
        )
        if ($extensionIds.Count -eq 0) {
            throw 'Load the Chromium extension first, then rerun with -RegisterOnly and the exact extension ID.'
        }

        foreach ($id in $extensionIds) {
            if ($id -cnotmatch '^[a-p]{32}$') {
                throw "Invalid Chromium extension ID '$id'. Use the exact 32-character ID shown by the browser."
            }
        }

        $installArgs = @{
            NativeHostPublishDirectory = $nativeHostRoot
            ExtensionId = $extensionIds
        }
        switch ($Browser) {
            'Chrome' { $installArgs.RegisterChrome = $true }
            'Edge' { $installArgs.RegisterEdge = $true }
            'ChromeAndEdge' {
                $installArgs.RegisterChrome = $true
                $installArgs.RegisterEdge = $true
            }
        }

        & $installerPath @installArgs
        Write-Host "Native host registered for $Browser. Fully restart the registered browser and Scrobblix."
        Write-Host "Extension folder: $chromiumRoot"
    }
}
finally {
    Pop-Location
}
