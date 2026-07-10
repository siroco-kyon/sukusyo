using System.Drawing;
using System.Text.Json;

namespace Sukusyo;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public bool AutoCopy { get; set; }
    public bool AutoSave { get; set; }
    public string AutoSaveDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "sukusyo");
    public bool AlwaysOnTop { get; set; } = true;
    public int DefaultOpacityPercent { get; set; } = 100;
    public int HideDurationMilliseconds { get; set; } = 1500;
    public int PenColorArgb { get; set; } = Color.Yellow.ToArgb();
    public int PenWidth { get; set; } = 12;

    public Color PenColor => Color.FromArgb(PenColorArgb);

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sukusyo",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            // A damaged settings file must never prevent a capture from starting.
            return new AppSettings();
        }
    }

    public void Save()
    {
        Normalize();
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public string CreateAutoSavePath(string? windowTitle = null)
    {
        Normalize();
        Directory.CreateDirectory(AutoSaveDirectory);

        var safeTitle = string.IsNullOrWhiteSpace(windowTitle) ? "capture" : SanitizeFileName(windowTitle);
        if (safeTitle.Length > 40)
        {
            safeTitle = safeTitle[..40];
        }

        var stem = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
        var path = Path.Combine(AutoSaveDirectory, stem + ".png");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(AutoSaveDirectory, $"{stem}_{suffix++}.png");
        }
        return path;
    }

    private void Normalize()
    {
        DefaultOpacityPercent = Math.Clamp(DefaultOpacityPercent, 25, 100);
        HideDurationMilliseconds = Math.Clamp(HideDurationMilliseconds, 250, 10000);
        PenWidth = Math.Clamp(PenWidth, 2, 64);
        if (string.IsNullOrWhiteSpace(AutoSaveDirectory))
        {
            AutoSaveDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "sukusyo");
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(result) ? "capture" : result;
    }
}
