using System.IO;
using System.Windows;

using QConvert.Core;

namespace QConvert
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0 && e.Args[0] == CommandLine.OpenOption)
            {
                if (e.Args.Length < 2)
                {
                    ShowError($"Missing file after '{CommandLine.OpenOption}'.");
                    Shutdown(2);
                    return;
                }

                if (!File.Exists(e.Args[1]))
                {
                    ShowError($"Image not found: {e.Args[1]}");
                    Shutdown(2);
                    return;
                }

                try
                {
                    MainWindow = new ImageEditorWindow(e.Args[1]);
                    MainWindow.Show();
                }
                catch (Exception ex)
                {
                    ShowError($"Could not open image: {ex.Message}");
                    Shutdown(1);
                }

                return;
            }

            if (e.Args.Length > 0)
            {
                ShowError("QConvert.exe is the graphical app. Use QConvert.Cli.exe for command-line conversion.");
                Shutdown(2);
                return;
            }

            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        private static void ShowError(string message) =>
            MessageBox.Show(message, "QConvert", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
