using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using QConvert.Core;

namespace QConvert.Cli;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        try
        {
            return RunConversion(args);
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static int RunConversion(string[] args)
    {
        var command = CommandLine.Parse(args);
        if (command.Error is not null)
        {
            ShowError(command.Error);
            return 2;
        }

        var settings = AppSettings.Load();
        var jpegQuality = command.JpegQuality ?? settings.JpegQuality;

        if (command.IcoSizes is not null)
        {
            settings.IcoSizes = command.IcoSizes.ToList();
        }

        if (command.Background is not null)
        {
            settings.TransparencyBackground = command.Background;
        }

        var errors = new List<string>();
        var messages = new List<string>();

        foreach (var file in command.Files)
        {
            try
            {
                Execute(command.Operation!, file, jpegQuality, settings);
            }
            catch (UserMessageException ex)
            {
                messages.Add(ex.Message);
            }
            catch (NotSupportedException)
            {
                errors.Add($"{file}: format not supported by Windows Imaging. For WebP files, install 'WebP Image Extensions' from the Microsoft Store.");
            }
            catch (Exception ex)
            {
                errors.Add($"{file}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine + Environment.NewLine, errors));
            return 1;
        }

        if (messages.Count > 0)
        {
            ShowNotification(string.Join(Environment.NewLine, messages.Distinct()));
        }

        return 0;
    }

    private static void Execute(Operation operation, string file, int jpegQuality, AppSettings settings)
    {
        switch (operation)
        {
            case ConvertOperation convert:
                ImageConverter.Convert(file, convert.Target, jpegQuality, settings);
                break;
            case FitOperation fit:
                ImageConverter.ResizeToFit(file, fit.Box, jpegQuality, settings);
                break;
            case CoverOperation cover:
                ImageConverter.CropToSize(file, cover.Box, jpegQuality, settings);
                break;
            case AspectCropOperation crop:
                ImageConverter.CropToAspect(file, crop.RatioX, crop.RatioY, jpegQuality, settings);
                break;
            case StripMetadataOperation:
                ImageConverter.StripMetadata(file, settings);
                break;
            case SepiaOperation sepia:
                ImageConverter.ApplySepia(file, sepia.Intensity, settings);
                break;
            case CompressJpegOperation:
                ImageConverter.CompressJpeg(file, settings);
                break;
            case OptimizePngOperation:
                ImageConverter.OptimizePng(file, settings);
                break;
            case FaviconBundleOperation:
                ImageConverter.CreateFaviconBundle(file, settings);
                break;
            case AvatarExportOperation avatar:
                ImageConverter.MakeAvatar(file, avatar.Size, settings);
                break;
            case PasteClipboardOperation paste:
                PasteClipboardImage(file, paste.Target, jpegQuality, settings);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static void PasteClipboardImage(string folderPath, ConversionTarget target, int jpegQuality, AppSettings settings)
    {
        var fullFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {fullFolderPath}");
        }

        if (!ClipboardImage.IsAvailable())
        {
            throw new UserMessageException("No image data in clipboard.");
        }

        var image = ClipboardImage.Read();
        if (image is null)
        {
            throw new UserMessageException("Clipboard image could not be read.");
        }

        var outputPath = ImageConverter.GetClipboardOutputPath(fullFolderPath, target);
        ImageConverter.SaveBitmap(image, outputPath, target, jpegQuality, settings, overwrite: false);

        // Surface the new file in Explorer with its name in inline-rename mode so the
        // user can immediately type a proper name instead of the auto timestamp.
        ExplorerRename.SelectForRename(outputPath);
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "QConvert", MessageBoxButton.OK, MessageBoxImage.Error);

    private static void ShowNotification(string message)
    {
        var text = new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
        };

        var window = new Window
        {
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 9, 14, 10),
                Child = text,
            },
            ResizeMode = ResizeMode.NoResize,
            ShowActivated = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            WindowStyle = WindowStyle.None,
        };

        window.Loaded += (_, _) =>
        {
            var point = GetCursorPoint();
            window.Left = point.X + 14;
            window.Top = point.Y + 14;

            var workArea = SystemParameters.WorkArea;
            if (window.Left + window.ActualWidth > workArea.Right)
            {
                window.Left = Math.Max(workArea.Left, point.X - window.ActualWidth - 14);
            }

            if (window.Top + window.ActualHeight > workArea.Bottom)
            {
                window.Top = Math.Max(workArea.Top, point.Y - window.ActualHeight - 14);
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        };

        window.ShowDialog();
    }

    private static Point GetCursorPoint()
    {
        return GetCursorPos(out var point)
            ? new Point(point.X, point.Y)
            : new Point(SystemParameters.PrimaryScreenWidth / 2, SystemParameters.PrimaryScreenHeight / 2);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    private sealed class UserMessageException(string message) : Exception(message);
}
