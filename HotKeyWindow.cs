using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class HotKeyWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0xB001;

    [Flags]
    public enum Modifiers : uint
    {
        None = 0x0,
        Alt = 0x1,
        Control = 0x2,
        Shift = 0x4,
        Win = 0x8,
        NoRepeat = 0x4000,
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event EventHandler? HotKeyPressed;

    private bool _registered;

    public HotKeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public bool Register(Modifiers modifiers, Keys key)
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
            _registered = false;
        }
        _registered = RegisterHotKey(Handle, HOTKEY_ID, (uint)(modifiers | Modifiers.NoRepeat), (uint)key);
        return _registered;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && (int)m.WParam == HOTKEY_ID)
        {
            HotKeyPressed?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
            _registered = false;
        }
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
