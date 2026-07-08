using System.Threading;
using System.Windows.Forms;

namespace Sukusyo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Mutex mutex;
        bool isNew;
        try
        {
            mutex = new Mutex(true, "Sukusyo.SingleInstance", out isNew);
        }
        catch (AbandonedMutexException ex)
        {
            // a previous instance crashed while holding the lock; we now own it
            mutex = (Mutex)ex.Mutex!;
            isNew = true;
        }

        using (mutex)
        {
            if (!isNew)
            {
                // a taskbar Jump List task always launches a new process; forward
                // the requested action to the already-running instance instead
                if (args.Length > 0 && CommandRelay.TryParseArgument(args[0], out var command))
                {
                    CommandRelay.Broadcast(command);
                }
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
