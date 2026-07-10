using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Sukusyo;

internal readonly record struct WindowCaptureTarget(Rectangle Bounds, string? Title);

internal static class WindowCapture
{
    private const uint GaRoot = 2;
    private const int DwmwaExtendedFrameBounds = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hWnd,
        int attribute,
        out NativeRect value,
        int valueSize);

    public static WindowCaptureTarget? FindAt(Point screenPoint, bool clientAreaOnly = false)
    {
        var window = WindowFromPoint(screenPoint);
        if (window == IntPtr.Zero)
        {
            return null;
        }

        window = GetAncestor(window, GaRoot);
        if (window == IntPtr.Zero || !IsWindowVisible(window))
        {
            return null;
        }

        Rectangle bounds;
        if (clientAreaOnly && TryGetClientBounds(window, out var clientBounds))
        {
            bounds = clientBounds;
        }
        else
        {
            NativeRect nativeRect;
            var result = DwmGetWindowAttribute(
                window,
                DwmwaExtendedFrameBounds,
                out nativeRect,
                Marshal.SizeOf<NativeRect>());
            if (result != 0 && !GetWindowRect(window, out nativeRect))
            {
                return null;
            }
            bounds = nativeRect.ToRectangle();
        }

        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return null;
        }

        return new WindowCaptureTarget(bounds, GetTitle(window));
    }

    private static bool TryGetClientBounds(IntPtr window, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (!GetClientRect(window, out var clientRect))
        {
            return false;
        }

        var topLeft = new Point(clientRect.Left, clientRect.Top);
        var bottomRight = new Point(clientRect.Right, clientRect.Bottom);
        if (!ClientToScreen(window, ref topLeft) || !ClientToScreen(window, ref bottomRight))
        {
            return false;
        }

        bounds = Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static string? GetTitle(IntPtr window)
    {
        var length = GetWindowTextLength(window);
        if (length < 1)
        {
            return null;
        }

        var builder = new StringBuilder(length + 1);
        GetWindowText(window, builder, builder.Capacity);
        return string.IsNullOrWhiteSpace(builder.ToString()) ? null : builder.ToString();
    }
}
