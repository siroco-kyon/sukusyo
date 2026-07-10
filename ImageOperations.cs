using System.Drawing;
using System.Drawing.Imaging;

namespace Sukusyo;

internal enum JoinDirection
{
    Above,
    Below,
    Left,
    Right,
}

internal static class ImageOperations
{
    public static Bitmap Clone(Image image)
    {
        var clone = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        clone.SetResolution(image.HorizontalResolution > 0 ? image.HorizontalResolution : 96f,
            image.VerticalResolution > 0 ? image.VerticalResolution : 96f);
        using var graphics = Graphics.FromImage(clone);
        graphics.DrawImageUnscaled(image, Point.Empty);
        return clone;
    }

    public static Bitmap Crop(Bitmap source, Rectangle selection)
    {
        selection = Rectangle.Intersect(new Rectangle(Point.Empty, source.Size), selection);
        if (selection.Width < 1 || selection.Height < 1)
        {
            throw new ArgumentException("切り抜く範囲がありません。", nameof(selection));
        }

        var result = new Bitmap(selection.Width, selection.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.DrawImage(source, new Rectangle(Point.Empty, result.Size), selection, GraphicsUnit.Pixel);
        return result;
    }

    public static Bitmap RemoveHorizontalStrip(Bitmap source, Rectangle selection)
    {
        selection = Rectangle.Intersect(new Rectangle(Point.Empty, source.Size), selection);
        if (selection.Height < 1 || selection.Height >= source.Height)
        {
            throw new ArgumentException("横ぶっこ抜きできる範囲を選択してください。", nameof(selection));
        }

        var result = new Bitmap(source.Width, source.Height - selection.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        if (selection.Top > 0)
        {
            var top = new Rectangle(0, 0, source.Width, selection.Top);
            graphics.DrawImage(source, top, top, GraphicsUnit.Pixel);
        }
        if (selection.Bottom < source.Height)
        {
            var sourceBottom = new Rectangle(0, selection.Bottom, source.Width, source.Height - selection.Bottom);
            var targetBottom = new Rectangle(0, selection.Top, source.Width, sourceBottom.Height);
            graphics.DrawImage(source, targetBottom, sourceBottom, GraphicsUnit.Pixel);
        }
        return result;
    }

    public static Bitmap RemoveVerticalStrip(Bitmap source, Rectangle selection)
    {
        selection = Rectangle.Intersect(new Rectangle(Point.Empty, source.Size), selection);
        if (selection.Width < 1 || selection.Width >= source.Width)
        {
            throw new ArgumentException("縦ぶっこ抜きできる範囲を選択してください。", nameof(selection));
        }

        var result = new Bitmap(source.Width - selection.Width, source.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        if (selection.Left > 0)
        {
            var left = new Rectangle(0, 0, selection.Left, source.Height);
            graphics.DrawImage(source, left, left, GraphicsUnit.Pixel);
        }
        if (selection.Right < source.Width)
        {
            var sourceRight = new Rectangle(selection.Right, 0, source.Width - selection.Right, source.Height);
            var targetRight = new Rectangle(selection.Left, 0, sourceRight.Width, source.Height);
            graphics.DrawImage(source, targetRight, sourceRight, GraphicsUnit.Pixel);
        }
        return result;
    }

    public static Bitmap RotateFlip(Bitmap source, RotateFlipType operation)
    {
        var result = Clone(source);
        result.RotateFlip(operation);
        return result;
    }

    public static Bitmap Join(Bitmap source, Image clipboardImage, JoinDirection direction)
    {
        using var other = Clone(clipboardImage);
        var horizontal = direction is JoinDirection.Left or JoinDirection.Right;
        var width = horizontal ? source.Width + other.Width : Math.Max(source.Width, other.Width);
        var height = horizontal ? Math.Max(source.Height, other.Height) : source.Height + other.Height;
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);

        var sourcePoint = direction switch
        {
            JoinDirection.Above => new Point(0, other.Height),
            JoinDirection.Left => new Point(other.Width, 0),
            _ => Point.Empty,
        };
        var otherPoint = direction switch
        {
            JoinDirection.Below => new Point(0, source.Height),
            JoinDirection.Right => new Point(source.Width, 0),
            _ => Point.Empty,
        };

        graphics.DrawImageUnscaled(source, sourcePoint);
        graphics.DrawImageUnscaled(other, otherPoint);
        return result;
    }
}
