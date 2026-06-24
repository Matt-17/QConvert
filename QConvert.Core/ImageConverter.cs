using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using SkiaSharp;

namespace QConvert.Core
{
    public static class ImageConverter
    {
        public const int StandardSepiaHue = 30;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Converts an image file to the target format. Returns the output path.</summary>
        public static string Convert(
            string inputPath,
            ConversionTarget target,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var outputPath = OutputPathResolver.GetUniquePath(fullPath, target.FileExtension(), settings);
            return Encode(
                loaded.Image,
                outputPath,
                target,
                jpegQuality,
                settings,
                GetMetadataForTarget(loaded.Metadata, loaded.SourceExtension, target, settings));
        }

        /// <summary>
        /// Scales the image (preserving aspect ratio, never upscaling) so it fits
        /// inside the box and writes the copy next to the source file.
        /// </summary>
        public static string ResizeToFit(
            string inputPath,
            PixelSize box,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null,
            bool keepAspect = true)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = loaded.Image;

            var size = keepAspect
                ? ResizeMath.Fit(new PixelSize(image.PixelWidth, image.PixelHeight), box)
                : box;
            if (size.Width != image.PixelWidth || size.Height != image.PixelHeight)
            {
                image = Scale(image, size);
            }

            return SaveSibling(fullPath, image, jpegQuality, settings, loaded.Metadata, loaded.SourceExtension);
        }

        public static string CropAndResize(
            string inputPath,
            PixelRect crop,
            PixelSize output,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = Crop(loaded.Image, crop);

            if (image.PixelWidth != output.Width || image.PixelHeight != output.Height)
            {
                image = Scale(image, output);
            }

            return SaveSibling(fullPath, image, jpegQuality, settings, loaded.Metadata, loaded.SourceExtension);
        }

        /// <summary>
        /// Scales the image to cover the box, then crops using the anchor from
        /// settings (default: center).
        /// </summary>
        public static string CropToSize(
            string inputPath,
            PixelSize box,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = loaded.Image;
            var anchor = settings?.CropAnchor ?? CropAnchor.Center;
            var plan = ResizeMath.Cover(new PixelSize(image.PixelWidth, image.PixelHeight), box, anchor);
            image = Crop(Scale(image, plan.Scaled), plan.Crop);

            return SaveSibling(fullPath, image, jpegQuality, settings, loaded.Metadata, loaded.SourceExtension);
        }

        /// <summary>
        /// Scales the image to cover the box, then crops at a normalized position
        /// where 0 is left/top, 0.5 is center, and 1 is right/bottom.
        /// </summary>
        public static string CropToSize(
            string inputPath,
            PixelSize box,
            double positionX,
            double positionY,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = loaded.Image;
            var plan = ResizeMath.Cover(new PixelSize(image.PixelWidth, image.PixelHeight), box, positionX, positionY);
            image = Crop(Scale(image, plan.Scaled), plan.Crop);

            return SaveSibling(fullPath, image, jpegQuality, settings, loaded.Metadata, loaded.SourceExtension);
        }

        /// <summary>Center-crops the image to the given aspect ratio without resizing.</summary>
        public static string CropToAspect(
            string inputPath,
            int ratioX,
            int ratioY,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = loaded.Image;

            var rect = ResizeMath.AspectCrop(new PixelSize(image.PixelWidth, image.PixelHeight), ratioX, ratioY);
            if (rect.Width != image.PixelWidth || rect.Height != image.PixelHeight)
            {
                image = Crop(image, rect);
            }

            return SaveSibling(fullPath, image, jpegQuality, settings, loaded.Metadata, loaded.SourceExtension);
        }

        /// <summary>Crops the image to the aspect ratio at a normalized position without resizing.</summary>
        public static string CropToAspect(
            string inputPath,
            int ratioX,
            int ratioY,
            double positionX,
            double positionY,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = loaded.Image;

            var rect = ResizeMath.AspectCrop(new PixelSize(image.PixelWidth, image.PixelHeight), ratioX, ratioY, positionX, positionY);
            if (rect.Width != image.PixelWidth || rect.Height != image.PixelHeight)
            {
                image = Crop(image, rect);
            }

            return SaveSibling(fullPath, image, jpegQuality, settings, loaded.Metadata, loaded.SourceExtension);
        }

