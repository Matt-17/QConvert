using System.IO;
using System.Text.Json;

namespace QConvert.Core
{
    public sealed record SizeSetting(int Width, int Height);

    public sealed record AspectRatioSetting(int X, int Y);

    public sealed class WindowPlacementSetting
    {
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public string State { get; set; } = "Normal";
    }

    public sealed class AppSettings
    {
        public const int MinJpegQuality = 1;
        public const int MaxJpegQuality = 100;
        public const int DefaultJpegQuality = 90;

        public const int MinWebPQuality = 1;
        public const int MaxWebPQuality = 100;
        public const int DefaultWebPQuality = 85;

        public const int MinAvifQuality = 1;
        public const int MaxAvifQuality = 100;
        public const int DefaultAvifQuality = 80;

        public static IReadOnlyList<int> BuiltInIcoSizes { get; } = new[] { 16, 32, 48, 64, 128, 256 };

        public int JpegQuality { get; set; } = DefaultJpegQuality;
        public int WebPQuality { get; set; } = DefaultWebPQuality;
        public int AvifQuality { get; set; } = DefaultAvifQuality;

        /// <summary>Hex color string for transparency background when converting to JPEG. E.g. "#ffffff".</summary>
        public string TransparencyBackground { get; set; } = "#ffffff";

        /// <summary>Sizes (pixels) included in .ico output.</summary>
        public List<int> IcoSizes { get; set; } = new() { 16, 32, 48, 64, 128 };

        /// <summary>Write converted files into a subfolder instead of next to the source.</summary>
        public bool UseSubfolder { get; set; } = false;

        /// <summary>Subfolder name relative to the source file's directory.</summary>
        public string SubfolderName { get; set; } = "_converted";

        /// <summary>Anchor used for cover-crop operations.</summary>
        public CropAnchor CropAnchor { get; set; } = CropAnchor.Center;

        /// <summary>
        /// Optional output name pattern. Supports {name}, {ext}, {target}, {width}, {height}.
        /// Empty string means use the default naming behavior.
        /// </summary>
        public string OutputNamePattern { get; set; } = "";

        /// <summary>Square pixel sizes for avatar export context-menu entries.</summary>
        public List<int> AvatarSizes { get; set; } = new() { 512, 256, 128 };

        /// <summary>Preserve metadata when re-encoding (where supported).</summary>
        public bool PreserveMetadata { get; set; } = true;

        /// <summary>Last normal bounds and state for the settings window.</summary>
        public WindowPlacementSetting SettingsWindowPlacement { get; set; } = new();

        /// <summary>Last normal bounds and state for the image editor window.</summary>
        public WindowPlacementSetting EditorWindowPlacement { get; set; } = new();

        /// <summary>Boxes for "Resize to fit" context-menu entries.</summary>
        public List<SizeSetting> FitSizes { get; set; } = new();

        /// <summary>Exact sizes for "Crop to size" (scale to cover, then crop) entries.</summary>
        public List<SizeSetting> CoverSizes { get; set; } = new();

        /// <summary>Ratios for "Crop to aspect" (crop only, no resize) entries.</summary>
        public List<AspectRatioSetting> AspectRatios { get; set; } = new();

        /// <summary>Sepia intensity percentages shown as context-menu entries.</summary>
        public List<int> SepiaIntensities { get; set; } = new() { 35, 65, 100 };

        // ── Per-entry feature toggles ──────────────────────────────────────────
        // Each flag controls one entry in the Explorer context menu. Resize/crop
        // and avatar entries are controlled by their size lists instead. With
        // everything off, the image-file Open entry remains available.

        /// <summary>Whether QConvert should be registered in the Explorer context menu.</summary>
        public bool ContextMenuEnabled { get; set; } = false;

        public bool EnableConvertToJpg { get; set; } = true;
        public bool EnableConvertToPng { get; set; } = true;
        public bool EnableConvertToWebP { get; set; } = true;
        public bool EnableConvertToAvif { get; set; } = true;
        public bool EnableConvertToIco { get; set; } = true;
        public bool EnableRemoveMetadata { get; set; } = true;
        public bool EnableCompressJpeg { get; set; } = true;
        public bool EnableOptimizePng { get; set; } = true;
        public bool EnableFavicon { get; set; } = true;
        public bool EnablePastePng { get; set; } = true;
        public bool EnablePasteJpg { get; set; } = true;

