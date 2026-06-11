namespace QConvert.Core
{
    public sealed class CommandLine
    {
        public const string Usage =
            "Usage: QConvert.exe (--to <jpg|png|ico> | --fit <WxH> | --cover <WxH> | --crop <X:Y>) [--quality <1-100>] <file> [<file> ...]";

        private readonly List<string> _files = new();

        public Operation? Operation { get; private set; }
        public IReadOnlyList<string> Files => _files;
        public int? JpegQuality { get; private set; }
        public string? Error { get; private set; }

        public static CommandLine Parse(string[] args)
        {
            var result = new CommandLine();

            for (var i = 0; i < args.Length && result.Error is null; i++)
            {
                switch (args[i])
                {
                    case "--to":
                        if (TryTakeValue(args, ref i, result, out var format))
                        {
                            var target = ConversionTargetExtensions.Parse(format);
                            if (target is null)
                            {
                                result.Error = $"Unknown target format '{format}'. Supported: jpg, png, ico.";
                            }
                            else
                            {
                                result.SetOperation(new ConvertOperation(target.Value));
                            }
                        }
                        break;

                    case "--fit":
                    case "--cover":
                        if (TryTakeValue(args, ref i, result, out var sizeText))
                        {
                            if (!TryParseSize(sizeText, out var box))
                            {
                                result.Error = $"Invalid size '{sizeText}'. Expected e.g. 1920x1080.";
                            }
                            else
                            {
                                result.SetOperation(args[i - 1] == "--fit"
                                    ? new FitOperation(box)
                                    : new CoverOperation(box));
                            }
                        }
                        break;

                    case "--crop":
                        if (TryTakeValue(args, ref i, result, out var aspectText))
                        {
                            if (!TryParseAspect(aspectText, out var x, out var y))
                            {
                                result.Error = $"Invalid aspect ratio '{aspectText}'. Expected e.g. 4:3.";
                            }
                            else
                            {
                                result.SetOperation(new AspectCropOperation(x, y));
                            }
                        }
                        break;

                    case "--quality":
                        if (TryTakeValue(args, ref i, result, out var qualityText))
                        {
                            if (int.TryParse(qualityText, out var quality)
                                && quality is >= AppSettings.MinJpegQuality and <= AppSettings.MaxJpegQuality)
                            {
                                result.JpegQuality = quality;
                            }
                            else
                            {
                                result.Error = $"Invalid quality '{qualityText}'. Expected a number from 1 to 100.";
                            }
                        }
                        break;

                    default:
                        if (args[i].StartsWith("--", StringComparison.Ordinal))
                        {
                            result.Error = $"Unknown option '{args[i]}'.\n\n{Usage}";
                        }
                        else
                        {
                            result._files.Add(args[i]);
                        }
                        break;
                }
            }

            if (result.Error is null && (result.Operation is null || result._files.Count == 0))
            {
                result.Error = Usage;
            }

            return result;
        }

        public static bool TryParseSize(string? text, out PixelSize size)
        {
            size = default;
            var parts = text?.Split('x', 'X', '×');
            if (parts is not { Length: 2 }
                || !int.TryParse(parts[0].Trim(), out var width) || width <= 0
                || !int.TryParse(parts[1].Trim(), out var height) || height <= 0)
            {
                return false;
            }

            size = new PixelSize(width, height);
            return true;
        }

        public static bool TryParseAspect(string? text, out int x, out int y)
        {
            x = y = 0;
            var parts = text?.Split(':');
            if (parts is not { Length: 2 }
                || !int.TryParse(parts[0].Trim(), out x) || x <= 0
                || !int.TryParse(parts[1].Trim(), out y) || y <= 0)
            {
                return false;
            }

            return true;
        }

        private void SetOperation(Operation operation)
        {
            if (Operation is not null)
            {
                Error = "Only one operation may be given per call.";
                return;
            }

            Operation = operation;
        }

        private static bool TryTakeValue(string[] args, ref int i, CommandLine result, out string value)
        {
            if (i + 1 < args.Length)
            {
                value = args[++i];
                return true;
            }

            result.Error = $"Option '{args[i]}' requires a value.\n\n{Usage}";
            value = string.Empty;
            return false;
        }
    }
}
