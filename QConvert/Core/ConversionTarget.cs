namespace QConvert.Core
{
    public enum ConversionTarget
    {
        Jpeg,
        Png,
    }

    public static class ConversionTargetExtensions
    {
        public static string FileExtension(this ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => ".jpg",
            ConversionTarget.Png => ".png",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public static ConversionTarget? Parse(string? value) => value?.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ConversionTarget.Jpeg,
            "png" => ConversionTarget.Png,
            _ => null,
        };
    }
}
