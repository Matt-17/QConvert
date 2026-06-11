using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QConvert.Core
{
    public static class ImageConverter
    {
        private static readonly int[] IconSizes = { 16, 32, 48, 64, 128 };

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
                case ConversionTarget.Ico:
                    WriteIcon(image, outputPath);
                    return outputPath;
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

        private static void WriteIcon(BitmapSource source, string outputPath)
        {
            var images = IconSizes
                .Select(size => new IconImage(size, EncodePng(CreateIconFrame(source, size))))
                .ToList();

            using var output = new FileStream(outputPath, FileMode.CreateNew);
            using var writer = new BinaryWriter(output);

            writer.Write((ushort)0); // Reserved.
            writer.Write((ushort)1); // Icon resource.
            writer.Write((ushort)images.Count);

            var offset = 6 + images.Count * 16;
            foreach (var image in images)
            {
                writer.Write((byte)image.Size);
                writer.Write((byte)image.Size);
                writer.Write((byte)0); // Color count.
                writer.Write((byte)0); // Reserved.
                writer.Write((ushort)1); // Color planes.
                writer.Write((ushort)32); // Bits per pixel.
                writer.Write(image.Data.Length);
                writer.Write(offset);
                offset += image.Data.Length;
            }

            foreach (var image in images)
            {
                writer.Write(image.Data);
            }
        }

        private static byte[] EncodePng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        private static BitmapSource CreateIconFrame(BitmapSource source, int size)
        {
            var scale = Math.Min(size / (double)source.PixelWidth, size / (double)source.PixelHeight);
            var fitted = new PixelSize(
                Math.Max(1, (int)Math.Round(source.PixelWidth * scale)),
                Math.Max(1, (int)Math.Round(source.PixelHeight * scale)));
            var scaled = ScaleWithAlpha(source, fitted);

            int outputStride = size * 4;
            var output = new byte[outputStride * size];
            int inputStride = scaled.PixelWidth * 4;
            var input = new byte[inputStride * scaled.PixelHeight];
            scaled.CopyPixels(input, inputStride, 0);

            var offsetX = (size - scaled.PixelWidth) / 2;
            var offsetY = (size - scaled.PixelHeight) / 2;
            for (var y = 0; y < scaled.PixelHeight; y++)
            {
                Buffer.BlockCopy(
                    input,
                    y * inputStride,
                    output,
                    (y + offsetY) * outputStride + offsetX * 4,
                    inputStride);
            }

            var frame = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, output, outputStride);
            frame.Freeze();
            return frame;
        }

        private static BitmapSource ScaleWithAlpha(BitmapSource source, PixelSize size)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int inputStride = bgra.PixelWidth * 4;
            var input = new byte[inputStride * bgra.PixelHeight];
            bgra.CopyPixels(input, inputStride, 0);

            int outputStride = size.Width * 4;
            var output = new byte[outputStride * size.Height];

            if (size.Width <= bgra.PixelWidth && size.Height <= bgra.PixelHeight)
            {
                ScaleArea(input, bgra.PixelWidth, bgra.PixelHeight, inputStride, output, size, outputStride);
            }
            else
            {
                ScaleBilinear(input, bgra.PixelWidth, bgra.PixelHeight, inputStride, output, size, outputStride);
            }

            var scaled = BitmapSource.Create(size.Width, size.Height, 96, 96, PixelFormats.Bgra32, null, output, outputStride);
            scaled.Freeze();
            return scaled;
        }

        private static void ScaleArea(byte[] input, int inputWidth, int inputHeight, int inputStride, byte[] output, PixelSize size, int outputStride)
        {
            var scaleX = inputWidth / (double)size.Width;
            var scaleY = inputHeight / (double)size.Height;
            var pixelArea = scaleX * scaleY;

            for (var y = 0; y < size.Height; y++)
            {
                var sourceTop = y * scaleY;
                var sourceBottom = sourceTop + scaleY;
                var firstY = Math.Max(0, (int)Math.Floor(sourceTop));
                var lastY = Math.Min(inputHeight - 1, (int)Math.Ceiling(sourceBottom) - 1);

                for (var x = 0; x < size.Width; x++)
                {
                    var sourceLeft = x * scaleX;
                    var sourceRight = sourceLeft + scaleX;
                    var firstX = Math.Max(0, (int)Math.Floor(sourceLeft));
                    var lastX = Math.Min(inputWidth - 1, (int)Math.Ceiling(sourceRight) - 1);

                    double sumAlpha = 0;
                    double sumBlue = 0;
                    double sumGreen = 0;
                    double sumRed = 0;

                    for (var sy = firstY; sy <= lastY; sy++)
                    {
                        var yWeight = Math.Min(sourceBottom, sy + 1) - Math.Max(sourceTop, sy);
                        if (yWeight <= 0)
                        {
                            continue;
                        }

                        for (var sx = firstX; sx <= lastX; sx++)
                        {
                            var xWeight = Math.Min(sourceRight, sx + 1) - Math.Max(sourceLeft, sx);
                            if (xWeight <= 0)
                            {
                                continue;
                            }

                            var weight = xWeight * yWeight;
                            var inputIndex = sy * inputStride + sx * 4;
                            var alpha = input[inputIndex + 3] / 255.0;
                            var weightedAlpha = alpha * weight;

                            sumAlpha += weightedAlpha;
                            sumBlue += input[inputIndex] * weightedAlpha;
                            sumGreen += input[inputIndex + 1] * weightedAlpha;
                            sumRed += input[inputIndex + 2] * weightedAlpha;
                        }
                    }

                    WriteStraightAlphaPixel(output, y * outputStride + x * 4, sumBlue, sumGreen, sumRed, sumAlpha, pixelArea);
                }
            }
        }

        private static void ScaleBilinear(byte[] input, int inputWidth, int inputHeight, int inputStride, byte[] output, PixelSize size, int outputStride)
        {
            var scaleX = inputWidth / (double)size.Width;
            var scaleY = inputHeight / (double)size.Height;

            for (var y = 0; y < size.Height; y++)
            {
                var sourceY = (y + 0.5) * scaleY - 0.5;
                var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, inputHeight - 1);
                var y1 = Math.Clamp(y0 + 1, 0, inputHeight - 1);
                var yWeight = sourceY - Math.Floor(sourceY);

                for (var x = 0; x < size.Width; x++)
                {
                    var sourceX = (x + 0.5) * scaleX - 0.5;
                    var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, inputWidth - 1);
                    var x1 = Math.Clamp(x0 + 1, 0, inputWidth - 1);
                    var xWeight = sourceX - Math.Floor(sourceX);

                    double sumAlpha = 0;
                    double sumBlue = 0;
                    double sumGreen = 0;
                    double sumRed = 0;

                    AddWeightedPixel(input, inputStride, x0, y0, (1 - xWeight) * (1 - yWeight), ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);
                    AddWeightedPixel(input, inputStride, x1, y0, xWeight * (1 - yWeight), ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);
                    AddWeightedPixel(input, inputStride, x0, y1, (1 - xWeight) * yWeight, ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);
                    AddWeightedPixel(input, inputStride, x1, y1, xWeight * yWeight, ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);

                    WriteStraightAlphaPixel(output, y * outputStride + x * 4, sumBlue, sumGreen, sumRed, sumAlpha, pixelArea: 1);
                }
            }
        }

        private static void AddWeightedPixel(byte[] input, int inputStride, int x, int y, double weight, ref double sumBlue, ref double sumGreen, ref double sumRed, ref double sumAlpha)
        {
            var inputIndex = y * inputStride + x * 4;
            var alpha = input[inputIndex + 3] / 255.0;
            var weightedAlpha = alpha * weight;

            sumAlpha += weightedAlpha;
            sumBlue += input[inputIndex] * weightedAlpha;
            sumGreen += input[inputIndex + 1] * weightedAlpha;
            sumRed += input[inputIndex + 2] * weightedAlpha;
        }

        private static void WriteStraightAlphaPixel(byte[] output, int index, double sumBlue, double sumGreen, double sumRed, double sumAlpha, double pixelArea)
        {
            if (sumAlpha <= 0)
            {
                return;
            }

            output[index] = ClampByte(sumBlue / sumAlpha);
            output[index + 1] = ClampByte(sumGreen / sumAlpha);
            output[index + 2] = ClampByte(sumRed / sumAlpha);
            output[index + 3] = ClampByte(255 * sumAlpha / pixelArea);
        }

        private static byte ClampByte(double value) =>
            (byte)Math.Clamp((int)Math.Round(value), 0, 255);

        private sealed record IconImage(int Size, byte[] Data);

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
