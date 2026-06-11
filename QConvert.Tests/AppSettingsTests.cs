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
        public void Save_CreatesMissingDirectory()
        {
            var nested = Path.Combine(_dir, "sub", "settings.json");

            new AppSettings().Save(nested);

            Assert.True(File.Exists(nested));
        }
    }
}
