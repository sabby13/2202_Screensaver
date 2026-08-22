using System.Drawing;
using System.Runtime.InteropServices;

namespace GlassButterfly;

/// <summary>
/// Low-level global keyboard/mouse hooks that fire once on real user input, used
/// to dismiss the screensaver. Needed because WebView2 is a child HWND that
/// consumes input before Form-level events see it. A grace period ignores the
/// initial cursor position Windows reports at start, and a small movement
/// threshold avoids spurious dismissal from tiny jitter.
/// </summary>
internal sealed class InputExitHook : IDisposable
{
    public event EventHandler? Triggered;

    private readonly int _threshold;
    private NativeMethods.HookProc? _mouseProc;
    private NativeMethods.HookProc? _keyboardProc;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private Point? _initial;
    private bool _fired;

    public InputExitHook(int threshold = 8) => _threshold = threshold;

    public void Start()
    {
        // Keep the delegates alive for the lifetime of the hooks.
        _mouseProc = MouseCallback;
        _keyboardProc = KeyboardCallback;
        IntPtr module = NativeMethods.GetModuleHandle(null);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, module, 0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, module, 0);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_fired)
        {
            int msg = (int)wParam;
            switch (msg)
            {
                case NativeMethods.WM_MOUSEMOVE:
                    var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    var p = new Point(data.pt.x, data.pt.y);
                    if (_initial is null)
                        _initial = p; // first report = grace; don't dismiss
                    else if (Math.Abs(p.X - _initial.Value.X) > _threshold ||
                             Math.Abs(p.Y - _initial.Value.Y) > _threshold)
                        Fire();
                    break;

                case NativeMethods.WM_LBUTTONDOWN:
                case NativeMethods.WM_RBUTTONDOWN:
                case NativeMethods.WM_MBUTTONDOWN:
                case NativeMethods.WM_XBUTTONDOWN:
                case NativeMethods.WM_MOUSEWHEEL:
                    Fire();
                    break;
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_fired)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                Fire();
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void Fire()
    {
        if (_fired) return;
        _fired = true;
        Triggered?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }
}
