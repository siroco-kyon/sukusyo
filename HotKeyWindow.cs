using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class HotKeyEventArgs(int id) : EventArgs
{
    public int Id { get; } = id;
}

internal sealed class HotKeyWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;

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

    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    private readonly HashSet<int> _registeredIds = [];

    public HotKeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public bool Register(int id, Modifiers modifiers, Keys key)
    {
        if (_registeredIds.Contains(id))
        {
            UnregisterHotKey(Handle, id);
            _registeredIds.Remove(id);
        }
        bool ok = RegisterHotKey(Handle, id, (uint)(modifiers | Modifiers.NoRepeat), (uint)key);
        if (ok)
        {
            _registeredIds.Add(id);
        }
        return ok;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _registeredIds.Contains((int)m.WParam))
        {
            HotKeyPressed?.Invoke(this, new HotKeyEventArgs((int)m.WParam));
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in _registeredIds)
        {
            UnregisterHotKey(Handle, id);
        }
        _registeredIds.Clear();
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