        /// <summary>
        /// Re-encodes the image without metadata (EXIF orientation is baked into
        /// pixels first). Output: {name}.clean.{ext}.
        /// </summary>
        public static string StripMetadata(string inputPath, AppSettings? settings = null)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var target = TargetForSource(Path.GetExtension(fullPath));
            var outputPath = OutputPathResolver.GetUniquePath(fullPath, $".clean{target.FileExtension()}", settings);
            return Encode(image, outputPath, target, settings.JpegQuality, settings, metadata: null);
        }

        /// <summary>Applies a sepia tone at 0-100% intensity and writes a copy next to the source.</summary>
        public static string ApplySepia(string inputPath, int intensity, AppSettings? settings = null, int hue = StandardSepiaHue)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var image = ApplySepiaTone(loaded.Image, intensity, hue);
            var target = TargetForSource(Path.GetExtension(fullPath));
            var clampedIntensity = Math.Clamp(intensity, 0, 100);
            var clampedHue = NormalizeHue(hue);
            var sepiaSuffix = clampedHue == StandardSepiaHue
                ? $".sepia{clampedIntensity}"
                : $".sepia{clampedIntensity}h{clampedHue}";
            var outputPath = OutputPathResolver.GetUniquePath(
                fullPath,
                $"{sepiaSuffix}{target.FileExtension()}",
                settings,
                image.PixelWidth,
                image.PixelHeight);

            return Encode(
                image,
                outputPath,
                target,
                settings.JpegQuality,
                settings,
                GetMetadataForTarget(loaded.Metadata, loaded.SourceExtension, target, settings));
        }

        /// <summary>
        /// Recompresses a JPEG using the configured quality. Works regardless of
        /// whether the source is already JPEG. Output: {name}.compressed.jpg.
        /// </summary>
        public static string CompressJpeg(string inputPath, AppSettings? settings = null)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var loaded = LoadOrientedWithMetadata(fullPath);
            var outputPath = OutputPathResolver.GetUniquePath(fullPath, ".compressed.jpg", settings);
            return Encode(
                loaded.Image,
                outputPath,
                ConversionTarget.Jpeg,
                settings.JpegQuality,
                settings,
                GetMetadataForTarget(loaded.Metadata, loaded.SourceExtension, ConversionTarget.Jpeg, settings));
        }

        /// <summary>
        /// Re-encodes a PNG using best compression. Uses better compression when it
        /// produces a smaller file, otherwise falls back to standard re-encode.
        /// Output: {name}.optimized.png.
        /// </summary>
        public static string OptimizePng(string inputPath, AppSettings? settings = null)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var outputPath = OutputPathResolver.GetUniquePath(fullPath, ".optimized.png", settings);
            var optimizedBytes = OptimizePngBytes(image);
            var sourceLength = new FileInfo(fullPath).Length;
            var bytes = optimizedBytes.Length < sourceLength ? optimizedBytes : EncodePng(image);
            File.WriteAllBytes(outputPath, bytes);
            return outputPath;
        }

        /// <summary>
        /// Generates a favicon bundle (ICO + PNGs + webmanifest) in a subfolder
        /// next to the source image. Returns the created folder path.
        /// </summary>
        public static string CreateFaviconBundle(string inputPath, AppSettings? settings = null)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var directory = Path.GetDirectoryName(fullPath)!;
            var baseName = Path.GetFileNameWithoutExtension(fullPath);

            var folderPath = Path.Combine(directory, $"{baseName}.favicon");
            if (Directory.Exists(folderPath))
            {
                for (var i = 1; i <= 999; i++)
                {
                    folderPath = Path.Combine(directory, $"{baseName}.{i:000}.favicon");
                    if (!Directory.Exists(folderPath)) break;
                }
            }
            Directory.CreateDirectory(folderPath);

            var iconSizes = settings.IcoSizes.Count > 0
                ? settings.IcoSizes.ToArray()
                : new[] { 16, 32, 48 };
            WriteIconToPath(image, Path.Combine(folderPath, "favicon.ico"), iconSizes);
            WriteFaviconPng(image, folderPath, "favicon-16x16.png", 16);
            WriteFaviconPng(image, folderPath, "favicon-32x32.png", 32);
            WriteFaviconPng(image, folderPath, "apple-touch-icon.png", 180);
            WriteFaviconPng(image, folderPath, "android-chrome-192x192.png", 192);
            WriteFaviconPng(image, folderPath, "android-chrome-512x512.png", 512);

            const string manifest = """
                {
                  "name": "",
                  "short_name": "",
                  "icons": [
                    { "src": "android-chrome-192x192.png", "sizes": "192x192", "type": "image/png" },
                    { "src": "android-chrome-512x512.png", "sizes": "512x512", "type": "image/png" }
                  ],
                  "theme_color": "#ffffff",
                  "background_color": "#ffffff",
                  "display": "standalone"
                }
                """;
            File.WriteAllText(Path.Combine(folderPath, "site.webmanifest"), manifest);

            return folderPath;
        }

        /// <summary>
        /// Center-crops to square and resizes to exactly <paramref name="size"/>×<paramref name="size"/>
        /// using the anchor from settings. Output: {name}.{size}x{size}.png.
        /// </summary>
        public static string MakeAvatar(string inputPath, int size, AppSettings? settings = null)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var box = new PixelSize(size, size);
            var plan = ResizeMath.Cover(new PixelSize(image.PixelWidth, image.PixelHeight), box, settings.CropAnchor);
            image = Crop(Scale(image, plan.Scaled), plan.Crop);

            var outputPath = OutputPathResolver.GetUniquePath(fullPath, $".{size}x{size}.png", settings, size, size);
            return Encode(image, outputPath, ConversionTarget.Png, settings.JpegQuality, settings);
        }

        public static string MakeAvatar(string inputPath, int size, double positionX, double positionY, AppSettings? settings = null)
        {
            settings ??= new AppSettings();
            var fullPath = Path.GetFullPath(inputPath);
            var image = LoadOriented(fullPath);

            var box = new PixelSize(size, size);
            var plan = ResizeMath.Cover(new PixelSize(image.PixelWidth, image.PixelHeight), box, positionX, positionY);
            image = Crop(Scale(image, plan.Scaled), plan.Crop);

            var outputPath = OutputPathResolver.GetUniquePath(fullPath, $".{size}x{size}.png", settings, size, size);
            return Encode(image, outputPath, ConversionTarget.Png, settings.JpegQuality, settings);
        }

        /// <summary>
        /// Output format for size operations: JPEG sources stay JPEG, everything
        /// else becomes PNG.
        /// </summary>
        public static ConversionTarget TargetForSource(string extension) =>
            extension.ToLowerInvariant() is ".jpg" or ".jpeg" ? ConversionTarget.Jpeg : ConversionTarget.Png;

        public static string SaveBitmap(
            BitmapSource image,
            string outputPath,
            ConversionTarget target,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null,
            bool overwrite = true)
        {
            if (target is not (ConversionTarget.Jpeg or ConversionTarget.Png))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "Clipboard paste supports JPG and PNG output.");
            }

            return Encode(image, Path.GetFullPath(outputPath), target, jpegQuality, settings, metadata: null, overwrite: overwrite);
        }

        public static string SaveImage(
            BitmapSource image,
            string outputPath,
            ConversionTarget target,
            int jpegQuality = AppSettings.DefaultJpegQuality,
            AppSettings? settings = null,
            bool overwrite = true) =>
            Encode(image, Path.GetFullPath(outputPath), target, jpegQuality, settings, metadata: null, overwrite: overwrite);

        public static string GetClipboardOutputPath(string folderPath, ConversionTarget target, DateTime? timestamp = null)
        {
            if (target is not (ConversionTarget.Jpeg or ConversionTarget.Png))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "Clipboard paste supports JPG and PNG output.");
            }

            var directory = Path.GetFullPath(folderPath);
            var stamp = (timestamp ?? DateTime.Now).ToString("yyyy-MM-ddTHH-mm-ss");
            return GetUniqueFilePath(directory, $"clipboard-{stamp}{target.FileExtension()}");
        }

        private static string GetUniqueFilePath(string directory, string fileName)
        {
            var candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            var name = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            for (var i = 1; i <= 999; i++)
            {
                candidate = Path.Combine(directory, $"{name}.{i:000}{extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException($"No free output name found for '{Path.Combine(directory, fileName)}' (tried up to suffix .999).");
        }

        /// <summary>Loads an image the same way conversion does: cached, unlocked, and EXIF-oriented.</summary>
        public static BitmapSource LoadPreview(string inputPath) =>
            LoadOriented(Path.GetFullPath(inputPath));

        /// <summary>Materializes a BitmapSource so it can safely be used by a worker thread.</summary>
        public static BitmapSource CreateThreadSafeCopy(BitmapSource source)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var width = bgra.PixelWidth;
            var height = bgra.PixelHeight;
            var stride = checked(width * 4);
            var pixels = new byte[checked(stride * height)];
            bgra.CopyPixels(pixels, stride, 0);

            var copy = BitmapSource.Create(
                width,
                height,
                source.DpiX,
                source.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
            copy.Freeze();
            return copy;
        }

        /// <summary>Renders an in-memory preview for an operation without writing an output file.</summary>
        public static BitmapSource RenderPreview(BitmapSource source, Operation operation, AppSettings? settings = null)
        {
            settings ??= new AppSettings();

            return operation switch
            {
                ConvertOperation { Target: ConversionTarget.Jpeg } => FlattenToConfiguredColor(source, settings),
                ConvertOperation { Target: ConversionTarget.Ico } => CreateIconFrame(
                    source,
                    settings.IcoSizes.Count > 0 ? settings.IcoSizes.Max() : 256),
                ConvertOperation => source,
                FitOperation fit => fit.KeepAspect ? PreviewFit(source, fit.Box) : Scale(source, fit.Box),
                CoverOperation cover => PreviewCover(source, cover.Box, cover.PositionX, cover.PositionY),
                CropResizeOperation cropResize => Scale(Crop(source, cropResize.Crop), cropResize.Output),
                AspectCropOperation aspect => Crop(source, ResizeMath.AspectCrop(
                    new PixelSize(source.PixelWidth, source.PixelHeight),
                    aspect.RatioX,
                    aspect.RatioY,
                    aspect.PositionX,
                    aspect.PositionY)),
                StripMetadataOperation => source,
                SepiaOperation sepia => ApplySepiaTone(source, sepia.Intensity, sepia.Hue),
                CompressJpegOperation => FlattenToConfiguredColor(source, settings),
                OptimizePngOperation => source,
                FaviconBundleOperation => CreateIconFrame(source, 512),
                AvatarExportOperation avatar => PreviewCover(source, new PixelSize(avatar.Size, avatar.Size), avatar.PositionX, avatar.PositionY),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
            };
        }

        // ── Encoding ─────────────────────────────────────────────────────────────

        private static string Encode(
            BitmapSource image,
            string outputPath,
            ConversionTarget target,
            int jpegQuality,
            AppSettings? settings,
            BitmapMetadata? metadata = null,
            bool overwrite = false)
        {
            switch (target)
            {
                case ConversionTarget.Jpeg:
                {
                    var (r, g, b) = settings?.GetBackgroundRgb() ?? (255, 255, 255);
                    var flat = FlattenToColor(image, r, g, b);
                    var encoder = new JpegBitmapEncoder
                    {
                        QualityLevel = Math.Clamp(jpegQuality, AppSettings.MinJpegQuality, AppSettings.MaxJpegQuality),
                    };
                    SaveWicEncoder(encoder, flat, outputPath, metadata, overwrite);
                    return outputPath;
                }

                case ConversionTarget.Png:
                {
                    var encoder = new PngBitmapEncoder();
                    SaveWicEncoder(encoder, image, outputPath, metadata, overwrite);
                    return outputPath;
                }

                case ConversionTarget.Ico:
                    WriteIconToPath(image, outputPath, settings?.IcoSizes?.ToArray() ?? new[] { 16, 32, 48, 64, 128 });
                    return outputPath;

                case ConversionTarget.WebP:
                {
                    var quality = settings?.WebPQuality ?? AppSettings.DefaultWebPQuality;
                    var data = EncodeSkia(image, SKEncodedImageFormat.Webp, quality);
                    File.WriteAllBytes(outputPath, data);
                    return outputPath;
                }

                case ConversionTarget.Avif:
                {
                    var quality = settings?.AvifQuality ?? AppSettings.DefaultAvifQuality;
                    var data = EncodeSkia(image, SKEncodedImageFormat.Avif, quality);
                    File.WriteAllBytes(outputPath, data);
                    return outputPath;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }
        }

        private static string SaveSibling(
            string fullPath,
            BitmapSource image,
            int jpegQuality,
            AppSettings? settings,
            BitmapMetadata? metadata = null,
            string? sourceExtension = null)
        {
            var target = TargetForSource(Path.GetExtension(fullPath));
            var ext = $".{image.PixelWidth}x{image.PixelHeight}{target.FileExtension()}";
            var outputPath = OutputPathResolver.GetUniquePath(fullPath, ext, settings, image.PixelWidth, image.PixelHeight);
            return Encode(
                image,
                outputPath,
                target,
                jpegQuality,
                settings,
                GetMetadataForTarget(metadata, sourceExtension ?? Path.GetExtension(fullPath), target, settings));
        }

        private static void SaveWicEncoder(
            BitmapEncoder encoder,
            BitmapSource image,
            string outputPath,
            BitmapMetadata? metadata,
            bool overwrite)
        {
            try
            {
                encoder.Frames.Add(metadata is null
                    ? BitmapFrame.Create(image)
                    : BitmapFrame.Create(image, null, metadata, null));
                SaveEncoder(encoder, outputPath, overwrite);
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
            {
                if (metadata is null)
                {
                    throw;
                }

                TryDeletePartialOutput(outputPath);
                var fallback = CreateMatchingEncoder(encoder);
                fallback.Frames.Add(BitmapFrame.Create(image));
                SaveEncoder(fallback, outputPath, overwrite);
            }
        }

        private static void SaveEncoder(BitmapEncoder encoder, string outputPath, bool overwrite)
        {
            using var output = new FileStream(outputPath, overwrite ? FileMode.Create : FileMode.CreateNew);
            encoder.Save(output);
        }

        private static BitmapEncoder CreateMatchingEncoder(BitmapEncoder encoder) => encoder switch
        {
            JpegBitmapEncoder jpeg => new JpegBitmapEncoder { QualityLevel = jpeg.QualityLevel },
            PngBitmapEncoder => new PngBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(encoder), encoder, null),
        };

        private static void TryDeletePartialOutput(string outputPath)
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // ── ICO writer ───────────────────────────────────────────────────────────

        private static void WriteIconToPath(BitmapSource source, string outputPath, int[] sizes)
        {
            var images = sizes
                .Select(size => new IconImage(size, EncodeIconDib(CreateIconFrame(source, size))))
                .ToList();

            using var output = new FileStream(outputPath, FileMode.CreateNew);
            using var writer = new BinaryWriter(output);

            writer.Write((ushort)0); // Reserved.
            writer.Write((ushort)1); // Icon resource.
            writer.Write((ushort)images.Count);

            var offset = 6 + images.Count * 16;
            foreach (var image in images)
            {
                // ICO spec: width/height byte of 0 means 256.
                writer.Write((byte)(image.Size >= 256 ? 0 : image.Size));
                writer.Write((byte)(image.Size >= 256 ? 0 : image.Size));
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

        private static byte[] EncodeIconDib(BitmapSource image)
        {
            var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var width = bgra.PixelWidth;
            var height = bgra.PixelHeight;
            var stride = checked(width * 4);
            var topDownPixels = new byte[checked(stride * height)];
            bgra.CopyPixels(topDownPixels, stride, 0);

            var xorPixels = new byte[topDownPixels.Length];
            var maskStride = ((width + 31) / 32) * 4;
            var andMask = new byte[checked(maskStride * height)];

            var outputOffset = 0;
            for (var y = height - 1; y >= 0; y--)
            {
                var inputRow = y * stride;
                Buffer.BlockCopy(topDownPixels, inputRow, xorPixels, outputOffset, stride);

                for (var x = 0; x < width; x++)
                {
                    var alpha = topDownPixels[inputRow + x * 4 + 3];
                    if (alpha >= 128)
                    {
                        continue;
                    }

                    var maskRow = (height - 1 - y) * maskStride;
                    andMask[maskRow + x / 8] |= (byte)(0x80 >> (x % 8));
                }

                outputOffset += stride;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(40);                // BITMAPINFOHEADER size.
            writer.Write(width);
            writer.Write(height * 2);        // ICO stores XOR bitmap plus AND mask.
            writer.Write((ushort)1);         // Color planes.
            writer.Write((ushort)32);        // Bits per pixel.
            writer.Write(0);                 // BI_RGB.
            writer.Write(xorPixels.Length + andMask.Length);
            writer.Write(0);                 // Horizontal pixels per meter.
            writer.Write(0);                 // Vertical pixels per meter.
            writer.Write(0);                 // Colors used.
            writer.Write(0);                 // Important colors.
            writer.Write(xorPixels);
            writer.Write(andMask);
            writer.Flush();

            return stream.ToArray();
        }

        private static byte[] EncodePng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        private static byte[] OptimizePngBytes(BitmapSource image)
        {
            // Use SkiaSharp for best-compression PNG re-encode.
            return EncodeSkia(image, SKEncodedImageFormat.Png, 100);
        }

        private static void WriteFaviconPng(BitmapSource source, string folder, string fileName, int size)
        {
            var bytes = EncodePng(CreateIconFrame(source, size));
            File.WriteAllBytes(Path.Combine(folder, fileName), bytes);
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

        // ── Scaling & cropping ───────────────────────────────────────────────────

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

        private static void ScaleArea(byte[] input, int inputWidth, int inputHeight, int inputStride,
            byte[] output, PixelSize size, int outputStride)
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

                    double sumAlpha = 0, sumBlue = 0, sumGreen = 0, sumRed = 0;

                    for (var sy = firstY; sy <= lastY; sy++)
                    {
                        var yWeight = Math.Min(sourceBottom, sy + 1) - Math.Max(sourceTop, sy);
                        if (yWeight <= 0) continue;

                        for (var sx = firstX; sx <= lastX; sx++)
                        {
                            var xWeight = Math.Min(sourceRight, sx + 1) - Math.Max(sourceLeft, sx);
                            if (xWeight <= 0) continue;

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

        private static void ScaleBilinear(byte[] input, int inputWidth, int inputHeight, int inputStride,
            byte[] output, PixelSize size, int outputStride)
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

                    double sumAlpha = 0, sumBlue = 0, sumGreen = 0, sumRed = 0;

                    AddWeightedPixel(input, inputStride, x0, y0, (1 - xWeight) * (1 - yWeight), ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);
                    AddWeightedPixel(input, inputStride, x1, y0, xWeight * (1 - yWeight), ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);
                    AddWeightedPixel(input, inputStride, x0, y1, (1 - xWeight) * yWeight, ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);
                    AddWeightedPixel(input, inputStride, x1, y1, xWeight * yWeight, ref sumBlue, ref sumGreen, ref sumRed, ref sumAlpha);

                    WriteStraightAlphaPixel(output, y * outputStride + x * 4, sumBlue, sumGreen, sumRed, sumAlpha, pixelArea: 1);
                }
            }
        }

        private static void AddWeightedPixel(byte[] input, int inputStride, int x, int y, double weight,
            ref double sumBlue, ref double sumGreen, ref double sumRed, ref double sumAlpha)
        {
            var inputIndex = y * inputStride + x * 4;
            var alpha = input[inputIndex + 3] / 255.0;
            var weightedAlpha = alpha * weight;

            sumAlpha += weightedAlpha;
            sumBlue += input[inputIndex] * weightedAlpha;
            sumGreen += input[inputIndex + 1] * weightedAlpha;
            sumRed += input[inputIndex + 2] * weightedAlpha;
        }

        private static void WriteStraightAlphaPixel(byte[] output, int index,
            double sumBlue, double sumGreen, double sumRed, double sumAlpha, double pixelArea)
        {
            if (sumAlpha <= 0) return;
            output[index] = ClampByte(sumBlue / sumAlpha);
            output[index + 1] = ClampByte(sumGreen / sumAlpha);
            output[index + 2] = ClampByte(sumRed / sumAlpha);
            output[index + 3] = ClampByte(255 * sumAlpha / pixelArea);
        }

        private static byte ClampByte(double value) =>
            (byte)Math.Clamp((int)Math.Round(value), 0, 255);

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
            var x = Math.Clamp(rect.X, 0, Math.Max(0, source.PixelWidth - 1));
            var y = Math.Clamp(rect.Y, 0, Math.Max(0, source.PixelHeight - 1));
            var width = Math.Min(rect.Width, source.PixelWidth - x);
            var height = Math.Min(rect.Height, source.PixelHeight - y);

            var cropped = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            cropped.Freeze();
            return cropped;
        }

        private static BitmapSource PreviewFit(BitmapSource source, PixelSize box)
        {
            var size = ResizeMath.Fit(new PixelSize(source.PixelWidth, source.PixelHeight), box);
            return size.Width == source.PixelWidth && size.Height == source.PixelHeight
                ? source
                : Scale(source, size);
        }

        private static BitmapSource PreviewCover(BitmapSource source, PixelSize box, CropAnchor anchor)
        {
            var plan = ResizeMath.Cover(new PixelSize(source.PixelWidth, source.PixelHeight), box, anchor);
            return Crop(Scale(source, plan.Scaled), plan.Crop);
        }

        private static BitmapSource PreviewCover(BitmapSource source, PixelSize box, double positionX, double positionY)
        {
            var plan = ResizeMath.Cover(new PixelSize(source.PixelWidth, source.PixelHeight), box, positionX, positionY);
            return Crop(Scale(source, plan.Scaled), plan.Crop);
        }

        // ── EXIF orientation ─────────────────────────────────────────────────────

        private static BitmapSource LoadOriented(string fullPath)
        {
            return LoadOrientedWithMetadata(fullPath).Image;
        }

        private static LoadedImage LoadOrientedWithMetadata(string fullPath)
        {
            BitmapFrame frame;
            using (var input = File.OpenRead(fullPath))
            {
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
            }

            var metadata = CloneMetadata(frame.Metadata as BitmapMetadata);
            ResetOrientation(metadata);
            return new LoadedImage(ApplyExifOrientation(frame), metadata, Path.GetExtension(fullPath));
        }

        private static BitmapMetadata? GetMetadataForTarget(
            BitmapMetadata? metadata,
            string sourceExtension,
            ConversionTarget target,
            AppSettings? settings)
        {
            if (metadata is null || (settings?.PreserveMetadata ?? new AppSettings().PreserveMetadata) == false)
            {
                return null;
            }

            if (!IsMetadataCompatible(sourceExtension, target))
            {
                return null;
            }

            return CloneMetadata(metadata);
        }

        private static bool IsMetadataCompatible(string sourceExtension, ConversionTarget target)
        {
            var source = sourceExtension.ToLowerInvariant();
            return target switch
            {
                ConversionTarget.Jpeg => source is ".jpg" or ".jpeg",
                ConversionTarget.Png => source is ".png",
                _ => false,
            };
        }

        private static BitmapMetadata? CloneMetadata(BitmapMetadata? metadata)
        {
            if (metadata is null)
            {
                return null;
            }

            try
            {
                return metadata.Clone();
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                return null;
            }
        }

        private static void ResetOrientation(BitmapMetadata? metadata)
        {
            if (metadata is null)
            {
                return;
            }

            try
            {
                metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)1);
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
            {
            }
        }

        private static BitmapSource ApplyExifOrientation(BitmapFrame frame)
        {
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
            }

            var angle = orientation switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };

            if (angle == 0) return frame;

            var transformed = new TransformedBitmap(frame, new RotateTransform(angle));
            transformed.Freeze();
            return transformed;
        }

        // ── Transparency flattening ───────────────────────────────────────────────

        private static BitmapSource FlattenToColor(BitmapSource source, byte bgR, byte bgG, byte bgB)
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
                    // Bgra32: B=input[i], G=input[i+1], R=input[i+2]
                    output[o]     = BlendChannel(input[i],     alpha, bgB);
                    output[o + 1] = BlendChannel(input[i + 1], alpha, bgG);
                    output[o + 2] = BlendChannel(input[i + 2], alpha, bgR);
                }
            }

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY,
                PixelFormats.Bgr24, null, output, outputStride);
            result.Freeze();
            return result;
        }

        private static BitmapSource FlattenToConfiguredColor(BitmapSource source, AppSettings settings)
        {
            var (r, g, b) = settings.GetBackgroundRgb();
            return FlattenToColor(source, r, g, b);
        }

        private static BitmapSource ApplySepiaTone(BitmapSource source, int intensity, int hue)
        {
            var amount = Math.Clamp(intensity, 0, 100) / 100.0;
            var normalizedHue = NormalizeHue(hue);
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var blue = pixels[i];
                var green = pixels[i + 1];
                var red = pixels[i + 2];

                var sepiaRed = ClampByte(red * 0.393 + green * 0.769 + blue * 0.189);
                var sepiaGreen = ClampByte(red * 0.349 + green * 0.686 + blue * 0.168);
                var sepiaBlue = ClampByte(red * 0.272 + green * 0.534 + blue * 0.131);

                if (normalizedHue != StandardSepiaHue)
                {
                    (sepiaRed, sepiaGreen, sepiaBlue) = ShiftHue(
                        sepiaRed,
                        sepiaGreen,
                        sepiaBlue,
                        (normalizedHue - StandardSepiaHue) / 360.0);
                }

                pixels[i] = Blend(blue, sepiaBlue, amount);
                pixels[i + 1] = Blend(green, sepiaGreen, amount);
                pixels[i + 2] = Blend(red, sepiaRed, amount);
            }

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }

        private static byte Blend(byte source, byte target, double amount) =>
            ClampByte(source + (target - source) * amount);

        private static int NormalizeHue(int hue) =>
            ((hue % 360) + 360) % 360;

        private static (byte R, byte G, byte B) ShiftHue(byte red, byte green, byte blue, double offset)
        {
            var (hue, saturation, lightness) = RgbToHsl(red, green, blue);
            return HslToRgb(NormalizeUnitHue(hue + offset), saturation, lightness);
        }

        private static (double Hue, double Saturation, double Lightness) RgbToHsl(byte red, byte green, byte blue)
        {
            var r = red / 255.0;
            var g = green / 255.0;
            var b = blue / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;
            var lightness = (max + min) / 2.0;

            if (delta <= 0)
            {
                return (0, 0, lightness);
            }

            var saturation = delta / (1 - Math.Abs(2 * lightness - 1));
            var hue = max switch
            {
                _ when max == r => ((g - b) / delta + (g < b ? 6 : 0)) / 6.0,
                _ when max == g => ((b - r) / delta + 2) / 6.0,
                _ => ((r - g) / delta + 4) / 6.0,
            };

            return (NormalizeUnitHue(hue), saturation, lightness);
        }

        private static double NormalizeUnitHue(double hue)
        {
            hue %= 1;
            return hue < 0 ? hue + 1 : hue;
        }

        private static (byte R, byte G, byte B) HslToRgb(double hue, double saturation, double lightness)
        {
            if (saturation <= 0)
            {
                var gray = ClampByte(lightness * 255);
                return (gray, gray, gray);
            }

            var q = lightness < 0.5
                ? lightness * (1 + saturation)
                : lightness + saturation - lightness * saturation;
            var p = 2 * lightness - q;

            return (
                ClampByte(HueToRgb(p, q, hue + 1.0 / 3.0) * 255),
                ClampByte(HueToRgb(p, q, hue) * 255),
                ClampByte(HueToRgb(p, q, hue - 1.0 / 3.0) * 255));
        }

        private static double HueToRgb(double p, double q, double hue)
        {
            if (hue < 0) hue += 1;
            if (hue > 1) hue -= 1;
            if (hue < 1.0 / 6.0) return p + (q - p) * 6 * hue;
            if (hue < 1.0 / 2.0) return q;
            if (hue < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - hue) * 6;
            return p;
        }

        private static byte BlendChannel(byte channel, int alpha, byte background) =>
            (byte)((channel * alpha + background * (255 - alpha)) / 255);

        // ── SkiaSharp bridge ─────────────────────────────────────────────────────

        /// <summary>
        /// Encodes a WPF BitmapSource using Skia. quality is 0-100 (ignored for PNG).
        /// </summary>
        private static byte[] EncodeSkia(BitmapSource source, SKEncodedImageFormat format, int quality)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);

            // WPF Bgra32 = Skia BGRA_8888.
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap(info);
            using var pin = bitmap.PeekPixels();
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, pin.GetPixels(), pixels.Length);

            using var skImage = SKImage.FromBitmap(bitmap);
            using var encoded = skImage.Encode(format, quality);
            if (encoded is null)
            {
                throw new NotSupportedException(
                    $"The current SkiaSharp native library does not support encoding to {format}. " +
                    "AVIF encoding requires a SkiaSharp build with AOM encoder support.");
            }
            return encoded.ToArray();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private sealed record IconImage(int Size, byte[] Data);

        private sealed record LoadedImage(BitmapSource Image, BitmapMetadata? Metadata, string SourceExtension);
    }
}
