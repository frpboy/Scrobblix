[CmdletBinding()]
param(
    [switch]$SkipBrowserChecks
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    foreach ($commandName in @('git', 'dotnet')) {
        if (-not (Get-Command $commandName -ErrorAction SilentlyContinue)) {
            throw "Required command '$commandName' was not found. Install Git for Windows and the .NET 10 SDK, then retry."
        }
    }

    if (-not $SkipBrowserChecks) {
        foreach ($commandName in @('node', 'npm')) {
            if (-not (Get-Command $commandName -ErrorAction SilentlyContinue)) {
                throw "Required command '$commandName' was not found. Install Node.js 22 or newer, then retry."
            }
        }
    }

    if (-not (Test-Path -LiteralPath '.git' -PathType Container)) {
        throw 'Run this script from a Scrobblix Git clone; .git was not found.'
    }

    if ((git status --porcelain).Length -gt 0) {
        throw 'Your working tree has uncommitted changes. Commit or stash them before updating the feature branch.'
    }

    $sdkList = dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match '(^|\r?\n)10\.0\.')) {
        throw 'The .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and rerun this script.'
    }

    $branch = 'feat/scrobblix-v1-complete'
    git fetch origin
    if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }

    $currentBranch = (git branch --show-current).Trim()
    if ($currentBranch -ne $branch) {
        $localBranch = git branch --list $branch
        if ($localBranch) {
            git switch $branch
        }
        else {
            git switch --track "origin/$branch"
        }
        if ($LASTEXITCODE -ne 0) { throw "Could not switch to $branch." }
    }

    git pull --ff-only origin $branch
    if ($LASTEXITCODE -ne 0) { throw "Could not fast-forward $branch." }

    dotnet restore Scrobblix.slnx
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build Scrobblix.slnx --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'The .NET Release build failed. See the first compiler/analyzer error above.' }

    dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'The Scrobblix assertion suite failed.' }

    if (-not $SkipBrowserChecks) {
        npm --prefix browser-extension ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed for browser-extension.' }

        npm --prefix browser-extension run check
        if ($LASTEXITCODE -ne 0) { throw 'The browser companion TypeScript check failed.' }

        npm --prefix browser-extension run build
        if ($LASTEXITCODE -ne 0) { throw 'The browser companion build failed.' }
    }

    Write-Host ''
    Write-Host 'Build and test checks passed. Launching the Scrobblix Windows desktop app...'
    Write-Host 'Close the app window to return to this terminal.'
    dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Scrobblix exited with a non-zero code. Check %LOCALAPPDATA%\Scrobblix\logs for the numbered diagnostic log.'
    }
}
finally {
    Pop-Location
}
