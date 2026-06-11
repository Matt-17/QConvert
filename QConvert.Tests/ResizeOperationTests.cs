using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using QConvert.Core;

using Xunit;

namespace QConvert.Tests
{
    public sealed class ResizeOperationTests : IDisposable
    {
        private readonly string _dir;

        public ResizeOperationTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private string CreatePng(string name, int width, int height)
        {
            int stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 1] = 128; // G
                pixels[i + 3] = 255; // A
            }

            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            var path = Path.Combine(_dir, name);
            using var stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        private static PixelSize SizeOf(string path)
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            return new PixelSize(frame.PixelWidth, frame.PixelHeight);
        }

        [Fact]
        public void ResizeToFit_ScalesDownIntoBox()
        {
            var input = CreatePng("wide.png", 400, 200);

            var output = Core.ImageConverter.ResizeToFit(input, new PixelSize(100, 100));

            Assert.Equal(new PixelSize(100, 50), SizeOf(output));
            Assert.Equal(Path.Combine(_dir, "wide.100x50.png"), output);
        }

        [Fact]
        public void ResizeToFit_DoesNotUpscale()
        {
            var input = CreatePng("small.png", 40, 20);

            var output = Core.ImageConverter.ResizeToFit(input, new PixelSize(1920, 1080));

            Assert.Equal(new PixelSize(40, 20), SizeOf(output));
        }

        [Fact]
        public void CropToSize_ProducesExactTargetSize()
        {
            var input = CreatePng("photo.png", 400, 300);

            var output = Core.ImageConverter.CropToSize(input, new PixelSize(100, 50));

            Assert.Equal(new PixelSize(100, 50), SizeOf(output));
        }

        [Fact]
        public void CropToSize_UpscalesSmallImages()
        {
            var input = CreatePng("tiny.png", 50, 50);

            var output = Core.ImageConverter.CropToSize(input, new PixelSize(200, 100));

            Assert.Equal(new PixelSize(200, 100), SizeOf(output));
        }

        [Fact]
        public void CropToAspect_RemovesSidesWithoutResizing()
        {
            var input = CreatePng("pano.png", 400, 100);

            var output = Core.ImageConverter.CropToAspect(input, 1, 1);

            Assert.Equal(new PixelSize(100, 100), SizeOf(output));
        }

        [Fact]
        public void CropToAspect_KeepsMatchingImageUnchanged()
        {
            var input = CreatePng("square.png", 120, 90);

            var output = Core.ImageConverter.CropToAspect(input, 4, 3);

            Assert.Equal(new PixelSize(120, 90), SizeOf(output));
        }

        [Fact]
        public void SizeOperations_KeepJpegSourcesAsJpeg()
        {
            var png = CreatePng("source.png", 200, 100);
            var jpg = Core.ImageConverter.Convert(png, ConversionTarget.Jpeg, 90);

            var output = Core.ImageConverter.ResizeToFit(jpg, new PixelSize(100, 100));

            Assert.EndsWith(".jpg", output);
            Assert.Equal(new PixelSize(100, 50), SizeOf(output));
        }

        [Fact]
        public void SizeOperations_UseCollisionSuffixWhenNameTaken()
        {
            var input = CreatePng("twice.png", 400, 200);

            var first = Core.ImageConverter.ResizeToFit(input, new PixelSize(100, 100));
            var second = Core.ImageConverter.ResizeToFit(input, new PixelSize(100, 100));

            Assert.Equal(Path.Combine(_dir, "twice.100x50.png"), first);
            Assert.Equal(Path.Combine(_dir, "twice.001.100x50.png"), second);
        }
    }
}
