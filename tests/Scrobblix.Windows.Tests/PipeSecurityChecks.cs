using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Scrobblix.Windows;

/// <summary>
/// Issue #74: exercises the production named-pipe authorization boundary on Windows with
/// synthetic clients only. Rejection scenarios must reject before any payload byte is read;
/// the positive control proves the full peer chain still works for a browser-shaped parent
/// in a standard install directory.
/// </summary>
internal static class PipeSecurityChecks
{
    private static readonly SecurityIdentifier[] BroadSids =
    [
        new SecurityIdentifier(WellKnownSidType.WorldSid, null),
        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
        new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
        new SecurityIdentifier(WellKnownSidType.AnonymousSid, null),
        new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
    ];

    public static async Task RunAsync()
    {
        Console.WriteLine("--- #74 named-pipe authorization checks (synthetic clients, no production data) ---");

        var clientSource = Environment.GetEnvironmentVariable("SCROBBLIX_PIPE_CLIENT_PATH");
        Check.True(!string.IsNullOrWhiteSpace(clientSource), "SCROBBLIX_PIPE_CLIENT_PATH is provided by CI");
        clientSource = Path.GetFullPath(clientSource!);
        Check.True(File.Exists(clientSource), "Pipe security test client exists at " + clientSource);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var tempRoot = Path.Combine(Path.GetTempPath(), "scrobblix-pipe-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        // Stage the synthetic client at the exact path production expects for the native host.
        var stagedHostDir = Path.Combine(localAppData, "Scrobblix", "NativeHost");
        var stagedHost = Path.Combine(stagedHostDir, "Scrobblix.NativeHost.exe");
        var cleanupStagedHost = false;
        string expectedHost;
        if (File.Exists(stagedHost))
        {
            expectedHost = Path.Combine(tempRoot, "Scrobblix.NativeHost.exe");
            Console.WriteLine("NOTE default native-host path already exists; using alternate expected path for this run.");
        }
        else
        {
            Directory.CreateDirectory(stagedHostDir);
            expectedHost = stagedHost;
            cleanupStagedHost = true;
        }

        File.Copy(clientSource, expectedHost, overwrite: false);

        var cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        Check.True(File.Exists(cmdExe), "cmd.exe located for synthetic parent staging");

        var spoofDir = Path.Combine(tempRoot, "spoof");
        Directory.CreateDirectory(spoofDir);
        var fakeBrowser = Path.Combine(spoofDir, "chrome.exe");
        File.Copy(cmdExe, fakeBrowser);

        var userChromeDir = Path.Combine(localAppData, "Google", "Chrome", "Application");
        var vendorChrome = Path.Combine(userChromeDir, "chrome.exe");
        var positiveControlAvailable = !File.Exists(vendorChrome);
        var createdVendorDir = !Directory.Exists(userChromeDir);

        try
        {
            // Pure-path authorization predicate checks.
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            Check.True(
                NamedPipePeerVerifier.IsSupportedBrowserImagePath(
                    Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe")),
                "Browser image predicate accepts the standard system Chrome install path");
            Check.True(
                NamedPipePeerVerifier.IsSupportedBrowserImagePath(
                    Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe")),
                "Browser image predicate accepts the standard system Firefox install path");
            Check.True(
                !NamedPipePeerVerifier.IsSupportedBrowserImagePath(fakeBrowser),
                "Browser image predicate rejects a renamed executable outside vendor install directories");
            Check.True(
                !NamedPipePeerVerifier.IsSupportedBrowserImagePath(
                    Path.Combine(tempRoot, "chrome.exe", "chrome.exe")),
                "Browser image predicate rejects lookalike nested directories");
            Check.True(
                NamedPipePeerVerifier.IsSupportedBrowserExecutableName(fakeBrowser)
                    && !NamedPipePeerVerifier.IsSupportedBrowserImagePath(fakeBrowser),
                "Differential proof: the retired basename-only rule accepted this exact spoof, the current rule rejects it");

            // Scenario 1: arbitrary same-user process (this test host) connects directly.
            var scenario = await RunScenarioAsync(expectedHost, pipe => ConnectInProcessAsync(pipe));
            Check.True(!scenario.Accepted && scenario.Reason == "client_image_not_registered_native_host",
                "Same-user arbitrary client rejected as non-native-host (reason=" + scenario.Reason + ")");
            Check.True(scenario.PayloadBytes == 0, "Rejected same-user client had zero payload bytes read (reject-before-persistence)");

            // Scenario 2: image at the registered native-host path, launched by a non-browser parent.
            scenario = await RunScenarioAsync(expectedHost, pipe => StartDirect(expectedHost, pipe));
            Check.True(!scenario.Accepted && scenario.Reason == "native_host_parent_not_supported_browser",
                "Registered-path host launched by non-browser parent rejected (reason=" + scenario.Reason + ")");
            Check.True(scenario.PayloadBytes == 0, "Direct-invocation client had zero payload bytes read");

            // Scenario 3: registered-path host launched by a renamed fake chrome.exe parent.
            scenario = await RunScenarioAsync(expectedHost, pipe => StartViaCmdCopy(fakeBrowser, expectedHost, pipe));
            Check.True(!scenario.Accepted && scenario.Reason == "native_host_parent_not_supported_browser",
                "Renamed fake chrome.exe parent rejected (reason=" + scenario.Reason + ")");
            Check.True(scenario.PayloadBytes == 0, "Fake-browser-parent client had zero payload bytes read");

            // Scenario 4 (positive control): browser-shaped parent in a real vendor install dir.
            if (positiveControlAvailable)
            {
                Directory.CreateDirectory(userChromeDir);
                File.Copy(cmdExe, vendorChrome);
                try
                {
                    scenario = await RunScenarioAsync(expectedHost, pipe => StartViaCmdCopy(vendorChrome, expectedHost, pipe));
                    Check.True(scenario.Accepted && scenario.Reason == "accepted",
                        "Positive control: vendor-install-location browser parent accepted");
                    Check.True(scenario.PayloadBytes > 0, "Positive control payload read after acceptance");
                    Check.True(scenario.BrowserInstanceId == "forged-instance-id",
                        "Payload browserInstanceId is opaque data, not identity: forged value observed post-acceptance carries no authority");
                }
                finally
                {
                    TryDelete(vendorChrome);
                }
            }
            else
            {
                Console.WriteLine("NOTE positive control skipped: a real user-level Chrome install occupies the vendor path.");
            }
        }
        finally
        {
            if (cleanupStagedHost)
            {
                TryDelete(stagedHost);
            }

            if (positiveControlAvailable && createdVendorDir)
            {
                TryRemoveEmptyDirectory(userChromeDir);
                TryRemoveEmptyDirectory(Path.GetDirectoryName(userChromeDir));
                TryRemoveEmptyDirectory(Path.Combine(localAppData, "Google"));
            }

            try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        Console.WriteLine("Pipe authorization checks complete. Boundary: same-user spoofing resistance plus OS cross-user isolation; not cryptographic caller authentication.");
    }

    private static async Task<ServerOutcome> RunScenarioAsync(string expectedHostPath, Func<string, Process?> clientStarter)
    {
        var pipeName = "scrobblix.test." + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        AssertPipeAclCurrentUserOnly(server);

        var waitForConnection = server.WaitForConnectionAsync();
        var process = clientStarter(pipeName);
        await waitForConnection.WaitAsync(TimeSpan.FromSeconds(20));

        var accepted = NamedPipePeerVerifier.IsTrustedBrowserNativeHost(
            server.SafePipeHandle.DangerousGetHandle(), expectedHostPath, out var reason);
        var payloadBytes = 0;
        string? browserInstanceId = null;
        if (accepted)
        {
            var header = new byte[sizeof(int)];
            if (await ReadExactAsync(server, header))
            {
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length is > 0 and <= 256 * 1024)
                {
                    var buffer = new byte[length];
                    if (await ReadExactAsync(server, buffer))
                    {
                        payloadBytes = length;
                        using var document = JsonDocument.Parse(buffer);
                        if (document.RootElement.TryGetProperty("browserInstanceId", out var idElement))
                        {
                            browserInstanceId = idElement.GetString();
                        }
                    }
                }
            }
        }

        if (process is not null)
        {
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                var output = await process.StandardOutput.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("  client: " + output.Trim());
                }
            }
            catch (TimeoutException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            }

            process.Dispose();
        }

        return new ServerOutcome(accepted, reason, payloadBytes, browserInstanceId);
    }

    private static void AssertPipeAclCurrentUserOnly(NamedPipeServerStream server)
    {
        var security = server.GetAccessControl();
        var currentUser = WindowsIdentity.GetCurrent().User;
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));
        var sawCurrentUser = false;
        foreach (PipeAccessRule rule in rules)
        {
            var sid = (SecurityIdentifier)rule.IdentityReference;
            Console.WriteLine("  pipe ACL: " + rule.AccessControlType + " " + rule.PipeAccessRights + " " + sid + " (" + DescribeSid(sid) + ")");
            if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow)
            {
                Check.True(
                    !BroadSids.Any(broad => broad.Equals(sid)),
                    "Pipe ACL grants no broad principal (" + DescribeSid(sid) + ")");
                if (currentUser is not null && sid.Equals(currentUser))
                {
                    sawCurrentUser = true;
                }
            }
        }

        Check.True(sawCurrentUser, "Pipe ACL grants the current user explicitly");
    }

    private static string DescribeSid(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }

    private static Process? ConnectInProcessAsync(string pipeName)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        client.Connect(TimeSpan.FromSeconds(10));
        var payload = Encoding.UTF8.GetBytes(
            "{\"protocolVersion\":1,\"messageId\":\"forged-inproc-1\",\"type\":\"playback.observation.v1\","
            + "\"browserInstanceId\":\"forged-instance-id\",\"siteOrigin\":\"https://music.example\","
            + "\"sequenceNumber\":1,\"artist\":\"synthetic-artist\",\"title\":\"synthetic-title\"}");
        try
        {
            var header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            client.Write(header);
            client.Write(payload);
            client.Flush();
        }
        catch (IOException)
        {
            // Expected: the server rejects the peer and disposes the pipe without reading.
        }

        client.Dispose();
        return null;
    }

    private static Process StartDirect(string hostPath, string pipeName)
    {
        var start = new ProcessStartInfo
        {
            FileName = hostPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(pipeName);
        return Process.Start(start)!;
    }

    private static Process StartViaCmdCopy(string fakeBrowserPath, string hostPath, string pipeName)
    {
        var start = new ProcessStartInfo
        {
            FileName = fakeBrowserPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("\"" + hostPath + "\" \"" + pipeName + "\"");
        return Process.Start(start)!;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset));
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); } }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryRemoveEmptyDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try { Directory.Delete(path, recursive: false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record ServerOutcome(bool Accepted, string Reason, int PayloadBytes, string? BrowserInstanceId);
}
