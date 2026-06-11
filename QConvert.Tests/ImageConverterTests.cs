using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using QConvert.Core;

using Xunit;

namespace QConvert.Tests
{
    public sealed class ImageConverterTests : IDisposable
    {
        private readonly string _dir;

        public ImageConverterTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private string CreatePng(string name, byte alpha = 255)
        {
            // 2x2 solid red square, optionally semi-transparent.
            const int width = 2;
            const int height = 2;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 2] = 255;   // R (Bgra32)
                pixels[i + 3] = alpha; // A
            }

            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            var path = Path.Combine(_dir, name);
            using var stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        private static BitmapDecoder Decode(string path)
        {
            using var stream = File.OpenRead(path);
            return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }

        [Fact]
        public void PngToJpeg_CreatesJpegFile()
        {
            var input = CreatePng("red.png");

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 90);

            Assert.Equal(Path.Combine(_dir, "red.jpg"), output);
            Assert.IsType<JpegBitmapDecoder>(Decode(output));
        }

        [Fact]
        public void JpegToPng_CreatesPngFile()
        {
            var intermediate = CreatePng("photo.png");
            var jpeg = Core.ImageConverter.Convert(intermediate, ConversionTarget.Jpeg, 90);

            var output = Core.ImageConverter.Convert(jpeg, ConversionTarget.Png);

            // photo.png already exists, so the copy gets a numeric suffix.
            Assert.Equal(Path.Combine(_dir, "photo.001.png"), output);
            Assert.IsType<PngBitmapDecoder>(Decode(output));
        }

        [Fact]
        public void PngToJpeg_FlattensTransparencyOntoWhite()
        {
            var input = CreatePng("transparent.png", alpha: 0);

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 100);

            var frame = Decode(output).Frames[0];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[4];
            converted.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixels, 4, 0);

            // Fully transparent red must become (nearly) white, not black.
            Assert.True(pixels[0] > 240, $"Blue channel was {pixels[0]}");
            Assert.True(pixels[1] > 240, $"Green channel was {pixels[1]}");
            Assert.True(pixels[2] > 240, $"Red channel was {pixels[2]}");
        }

        [Fact]
        public void Convert_PreservesImageDimensions()
        {
            var input = CreatePng("size.png");

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 90);

            var frame = Decode(output).Frames[0];
            Assert.Equal(2, frame.PixelWidth);
            Assert.Equal(2, frame.PixelHeight);
        }

        [Fact]
        public void Convert_DoesNotModifyOriginalFile()
        {
            var input = CreatePng("original.png");
            var before = File.ReadAllBytes(input);

            Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 90);

            Assert.Equal(before, File.ReadAllBytes(input));
        }

        [Fact]
        public void Convert_LowQualityProducesSmallerFile()
        {
            // Use a noisy image so quality actually affects size.
            var random = new Random(1234);
            const int size = 64;
            int stride = size * 4;
            var pixels = new byte[stride * size];
            random.NextBytes(pixels);
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            var source = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            var noisy = Path.Combine(_dir, "noise.png");
            using (var stream = File.Create(noisy))
            {
                encoder.Save(stream);
            }

            var high = Core.ImageConverter.Convert(noisy, ConversionTarget.Jpeg, 100);
            var low = Core.ImageConverter.Convert(noisy, ConversionTarget.Jpeg, 10);

            Assert.True(new FileInfo(low).Length < new FileInfo(high).Length);
        }

        [Fact]
        public void Convert_ThrowsForMissingFile()
        {
            Assert.Throws<FileNotFoundException>(() =>
                Core.ImageConverter.Convert(Path.Combine(_dir, "missing.png"), ConversionTarget.Jpeg));
        }
    }
}
