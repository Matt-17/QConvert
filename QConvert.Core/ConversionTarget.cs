namespace QConvert.Core
{
    public enum ConversionTarget
    {
        Jpeg,
        Png,
        Ico,
        WebP,
        Avif,
    }

    public static class ConversionTargetExtensions
    {
        public static string FileExtension(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => ".jpg",
            ConversionTarget.Png => ".png",
            ConversionTarget.Ico => ".ico",
            ConversionTarget.WebP => ".webp",
            ConversionTarget.Avif => ".avif",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static string CliValue(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => "jpg",
            ConversionTarget.Png => "png",
            ConversionTarget.Ico => "ico",
            ConversionTarget.WebP => "webp",
            ConversionTarget.Avif => "avif",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static string DisplayName(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => "JPG",
            ConversionTarget.Png => "PNG",
            ConversionTarget.Ico => "ICO",
            ConversionTarget.WebP => "WebP",
            ConversionTarget.Avif => "AVIF",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static ConversionTarget? Parse(string? value) => value?.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ConversionTarget.Jpeg,
            "png" => ConversionTarget.Png,
            "ico" => ConversionTarget.Ico,
            "webp" => ConversionTarget.WebP,
            "avif" => ConversionTarget.Avif,
            _ => null,
        };
    }
}
