namespace QConvert.Core
{
    public abstract record Operation;

    public sealed record ConvertOperation(ConversionTarget Target) : Operation;

    public sealed record FitOperation(PixelSize Box) : Operation;

    public sealed record CoverOperation(PixelSize Box) : Operation;

    public sealed record AspectCropOperation(int RatioX, int RatioY) : Operation;
}
