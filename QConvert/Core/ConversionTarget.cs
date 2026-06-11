namespace QConvert.Core
{
    public enum ConversionTarget
    {
        Jpeg,
        Png,
        Ico,
    }

    public static class ConversionTargetExtensions
    {
        public static string FileExtension(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => ".jpg",
            ConversionTarget.Png => ".png",
            ConversionTarget.Ico => ".ico",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static string CliValue(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => "jpg",
            ConversionTarget.Png => "png",
            ConversionTarget.Ico => "ico",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static string DisplayName(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => "JPG",
            ConversionTarget.Png => "PNG",
            ConversionTarget.Ico => "ICO",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static ConversionTarget? Parse(string? value) => value?.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ConversionTarget.Jpeg,
            "png" => ConversionTarget.Png,
            "ico" => ConversionTarget.Ico,
            _ => null,
        };
    }
}
