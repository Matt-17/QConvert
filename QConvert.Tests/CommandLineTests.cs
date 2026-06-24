using Microsoft.VisualStudio.TestTools.UnitTesting;

using QConvert.Core;

namespace QConvert.Tests
{
    [TestClass]
    public class CommandLineTests
    {
        [TestMethod]
        public void Parse_ConvertOperation()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", @"C:\img.png" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new ConvertOperation(ConversionTarget.Jpeg), result.Operation);
            CollectionAssert.AreEqual(new[] { @"C:\img.png" }, result.Files.ToArray());
        }

        [TestMethod]
        public void Parse_FitOperation()
        {
            var result = CommandLine.Parse(new[] { "--fit", "1920x1080", "a.png", "b.png" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new FitOperation(new PixelSize(1920, 1080)), result.Operation);
            Assert.AreEqual(2, result.Files.Count);
        }

        [TestMethod]
        public void Parse_CoverOperation()
        {
            var result = CommandLine.Parse(new[] { "--cover", "1200x630", "a.jpg" });
            Assert.AreEqual(new CoverOperation(new PixelSize(1200, 630)), result.Operation);
        }

        [TestMethod]
        public void Parse_AspectCropOperation()
        {
            var result = CommandLine.Parse(new[] { "--crop", "4:3", "a.jpg" });
            Assert.AreEqual(new AspectCropOperation(4, 3), result.Operation);
        }

        [TestMethod]
        public void Parse_QualityOverride()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", "--quality", "55", "a.png" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(55, result.JpegQuality);
        }

        [TestMethod]
        public void Parse_IcoSizesOverride()
        {
            var result = CommandLine.Parse(new[] { "--to", "ico", "--ico-sizes", "16,32,256", "a.png" });

            Assert.IsNull(result.Error);
            CollectionAssert.AreEqual(new[] { 16, 32, 256 }, result.IcoSizes!.ToArray());
        }

