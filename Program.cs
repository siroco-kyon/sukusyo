using System.Windows.Forms;

namespace Sukusyo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new System.Threading.Mutex(true, "Sukusyo.SingleInstance", out bool isNew);
        if (!isNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
