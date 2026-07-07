using Microsoft.VisualStudio.TestTools.UnitTesting;

using QConvert.Core;

namespace QConvert.Tests
{
    /// <summary>
    /// Tests the pure menu-entry generation used both for registering the
    /// Explorer context menu and for the live preview in the settings UI.
    /// </summary>
    [TestClass]
    public sealed class ShellIntegrationMenuTests
    {
        private static List<string> Labels(string extension, AppSettings settings) =>
            ShellIntegration.MenuEntries(extension, settings).Select(e => e.Label).ToList();

        [TestMethod]
        public void Png_DoesNotOfferConversionToItself()
        {
            var labels = Labels(".png", new AppSettings());

            CollectionAssert.DoesNotContain(labels, "Convert to PNG");
            CollectionAssert.Contains(labels, "Convert to JPG");
        }

        [TestMethod]
        public void DisabledConvertTarget_IsOmitted()
        {
            var settings = new AppSettings { EnableConvertToJpg = false };

            CollectionAssert.DoesNotContain(Labels(".png", settings), "Convert to JPG");
        }

        [TestMethod]
        public void CompressJpg_OnlyAppearsOnJpegFiles()
        {
            var settings = new AppSettings();

            CollectionAssert.Contains(Labels(".jpg", settings), "Compress JPG");
            CollectionAssert.Contains(Labels(".jpeg", settings), "Compress JPG");
            CollectionAssert.DoesNotContain(Labels(".png", settings), "Compress JPG");
        }

        [TestMethod]
        public void SizeLists_GenerateOneEntryPerSize()
        {
            var settings = new AppSettings
            {
                FitSizes = { new SizeSetting(1920, 1080) },
                CoverSizes = { new SizeSetting(1200, 630) },
                AspectRatios = { new AspectRatioSetting(16, 9) },
            };

            var labels = Labels(".png", settings);

            CollectionAssert.Contains(labels, "Resize to fit 1920×1080");
            CollectionAssert.Contains(labels, "Crop to 1200×630");
            CollectionAssert.Contains(labels, "Crop to 16:9");
        }

        [TestMethod]
        public void SepiaIntensities_GenerateOneEntryPerStrength()
        {
            var settings = new AppSettings { SepiaIntensities = new List<int> { 25, 75 } };

            var labels = Labels(".png", settings);

            CollectionAssert.Contains(labels, "Sepia 25%");
            CollectionAssert.Contains(labels, "Sepia 75%");
        }

        [TestMethod]
        public void AllEntriesDisabled_KeepsOpenEntry()
        {
            var settings = new AppSettings
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
                AvatarSizes = new List<int>(),
                SepiaIntensities = new List<int>(),
            };

            CollectionAssert.AreEqual(new[] { "Open" }, Labels(".png", settings));
            CollectionAssert.AreEqual(new[] { "Open" }, Labels(".jpg", settings));
        }

        [TestMethod]
        public void FolderMenu_HonorsPasteToggles()
        {
            var both = ShellIntegration.FolderMenuEntries(new AppSettings()).Select(e => e.Label).ToList();
            CollectionAssert.AreEqual(
                new[] { "Paste image as PNG", "Paste image as JPG" }, both);

            var none = new AppSettings { EnablePastePng = false, EnablePasteJpg = false };
            Assert.AreEqual(0, ShellIntegration.FolderMenuEntries(none).Count());

            var pngOnly = new AppSettings { EnablePasteJpg = false };
            CollectionAssert.AreEqual(
                new[] { "Paste image as PNG" },
                ShellIntegration.FolderMenuEntries(pngOnly).Select(e => e.Label).ToList());
        }
    }
}