        [TestMethod] public void Parse_RejectsInvalidSize_ZeroWidth()    => Assert.IsNotNull(CommandLine.Parse(new[] { "--fit", "0x100", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidSize_ZeroHeight()   => Assert.IsNotNull(CommandLine.Parse(new[] { "--fit", "100x0", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidSize_Negative()     => Assert.IsNotNull(CommandLine.Parse(new[] { "--fit", "-5x100", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidSize_NotANumber()   => Assert.IsNotNull(CommandLine.Parse(new[] { "--fit", "abc", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidSize_NoDelimiter()  => Assert.IsNotNull(CommandLine.Parse(new[] { "--fit", "1920", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidAspect_ZeroX()      => Assert.IsNotNull(CommandLine.Parse(new[] { "--crop", "0:3", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidAspect_ZeroY()      => Assert.IsNotNull(CommandLine.Parse(new[] { "--crop", "4:0", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidAspect_NoColon()    => Assert.IsNotNull(CommandLine.Parse(new[] { "--crop", "4", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidAspect_Letters()    => Assert.IsNotNull(CommandLine.Parse(new[] { "--crop", "x:y", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidQuality_Zero()      => Assert.IsNotNull(CommandLine.Parse(new[] { "--to", "jpg", "--quality", "0", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidQuality_Over100()   => Assert.IsNotNull(CommandLine.Parse(new[] { "--to", "jpg", "--quality", "101", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidQuality_Text()      => Assert.IsNotNull(CommandLine.Parse(new[] { "--to", "jpg", "--quality", "high", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidIcoSizes_Over256()  => Assert.IsNotNull(CommandLine.Parse(new[] { "--to", "ico", "--ico-sizes", "16,300", "a.png" }).Error);
        [TestMethod] public void Parse_RejectsInvalidIcoSizes_Text()     => Assert.IsNotNull(CommandLine.Parse(new[] { "--to", "ico", "--ico-sizes", "small", "a.png" }).Error);

        [TestMethod]
        public void Parse_RejectsMultipleOperations()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", "--fit", "100x100", "a.png" });
            Assert.IsNotNull(result.Error);
        }

        [TestMethod]
        public void Parse_RejectsMissingFiles()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg" });
            Assert.IsNotNull(result.Error);
        }

        [TestMethod]
        public void Parse_RejectsMissingOperation()
        {
            var result = CommandLine.Parse(new[] { "a.png" });
            Assert.IsNotNull(result.Error);
        }

        [TestMethod]
        public void Parse_RejectsUnknownOption()
        {
            var result = CommandLine.Parse(new[] { "--rotate", "90", "a.png" });
            Assert.IsNotNull(result.Error);
        }

        [TestMethod]
        public void TryParseSize_AcceptsMultiplicationSign()
        {
            Assert.IsTrue(CommandLine.TryParseSize("1920x1080", out var size));
            Assert.AreEqual(new PixelSize(1920, 1080), size);
        }

        [TestMethod]
        public void Parse_StripMetadataOperation()
        {
            var result = CommandLine.Parse(new[] { "--strip-metadata", "a.jpg" });
            Assert.IsNull(result.Error);
            Assert.IsInstanceOfType(result.Operation, typeof(StripMetadataOperation));
        }

        [TestMethod]
        public void Parse_SepiaOperation()
        {
            var result = CommandLine.Parse(new[] { "--sepia", "65", "a.jpg" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new SepiaOperation(65), result.Operation);
        }

        [TestMethod]
        public void Parse_RejectsInvalidSepia()
        {
            var result = CommandLine.Parse(new[] { "--sepia", "101", "a.jpg" });
            Assert.IsNotNull(result.Error);
        }

        [TestMethod]
        public void Parse_CompressJpegOperation()
        {
            var result = CommandLine.Parse(new[] { "--compress-jpg", "a.jpg" });
            Assert.IsNull(result.Error);
            Assert.IsInstanceOfType(result.Operation, typeof(CompressJpegOperation));
        }

        [TestMethod]
        public void Parse_OptimizePngOperation()
        {
            var result = CommandLine.Parse(new[] { "--optimize-png", "a.png" });
            Assert.IsNull(result.Error);
            Assert.IsInstanceOfType(result.Operation, typeof(OptimizePngOperation));
        }

        [TestMethod]
        public void Parse_FaviconOperation()
        {
            var result = CommandLine.Parse(new[] { "--favicon", "a.png" });
            Assert.IsNull(result.Error);
            Assert.IsInstanceOfType(result.Operation, typeof(FaviconBundleOperation));
        }

        [TestMethod]
        public void Parse_AvatarOperation()
        {
            var result = CommandLine.Parse(new[] { "--avatar", "256", "a.png" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new AvatarExportOperation(256), result.Operation);
        }

        [TestMethod]
        public void Parse_WebPTarget()
        {
            var result = CommandLine.Parse(new[] { "--to", "webp", "a.png" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new ConvertOperation(ConversionTarget.WebP), result.Operation);
        }

        [TestMethod]
        public void Parse_AvifTarget()
        {
            var result = CommandLine.Parse(new[] { "--to", "avif", "a.png" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new ConvertOperation(ConversionTarget.Avif), result.Operation);
        }

        [TestMethod]
        public void Parse_PasteClipboardAsPng()
        {
            var result = CommandLine.Parse(new[] { "--paste", "png", @"C:\Images" });
            Assert.IsNull(result.Error);
            Assert.AreEqual(new PasteClipboardOperation(ConversionTarget.Png), result.Operation);
            CollectionAssert.AreEqual(new[] { @"C:\Images" }, result.Files.ToArray());
        }

        [TestMethod]
        public void Parse_RejectsInvalidPasteTarget()
        {
            var result = CommandLine.Parse(new[] { "--paste", "ico", @"C:\Images" });
            Assert.IsNotNull(result.Error);
        }
    }
}
