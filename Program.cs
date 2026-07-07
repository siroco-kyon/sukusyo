using System.Threading;
using System.Windows.Forms;

namespace Sukusyo;

internal static class Program
{
    [STAThread]
    private static void Main()
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
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
