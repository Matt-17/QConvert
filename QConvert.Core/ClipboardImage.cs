using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QConvert.Core
{
    /// <summary>
    /// Robust clipboard image access. WPF's <see cref="Clipboard.GetImage"/> returns an
    /// InteropBitmap over the raw clipboard DIB and frequently drops the alpha channel,
    /// producing an all-transparent ("empty") image for pasted PNGs. We therefore prefer
    /// the real "PNG" clipboard format and only fall back to GetImage as a last resort.
    /// </summary>
    public static class ClipboardImage
    {
        private static readonly string[] PngFormats = { "PNG", "image/png" };

        /// <summary>True when the clipboard holds an image we can read.</summary>
        public static bool IsAvailable()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    return true;
                }

                foreach (var format in PngFormats)
                {
                    if (Clipboard.ContainsData(format))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Clipboard can be transiently locked by another process.
            }

            return false;
        }

        /// <summary>Reads the clipboard image, or null when none is available/readable.</summary>
        public static BitmapSource? Read()
        {
            // 1. Preferred: apps (browsers, screenshot tools) place the original PNG bytes
            //    on the clipboard. Decode them directly so transparency is preserved.
            foreach (var format in PngFormats)
            {
                try
                {
                    if (Clipboard.ContainsData(format) &&
                        Clipboard.GetData(format) is MemoryStream stream &&
                        stream.Length > 0)
                    {
                        stream.Position = 0;
                        var decoder = BitmapDecoder.Create(
                            stream,
                            BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0)
                        {
                            var frame = decoder.Frames[0];
                            if (frame.CanFreeze)
                            {
                                frame.Freeze();
                            }

                            return frame;
                        }
                    }
                }
                catch
                {
                    // Ignore and try the next strategy.
                }
            }

            // 2. Fall back to the standard (lossy) accessor for sources that only provide
            //    a plain bitmap/DIB without transparency.
            try
            {
                var image = Clipboard.GetImage();
                if (image is not null && image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
