using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QConvert.Core;

namespace QConvert.Tests
{
    [TestClass]
    public sealed class ImageConverterTests
    {
        private readonly string _dir;

        public ImageConverterTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_dir, recursive: true);

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

        private string CreateJpegWithMetadata(string name)
        {
            const int width = 4;
            const int height = 4;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 20;
                pixels[i + 1] = 120;
                pixels[i + 2] = 220;
                pixels[i + 3] = 255;
            }

            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var metadata = new BitmapMetadata("jpg");
            metadata.SetQuery("/app1/ifd/{ushort=270}", "QConvert metadata test");

            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));

            var path = Path.Combine(_dir, name);
            using var stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        private static string? ReadImageDescription(string path)
        {
            var metadata = Decode(path).Frames[0].Metadata as BitmapMetadata;
            return metadata?.GetQuery("/app1/ifd/{ushort=270}") as string;
        }

        private static BitmapDecoder Decode(string path)
        {
            using var stream = File.OpenRead(path);
            return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }

        [TestMethod]
        public void PngToJpeg_CreatesJpegFile()
        {
            var input = CreatePng("red.png");

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 90);

            Assert.AreEqual(Path.Combine(_dir, "red.jpg"), output);
            Assert.IsInstanceOfType(Decode(output), typeof(JpegBitmapDecoder));
        }

        [TestMethod]
        public void JpegToPng_CreatesPngFile()
        {
            var intermediate = CreatePng("photo.png");
            var jpeg = Core.ImageConverter.Convert(intermediate, ConversionTarget.Jpeg, 90);

            var output = Core.ImageConverter.Convert(jpeg, ConversionTarget.Png);

            // photo.png already exists, so the copy gets a numeric suffix.
            Assert.AreEqual(Path.Combine(_dir, "photo.001.png"), output);
            Assert.IsInstanceOfType(Decode(output), typeof(PngBitmapDecoder));
        }

        [TestMethod]
        public void PngToJpeg_FlattensTransparencyOntoWhite()
        {
            var input = CreatePng("transparent.png", alpha: 0);

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 100);

            var frame = Decode(output).Frames[0];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[4];
            converted.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixels, 4, 0);

            // Fully transparent red must become (nearly) white, not black.
            Assert.IsTrue(pixels[0] > 240, $"Blue channel was {pixels[0]}");
            Assert.IsTrue(pixels[1] > 240, $"Green channel was {pixels[1]}");
            Assert.IsTrue(pixels[2] > 240, $"Red channel was {pixels[2]}");
        }

        [TestMethod]
        public void Convert_PreservesImageDimensions()
        {
            var input = CreatePng("size.png");

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 90);

            var frame = Decode(output).Frames[0];
            Assert.AreEqual(2, frame.PixelWidth);
            Assert.AreEqual(2, frame.PixelHeight);
        }

        [TestMethod]
        public void Convert_DoesNotModifyOriginalFile()
        {
            var input = CreatePng("original.png");
            var before = File.ReadAllBytes(input);

            Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 90);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(input));
        }

        [TestMethod]
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

            Assert.IsTrue(new FileInfo(low).Length < new FileInfo(high).Length);
        }

        [TestMethod]
        public void Convert_ThrowsForMissingFile()
        {
            Assert.ThrowsException<FileNotFoundException>(() =>
                Core.ImageConverter.Convert(Path.Combine(_dir, "missing.png"), ConversionTarget.Jpeg));
        }

        [TestMethod]
        public void PngToWebP_CreatesWebPFile()
        {
            var input = CreatePng("image.png");
            var output = Core.ImageConverter.Convert(input, ConversionTarget.WebP);
            Assert.AreEqual(Path.Combine(_dir, "image.webp"), output);
            Assert.IsTrue(File.Exists(output));
            Assert.IsTrue(new FileInfo(output).Length > 0);
        }

        [TestMethod]
        public void PngToAvif_ThrowsNotSupported_WhenSkiaNativeHasNoAvifEncoder()
        {
            // SkiaSharp 2.88.x Win32 native does not include the AOM AVIF encoder.
            // The converter should throw NotSupportedException rather than NullReferenceException.
            var input = CreatePng("image.png");
            Assert.ThrowsException<NotSupportedException>(() =>
                Core.ImageConverter.Convert(input, ConversionTarget.Avif));
        }

        [TestMethod]
        public void StripMetadata_CreatesCleanFile()
        {
            var input = CreatePng("meta.png");
            var output = Core.ImageConverter.StripMetadata(input);
            Assert.IsTrue(output.Contains(".clean."));
            Assert.IsTrue(File.Exists(output));
        }

        [TestMethod]
        public void ApplySepia_CreatesTintedFile()
        {
            var input = CreatePng("tone.png");

            var output = Core.ImageConverter.ApplySepia(input, 65);

            Assert.IsTrue(output.Contains(".sepia65."));
            Assert.IsTrue(File.Exists(output));
            Assert.AreEqual(new PixelSize(2, 2), new PixelSize(Decode(output).Frames[0].PixelWidth, Decode(output).Frames[0].PixelHeight));
        }

        [TestMethod]
        public void RenderPreview_SepiaHue30_IsContinuousWithNeighboringHues()
        {
            var before = RenderSepiaPixel(29);
            var standard = RenderSepiaPixel(30);
            var after = RenderSepiaPixel(31);

            Assert.IsTrue(ColorDistance(before, standard) <= 12, $"Hue 29 to 30 distance was {ColorDistance(before, standard)}.");
            Assert.IsTrue(ColorDistance(standard, after) <= 12, $"Hue 30 to 31 distance was {ColorDistance(standard, after)}.");
        }

        [TestMethod]
        public void RenderPreview_SepiaDefaultHue_UsesClassicSepiaMatrix()
        {
            var result = RenderSepiaPixel(ImageConverter.StandardSepiaHue);
            var expected = ClassicSepiaPixel(210, 140, 64);

            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod]
        public void JpegToJpeg_PreservesMetadataByDefault()
        {
            var input = CreateJpegWithMetadata("meta.jpg");

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg);

            Assert.AreEqual("QConvert metadata test", ReadImageDescription(output));
        }

        [TestMethod]
        public void JpegToJpeg_DropsMetadata_WhenDisabled()
        {
            var input = CreateJpegWithMetadata("private.jpg");
            var settings = new AppSettings { PreserveMetadata = false };

            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, settings: settings);

            Assert.IsNull(ReadImageDescription(output));
        }

        [TestMethod]
        public void StripMetadata_DropsJpegMetadata()
        {
            var input = CreateJpegWithMetadata("strip.jpg");

            var output = Core.ImageConverter.StripMetadata(input);

            Assert.IsNull(ReadImageDescription(output));
        }

        [TestMethod]
        public void CompressJpeg_CreatesCompressedFile()
        {
            var png = CreatePng("photo.png");
            var jpeg = Core.ImageConverter.Convert(png, ConversionTarget.Jpeg, 90);
            var compressed = Core.ImageConverter.CompressJpeg(jpeg);
            Assert.IsTrue(compressed.Contains(".compressed."));
            Assert.IsTrue(File.Exists(compressed));
        }

        [TestMethod]
        public void OptimizePng_CreatesOptimizedFile()
        {
            var input = CreatePng("big.png");
            var output = Core.ImageConverter.OptimizePng(input);
            Assert.IsTrue(output.Contains(".optimized."));
            Assert.IsTrue(File.Exists(output));
        }

        [TestMethod]
        public void CreateFaviconBundle_CreatesAllFiles()
        {
            var input = CreatePng("logo.png");
            var folder = Core.ImageConverter.CreateFaviconBundle(input);

            Assert.IsTrue(Directory.Exists(folder));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "favicon.ico")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "favicon-16x16.png")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "favicon-32x32.png")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "apple-touch-icon.png")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "android-chrome-192x192.png")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "android-chrome-512x512.png")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "site.webmanifest")));
        }

        [TestMethod]
        public void MakeAvatar_CreatesSquareFile()
        {
            var input = CreatePng("wide.png", alpha: 255);
            // Need a bigger source for avatar crop
            var bigInput = CreateBigPng("bigwide.png", 400, 200);
            var output = Core.ImageConverter.MakeAvatar(bigInput, 128);
            Assert.IsTrue(output.Contains("128x128"));
            Assert.IsTrue(File.Exists(output));
        }

        [TestMethod]
        public void PngToJpeg_FlattensTransparencyWithCustomBackground()
        {
            var input = CreatePng("transp.png", alpha: 0);
            var settings = new AppSettings { TransparencyBackground = "#000000" };
            var output = Core.ImageConverter.Convert(input, ConversionTarget.Jpeg, 100, settings);

            var frame = Decode(output).Frames[0];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[4];
            converted.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixels, 4, 0);

            // Fully transparent image on black background should be nearly black.
            Assert.IsTrue(pixels[0] < 15, $"Blue channel was {pixels[0]}");
            Assert.IsTrue(pixels[1] < 15, $"Green channel was {pixels[1]}");
            Assert.IsTrue(pixels[2] < 15, $"Red channel was {pixels[2]}");
        }

        [TestMethod]
        public void CustomIcoSizes_AreRespected()
        {
            var input = CreatePng("icon.png");
            var settings = new AppSettings { IcoSizes = new System.Collections.Generic.List<int> { 16, 256 } };
            var output = Core.ImageConverter.Convert(input, ConversionTarget.Ico, 90, settings);
            Assert.IsTrue(File.Exists(output));
            // ICO directory: 6 + count*16 bytes header. With 2 entries = 38 bytes header.
            var data = File.ReadAllBytes(output);
            var entryCount = System.BitConverter.ToUInt16(data, 4);
            Assert.AreEqual(2, entryCount);
        }

        [TestMethod]
        public void IcoOutput_UsesDibFrames_NotPngCompressedFrames()
        {
            var input = CreatePng("icon.png");
            var settings = new AppSettings { IcoSizes = new System.Collections.Generic.List<int> { 16, 32 } };
            var output = Core.ImageConverter.Convert(input, ConversionTarget.Ico, 90, settings);

            var data = File.ReadAllBytes(output);
            var firstImageOffset = System.BitConverter.ToInt32(data, 18);
            var dibHeaderSize = System.BitConverter.ToInt32(data, firstImageOffset);

            Assert.AreEqual(40, dibHeaderSize);
        }

        [TestMethod]
        public void ClipboardOutputPath_UsesIsoStyleTimestampAndCollisionSuffix()
        {
            var timestamp = new DateTime(2026, 6, 12, 14, 30, 22);

            var first = Core.ImageConverter.GetClipboardOutputPath(_dir, ConversionTarget.Png, timestamp);
            File.WriteAllText(first, "");
            var second = Core.ImageConverter.GetClipboardOutputPath(_dir, ConversionTarget.Png, timestamp);

            Assert.AreEqual(Path.Combine(_dir, "2026-06-12T14-30-22.png"), first);
            Assert.AreEqual(Path.Combine(_dir, "2026-06-12T14-30-22.001.png"), second);
        }

        private string CreateBigPng(string name, int width, int height)
        {
            int stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i + 1] = 200; pixels[i + 3] = 255; }
            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            var path = Path.Combine(_dir, name);
            using var stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        private static byte[] RenderSepiaPixel(int hue)
        {
            var pixels = new byte[] { 64, 140, 210, 255 };
            var source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
            var preview = Core.ImageConverter.RenderPreview(source, new SepiaOperation(100, hue));
            var converted = new FormatConvertedBitmap(preview, PixelFormats.Bgra32, null, 0);
            var result = new byte[4];
            converted.CopyPixels(result, 4, 0);
            return result;
        }

        private static byte[] ClassicSepiaPixel(byte red, byte green, byte blue) =>
            new[]
            {
                ClampByte(red * 0.272 + green * 0.534 + blue * 0.131),
                ClampByte(red * 0.349 + green * 0.686 + blue * 0.168),
                ClampByte(red * 0.393 + green * 0.769 + blue * 0.189),
                (byte)255,
            };

        private static int ColorDistance(byte[] left, byte[] right) =>
            Math.Abs(left[0] - right[0])
            + Math.Abs(left[1] - right[1])
            + Math.Abs(left[2] - right[2]);

        private static byte ClampByte(double value) =>
            (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
