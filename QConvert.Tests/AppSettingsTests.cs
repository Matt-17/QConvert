using System.IO;

using QConvert.Core;

using Xunit;

namespace QConvert.Tests
{
    public sealed class AppSettingsTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;

        public AppSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "settings.json");
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Fact]
        public void Load_ReturnsDefaults_WhenFileMissing()
        {
            var settings = AppSettings.Load(_path);

            Assert.Equal(AppSettings.DefaultJpegQuality, settings.JpegQuality);
        }

        [Fact]
        public void SaveAndLoad_RoundTripsValues()
        {
            new AppSettings { JpegQuality = 42 }.Save(_path);

            var loaded = AppSettings.Load(_path);

            Assert.Equal(42, loaded.JpegQuality);
        }

        [Fact]
        public void Load_ReturnsDefaults_WhenFileIsCorrupt()
        {
            File.WriteAllText(_path, "{ not json at all");

            var settings = AppSettings.Load(_path);

            Assert.Equal(AppSettings.DefaultJpegQuality, settings.JpegQuality);
        }

        [Fact]
        public void Load_ClampsOutOfRangeValues()
        {
            File.WriteAllText(_path, "{\"JpegQuality\": 500}");

            var settings = AppSettings.Load(_path);

            Assert.Equal(AppSettings.MaxJpegQuality, settings.JpegQuality);
        }

        [Fact]
        public void SaveAndLoad_RoundTripsResizeSelections()
        {
            var settings = new AppSettings
            {
                FitSizes = { new SizeSetting(1920, 1080) },
                CoverSizes = { new SizeSetting(1200, 630), new SizeSetting(3840, 2160) },
                AspectRatios = { new AspectRatioSetting(4, 3), new AspectRatioSetting(16, 9) },
            };
            settings.Save(_path);

            var loaded = AppSettings.Load(_path);

            Assert.Equal(settings.FitSizes, loaded.FitSizes);
            Assert.Equal(settings.CoverSizes, loaded.CoverSizes);
            Assert.Equal(settings.AspectRatios, loaded.AspectRatios);
        }

        [Fact]
        public void Load_DropsInvalidAndDuplicateEntries()
        {
            File.WriteAllText(_path, """
                {
                  "FitSizes": [
                    {"Width": 1920, "Height": 1080},
                    {"Width": 1920, "Height": 1080},
                    {"Width": 0, "Height": 100},
                    {"Width": -5, "Height": 100}
                  ],
                  "AspectRatios": [{"X": 4, "Y": 0}]
                }
                """);

            var settings = AppSettings.Load(_path);

            Assert.Equal(new[] { new SizeSetting(1920, 1080) }, settings.FitSizes);
            Assert.Empty(settings.AspectRatios);
        }

        [Fact]
        public void Save_CreatesMissingDirectory()
        {
            var nested = Path.Combine(_dir, "sub", "settings.json");

            new AppSettings().Save(nested);

            Assert.True(File.Exists(nested));
        }
    }
}
