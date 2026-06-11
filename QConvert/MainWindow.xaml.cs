using System.Windows;

using QConvert.Core;

namespace QConvert
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private bool _initialized;

        public MainWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            QualitySlider.Value = _settings.JpegQuality;
            _initialized = true;
            UpdateStatus();
        }

        private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized)
            {
                return;
            }

            _settings.JpegQuality = (int)e.NewValue;
            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not save settings: {ex.Message}";
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null)
            {
                StatusText.Text = "Could not determine the application path.";
                return;
            }

            try
            {
                ShellIntegration.Register(exePath);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Registration failed: {ex.Message}";
            }
        }

        private void Unregister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShellIntegration.Unregister();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Removal failed: {ex.Message}";
            }
        }

        private void UpdateStatus()
        {
            StatusText.Text = ShellIntegration.IsRegistered()
                ? "Context menu entries are installed for the current user."
                : "Context menu entries are not installed.";
        }
    }
}
