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
        /// crop to exactly the box size using the specified anchor.
        /// </summary>
        public static CoverPlan Cover(PixelSize source, PixelSize box, CropAnchor anchor = CropAnchor.Center)
        {
            var scale = Math.Max((double)box.Width / source.Width, (double)box.Height / source.Height);
            var scaled = new PixelSize(
                Math.Max(box.Width, (int)Math.Round(source.Width * scale)),
                Math.Max(box.Height, (int)Math.Round(source.Height * scale)));

            var maxX = scaled.Width - box.Width;
            var maxY = scaled.Height - box.Height;

            var x = anchor switch
            {
                CropAnchor.Left or CropAnchor.TopLeft or CropAnchor.BottomLeft => 0,
                CropAnchor.Right or CropAnchor.TopRight or CropAnchor.BottomRight => maxX,
                _ => maxX / 2,
            };

            var y = anchor switch
            {
                CropAnchor.Top or CropAnchor.TopLeft or CropAnchor.TopRight => 0,
                CropAnchor.Bottom or CropAnchor.BottomLeft or CropAnchor.BottomRight => maxY,
                _ => maxY / 2,
            };

            return new CoverPlan(scaled, new PixelRect(x, y, box.Width, box.Height));
        }

        /// <summary>
        /// Scale so the source covers the box, then crop at a normalized position
        /// where 0 is left/top, 0.5 is center, and 1 is right/bottom.
        /// </summary>
        public static CoverPlan Cover(PixelSize source, PixelSize box, double positionX, double positionY)
        {
            var scale = Math.Max((double)box.Width / source.Width, (double)box.Height / source.Height);
            var scaled = new PixelSize(
                Math.Max(box.Width, (int)Math.Round(source.Width * scale)),
                Math.Max(box.Height, (int)Math.Round(source.Height * scale)));

            var maxX = scaled.Width - box.Width;
            var maxY = scaled.Height - box.Height;
            var x = (int)(maxX * Math.Clamp(positionX, 0, 1));
            var y = (int)(maxY * Math.Clamp(positionY, 0, 1));

            return new CoverPlan(scaled, new PixelRect(x, y, box.Width, box.Height));
        }

        /// <summary>
        /// Centered crop to the given aspect ratio without any resizing. A source
        /// already at the target ratio is returned in full.
        /// </summary>
        public static PixelRect AspectCrop(PixelSize source, int ratioX, int ratioY) =>
            AspectCrop(source, ratioX, ratioY, 0.5, 0.5);

        /// <summary>
        /// Crops to the given aspect ratio at a normalized position. Only the axis
        /// with excess image area uses the position value.
        /// </summary>
        public static PixelRect AspectCrop(PixelSize source, int ratioX, int ratioY, double positionX, double positionY)
        {
            long actual = (long)source.Width * ratioY;
            long target = (long)source.Height * ratioX;

            if (actual > target)
            {
                // Too wide: trim left and right.
                var width = Math.Max(1, (int)Math.Round((double)source.Height * ratioX / ratioY));
                var x = (int)((source.Width - width) * Math.Clamp(positionX, 0, 1));
                return new PixelRect(x, 0, width, source.Height);
            }

            if (actual < target)
            {
                // Too tall: trim top and bottom.
                var height = Math.Max(1, (int)Math.Round((double)source.Width * ratioY / ratioX));
                var y = (int)((source.Height - height) * Math.Clamp(positionY, 0, 1));
                return new PixelRect(0, y, source.Width, height);
            }

            return new PixelRect(0, 0, source.Width, source.Height);
        }
    }
}
