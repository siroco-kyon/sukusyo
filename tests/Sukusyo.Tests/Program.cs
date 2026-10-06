using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Sukusyo;

var tests = new (string Name, Action Run)[]
{
    ("Clone preserves pixels", ClonePreservesPixels),
    ("Crop extracts the requested rectangle", CropExtractsRectangle),
    ("Snapshot crop translates negative monitor coordinates", SnapshotCropTranslatesCoordinates),
    ("Snapshot crop clips to the captured desktop", SnapshotCropClipsToDesktop),
    ("Snapshot crop survives source changes and disposal", SnapshotCropOwnsPixels),
    ("Horizontal strip removal closes the gap", HorizontalRemovalClosesGap),
    ("Vertical strip removal closes the gap", VerticalRemovalClosesGap),
    ("Rotation swaps dimensions", RotationSwapsDimensions),
    ("Joining images uses the expected canvas", JoiningUsesExpectedCanvas),
    ("Redaction replaces pixels with opaque black and preserves the source", RedactionReplacesPixels),
    ("Redaction clips to image bounds and rejects empty selections", RedactionHandlesBounds),
    ("Pinned redaction supports undo, redo and PNG export", PinnedRedactionWorkflow),
    ("Double-click does not hide a pin during drawing or selection", DoubleClickRespectsEditing),
    ("Double-click does not hide a pin while Ctrl or Shift is held", DoubleClickRespectsModifiers),
    ("Expired temporary hide waits for capture completion", TemporaryHideWaitsForCapture),
    ("Capture completion preserves an unexpired temporary hide", CapturePreservesHideTimer),
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

static void SnapshotCropTranslatesCoordinates()
{
    using var source = CreateFixture();
    using var result = ScreenCapture.CropSnapshot(source, new Point(-1920, -1080), new Rectangle(-1919, -1079, 2, 2));
    AssertEqual(new Size(2, 2), result.Size, "size");
    AssertEqual(source.GetPixel(1, 1).ToArgb(), result.GetPixel(0, 0).ToArgb(), "top-left pixel");
    AssertEqual(source.GetPixel(2, 2).ToArgb(), result.GetPixel(1, 1).ToArgb(), "bottom-right pixel");
}

static void SnapshotCropClipsToDesktop()
{
    using var source = CreateFixture();
    using var result = ScreenCapture.CropSnapshot(source, new Point(-4, 0), new Rectangle(-5, -1, 3, 3));
    AssertEqual(new Size(2, 2), result.Size, "clipped size");
    AssertEqual(source.GetPixel(0, 0).ToArgb(), result.GetPixel(0, 0).ToArgb(), "top-left pixel");
}

static void SnapshotCropOwnsPixels()
{
    Bitmap result;
    int originalPixel;
    using (var source = CreateFixture())
    {
        originalPixel = source.GetPixel(1, 1).ToArgb();
        result = ScreenCapture.CropSnapshot(source, Point.Empty, new Rectangle(1, 1, 2, 2));
        source.SetPixel(1, 1, Color.Magenta);
    }
    using (result)
    {
        AssertEqual(originalPixel, result.GetPixel(0, 0).ToArgb(), "frozen pixel");
    }
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

static void RedactionReplacesPixels()
{
    using var source = CreateFixture();
    source.SetPixel(2, 1, Color.FromArgb(50, 200, 100, 30));
    var original = source.GetPixel(2, 1).ToArgb();
    using var result = ImageOperations.Redact(source, new Rectangle(1, 1, 2, 2));
    AssertEqual(source.Size, result.Size, "size");
    for (var y = 0; y < source.Height; y++)
    {
        for (var x = 0; x < source.Width; x++)
        {
            var expected = x >= 1 && x < 3 && y >= 1 ? Color.Black.ToArgb() : source.GetPixel(x, y).ToArgb();
            AssertEqual(expected, result.GetPixel(x, y).ToArgb(), $"pixel {x},{y}");
        }
    }
    AssertEqual(original, source.GetPixel(2, 1).ToArgb(), "source unchanged");
}

static void RedactionHandlesBounds()
{
    using var source = CreateFixture();
    using var result = ImageOperations.Redact(source, new Rectangle(-2, -2, 10, 10));
    AssertEqual(Color.Black.ToArgb(), result.GetPixel(3, 2).ToArgb(), "bottom-right edge");
    foreach (var selection in new[] { Rectangle.Empty, new Rectangle(10, 10, 2, 2) })
    {
        try
        {
            using var invalid = ImageOperations.Redact(source, selection);
        }
        catch (ArgumentException)
        {
            continue;
        }
        throw new InvalidOperationException("Empty redaction must be rejected.");
    }
}

static void PinnedRedactionWorkflow() => RunSta(() =>
{
    using var pin = new PinnedWindow(CreateFixture(), Point.Empty, new AppSettings());
    var picture = (PictureBox)pin.Controls[0].Controls[0];
    var original = ((Bitmap)picture.Image!).GetPixel(2, 1).ToArgb();
    typeof(PinnedWindow).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(pin, new Rectangle(1, 1, 3, 2));
    var edit = (ToolStripMenuItem)picture.ContextMenuStrip!.Items[1];
    edit.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "選択範囲を黒塗りで伏せる").PerformClick();
    AssertEqual(Color.Black.ToArgb(), ((Bitmap)picture.Image!).GetPixel(3, 2).ToArgb(), "redacted edge");
    using var stream = new MemoryStream();
    picture.Image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
    stream.Position = 0;
    using var exported = new Bitmap(stream);
    AssertEqual(Color.Black.ToArgb(), exported.GetPixel(2, 1).ToArgb(), "exported black pixel");
    edit.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "元に戻す").PerformClick();
    AssertEqual(original, ((Bitmap)picture.Image!).GetPixel(2, 1).ToArgb(), "undo");
    edit.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "やり直し").PerformClick();
    AssertEqual(Color.Black.ToArgb(), ((Bitmap)picture.Image!).GetPixel(2, 1).ToArgb(), "redo");
    pin.Close();
});

