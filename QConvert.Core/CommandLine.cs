namespace QConvert.Core
{
    public sealed class CommandLine
    {
        public const string OpenOption = "--open";

        public const string Usage =
            "Usage: QConvert.Cli.exe (--to <jpg|png|ico|webp|avif> | --fit <WxH> | --cover <WxH> | --crop <X:Y> | --strip-metadata | --sepia <0-100> | --compress-jpg | --optimize-png | --favicon | --avatar <size> | --paste <jpg|png>) [--quality <1-100>] [--ico-sizes <16,32,48,256>] [--background <#rrggbb>] <file-or-folder> [<file-or-folder> ...]";

        private readonly List<string> _files = new();

        public Operation? Operation { get; private set; }
        public IReadOnlyList<string> Files => _files;
        public int? JpegQuality { get; private set; }
        public IReadOnlyList<int>? IcoSizes { get; private set; }
        public string? Background { get; private set; }
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
                                result.Error = $"Unknown target format '{format}'. Supported: jpg, png, ico, webp, avif.";
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

                    case "--strip-metadata":
                        result.SetOperation(new StripMetadataOperation());
                        break;

                    case "--sepia":
                        if (TryTakeValue(args, ref i, result, out var sepiaText))
                        {
                            if (!int.TryParse(sepiaText, out var sepiaIntensity) || sepiaIntensity is < 0 or > 100)
                            {
                                result.Error = $"Invalid sepia intensity '{sepiaText}'. Expected a number from 0 to 100.";
                            }
                            else
                            {
                                result.SetOperation(new SepiaOperation(sepiaIntensity));
                            }
                        }
                        break;

                    case "--compress-jpg":
                        result.SetOperation(new CompressJpegOperation());
                        break;

                    case "--optimize-png":
                        result.SetOperation(new OptimizePngOperation());
                        break;

                    case "--favicon":
                        result.SetOperation(new FaviconBundleOperation());
                        break;

                    case "--avatar":
                        if (TryTakeValue(args, ref i, result, out var sizeStr))
                        {
                            if (!int.TryParse(sizeStr, out var avatarSize) || avatarSize <= 0)
                            {
                                result.Error = $"Invalid avatar size '{sizeStr}'. Expected a positive integer.";
                            }
                            else
                            {
                                result.SetOperation(new AvatarExportOperation(avatarSize));
                            }
                        }
                        break;

                    case "--paste":
                        if (TryTakeValue(args, ref i, result, out var pasteFormat))
                        {
                            var target = ConversionTargetExtensions.Parse(pasteFormat);
                            if (target is not (ConversionTarget.Jpeg or ConversionTarget.Png))
                            {
                                result.Error = $"Invalid paste format '{pasteFormat}'. Supported: jpg, png.";
                            }
                            else
                            {
                                result.SetOperation(new PasteClipboardOperation(target.Value));
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

                    case "--ico-sizes":
                        if (TryTakeValue(args, ref i, result, out var sizesText))
                        {
                            if (TryParseIcoSizes(sizesText, out var sizes))
                            {
                                result.IcoSizes = sizes;
                            }
                            else
                            {
                                result.Error = $"Invalid icon sizes '{sizesText}'. Expected comma-separated sizes from 1 to 256.";
                            }
                        }
                        break;

                    case "--background":
                        if (TryTakeValue(args, ref i, result, out var bgText))
                        {
                            if (TryParseHexColor(bgText, out _))
                            {
                                result.Background = bgText;
                            }
                            else
                            {
                                result.Error = $"Invalid background color '{bgText}'. Expected e.g. #ffffff.";
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

        public static bool TryParseIcoSizes(string? text, out IReadOnlyList<int> sizes)
        {
            sizes = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parsed = new List<int>();
            foreach (var part in text.Split(',', ';'))
            {
                if (!int.TryParse(part.Trim(), out var size) || size is <= 0 or > 256)
                {
                    return false;
                }

                parsed.Add(size);
            }

            sizes = parsed.Distinct().OrderBy(size => size).ToArray();
            return sizes.Count > 0;
        }

        public static bool TryParseHexColor(string? text, out (byte R, byte G, byte B) color)
        {
            color = default;
            if (text is null) return false;
            var hex = text.TrimStart('#');
            if (hex.Length != 6) return false;
            if (!byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r)) return false;
            if (!byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)) return false;
            if (!byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;
            color = (r, g, b);
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

            result.Error = $"Missing value after '{args[i]}'.";
            value = "";
            return false;
        }
    }
}

