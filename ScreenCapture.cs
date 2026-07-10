using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Sukusyo;

internal static class ScreenCapture
{
    public static Bitmap CaptureRegion(Rectangle regionInVirtualPixels)
    {
        var bmp = new Bitmap(regionInVirtualPixels.Width, regionInVirtualPixels.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(regionInVirtualPixels.Location, Point.Empty, regionInVirtualPixels.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    public static Bitmap CaptureVirtualScreen()
    {
        return CaptureRegion(SystemInformation.VirtualScreen);
    }

    public static void CopyToClipboard(Bitmap bmp)
    {
        // The clipboard is briefly locked surprisingly often by Office and remote
        // desktop software. A short retry keeps the capture gesture dependable.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetImage(bmp);
                return;
            }
            catch (System.Runtime.InteropServices.ExternalException) when (attempt < 4)
            {
                Thread.Sleep(40 * (attempt + 1));
            }
        }
    }

    public static void Save(Bitmap bmp, string path)
    {
        var format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            _ => ImageFormat.Png,
        };
        bmp.Save(path, format);
    }
}