static void RunSta(Action action)
{
    Exception? error = null;
    var thread = new Thread(() =>
    {
        try { action(); }
        catch (Exception ex) { error = ex; }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (error is not null) throw new InvalidOperationException("STA test failed", error);
}

static void InvokePin(PinnedWindow pin, string method, params object?[] arguments) =>
    typeof(PinnedWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pin, arguments);

static void DoubleClickRespectsEditing() => RunSta(() =>
{
    using var pin = new PinnedWindow(CreateFixture(), new Point(50, 50), new AppSettings { HideDurationMilliseconds = 10000 });
    pin.Show();
    var doubleClick = new MouseEventArgs(MouseButtons.Left, 2, 1, 1, 0);
    foreach (var fieldName in new[] { "_drawing", "_selecting" })
    {
        var field = typeof(PinnedWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(pin, true);
        InvokePin(pin, "OnPictureDoubleClick", null, doubleClick);
        AssertEqual(true, pin.Visible, $"visible during {fieldName}");
        AssertEqual(true, (bool)field.GetValue(pin)!, $"editing continues during {fieldName}");
        field.SetValue(pin, false);
    }
    InvokePin(pin, "OnPictureDoubleClick", null, doubleClick);
    AssertEqual(false, pin.Visible, "ordinary double-click still hides");
    pin.Close();
});

static void TemporaryHideWaitsForCapture() => RunSta(() =>
{
    using var pin = new PinnedWindow(CreateFixture(), new Point(50, 50), new AppSettings { HideDurationMilliseconds = 10000 });
    pin.Show();
    InvokePin(pin, "HideTemporarily");
    pin.SetCaptureActive(true);
    InvokePin(pin, "RevealAfterTemporaryHide");
    AssertEqual(false, pin.Visible, "stays hidden after expiration during capture");
    var timer = (System.Windows.Forms.Timer)typeof(PinnedWindow)
        .GetField("_revealTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pin)!;
    AssertEqual(false, timer.Enabled, "expired timer stops");
    pin.SetCaptureActive(false);
    AssertEqual(true, pin.Visible, "reveals after capture completes or is cancelled");
    AssertEqual(true, pin.TopMost, "topmost restored");
    pin.Close();
});

static void DoubleClickRespectsModifiers() => RunSta(() =>
{
    using var pin = new PinnedWindow(CreateFixture(), new Point(50, 50), new AppSettings { HideDurationMilliseconds = 10000 });
    pin.Show();
    var savedState = new byte[256];
    if (!KeyboardState.GetKeyboardState(savedState)) throw new InvalidOperationException("Cannot read keyboard state.");
    try
    {
        foreach (var key in new[] { Keys.ControlKey, Keys.ShiftKey })
        {
            var state = new byte[256];
            state[(int)key] = 0x80;
            if (!KeyboardState.SetKeyboardState(state)) throw new InvalidOperationException("Cannot set thread keyboard state.");
            InvokePin(pin, "OnPictureDoubleClick", null, new MouseEventArgs(MouseButtons.Left, 2, 1, 1, 0));
            AssertEqual(true, pin.Visible, $"visible with {key} held");
        }
    }
    finally
    {
        KeyboardState.SetKeyboardState(savedState);
        pin.Close();
    }
});

static void CapturePreservesHideTimer() => RunSta(() =>
{
    using var pin = new PinnedWindow(CreateFixture(), new Point(50, 50), new AppSettings { HideDurationMilliseconds = 10000 });
    pin.Show();
    InvokePin(pin, "HideTemporarily");
    pin.SetCaptureActive(true);
    pin.SetCaptureActive(false);
    AssertEqual(false, pin.Visible, "capture ending early does not reveal the pin");
    var timer = (System.Windows.Forms.Timer)typeof(PinnedWindow)
        .GetField("_revealTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pin)!;
    AssertEqual(true, timer.Enabled, "original timer remains active");
    InvokePin(pin, "RevealAfterTemporaryHide");
    AssertEqual(true, pin.Visible, "reveals at original expiration");
    pin.Close();
});

internal static class KeyboardState
{
    // These calls affect only this test thread's keyboard state, not physical input.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetKeyboardState([Out] byte[] state);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetKeyboardState(byte[] state);
}
