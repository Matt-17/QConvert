namespace QConvert.Core
{
    public static class ResizeMath
    {
        public readonly record struct CoverPlan(PixelSize Scaled, PixelRect Crop);

        /// <summary>
        /// Largest size with the source aspect ratio that fits inside the box.
        /// Never upscales: a source smaller than the box is returned unchanged.
        /// </summary>
        public static PixelSize Fit(PixelSize source, PixelSize box)
        {
            if (source.Width <= box.Width && source.Height <= box.Height)
            {
                return source;
            }

            var scale = Math.Min((double)box.Width / source.Width, (double)box.Height / source.Height);
            return new PixelSize(
                Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale)));
        }

        /// <summary>
        /// Scale (up or down) so the source covers the box completely, then
        /// center-crop to exactly the box size.
        /// </summary>
        public static CoverPlan Cover(PixelSize source, PixelSize box)
        {
            var scale = Math.Max((double)box.Width / source.Width, (double)box.Height / source.Height);
            var scaled = new PixelSize(
                Math.Max(box.Width, (int)Math.Round(source.Width * scale)),
                Math.Max(box.Height, (int)Math.Round(source.Height * scale)));

            var crop = new PixelRect(
                (scaled.Width - box.Width) / 2,
                (scaled.Height - box.Height) / 2,
                box.Width,
                box.Height);

            return new CoverPlan(scaled, crop);
        }

        /// <summary>
        /// Centered crop to the given aspect ratio without any resizing. A source
        /// already at the target ratio is returned in full.
        /// </summary>
        public static PixelRect AspectCrop(PixelSize source, int ratioX, int ratioY)
        {
            long actual = (long)source.Width * ratioY;
            long target = (long)source.Height * ratioX;

            if (actual > target)
            {
                // Too wide: trim left and right.
                var width = Math.Max(1, (int)Math.Round((double)source.Height * ratioX / ratioY));
                return new PixelRect((source.Width - width) / 2, 0, width, source.Height);
            }

            if (actual < target)
            {
                // Too tall: trim top and bottom.
                var height = Math.Max(1, (int)Math.Round((double)source.Width * ratioY / ratioX));
                return new PixelRect(0, (source.Height - height) / 2, source.Width, height);
            }

            return new PixelRect(0, 0, source.Width, source.Height);
        }
    }
}
