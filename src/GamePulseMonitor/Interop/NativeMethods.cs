using System.Runtime.InteropServices;

namespace GamePulseMonitor.Interop;

internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const int HotkeyToggleOverlay = 1001;
    public const int HotkeyExit = 1002;
    public const int HotkeyToggleBenchmark = 1003;
    public const uint ModAlt = 0x0001;
    public const uint ModShift = 0x0004;
    public const uint ModControl = 0x0002;
    public const uint VirtualKeyA = 0x41;
    public const uint VirtualKeyF11 = 0x7A;
    public const uint VirtualKeyF12 = 0x7B;
    public const int VirtualKeyMenu = 0x12;

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    public static void MakeOverlayWindow(nint hwnd, bool clickThrough)
    {
        var style = GetExtendedStyle(hwnd);
        style |= WsExToolWindow | WsExLayered | WsExNoActivate;
        if (clickThrough)
        {
            style |= WsExTransparent;
        }

        SetExtendedStyle(hwnd, style);
    }

    public static void SetOverlayClickThrough(nint hwnd, bool clickThrough)
    {
        var style = GetExtendedStyle(hwnd);
        if (clickThrough)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~WsExTransparent;
        }

        SetExtendedStyle(hwnd, style);
    }

    public static bool IsAltPressed()
    {
        return (GetAsyncKeyState(VirtualKeyMenu) & unchecked((short)0x8000)) != 0;
    }

    public static bool IsKeyPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }

    private static int GetExtendedStyle(nint hwnd)
    {
        return nint.Size == 8
            ? unchecked((int)GetWindowLongPtr64(hwnd, GwlExStyle))
            : GetWindowLong32(hwnd, GwlExStyle);
    }

    private static void SetExtendedStyle(nint hwnd, int style)
    {
        if (nint.Size == 8)
        {
            SetWindowLongPtr64(hwnd, GwlExStyle, style);
        }
        else
        {
            SetWindowLong32(hwnd, GwlExStyle, style);
        }

        SetWindowPos(hwnd, nint.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }
}
