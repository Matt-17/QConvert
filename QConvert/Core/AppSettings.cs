using System.IO;
using System.Text.Json;

namespace QConvert.Core
{
    public sealed class AppSettings
    {
        public const int MinJpegQuality = 1;
        public const int MaxJpegQuality = 100;
        public const int DefaultJpegQuality = 90;

        public int JpegQuality { get; set; } = DefaultJpegQuality;

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
                        settings.Clamp();
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
            Clamp();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
        }

        private void Clamp()
        {
            JpegQuality = Math.Clamp(JpegQuality, MinJpegQuality, MaxJpegQuality);
        }
    }
}
