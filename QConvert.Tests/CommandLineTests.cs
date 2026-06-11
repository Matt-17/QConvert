using QConvert.Core;

using Xunit;

namespace QConvert.Tests
{
    public class CommandLineTests
    {
        [Fact]
        public void Parse_ConvertOperation()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", @"C:\img.png" });

            Assert.Null(result.Error);
            Assert.Equal(new ConvertOperation(ConversionTarget.Jpeg), result.Operation);
            Assert.Equal(new[] { @"C:\img.png" }, result.Files);
        }

        [Fact]
        public void Parse_FitOperation()
        {
            var result = CommandLine.Parse(new[] { "--fit", "1920x1080", "a.png", "b.png" });

            Assert.Null(result.Error);
            Assert.Equal(new FitOperation(new PixelSize(1920, 1080)), result.Operation);
            Assert.Equal(2, result.Files.Count);
        }

        [Fact]
        public void Parse_CoverOperation()
        {
            var result = CommandLine.Parse(new[] { "--cover", "1200x630", "a.jpg" });

            Assert.Equal(new CoverOperation(new PixelSize(1200, 630)), result.Operation);
        }

        [Fact]
        public void Parse_AspectCropOperation()
        {
            var result = CommandLine.Parse(new[] { "--crop", "4:3", "a.jpg" });

            Assert.Equal(new AspectCropOperation(4, 3), result.Operation);
        }

        [Fact]
        public void Parse_QualityOverride()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", "--quality", "55", "a.png" });

            Assert.Null(result.Error);
            Assert.Equal(55, result.JpegQuality);
        }

        [Theory]
        [InlineData("0x100")]
        [InlineData("100x0")]
        [InlineData("-5x100")]
        [InlineData("abc")]
        [InlineData("1920")]
        public void Parse_RejectsInvalidSizes(string size)
        {
            var result = CommandLine.Parse(new[] { "--fit", size, "a.png" });

            Assert.NotNull(result.Error);
        }

        [Theory]
        [InlineData("0:3")]
        [InlineData("4:0")]
        [InlineData("4")]
        [InlineData("x:y")]
        public void Parse_RejectsInvalidAspects(string aspect)
        {
            var result = CommandLine.Parse(new[] { "--crop", aspect, "a.png" });

            Assert.NotNull(result.Error);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("101")]
        [InlineData("high")]
        public void Parse_RejectsInvalidQuality(string quality)
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", "--quality", quality, "a.png" });

            Assert.NotNull(result.Error);
        }

        [Fact]
        public void Parse_RejectsMultipleOperations()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg", "--fit", "100x100", "a.png" });

            Assert.NotNull(result.Error);
        }

        [Fact]
        public void Parse_RejectsMissingFiles()
        {
            var result = CommandLine.Parse(new[] { "--to", "jpg" });

            Assert.NotNull(result.Error);
        }

        [Fact]
        public void Parse_RejectsMissingOperation()
        {
            var result = CommandLine.Parse(new[] { "a.png" });

            Assert.NotNull(result.Error);
        }

        [Fact]
        public void Parse_RejectsUnknownOption()
        {
            var result = CommandLine.Parse(new[] { "--rotate", "90", "a.png" });

            Assert.NotNull(result.Error);
        }

        [Fact]
        public void TryParseSize_AcceptsMultiplicationSign()
        {
            Assert.True(CommandLine.TryParseSize("1920×1080", out var size));
            Assert.Equal(new PixelSize(1920, 1080), size);
        }
    }
}
