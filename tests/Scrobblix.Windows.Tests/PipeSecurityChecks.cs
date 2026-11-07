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

    // Principals whose presence in the DACL does not widen access beyond the owning
    // user on a normal single-user machine: the user itself, SYSTEM, Administrators
    // (a same-machine admin already owns every user process), and owner pseudo-SIDs.
    private static readonly SecurityIdentifier[] AllowedGranteeSids =
    [
        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
        new SecurityIdentifier("S-1-3-0"), // CREATOR OWNER
        new SecurityIdentifier("S-1-3-4"), // OWNER RIGHTS
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

        var spoofDir = Path.Combine(tempRoot, "spoof");
        Directory.CreateDirectory(spoofDir);
        var fakeBrowser = Path.Combine(spoofDir, "chrome.exe");
        File.Copy(clientSource, fakeBrowser);

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
            var scenario = await RunScenarioAsync(expectedHost, pipe => { Task.Run(() => ConnectInProcessAsync(pipe)); return null; });
            Check.True(!scenario.Accepted && scenario.Reason == "client_image_not_registered_native_host",
                "Same-user arbitrary client rejected as non-native-host (reason=" + scenario.Reason + ")");
            Check.True(scenario.PayloadBytes == 0, "Rejected same-user client had zero payload bytes read (reject-before-persistence)");

            // Scenario 2: image at the registered native-host path, launched by a non-browser parent.
            scenario = await RunScenarioAsync(expectedHost, pipe => StartDirect(expectedHost, pipe));
            Check.True(!scenario.Accepted && scenario.Reason == "native_host_parent_not_supported_browser",
                "Registered-path host launched by non-browser parent rejected (reason=" + scenario.Reason + ")");
            Check.True(scenario.PayloadBytes == 0, "Direct-invocation client had zero payload bytes read");

            // Scenario 3: registered-path host launched by a renamed fake chrome.exe parent.
            scenario = await RunScenarioAsync(expectedHost, pipe => StartViaFakeBrowser(fakeBrowser, expectedHost, pipe));
            Check.True(!scenario.Accepted && scenario.Reason == "native_host_parent_not_supported_browser",
                "Renamed fake chrome.exe parent rejected (reason=" + scenario.Reason + ")");
            Check.True(scenario.PayloadBytes == 0, "Fake-browser-parent client had zero payload bytes read");

            // Scenario 4 (positive control): browser-shaped parent in a real vendor install dir.
            if (positiveControlAvailable)
            {
                Directory.CreateDirectory(userChromeDir);
                File.Copy(clientSource, vendorChrome);
                try
                {
                    scenario = await RunScenarioAsync(expectedHost, pipe => StartViaFakeBrowser(vendorChrome, expectedHost, pipe));
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

    private static Task<ServerOutcome> RunScenarioAsync(string expectedHostPath, Func<string, Process?> clientStarter)
    {
        var pipeName = "scrobblix.test." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        AssertPipeAclCurrentUserOnly(server);

        // The server runs on a dedicated thread with synchronous blocking pipe calls; the
        // async overlapped accept proved unreliable on the hosted runner (the accept never
        // completed and even timeout tokens stayed pending). A hard watchdog disposes the
        // server to unblock the thread and fails the scenario loudly instead of hanging.
        using var completed = new ManualResetEventSlim();
        ServerOutcome? outcome = null;
        Exception? serverError = null;
        var serverThread = new Thread(() =>
        {
            try
            {
                server.WaitForConnection();
                Console.WriteLine("  server accepted a connection for " + pipeName);
                var accepted = NamedPipePeerVerifier.IsTrustedBrowserNativeHost(
                    server.SafePipeHandle.DangerousGetHandle(), expectedHostPath, out var reason);
                Console.WriteLine("  peer verdict for " + pipeName + ": accepted=" + accepted + " reason=" + reason);
                var payloadBytes = 0;
                string? browserInstanceId = null;
                if (accepted)
                {
                    var header = new byte[sizeof(int)];
                    if (ReadExact(server, header))
                    {
                        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                        if (length is > 0 and <= 256 * 1024)
                        {
                            var buffer = new byte[length];
                            if (ReadExact(server, buffer))
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

                outcome = new ServerOutcome(accepted, reason, payloadBytes, browserInstanceId);
            }
            catch (Exception exception)
            {
                serverError = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
        };
        serverThread.Start();

        Process? process = null;
        Exception? clientError = null;
        try
        {
            process = clientStarter(pipeName);
            Console.WriteLine("  scenario client started for " + pipeName);
        }
        catch (Exception exception)
        {
            clientError = exception;
        }

        if (!completed.Wait(TimeSpan.FromSeconds(60)))
        {
            var clientState = "no client process";
            if (process is not null)
            {
                clientState = "client hasExited=" + process.HasExited;
                if (process.HasExited)
                {
                    clientState += " exitCode=" + process.ExitCode
                        + " stdout=[" + process.StandardOutput.ReadToEnd().Trim() + "]"
                        + " stderr=[" + process.StandardError.ReadToEnd().Trim() + "]";
                }
            }

            try { server.Dispose(); } catch (ObjectDisposedException) { }
            throw new InvalidOperationException("FAIL scenario timed out waiting for the server verdict on " + pipeName
                + "; " + clientState
                + (clientError is null ? string.Empty : " (client error: " + clientError.GetType().Name + ")"));
        }

        if (clientError is not null)
        {
            throw new InvalidOperationException("FAIL scenario client failed for " + pipeName, clientError);
        }

        if (serverError is not null)
        {
            throw new InvalidOperationException("FAIL scenario server failed for " + pipeName + ": " + serverError.Message, serverError);
        }

        if (process is not null)
        {
            try
            {
                if (!process.WaitForExit((int)TimeSpan.FromSeconds(15).TotalMilliseconds))
                {
                    process.Kill(entireProcessTree: true);
                }

                var output = process.StandardOutput.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("  client: " + output.Trim());
                }
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
        }

        return Task.FromResult(outcome!);
    }

    private static bool ReadExact(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static void AssertPipeAclCurrentUserOnly(NamedPipeServerStream server)
    {
        var security = server.GetAccessControl();
        var currentUser = WindowsIdentity.GetCurrent().User;
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));
        var allowCount = 0;
        foreach (PipeAccessRule rule in rules)
        {
            var sid = (SecurityIdentifier)rule.IdentityReference;
            Console.WriteLine("  pipe ACL: " + rule.AccessControlType + " " + rule.PipeAccessRights + " " + sid + " (" + DescribeSid(sid) + ")");
            if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow)
            {
                allowCount++;
                Check.True(
                    !BroadSids.Any(broad => broad.Equals(sid)),
                    "Pipe ACL grants no broad principal (" + DescribeSid(sid) + ")");
                var isCurrentUser = currentUser is not null && sid.Equals(currentUser);
                Check.True(
                    isCurrentUser || AllowedGranteeSids.Any(allowed => allowed.Equals(sid)),
                    "Pipe ACL allow entry is the current user or an owner-equivalent principal (" + DescribeSid(sid) + ")");
            }
        }

        Check.True(allowCount >= 1, "Pipe ACL grants at least one principal (effective DACL observed)");
        Console.WriteLine("  effective pipe DACL: " + allowCount + " allow rule(s), current user " + currentUser);
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
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
        client.Connect((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        var payload = Encoding.UTF8.GetBytes(
            "{\"protocolVersion\":1,\"messageId\":\"forged-inproc-1\",\"type\":\"playback.observation.v1\","
            + "\"browserInstanceId\":\"forged-instance-id\",\"siteOrigin\":\"https://music.example\","
            + "\"sequenceNumber\":1,\"artist\":\"synthetic-artist\",\"title\":\"synthetic-title\"}");
        // Stay connected while the server verifies the peer: a client that connects and
        // disconnects inside the accept race can leave the server accept pending.
        Thread.Sleep(TimeSpan.FromSeconds(2));
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

    private static Process StartViaFakeBrowser(string fakeBrowserPath, string hostPath, string pipeName)
    {
        var start = new ProcessStartInfo
        {
            FileName = fakeBrowserPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("spawn");
        start.ArgumentList.Add(hostPath);
        start.ArgumentList.Add(pipeName);
        return Process.Start(start)!;
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
