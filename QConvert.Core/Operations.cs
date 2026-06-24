namespace QConvert.Core
{
    public abstract record Operation;

    public sealed record ConvertOperation(ConversionTarget Target) : Operation;

    public sealed record FitOperation(PixelSize Box, bool KeepAspect = true) : Operation;

    public sealed record CoverOperation(PixelSize Box, double PositionX = 0.5, double PositionY = 0.5) : Operation;

    public sealed record CropResizeOperation(PixelRect Crop, PixelSize Output) : Operation;

    public sealed record AspectCropOperation(int RatioX, int RatioY, double PositionX = 0.5, double PositionY = 0.5) : Operation;

    public sealed record StripMetadataOperation : Operation;

    public sealed record SepiaOperation(int Intensity, int Hue = ImageConverter.StandardSepiaHue) : Operation;

    public sealed record CompressJpegOperation : Operation;

    public sealed record OptimizePngOperation : Operation;

    public sealed record FaviconBundleOperation : Operation;

    public sealed record AvatarExportOperation(int Size, double PositionX = 0.5, double PositionY = 0.5) : Operation;

    public sealed record PasteClipboardOperation(ConversionTarget Target) : Operation;
}
