using System.Windows;

using QConvert.Core;

namespace QConvert
{
    /// <summary>
    /// With file arguments the app converts and exits without showing a window
    /// (context-menu mode); without arguments it opens the settings window.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                Shutdown(RunConversion(e.Args));
                return;
            }

            MainWindow = new MainWindow();
            MainWindow.Show();
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
            var errors = new List<string>();

            foreach (var file in command.Files)
            {
                try
                {
                    Execute(command.Operation!, file, jpegQuality);
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

            return 0;
        }

        private static void Execute(Operation operation, string file, int jpegQuality)
        {
            switch (operation)
            {
                case ConvertOperation convert:
                    Core.ImageConverter.Convert(file, convert.Target, jpegQuality);
                    break;
                case FitOperation fit:
                    Core.ImageConverter.ResizeToFit(file, fit.Box, jpegQuality);
                    break;
                case CoverOperation cover:
                    Core.ImageConverter.CropToSize(file, cover.Box, jpegQuality);
                    break;
                case AspectCropOperation crop:
                    Core.ImageConverter.CropToAspect(file, crop.RatioX, crop.RatioY, jpegQuality);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private static void ShowError(string message) =>
            MessageBox.Show(message, "QConvert", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