        public bool IsConvertTargetEnabled(ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => EnableConvertToJpg,
            ConversionTarget.Png => EnableConvertToPng,
            ConversionTarget.WebP => EnableConvertToWebP,
            ConversionTarget.Avif => EnableConvertToAvif,
            ConversionTarget.Ico => EnableConvertToIco,
            _ => false,
        };

        /// <summary>True if any entry shown on image files is enabled.</summary>
        public bool HasAnyFileFeature() =>
            EnableConvertToJpg || EnableConvertToPng || EnableConvertToWebP
            || EnableConvertToAvif || EnableConvertToIco
            || EnableRemoveMetadata || EnableCompressJpeg || EnableOptimizePng || EnableFavicon
            || AvatarSizes.Count > 0 || FitSizes.Count > 0 || CoverSizes.Count > 0
            || AspectRatios.Count > 0 || SepiaIntensities.Count > 0;

        /// <summary>True if any entry shown on folders is enabled.</summary>
        public bool HasAnyFolderFeature() => EnablePastePng || EnablePasteJpg;

        /// <summary>True if any context-menu entry (file or folder) is enabled.</summary>
        public bool HasAnyFeature() => HasAnyFileFeature() || HasAnyFolderFeature();

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

        /// <summary>Parses TransparencyBackground as RGB bytes. Returns white on any parse failure.</summary>
        public (byte R, byte G, byte B) GetBackgroundRgb()
        {
            var hex = TransparencyBackground.TrimStart('#');
            if (hex.Length == 6
                && byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
                && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)
                && byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                return (r, g, b);
            }
            return (255, 255, 255);
        }

        private void Sanitize()
        {
            JpegQuality = Math.Clamp(JpegQuality, MinJpegQuality, MaxJpegQuality);
            WebPQuality = Math.Clamp(WebPQuality, MinWebPQuality, MaxWebPQuality);
            AvifQuality = Math.Clamp(AvifQuality, MinAvifQuality, MaxAvifQuality);
            FitSizes = FitSizes.Where(s => s is { Width: > 0, Height: > 0 }).Distinct().ToList();
            CoverSizes = CoverSizes.Where(s => s is { Width: > 0, Height: > 0 }).Distinct().ToList();
            AspectRatios = AspectRatios.Where(r => r is { X: > 0, Y: > 0 }).Distinct().ToList();
            SepiaIntensities = SepiaIntensities.Where(i => i is >= 0 and <= 100).Distinct().OrderBy(i => i).ToList();
            IcoSizes = IcoSizes.Where(s => s is > 0 and <= 256).Distinct().OrderBy(s => s).ToList();
            if (IcoSizes.Count == 0) IcoSizes = new() { 16, 32, 48, 64, 128 };
            // An empty list is allowed: it removes the avatar entries from the menu.
            AvatarSizes = AvatarSizes.Where(s => s > 0).Distinct().OrderByDescending(s => s).ToList();
            SubfolderName = SanitizeSubfolderName(SubfolderName);
            SettingsWindowPlacement = SanitizeWindowPlacement(SettingsWindowPlacement);
            EditorWindowPlacement = SanitizeWindowPlacement(EditorWindowPlacement);
        }

        private static string SanitizeSubfolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_converted";
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "_converted" : cleaned;
        }

        private static WindowPlacementSetting SanitizeWindowPlacement(WindowPlacementSetting? placement)
        {
            placement ??= new WindowPlacementSetting();

            if (!IsValidPlacementValue(placement.Left)
                || !IsValidPlacementValue(placement.Top)
                || !IsValidPlacementValue(placement.Width)
                || !IsValidPlacementValue(placement.Height)
                || placement.Width <= 0
                || placement.Height <= 0)
            {
                placement.Left = null;
                placement.Top = null;
                placement.Width = null;
                placement.Height = null;
            }

            if (!string.Equals(placement.State, "Maximized", StringComparison.OrdinalIgnoreCase))
            {
                placement.State = "Normal";
            }
            else
            {
                placement.State = "Maximized";
            }

            return placement;
        }

        private static bool IsValidPlacementValue(double? value) =>
            value is null || double.IsFinite(value.Value);
    }
}
