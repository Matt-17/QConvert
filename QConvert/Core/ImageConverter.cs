using System.IO;
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

            BitmapFrame frame;
            using (var input = File.OpenRead(fullPath))
            {
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
            }

            BitmapSource source = ApplyExifOrientation(frame);

            BitmapEncoder encoder;
            switch (target)
            {
                case ConversionTarget.Jpeg:
                    encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, AppSettings.MinJpegQuality, AppSettings.MaxJpegQuality) };
                    // JPEG has no alpha channel; composite transparent pixels onto white
                    // instead of letting the encoder turn them black.
                    source = FlattenToWhite(source);
                    break;
                case ConversionTarget.Png:
                    encoder = new PngBitmapEncoder();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }

            encoder.Frames.Add(BitmapFrame.Create(source));

            var outputPath = OutputPathResolver.GetUniquePath(fullPath, target.FileExtension());
            using (var output = new FileStream(outputPath, FileMode.CreateNew))
            {
                encoder.Save(output);
            }

            return outputPath;
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

            var rotation = orientation switch
            {
                3 => Rotation.Rotate180,
                6 => Rotation.Rotate90,
                8 => Rotation.Rotate270,
                _ => Rotation.Rotate0,
            };

            if (rotation == Rotation.Rotate0)
            {
                return frame;
            }

            var transformed = new TransformedBitmap(frame, rotation switch
            {
                Rotation.Rotate90 => new RotateTransform(90),
                Rotation.Rotate180 => new RotateTransform(180),
                _ => new RotateTransform(270),
            });
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
