using System.Drawing;
using Sukusyo;

var tests = new (string Name, Action Run)[]
{
    ("Clone preserves pixels", ClonePreservesPixels),
    ("Crop extracts the requested rectangle", CropExtractsRectangle),
    ("Horizontal strip removal closes the gap", HorizontalRemovalClosesGap),
    ("Vertical strip removal closes the gap", VerticalRemovalClosesGap),
    ("Rotation swaps dimensions", RotationSwapsDimensions),
    ("Joining images uses the expected canvas", JoiningUsesExpectedCanvas),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {test.Name}: {ex.Message}");
    }
}

foreach (var failure in failures)
{
    Console.Error.WriteLine(failure);
}

return failures.Count == 0 ? 0 : 1;

static Bitmap CreateFixture()
{
    var bitmap = new Bitmap(4, 3);
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            bitmap.SetPixel(x, y, Color.FromArgb(255, x * 40, y * 60, x + y));
        }
    }
    return bitmap;
}

static void ClonePreservesPixels()
{
    using var source = CreateFixture();
    using var clone = ImageOperations.Clone(source);
    AssertEqual(source.Size, clone.Size, "size");
    AssertEqual(source.GetPixel(3, 2).ToArgb(), clone.GetPixel(3, 2).ToArgb(), "pixel");
}

static void CropExtractsRectangle()
{
    using var source = CreateFixture();
    using var result = ImageOperations.Crop(source, new Rectangle(1, 1, 2, 2));
    AssertEqual(new Size(2, 2), result.Size, "size");
    AssertEqual(source.GetPixel(1, 1).ToArgb(), result.GetPixel(0, 0).ToArgb(), "top-left pixel");
}

static void HorizontalRemovalClosesGap()
{
    using var source = CreateFixture();
    using var result = ImageOperations.RemoveHorizontalStrip(source, new Rectangle(0, 1, 4, 1));
    AssertEqual(new Size(4, 2), result.Size, "size");
    AssertEqual(source.GetPixel(2, 2).ToArgb(), result.GetPixel(2, 1).ToArgb(), "shifted bottom pixel");
}

static void VerticalRemovalClosesGap()
{
    using var source = CreateFixture();
    using var result = ImageOperations.RemoveVerticalStrip(source, new Rectangle(1, 0, 2, 3));
    AssertEqual(new Size(2, 3), result.Size, "size");
    AssertEqual(source.GetPixel(3, 1).ToArgb(), result.GetPixel(1, 1).ToArgb(), "shifted right pixel");
}

static void RotationSwapsDimensions()
{
    using var source = CreateFixture();
    using var result = ImageOperations.RotateFlip(source, RotateFlipType.Rotate90FlipNone);
    AssertEqual(new Size(3, 4), result.Size, "size");
}

static void JoiningUsesExpectedCanvas()
{
    using var source = CreateFixture();
    using var other = new Bitmap(2, 5);
    using var right = ImageOperations.Join(source, other, JoinDirection.Right);
    using var above = ImageOperations.Join(source, other, JoinDirection.Above);
    AssertEqual(new Size(6, 5), right.Size, "right size");
    AssertEqual(new Size(4, 8), above.Size, "above size");
}

static void AssertEqual<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
    }
}
