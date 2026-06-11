using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QConvert.Core
{
    public static class ImageConverter
    {
        /// <summary>
        /// Converts an image file to the target format and writes it next to the
        /// source file. Returns the path of the created file.
        /// </summary>
        public static string Convert(string inputPath, ConversionTarget target, int jpegQuality = AppSettings.DefaultJpegQuality)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);
            var outputPath = OutputPathResolver.GetUniquePath(fullPath, target.FileExtension());
            return Encode(image, outputPath, target, jpegQuality);
        }

        /// <summary>
        /// Scales the image (preserving aspect ratio, never upscaling) so it fits
        /// inside the box and writes the copy next to the source file.
        /// </summary>
        public static string ResizeToFit(string inputPath, PixelSize box, int jpegQuality = AppSettings.DefaultJpegQuality)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var size = ResizeMath.Fit(new PixelSize(image.PixelWidth, image.PixelHeight), box);
            if (size.Width != image.PixelWidth || size.Height != image.PixelHeight)
            {
                image = Scale(image, size);
            }

            return SaveSibling(fullPath, image, jpegQuality);
        }

        /// <summary>
        /// Scales the image (up or down) to cover the box, then center-crops to
        /// exactly the box size.
        /// </summary>
        public static string CropToSize(string inputPath, PixelSize box, int jpegQuality = AppSettings.DefaultJpegQuality)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var plan = ResizeMath.Cover(new PixelSize(image.PixelWidth, image.PixelHeight), box);
            image = Crop(Scale(image, plan.Scaled), plan.Crop);

            return SaveSibling(fullPath, image, jpegQuality);
        }

        /// <summary>
        /// Center-crops the image to the given aspect ratio without resizing.
        /// </summary>
        public static string CropToAspect(string inputPath, int ratioX, int ratioY, int jpegQuality = AppSettings.DefaultJpegQuality)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var rect = ResizeMath.AspectCrop(new PixelSize(image.PixelWidth, image.PixelHeight), ratioX, ratioY);
            if (rect.Width != image.PixelWidth || rect.Height != image.PixelHeight)
            {
                image = Crop(image, rect);
            }

            return SaveSibling(fullPath, image, jpegQuality);
        }

        /// <summary>
        /// Output format for size operations: JPEG sources stay JPEG, everything
        /// else becomes PNG (WIC has no WebP encoder).
        /// </summary>
        public static ConversionTarget TargetForSource(string extension) =>
            extension.ToLowerInvariant() is ".jpg" or ".jpeg" ? ConversionTarget.Jpeg : ConversionTarget.Png;

        private static string SaveSibling(string fullPath, BitmapSource image, int jpegQuality)
        {
            var target = TargetForSource(Path.GetExtension(fullPath));
            var outputPath = OutputPathResolver.GetUniquePath(
                fullPath,
                $".{image.PixelWidth}x{image.PixelHeight}{target.FileExtension()}");
            return Encode(image, outputPath, target, jpegQuality);
        }

        private static string Encode(BitmapSource image, string outputPath, ConversionTarget target, int jpegQuality)
        {
            BitmapEncoder encoder;
            switch (target)
            {
                case ConversionTarget.Jpeg:
                    encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, AppSettings.MinJpegQuality, AppSettings.MaxJpegQuality) };
                    // JPEG has no alpha channel; composite transparent pixels onto white
                    // instead of letting the encoder turn them black.
                    image = FlattenToWhite(image);
                    break;
                case ConversionTarget.Png:
                    encoder = new PngBitmapEncoder();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }

            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var output = new FileStream(outputPath, FileMode.CreateNew))
            {
                encoder.Save(output);
            }

            return outputPath;
        }

        private static BitmapSource LoadOriented(string fullPath)
        {
            BitmapFrame frame;
            using (var input = File.OpenRead(fullPath))
            {
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
            }

            return ApplyExifOrientation(frame);
        }

        private static BitmapSource Scale(BitmapSource source, PixelSize size)
        {
            var scaled = new TransformedBitmap(source, new ScaleTransform(
                size.Width / (double)source.PixelWidth,
                size.Height / (double)source.PixelHeight));
            scaled.Freeze();
            return scaled;
        }

        private static BitmapSource Crop(BitmapSource source, PixelRect rect)
        {
            // Guard against rounding drift from scaling.
            var x = Math.Clamp(rect.X, 0, Math.Max(0, source.PixelWidth - 1));
            var y = Math.Clamp(rect.Y, 0, Math.Max(0, source.PixelHeight - 1));
            var width = Math.Min(rect.Width, source.PixelWidth - x);
            var height = Math.Min(rect.Height, source.PixelHeight - y);

            var cropped = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            cropped.Freeze();
            return cropped;
        }

        private static BitmapSource ApplyExifOrientation(BitmapFrame frame)
        {
            // PNG output drops EXIF, so bake the common orientations (3, 6, 8)
            // into the pixels to keep phone photos upright.
            ushort orientation = 1;
            try
            {
                if (frame.Metadata is BitmapMetadata metadata
                    && metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort value)
                {
                    orientation = value;
                }
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
            {
                // No EXIF block or container without metadata support.
            }

            var angle = orientation switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0,
            };

            if (angle == 0)
            {
                return frame;
            }

            var transformed = new TransformedBitmap(frame, new RotateTransform(angle));
            transformed.Freeze();
            return transformed;
        }

        private static BitmapSource FlattenToWhite(BitmapSource source)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int inputStride = width * 4;
            var input = new byte[inputStride * height];
            bgra.CopyPixels(input, inputStride, 0);

            int outputStride = width * 3;
            var output = new byte[outputStride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    int i = y * inputStride + x * 4;
                    int o = y * outputStride + x * 3;
                    int alpha = input[i + 3];
                    output[o] = Blend(input[i], alpha);
                    output[o + 1] = Blend(input[i + 1], alpha);
                    output[o + 2] = Blend(input[i + 2], alpha);
                }
            }

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgr24, null, output, outputStride);
            result.Freeze();
            return result;
        }

        private static byte Blend(byte channel, int alpha) =>
            (byte)((channel * alpha + 255 * (255 - alpha)) / 255);
    }
}
