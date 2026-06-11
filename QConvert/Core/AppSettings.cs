using System.IO;
using System.Text.Json;

namespace QConvert.Core
{
    public sealed record SizeSetting(int Width, int Height);

    public sealed record AspectRatioSetting(int X, int Y);

    public sealed class AppSettings
    {
        public const int MinJpegQuality = 1;
        public const int MaxJpegQuality = 100;
        public const int DefaultJpegQuality = 90;

        public int JpegQuality { get; set; } = DefaultJpegQuality;

        /// <summary>Boxes for "Resize to fit" context-menu entries.</summary>
        public List<SizeSetting> FitSizes { get; set; } = new();

        /// <summary>Exact sizes for "Crop to size" (scale to cover, then crop) entries.</summary>
        public List<SizeSetting> CoverSizes { get; set; } = new();

        /// <summary>Ratios for "Crop to aspect" (crop only, no resize) entries.</summary>
        public List<AspectRatioSetting> AspectRatios { get; set; } = new();

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QConvert",
            "settings.json");

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        /// <summary>Loads settings from disk; falls back to defaults if missing or unreadable.</summary>
        public static AppSettings Load(string? path = null)
        {
            path ??= DefaultPath;
            try
            {
                if (File.Exists(path))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                    if (settings is not null)
                    {
                        settings.Sanitize();
                        return settings;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Corrupt or inaccessible settings must never break a conversion.
            }

            return new AppSettings();
        }

        public void Save(string? path = null)
        {
            path ??= DefaultPath;
            Sanitize();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
        }

        private void Sanitize()
        {
            JpegQuality = Math.Clamp(JpegQuality, MinJpegQuality, MaxJpegQuality);
            FitSizes = FitSizes.Where(s => s is { Width: > 0, Height: > 0 }).Distinct().ToList();
            CoverSizes = CoverSizes.Where(s => s is { Width: > 0, Height: > 0 }).Distinct().ToList();
            AspectRatios = AspectRatios.Where(r => r is { X: > 0, Y: > 0 }).Distinct().ToList();
        }
    }
}
