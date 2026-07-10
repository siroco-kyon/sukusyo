using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sukusyo;

internal enum RemoteCommand
{
    Clipboard,
    Pin,
    OpenImage,
    Settings,
    BringAllToFront,
    CloseAllPins,
    Exit,
}

/// <summary>
/// Relays a command from a second, throwaway process launch (e.g. a taskbar
/// Jump List task, which always starts a new process) to the single running
/// instance via a registered window message broadcast.
/// </summary>
internal sealed class CommandRelay : NativeWindow, IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HwndBroadcast = new(0xffff);
    private static readonly int RelayMessageId = RegisterWindowMessage("Sukusyo.CommandRelay");

    public event EventHandler<RemoteCommand>? CommandReceived;

    public CommandRelay()
    {
        CreateHandle(new CreateParams());
    }

    public static bool TryParseArgument(string arg, out RemoteCommand command)
    {
        switch (arg)
        {
            case "--clipboard": command = RemoteCommand.Clipboard; return true;
            case "--pin": command = RemoteCommand.Pin; return true;
            case "--open": command = RemoteCommand.OpenImage; return true;
            case "--settings": command = RemoteCommand.Settings; return true;
            case "--bring-front": command = RemoteCommand.BringAllToFront; return true;
            case "--close-pins": command = RemoteCommand.CloseAllPins; return true;
            case "--exit": command = RemoteCommand.Exit; return true;
            default: command = default; return false;
        }
    }

    public static void Broadcast(RemoteCommand command)
    {
        PostMessage(HwndBroadcast, RelayMessageId, (IntPtr)(int)command, IntPtr.Zero);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == RelayMessageId)
        {
            CommandReceived?.Invoke(this, (RemoteCommand)(int)m.WParam);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
