using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sukusyo;

internal static class ScreenCapture
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    public static Bitmap CaptureRegion(Rectangle regionInVirtualPixels)
    {
        var bmp = new Bitmap(regionInVirtualPixels.Width, regionInVirtualPixels.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(regionInVirtualPixels.Location, Point.Empty, regionInVirtualPixels.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    public static void CopyToClipboard(Bitmap bmp)
    {
        Clipboard.SetImage(bmp);
    }
}
