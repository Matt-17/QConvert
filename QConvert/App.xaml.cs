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
            ConversionTarget? target = null;
            var files = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--to" && i + 1 < args.Length)
                {
                    target = ConversionTargetExtensions.Parse(args[++i]);
                    if (target is null)
                    {
                        ShowError($"Unknown target format '{args[i]}'. Supported: jpg, png.");
                        return 2;
                    }
                }
                else
                {
                    files.Add(args[i]);
                }
            }

            if (target is null || files.Count == 0)
            {
                ShowError("Usage: QConvert.exe --to <jpg|png> <file> [<file> ...]");
                return 2;
            }

            var settings = AppSettings.Load();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    Core.ImageConverter.Convert(file, target.Value, settings.JpegQuality);
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

        private static void ShowError(string message) =>
            MessageBox.Show(message, "QConvert", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
