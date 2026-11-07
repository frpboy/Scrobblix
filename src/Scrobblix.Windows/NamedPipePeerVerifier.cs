using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Scrobblix.Windows;

/// <summary>
/// Adds a same-user pipe peer check on top of PipeOptions.CurrentUserOnly. Browser ingress
/// is accepted only from the exact native-host executable installed by Scrobblix and when
/// the native host's parent image is a Chrome, Edge, or Firefox executable in that
/// browser's standard install directory.
///
/// This is a spoofing-resistance boundary, not cryptographic caller authentication. A
/// same-user process that can write into the browser's install directory can still forge
/// the parent chain; see docs/security/SECURITY_AUDIT_FOLLOW_UP_2026-10-28.md. Cross-user
/// isolation comes from PipeOptions.CurrentUserOnly and the pipe ACL, not from this check.
/// </summary>
public static class NamedPipePeerVerifier
{
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessBasicInformationClass = 0;

    public static bool IsTrustedBrowserNativeHost(
        IntPtr pipeHandle,
        string expectedNativeHostPath,
        out string rejectionReason)
    {
        rejectionReason = "unknown";
        if (!OperatingSystem.IsWindows())
        {
            rejectionReason = "windows_required";
            return false;
        }

        if (pipeHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(expectedNativeHostPath))
        {
            rejectionReason = "invalid_pipe_or_expected_path";
            return false;
        }

        if (!GetNamedPipeClientProcessId(pipeHandle, out var clientProcessId) || clientProcessId == 0)
        {
            rejectionReason = "client_process_id_unavailable";
            return false;
        }

        using var clientProcess = OpenProcess(
            ProcessQueryInformation | ProcessQueryLimitedInformation,
            inheritHandle: false,
            clientProcessId);
        if (clientProcess.IsInvalid)
        {
            rejectionReason = "client_process_unavailable";
            return false;
        }

        if (!TryGetProcessImagePath(clientProcess, out var clientImagePath))
        {
            rejectionReason = "client_image_unavailable";
            return false;
        }

        try
        {
            if (!string.Equals(
                Path.GetFullPath(clientImagePath),
                Path.GetFullPath(expectedNativeHostPath),
                StringComparison.OrdinalIgnoreCase))
            {
                rejectionReason = "client_image_not_registered_native_host";
                return false;
            }
        }
        catch (ArgumentException)
        {
            rejectionReason = "client_image_path_invalid";
            return false;
        }
        catch (IOException)
        {
            rejectionReason = "client_image_path_unavailable";
            return false;
        }

        var status = NtQueryInformationProcess(
            clientProcess,
            ProcessBasicInformationClass,
            out var processInfo,
            Marshal.SizeOf<ProcessBasicInformation>(),
            out _);
        if (status < 0 || processInfo.InheritedFromUniqueProcessId == IntPtr.Zero)
        {
            rejectionReason = "parent_process_unavailable";
            return false;
        }

        var parentProcessId = unchecked((uint)processInfo.InheritedFromUniqueProcessId.ToInt64());
        using var parentProcess = OpenProcess(
            ProcessQueryInformation | ProcessQueryLimitedInformation,
            inheritHandle: false,
            parentProcessId);
        if (parentProcess.IsInvalid)
        {
            rejectionReason = "parent_process_unavailable";
            return false;
        }

        if (!TryGetProcessImagePath(parentProcess, out var parentImagePath)
            || !IsSupportedBrowserImagePath(parentImagePath))
        {
            rejectionReason = "native_host_parent_not_supported_browser";
            return false;
        }

        rejectionReason = "accepted";
        return true;
    }

    /// <summary>
    /// Legacy basename-only predicate. This is NOT an authorization decision: a renamed
    /// executable satisfies it. It is retained for differential regression tests that prove
    /// the stronger <see cref="IsSupportedBrowserImagePath"/> check rejects such spoofs.
    /// </summary>
    public static bool IsSupportedBrowserExecutableName(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        var name = Path.GetFileName(imagePath);
        return IsSupportedBrowserFileName(name);
    }

    /// <summary>
    /// Accepts only browser executable images launched from the vendor's standard install
    /// directory under Program Files, Program Files (x86), or the user's LocalApplicationData
    /// (per-user Chrome/Firefox installs). Fails closed for any other path, including a
    /// renamed copy of an unrelated executable in an arbitrary directory.
    /// </summary>
    public static bool IsSupportedBrowserImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(imagePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }

        var name = Path.GetFileName(fullPath);
        if (!IsSupportedBrowserFileName(name))
        {
            return false;
        }

        string vendorDirectory;
        if (string.Equals(name, "chrome.exe", StringComparison.OrdinalIgnoreCase))
        {
            vendorDirectory = Path.Combine("Google", "Chrome", "Application");
        }
        else if (string.Equals(name, "msedge.exe", StringComparison.OrdinalIgnoreCase))
        {
            vendorDirectory = Path.Combine("Microsoft", "Edge", "Application");
        }
        else
        {
            vendorDirectory = "Mozilla Firefox";
        }

        foreach (var root in BrowserInstallRoots())
        {
            var expected = Path.Combine(root, vendorDirectory, name);
            if (string.Equals(fullPath, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedBrowserFileName(string? fileName)
    {
        return string.Equals(fileName, "chrome.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "msedge.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "firefox.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BrowserInstallRoots()
    {
        foreach (var folder in new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.LocalApplicationData,
        })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static bool TryGetProcessImagePath(SafeProcessHandle process, out string imagePath)
    {
        var buffer = new char[32768];
        uint length = (uint)buffer.Length;
        if (!QueryFullProcessImageName(process, 0, buffer, ref length) || length == 0 || length > buffer.Length)
        {
            imagePath = string.Empty;
            return false;
        }

        imagePath = new string(buffer, 0, checked((int)length));
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public int ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetNamedPipeClientProcessId", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr pipeHandle, out uint clientProcessId);

    // The return value is a handle, not a boolean: a [return: MarshalAs(UnmanagedType.Bool)]
    // attribute here throws MarshalDirectiveException on every call, which made the verifier
    // fail closed (browser ingress unusable) until the #74 scenario tests caught it.
    [DllImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] imageName, ref uint size);

    [DllImport("ntdll.dll", EntryPoint = "NtQueryInformationProcess")]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle process,
        int processInformationClass,
        out ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);
}
