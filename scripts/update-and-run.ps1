[CmdletBinding()]
param(
    [switch]$SkipBrowserChecks,
    [string]$Branch
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
        throw 'Your working tree has uncommitted changes. Commit or stash them before updating.'
    }

    $sdkList = dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match '(^|\r?\n)10\.0\.')) {
        throw 'The .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and rerun this script.'
    }

    git fetch origin
    if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }

    $currentBranch = (git branch --show-current).Trim()
    if ([string]::IsNullOrWhiteSpace($currentBranch)) {
        throw 'A branch must be checked out before updating. Switch to a feature branch or pass -Branch <name>.'
    }

    $sourceBranch = if ([string]::IsNullOrWhiteSpace($Branch)) { $currentBranch } else { $Branch }
    if ($sourceBranch -ne 'main') {
        git show-ref --verify --quiet "refs/remotes/origin/$sourceBranch"
        if ($LASTEXITCODE -ne 0) { throw "Remote branch origin/$sourceBranch was not found." }

        if ($currentBranch -ne $sourceBranch) {
            $localBranch = git branch --list $sourceBranch
            if ($localBranch) {
                git switch $sourceBranch
            }
            else {
                git switch --track "origin/$sourceBranch"
            }
            if ($LASTEXITCODE -ne 0) { throw "Could not switch to $sourceBranch." }
        }

        git merge --no-edit "origin/$sourceBranch"
        if ($LASTEXITCODE -ne 0) {
            $gitDirectory = (git rev-parse --git-dir).Trim()
            $mergeHead = Join-Path $gitDirectory 'MERGE_HEAD'
            if (Test-Path -LiteralPath $mergeHead) {
                git merge --abort
            }
            throw "Could not integrate origin/$sourceBranch. The merge was aborted; resolve the conflict manually, then rerun the script."
        }
    }

    git switch main
    if ($LASTEXITCODE -ne 0) { throw 'Could not switch to main.' }

    git merge --no-edit origin/main
    if ($LASTEXITCODE -ne 0) {
        $gitDirectory = (git rev-parse --git-dir).Trim()
        $mergeHead = Join-Path $gitDirectory 'MERGE_HEAD'
        if (Test-Path -LiteralPath $mergeHead) {
            git merge --abort
        }
        throw 'Could not integrate origin/main. The merge was aborted; resolve the conflict manually, then rerun the script.'
    }

    if ($sourceBranch -ne 'main') {
        git merge --no-ff --no-edit $sourceBranch
        if ($LASTEXITCODE -ne 0) {
            $gitDirectory = (git rev-parse --git-dir).Trim()
            $mergeHead = Join-Path $gitDirectory 'MERGE_HEAD'
            if (Test-Path -LiteralPath $mergeHead) {
                git merge --abort
            }
            throw "Could not merge $sourceBranch into main. The merge was aborted; resolve the conflict manually, then rerun the script."
        }
    }

    $runningApps = @(Get-Process -Name 'Scrobblix.Desktop' -ErrorAction SilentlyContinue)
    if ($runningApps.Count -gt 0) {
        $processIds = ($runningApps | ForEach-Object { $_.Id }) -join ', '
        throw "Scrobblix.Desktop is still running (PID(s): $processIds). Close the app, including its tray instance, before rebuilding; its loaded DLLs lock the build output. No process was terminated automatically."
    }

    dotnet restore Scrobblix.slnx
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = if (Test-Path -LiteralPath $vswhere) {
        (& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
    }

    if ($msbuild) {
        Write-Host 'Building with Visual Studio MSBuild to avoid the WinUI CLI XAML compiler failure.'
        & $msbuild Scrobblix.slnx /t:Build /p:Restore=false /p:Configuration=Release /v:minimal /nologo
    }
    else {
        dotnet build Scrobblix.slnx --no-restore --configuration Release
    }

    if ($LASTEXITCODE -ne 0) {
        throw 'The .NET Release build failed. If this only reports XamlCompiler.exe with MSB3073, install Visual Studio Build Tools with the MSBuild and Windows App SDK workloads, then rerun this script.'
    }

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
