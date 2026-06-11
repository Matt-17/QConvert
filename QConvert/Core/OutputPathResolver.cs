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
            var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
            var baseName = Path.GetFileNameWithoutExtension(inputPath);

            var candidate = Path.Combine(directory, baseName + newExtension);
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            for (var i = 1; i <= 999; i++)
            {
                candidate = Path.Combine(directory, $"{baseName}.{i:000}{newExtension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException($"No free output name found for '{inputPath}' (tried {baseName}.001{newExtension} through {baseName}.999{newExtension}).");
        }
    }
}
