param([Parameter(Mandatory=$true)][string]$BundleDirectory)
$ErrorActionPreference = 'Stop'
$bundle = (Resolve-Path $BundleDirectory).Path
foreach ($relative in @('desktop/Scrobblix.Desktop.exe','desktop/Scrobblix.Desktop.pri','desktop/coreclr.dll','desktop/hostfxr.dll','desktop/hostpolicy.dll','desktop/Microsoft.UI.Xaml.dll','native-host/Scrobblix.NativeHost.exe','native-host/coreclr.dll','README-TESTING.txt')) {
    $path = Join-Path $bundle $relative
    if (-not (Test-Path $path -PathType Leaf) -or (Get-Item $path).Length -eq 0) { throw "Portable bundle missing $relative" }
}
foreach ($project in @('desktop/Scrobblix.Desktop','native-host/Scrobblix.NativeHost')) {
    $runtime = Get-Content (Join-Path $bundle "$project.runtimeconfig.json") -Raw | ConvertFrom-Json
    if ($runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks) { throw "Framework-dependent runtimeconfig: $project" }
    if (-not $runtime.runtimeOptions.includedFrameworks) { throw "Private framework declaration missing: $project" }
}
'PASS nonempty PRI, private runtimes and self-contained runtimeconfigs'
