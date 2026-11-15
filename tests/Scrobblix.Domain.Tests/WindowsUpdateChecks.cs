using Scrobblix.Domain.Updates;

internal static class WindowsUpdateChecks
{
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("windows-update-check: " + message);
    }

    public static Task RunAsync()
    {
        // Version parsing: strict shape, safe on hostile input.
        Assert(WindowsVersion.Parse("0.1.0") == new WindowsVersion(0, 1, 0, null, null), "stable parse");
        Assert(WindowsVersion.Parse("0.1.0-beta.12") == new WindowsVersion(0, 1, 0, "beta", 12), "prerelease parse");
        Assert(WindowsVersion.Parse("99999999999999999999.0.0") is null, "overflow rejected");
        Assert(WindowsVersion.Parse("1.2.3-beta.99999999999999999999") is null, "channel overflow rejected");
        Assert(WindowsVersion.Parse("") is null && WindowsVersion.Parse("v1.2.3") is null, "malformed rejected");

        // Ordering: core triple, then channel rank, then channel number.
        Assert(WindowsVersion.Parse("0.1.0-beta.12") > WindowsVersion.Parse("0.1.0-beta.11"), "beta.12 newer than beta.11");
        Assert(WindowsVersion.Parse("0.1.0") > WindowsVersion.Parse("0.1.0-rc.3"), "stable newer than rc");
        Assert(WindowsVersion.Parse("0.2.0-alpha.1") > WindowsVersion.Parse("0.1.0"), "newer triple wins over channel");

        // Tag prefix discipline.
        Assert(WindowsUpdateCatalog.ParseTag("windows-v0.1.0-beta.12") == WindowsVersion.Parse("0.1.0-beta.12"), "windows tag parsed");
        Assert(WindowsUpdateCatalog.ParseTag("android-v0.1.0-alpha.2") is null, "android tag rejected");
        Assert(WindowsUpdateCatalog.ParseTag("v0.1.0") is null, "unprefixed tag rejected");

        // Selection: newest windows-v* with an installer asset; drafts skipped.
        var beta11 = new WindowsRelease("windows-v0.1.0-beta.11", "beta 11", true, false, "notes",
            new[] { new WindowsReleaseAsset(WindowsUpdateCatalog.InstallerName, "https://github.com/frpboy/Scrobblix/releases/download/x/11.exe", 100) });
        var beta12 = new WindowsRelease("windows-v0.1.0-beta.12", "beta 12", true, false, "notes",
            new[] { new WindowsReleaseAsset(WindowsUpdateCatalog.InstallerName, "https://github.com/frpboy/Scrobblix/releases/download/x/12.exe", 100),
                    new WindowsReleaseAsset("SHA256SUMS.txt", "https://github.com/frpboy/Scrobblix/releases/download/x/sums", 10) });
        var draftNewer = new WindowsRelease("windows-v0.2.0", "draft", false, true, "notes",
            new[] { new WindowsReleaseAsset(WindowsUpdateCatalog.InstallerName, "https://github.com/frpboy/Scrobblix/releases/download/x/2.exe", 100) });
        var android = new WindowsRelease("android-v0.1.0-alpha.3", "android", true, false, "notes",
            new[] { new WindowsReleaseAsset("app.apk", "https://github.com/frpboy/Scrobblix/releases/download/x/app.apk", 100) });
        var noInstaller = new WindowsRelease("windows-v0.3.0", "zip only", false, false, "notes",
            new[] { new WindowsReleaseAsset("portable.zip", "https://github.com/frpboy/Scrobblix/releases/download/x/p.zip", 100) });
        var otherExe = new WindowsRelease("windows-v0.4.0", "other exe", false, false, "notes",
            new[] { new WindowsReleaseAsset("SomeOther-Setup.exe", "https://github.com/frpboy/Scrobblix/releases/download/x/o.exe", 100) });
        Assert(WindowsUpdateCatalog.SelectLatest(new[] { beta11, beta12, draftNewer, android, noInstaller, otherExe }) == beta12,
            "newest non-draft windows release with the exact installer asset selected");
        Assert(WindowsUpdateCatalog.SelectLatest(new[] { android, otherExe }) is null,
            "no release with the exact installer contract means none selected");

        // Checksums: exact "hash  filename" lines only.
        var sums = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  " + WindowsUpdateCatalog.InstallerName + "\n" +
                   "fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321  Other-Tool.exe\n";
        Assert(WindowsUpdateCatalog.ChecksumFor(sums, WindowsUpdateCatalog.InstallerName) ==
               "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "checksum line matched");
        Assert(WindowsUpdateCatalog.ChecksumFor(sums, "Missing.exe") is null, "missing checksum rejected");
        Assert(WindowsUpdateCatalog.ChecksumFor("nothex  " + WindowsUpdateCatalog.InstallerName + "\n", WindowsUpdateCatalog.InstallerName) is null, "non-hash rejected");

        // Safe names, including Windows-specific rules.
        Assert(WindowsUpdateCatalog.SafeAssetName(WindowsUpdateCatalog.InstallerName), "contract installer name allowed");
        Assert(!WindowsUpdateCatalog.SafeAssetName("../evil.exe"), "traversal rejected");
        Assert(!WindowsUpdateCatalog.SafeAssetName("a/b.exe") && !WindowsUpdateCatalog.SafeAssetName("a\\b.exe"), "separators rejected");
        Assert(!WindowsUpdateCatalog.SafeAssetName("a:b.exe") && !WindowsUpdateCatalog.SafeAssetName("a|b.exe"), "windows-invalid characters rejected");
        Assert(!WindowsUpdateCatalog.SafeAssetName("name.exe.") && !WindowsUpdateCatalog.SafeAssetName("name.exe "), "trailing dot or space rejected");
        Assert(!WindowsUpdateCatalog.SafeAssetName("CON.exe") && !WindowsUpdateCatalog.SafeAssetName("com1.exe"), "device names rejected");
        Assert(WindowsUpdateCatalog.SafeAssetName("console.exe"), "device-name prefix is not a device name");

        // Build identity and compare interpretation.
        Assert(WindowsUpdateDecision.SourceRevision("1.0.0+101e3c5358b041712f9e8efe8728bb20c1ec42fb") ==
               "101e3c5358b041712f9e8efe8728bb20c1ec42fb", "source revision extracted");
        Assert(WindowsUpdateDecision.SourceRevision("1.0.0") is null && WindowsUpdateDecision.SourceRevision(null) is null,
            "missing source revision is null, not guessed");
        Assert(WindowsUpdateDecision.FromCompareStatus("ahead") == WindowsUpdateState.UpdateAvailable, "ahead means update");
        Assert(WindowsUpdateDecision.FromCompareStatus("identical") == WindowsUpdateState.UpToDate, "identical means current");
        Assert(WindowsUpdateDecision.FromCompareStatus("behind") == WindowsUpdateState.NewerThanRelease, "behind means local newer");
        Assert(WindowsUpdateDecision.FromCompareStatus("diverged") == WindowsUpdateState.Unknown, "diverged is unknown");
        Assert(WindowsUpdateDecision.FromCompareStatus(null) == WindowsUpdateState.Unknown, "missing status is unknown");

        Console.WriteLine("windows-update-check: PASS");
        return Task.CompletedTask;
    }
}
