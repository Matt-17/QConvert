using System.IO;

namespace QConvert.Core
{
    public static class OutputPathResolver
    {
        /// <summary>
        /// Returns the input path with its extension replaced. If that file already
        /// exists, a numeric suffix is inserted before the extension
        /// (photo.jpg → photo.001.jpg, photo.002.jpg, ...).
        /// </summary>
        public static string GetUniquePath(string inputPath, string newExtension)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var directory = Path.GetDirectoryName(fullPath)!;
            var baseName = Path.GetFileNameWithoutExtension(fullPath);
            return FindFreePath(directory, baseName + newExtension);
        }

        /// <summary>
        /// Returns the output path honouring subfolder and output-name-pattern settings.
        /// When <paramref name="newExtension"/> is a compound suffix like ".clean.jpg" the
        /// pattern is not applied (it is only applied to simple extensions like ".jpg").
        /// </summary>
        public static string GetUniquePath(string inputPath, string newExtension, AppSettings? settings, int pixelWidth = 0, int pixelHeight = 0)
        {
            var fullPath = Path.GetFullPath(inputPath);
            var directory = Path.GetDirectoryName(fullPath)!;
            var baseName = Path.GetFileNameWithoutExtension(fullPath);

            // Apply subfolder if configured.
            if (settings is { UseSubfolder: true })
            {
                directory = Path.Combine(directory, settings.SubfolderName);
                Directory.CreateDirectory(directory);
            }

            // Apply pattern only to simple extensions (e.g. ".jpg", not ".clean.jpg").
            string outputFileName;
            if (settings is not null
                && !string.IsNullOrEmpty(settings.OutputNamePattern)
                && IsSimpleExtension(newExtension))
            {
                outputFileName = ApplyPattern(settings.OutputNamePattern, baseName, newExtension, pixelWidth, pixelHeight);
            }
            else
            {
                outputFileName = baseName + newExtension;
            }

            return FindFreePath(directory, outputFileName);
        }

        private static bool IsSimpleExtension(string ext) =>
            ext.StartsWith('.') && !ext.TrimStart('.').Contains('.');

        public static string ApplyPattern(string pattern, string name, string newExtension, int width, int height)
        {
            var ext = newExtension.TrimStart('.');
            var result = pattern
                .Replace("{name}", name, StringComparison.OrdinalIgnoreCase)
                .Replace("{ext}", ext, StringComparison.OrdinalIgnoreCase)
                .Replace("{target}", ext, StringComparison.OrdinalIgnoreCase)
                .Replace("{width}", width > 0 ? width.ToString() : "", StringComparison.OrdinalIgnoreCase)
                .Replace("{height}", height > 0 ? height.ToString() : "", StringComparison.OrdinalIgnoreCase)
                .Replace("{operation}", "converted", StringComparison.OrdinalIgnoreCase);

            // Strip characters that are invalid in file names.
            var invalid = Path.GetInvalidFileNameChars();
            result = new string(result.Where(c => !invalid.Contains(c)).ToArray()).Trim();

            if (string.IsNullOrEmpty(result))
                return name + newExtension;

            // Ensure the result has an extension.
            if (!result.Contains('.'))
                result += newExtension;

            return result;
        }

        private static string FindFreePath(string directory, string fileName)
        {
            var candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);

            for (var i = 1; i <= 999; i++)
            {
                candidate = Path.Combine(directory, $"{nameWithoutExt}.{i:000}{ext}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException($"No free output name found for '{Path.Combine(directory, fileName)}' (tried up to suffix .999).");
        }
    }
}
