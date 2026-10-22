using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Scrobblix.Desktop;

/// <summary>
/// Keeps the listening companion available in the Windows notification area while its
/// window is minimized. The app continues tracking; the tray menu provides a clear way
/// to restore the window or explicitly exit the application.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const uint WmSize = 0x0005;
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
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private const uint OpenCommand = 1001;
    private const uint ExitCommand = 1002;
    private const uint SubclassId = 1;
    private static readonly Guid TrayGuid = new("605D2E99-F0D5-4F49-97ED-BAF322DC27A1");

    private readonly Window _window;
    private readonly IntPtr _windowHandle;
    private readonly SubclassProcedure _subclassProcedure;
    private readonly NotifyIconData _iconData;
    private readonly IntPtr _ownedIcon;
    private bool _iconInstalled;
    private bool _disposed;
    private bool _restoring;

    public TrayIconService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
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
            RemoveWindowSubclass(_windowHandle, _subclassProcedure, new UIntPtr(SubclassId));
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
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            _ = AppendMenu(menu, MfString, new UIntPtr(OpenCommand), "Open Scrobblix");
            _ = AppendMenu(menu, MfSeparator, UIntPtr.Zero, string.Empty);
            _ = AppendMenu(menu, MfString, new UIntPtr(ExitCommand), "Exit Scrobblix");

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

            if (command == OpenCommand)
            {
                RestoreWindow();
            }
            else if (command == ExitCommand)
            {
                _window.Close();
            }

        }
        finally
        {
            _ = DestroyMenu(menu);
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

}
