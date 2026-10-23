using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Scrobblix.Desktop;

/// <summary>
/// Owns the existing Win32 notification-area icon and native context menu.
/// The menu is rebuilt from the window's latest local read-model snapshot each time it
/// opens; building it performs no storage or provider I/O.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const uint WmSize = 0x0005;
    private const uint WmNull = 0x0000;
    private const uint WmApp = 0x8000;
    private const uint TrayCallbackMessage = WmApp + 0x31;
    private const uint SizeMinimized = 1;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifGuid = 0x00000020;
    private const uint ImageIcon = 1;
    private const uint LoadImageFromFile = 0x0010;
    private const uint LoadImageDefaultSize = 0x0040;
    private const uint MfString = 0x00000000;
    private const uint MfGray = 0x00000001;
    private const uint MfDisabled = 0x00000002;
    private const uint MfSeparator = 0x00000800;
    private const uint MfPopup = 0x00000010;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private const uint OpenCommand = 1001;
    private const uint ExitCommand = 1002;
    private const uint TrackingCommand = 1003;
    private const uint LoveTrackCommand = 1004;
    private const uint CopyTrackCommand = 1005;
    private const uint RetryFailedCommand = 1006;
    private const uint OpenQueueCommand = 1007;
    private const uint OpenSettingsCommand = 1008;
    private const uint OpenDiagnosticsCommand = 1009;
    private const uint OpenLogFolderCommand = 1010;
    private const uint SubclassId = 1;
    private static readonly Guid TrayGuid = new("605D2E99-F0D5-4F49-97ED-BAF322DC27A1");

    private readonly Window _window;
    private readonly MainWindow _mainWindow;
    private readonly IntPtr _windowHandle;
    private readonly SubclassProcedure _subclassProcedure;
    private readonly NotifyIconData _iconData;
    private readonly IntPtr _ownedIcon;
    private bool _iconInstalled;
    private bool _disposed;
    private bool _restoring;

    public TrayIconService(MainWindow window)
    {
        _mainWindow = window ?? throw new ArgumentNullException(nameof(window));
        _window = window;
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _subclassProcedure = WindowSubclass;
        if (!SetWindowSubclass(_windowHandle, _subclassProcedure, new UIntPtr(SubclassId), UIntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not attach the Scrobblix tray window handler.");
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "scrobblix.ico");
        var ownedIcon = File.Exists(iconPath)
            ? LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LoadImageFromFile | LoadImageDefaultSize)
            : IntPtr.Zero;
        _ownedIcon = ownedIcon;
        var systemIcon = ownedIcon != IntPtr.Zero ? ownedIcon : LoadIcon(IntPtr.Zero, new IntPtr(32512));
        _iconData = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            WindowHandle = _windowHandle,
            Id = 1,
            Flags = NifMessage | NifIcon | NifTip | NifGuid,
            CallbackMessage = TrayCallbackMessage,
            IconHandle = systemIcon,
            Tip = "Scrobblix — listening companion",
            ItemGuid = TrayGuid,
            Info = string.Empty,
            InfoTitle = string.Empty,
        };

        var iconData = _iconData;
        if (!Shell_NotifyIcon(NimAdd, ref iconData))
        {
            _ = RemoveWindowSubclass(_windowHandle, _subclassProcedure, new UIntPtr(SubclassId));
            if (_ownedIcon != IntPtr.Zero) _ = DestroyIcon(_ownedIcon);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not add the Scrobblix notification-area icon.");
        }

        _iconInstalled = true;
    }

    private IntPtr WindowSubclass(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData)
    {
        if (message == WmSize && unchecked((uint)wParam.ToInt64()) == SizeMinimized)
        {
            _restoring = false;
            _ = ShowWindow(windowHandle, SwHide);
            return DefSubclassProc(windowHandle, message, wParam, lParam);
        }

        if (message == TrayCallbackMessage)
        {
            var notification = unchecked((uint)lParam.ToInt64());
            if (notification is WmLButtonUp or WmLButtonDoubleClick)
            {
                RestoreWindow();
            }
            else if (notification == WmRButtonUp)
            {
                ShowContextMenu();
            }
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    public void RestoreFromNotification() => RestoreWindow();

    private void RestoreWindow()
    {
        if (_disposed || _restoring) return;
        _restoring = true;
        try
        {
            _ = ShowWindow(_windowHandle, SwRestore);
            _window.Activate();
        }
        finally
        {
            _restoring = false;
        }
    }

    private void ShowContextMenu()
    {
        if (_disposed) return;

        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            var status = _mainWindow.GetTrayMenuStatus();
            AppendStatusItem(menu, "Scrobblix — Running");
            AppendStatusItem(menu, status.LastFmLabel);
            AppendStatusItem(menu, status.PlaybackLabel);
            AppendStatusItem(menu, status.QueueLabel);
            AppendSeparator(menu);

            AppendItem(menu, OpenCommand, "Open Scrobblix", status.IsClosed);
            AppendItem(menu, TrackingCommand, status.TrackingCommandLabel, status.IsClosed);

            var currentTrackMenu = CreatePopupMenu();
            if (currentTrackMenu != IntPtr.Zero)
            {
                AppendStatusItem(currentTrackMenu, status.CurrentTrackStatusLabel);
                AppendSeparator(currentTrackMenu);
                AppendItem(currentTrackMenu, LoveTrackCommand, status.LoveCommandLabel, !status.CanLoveTrack);
                AppendItem(currentTrackMenu, CopyTrackCommand, "Copy Track Details", !status.CanCopyTrackDetails);
                AppendSubmenu(menu, currentTrackMenu, "Current Track");
            }

            var queueMenu = CreatePopupMenu();
            if (queueMenu != IntPtr.Zero)
            {
                AppendStatusItem(queueMenu, status.QueueLabel);
                if (status.RetryableFailedScrobbleCount is > 0)
                {
                    AppendItem(
                        queueMenu,
                        RetryFailedCommand,
                        $"Retry Failed Scrobbles ({status.RetryableFailedScrobbleCount.Value:N0})",
                        !status.CanRetryFailedScrobbles);
                }
                else
                {
                    AppendItem(queueMenu, 0, status.QueueRetryStatusLabel, disabled: true);
                }

                AppendItem(queueMenu, OpenQueueCommand, "Open Queue", status.IsClosed);
                AppendSubmenu(menu, queueMenu, "Scrobble Queue");
            }

            AppendSeparator(menu);
            AppendItem(menu, OpenLogFolderCommand, "Open Log Folder", status.IsClosed);
            AppendItem(menu, OpenSettingsCommand, "Open Settings", status.IsClosed);
            AppendItem(menu, OpenDiagnosticsCommand, "Open Diagnostics", status.IsClosed);
            AppendSeparator(menu);
            AppendItem(menu, ExitCommand, "Exit Scrobblix", status.IsClosed);

            _ = SetForegroundWindow(_windowHandle);
            if (!GetCursorPos(out var point)) return;
            var command = TrackPopupMenu(
                menu,
                TpmRightButton | TpmReturnCommand,
                point.X,
                point.Y,
                0,
                _windowHandle,
                IntPtr.Zero);

            // The documented WM_NULL post helps Windows dismiss the native popup reliably.
            _ = PostMessage(_windowHandle, WmNull, IntPtr.Zero, IntPtr.Zero);
            if (!_disposed)
            {
                DispatchCommand(command);
            }
        }
        catch (Exception exception)
        {
            // Tray UI must never take down the listening process. Report only the operation and
            // exception type/HRESULT; never include track names, account details or credentials.
            try { _mainWindow.ReportTrayFailure("context menu", exception); }
            catch { System.Diagnostics.Debug.WriteLine($"Scrobblix tray menu failed: {exception.GetType().Name}"); }
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    private void DispatchCommand(uint command)
    {
        if (_disposed) return;

        switch (command)
        {
            case OpenCommand:
                RestoreWindow();
                break;
            case TrackingCommand:
                var status = _mainWindow.GetTrayMenuStatus();
                if (!status.IsClosed)
                {
                    _mainWindow.SetTrackingEnabledFromTray(!status.TrackingEnabled);
                }
                break;
            case LoveTrackCommand:
                if (_mainWindow.GetTrayMenuStatus().CanLoveTrack)
                {
                    _ = _mainWindow.ToggleLovedTrackFromTrayAsync();
                }
                break;
            case CopyTrackCommand:
                _mainWindow.CopyCurrentTrackDetailsFromTray();
                break;
            case RetryFailedCommand:
                if (_mainWindow.GetTrayMenuStatus().CanRetryFailedScrobbles)
                {
                    _ = _mainWindow.RetryFailedScrobblesFromTrayAsync();
                }
                break;
            case OpenQueueCommand:
                RestoreWindow();
                _mainWindow.NavigateFromTray("queue");
                break;
            case OpenSettingsCommand:
                RestoreWindow();
                _mainWindow.NavigateFromTray("settings");
                break;
            case OpenDiagnosticsCommand:
                RestoreWindow();
                _mainWindow.NavigateFromTray("diagnostics");
                break;
            case OpenLogFolderCommand:
                _mainWindow.OpenLogFolderFromTray();
                break;
            case ExitCommand:
                _window.Close();
                break;
        }
    }

    private static void AppendStatusItem(IntPtr menu, string label) =>
        _ = AppendMenu(menu, MfString | MfGray | MfDisabled, UIntPtr.Zero, label);

    private static void AppendItem(IntPtr menu, uint command, string label, bool disabled = false) =>
        _ = AppendMenu(
            menu,
            MfString | (disabled ? MfGray | MfDisabled : 0),
            new UIntPtr(command),
            label);

    private static void AppendSeparator(IntPtr menu) =>
        _ = AppendMenu(menu, MfSeparator, UIntPtr.Zero, string.Empty);

    private static void AppendSubmenu(IntPtr menu, IntPtr submenu, string label)
    {
        if (submenu == IntPtr.Zero) return;
        if (!AppendMenu(menu, MfPopup, new UIntPtr(unchecked((ulong)submenu.ToInt64())), label))
        {
            _ = DestroyMenu(submenu);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_iconInstalled)
        {
            var iconData = _iconData;
            _ = Shell_NotifyIcon(NimDelete, ref iconData);
            _iconInstalled = false;
        }

        _ = RemoveWindowSubclass(_windowHandle, _subclassProcedure, new UIntPtr(SubclassId));
        if (_ownedIcon != IntPtr.Zero) _ = DestroyIcon(_ownedIcon);
        GC.KeepAlive(_subclassProcedure);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private delegate IntPtr SubclassProcedure(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", EntryPoint = "LoadIconW", SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint imageType, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("comctl32.dll", EntryPoint = "SetWindowSubclass", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr windowHandle, SubclassProcedure callback, UIntPtr subclassId, UIntPtr referenceData);

    [DllImport("comctl32.dll", EntryPoint = "RemoveWindowSubclass", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr windowHandle, SubclassProcedure callback, UIntPtr subclassId);

    [DllImport("comctl32.dll", EntryPoint = "DefSubclassProc")]
    private static extern IntPtr DefSubclassProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr itemId, string itemText);

    [DllImport("user32.dll", EntryPoint = "TrackPopupMenu", SetLastError = true)]
    private static extern uint TrackPopupMenu(
        IntPtr menu, uint flags, int x, int y, int reserved, IntPtr windowHandle, IntPtr rect);

    [DllImport("user32.dll", EntryPoint = "DestroyMenu", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
