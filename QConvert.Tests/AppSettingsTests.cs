using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QConvert.Core;

namespace QConvert.Tests
{
    [TestClass]
    public sealed class AppSettingsTests
    {
        private readonly string _dir;
        private readonly string _path;

        public AppSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "settings.json");
        }

        [TestCleanup]
        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [TestMethod]
        public void Load_ReturnsDefaults_WhenFileMissing()
        {
            var settings = AppSettings.Load(_path);

            Assert.AreEqual(AppSettings.DefaultJpegQuality, settings.JpegQuality);
            Assert.IsTrue(settings.PreserveMetadata);
        }

        [TestMethod]
        public void SaveAndLoad_RoundTripsValues()
        {
            new AppSettings { JpegQuality = 42 }.Save(_path);

            var loaded = AppSettings.Load(_path);

            Assert.AreEqual(42, loaded.JpegQuality);
        }

        [TestMethod]
        public void Load_ReturnsDefaults_WhenFileIsCorrupt()
        {
            File.WriteAllText(_path, "{ not json at all");

            var settings = AppSettings.Load(_path);

            Assert.AreEqual(AppSettings.DefaultJpegQuality, settings.JpegQuality);
        }

        [TestMethod]
        public void Load_ClampsOutOfRangeValues()
        {
            File.WriteAllText(_path, "{\"JpegQuality\": 500}");

            var settings = AppSettings.Load(_path);

            Assert.AreEqual(AppSettings.MaxJpegQuality, settings.JpegQuality);
        }

        [TestMethod]
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

            CollectionAssert.AreEqual(settings.FitSizes, loaded.FitSizes);
            CollectionAssert.AreEqual(settings.CoverSizes, loaded.CoverSizes);
            CollectionAssert.AreEqual(settings.AspectRatios, loaded.AspectRatios);
        }

        [TestMethod]
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

            CollectionAssert.AreEqual(new[] { new SizeSetting(1920, 1080) }, settings.FitSizes);
            Assert.AreEqual(0, settings.AspectRatios.Count);
        }

        [TestMethod]
        public void Save_CreatesMissingDirectory()
        {
            var nested = Path.Combine(_dir, "sub", "settings.json");

            new AppSettings().Save(nested);

            Assert.IsTrue(File.Exists(nested));
        }

        [TestMethod]
        public void GetBackgroundRgb_ParsesWhite()
        {
            var s = new AppSettings { TransparencyBackground = "#ffffff" };
            var (r, g, b) = s.GetBackgroundRgb();
            Assert.AreEqual(255, r); Assert.AreEqual(255, g); Assert.AreEqual(255, b);
        }

        [TestMethod]
        public void GetBackgroundRgb_ParsesBlack()
        {
            var s = new AppSettings { TransparencyBackground = "#000000" };
            var (r, g, b) = s.GetBackgroundRgb();
            Assert.AreEqual(0, r); Assert.AreEqual(0, g); Assert.AreEqual(0, b);
        }

        [TestMethod]
        public void GetBackgroundRgb_FallsBackToWhite_OnInvalidInput()
        {
            var s = new AppSettings { TransparencyBackground = "notacolor" };
            var (r, g, b) = s.GetBackgroundRgb();
            Assert.AreEqual(255, r);
        }

        [TestMethod]
        public void WebPQuality_RoundTrips()
        {
            new AppSettings { WebPQuality = 70 }.Save(_path);
            Assert.AreEqual(70, AppSettings.Load(_path).WebPQuality);
        }

        [TestMethod]
        public void IcoSizes_SanitizesInvalidValues()
        {
            File.WriteAllText(_path, "{\"IcoSizes\": [0, -1, 300, 32]}");
            var loaded = AppSettings.Load(_path);
            CollectionAssert.AreEqual(new[] { 32 }, loaded.IcoSizes);
        }

        [TestMethod]
        public void SaveAndLoad_RoundTripsNewSettings()
        {
            var s = new AppSettings
            {
                WebPQuality = 75,
                AvifQuality = 60,
                UseSubfolder = true,
                SubfolderName = "_out",
                CropAnchor = CropAnchor.TopLeft,
                OutputNamePattern = "{name}-converted",
                AvatarSizes = new System.Collections.Generic.List<int> { 64, 128 },
                IcoSizes = new System.Collections.Generic.List<int> { 16, 32, 64 },
            };
            s.Save(_path);
            var loaded = AppSettings.Load(_path);
            Assert.AreEqual(75, loaded.WebPQuality);
            Assert.AreEqual(60, loaded.AvifQuality);
            Assert.IsTrue(loaded.UseSubfolder);
            Assert.AreEqual("_out", loaded.SubfolderName);
            Assert.AreEqual(CropAnchor.TopLeft, loaded.CropAnchor);
            Assert.AreEqual("{name}-converted", loaded.OutputNamePattern);
            CollectionAssert.AreEqual(new[] { 128, 64 }, loaded.AvatarSizes); // sorted descending
            CollectionAssert.AreEqual(new[] { 16, 32, 64 }, loaded.IcoSizes);
        }

        [TestMethod]
        public void WindowPlacement_RoundTrips()
        {
            var settings = new AppSettings
            {
                SettingsWindowPlacement = new WindowPlacementSetting
                {
                    Left = 120,
                    Top = 90,
                    Width = 1240,
                    Height = 720,
                    State = "Maximized",
                },
            };

            settings.Save(_path);
            var loaded = AppSettings.Load(_path);

            Assert.AreEqual(120, loaded.SettingsWindowPlacement.Left);
            Assert.AreEqual(90, loaded.SettingsWindowPlacement.Top);
            Assert.AreEqual(1240, loaded.SettingsWindowPlacement.Width);
            Assert.AreEqual(720, loaded.SettingsWindowPlacement.Height);
            Assert.AreEqual("Maximized", loaded.SettingsWindowPlacement.State);
        }

        [TestMethod]
        public void WindowPlacement_DropsInvalidBounds()
        {
            File.WriteAllText(_path, """
                {
                  "SettingsWindowPlacement": {
                    "Left": 40,
                    "Top": 30,
                    "Width": -1,
                    "Height": 720,
                    "State": "Minimized"
                  }
                }
                """);

            var loaded = AppSettings.Load(_path);

            Assert.IsNull(loaded.SettingsWindowPlacement.Left);
            Assert.IsNull(loaded.SettingsWindowPlacement.Top);
            Assert.IsNull(loaded.SettingsWindowPlacement.Width);
            Assert.IsNull(loaded.SettingsWindowPlacement.Height);
            Assert.AreEqual("Normal", loaded.SettingsWindowPlacement.State);
        }

        [TestMethod]
        public void FeatureToggles_DefaultToEnabled()
        {
            var settings = AppSettings.Load(_path);

            Assert.IsTrue(settings.EnableConvertToJpg);
            Assert.IsTrue(settings.EnablePastePng);
            Assert.IsFalse(settings.ContextMenuEnabled);
            Assert.IsTrue(settings.HasAnyFileFeature());
            Assert.IsTrue(settings.HasAnyFolderFeature());
            Assert.IsTrue(settings.HasAnyFeature());
        }

        [TestMethod]
        public void FeatureToggles_RoundTrip()
        {
            new AppSettings
            {
                EnableConvertToJpg = false,
                EnableConvertToWebP = false,
                EnableRemoveMetadata = false,
                EnableCompressJpeg = true,
                EnablePastePng = false,
                ContextMenuEnabled = true,
            }.Save(_path);

            var loaded = AppSettings.Load(_path);

            Assert.IsFalse(loaded.EnableConvertToJpg);
            Assert.IsFalse(loaded.EnableConvertToWebP);
            Assert.IsTrue(loaded.EnableConvertToPng);
            Assert.IsFalse(loaded.EnableRemoveMetadata);
            Assert.IsTrue(loaded.EnableCompressJpeg);
            Assert.IsFalse(loaded.EnablePastePng);
            Assert.IsTrue(loaded.EnablePasteJpg);
            Assert.IsTrue(loaded.ContextMenuEnabled);
        }

        private static AppSettings AllDisabled() => new()
        {
            EnableConvertToJpg = false,
            EnableConvertToPng = false,
            EnableConvertToWebP = false,
            EnableConvertToAvif = false,
            EnableConvertToIco = false,
            EnableRemoveMetadata = false,
            EnableCompressJpeg = false,
            EnableOptimizePng = false,
            EnableFavicon = false,
            EnablePastePng = false,
            EnablePasteJpg = false,
            AvatarSizes = new System.Collections.Generic.List<int>(),
            SepiaIntensities = new System.Collections.Generic.List<int>(),
        };

        [TestMethod]
        public void HasAnyFeature_IsFalse_WhenEverythingIsDisabled()
        {
            var settings = AllDisabled();

            Assert.IsFalse(settings.HasAnyFileFeature());
            Assert.IsFalse(settings.HasAnyFolderFeature());
            Assert.IsFalse(settings.HasAnyFeature());
        }

        [TestMethod]
        public void HasAnyFeature_IsTrue_WhenOnlyPasteJpgIsEnabled()
        {
            var settings = AllDisabled();
            settings.EnablePasteJpg = true;

            Assert.IsFalse(settings.HasAnyFileFeature());
            Assert.IsTrue(settings.HasAnyFeature());
        }

        [TestMethod]
        public void HasAnyFeature_IsTrue_WhenOnlyAFitSizeIsEnabled()
        {
            var settings = AllDisabled();
            settings.FitSizes.Add(new SizeSetting(1920, 1080));

            Assert.IsTrue(settings.HasAnyFileFeature());
            Assert.IsTrue(settings.HasAnyFeature());
        }

        [TestMethod]
        public void AvatarSizes_MayBeEmpty()
        {
            new AppSettings { AvatarSizes = new System.Collections.Generic.List<int>() }.Save(_path);

            Assert.AreEqual(0, AppSettings.Load(_path).AvatarSizes.Count);
        }
    }
}
