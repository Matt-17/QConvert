namespace QConvert.Core
{
    public readonly record struct PixelSize(int Width, int Height)
    {
        public override string ToString() => $"{Width}x{Height}";
    }

    public readonly record struct PixelRect(int X, int Y, int Width, int Height);
}
