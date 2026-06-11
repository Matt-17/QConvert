using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using QConvert.Core;

namespace QConvert
{
    public partial class MainWindow : Window
    {
        private static readonly (string Name, SizeSetting Size)[] SizePresets =
        {
            ("HD", new SizeSetting(1280, 720)),
            ("Full HD", new SizeSetting(1920, 1080)),
            ("WQHD", new SizeSetting(2560, 1440)),
            ("4K UHD", new SizeSetting(3840, 2160)),
            ("Mobile", new SizeSetting(1080, 1920)),
        };

        private static readonly AspectRatioSetting[] AspectPresets =
        {
            new(4, 3),
            new(3, 2),
            new(16, 9),
            new(21, 9),
            new(1, 1),
            new(9, 16),
        };

        private readonly AppSettings _settings;

        public MainWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            QualitySlider.Value = _settings.JpegQuality;
            PopulateSizePanel(FitPanel, _settings.FitSizes);
            PopulateSizePanel(CoverPanel, _settings.CoverSizes);
            PopulateAspectPanel(_settings.AspectRatios);
            UpdateStatus();
        }

        private void PopulateSizePanel(WrapPanel panel, List<SizeSetting> selected)
        {
            foreach (var (name, size) in SizePresets)
            {
                AddOption(panel, $"{name} ({size.Width}×{size.Height})", size, selected.Contains(size));
            }

            foreach (var size in selected.Where(s => !SizePresets.Any(p => p.Size == s)))
            {
                AddOption(panel, $"{size.Width}×{size.Height}", size, isChecked: true);
            }
        }

        private void PopulateAspectPanel(List<AspectRatioSetting> selected)
        {
            foreach (var ratio in AspectPresets)
            {
                AddOption(AspectPanel, $"{ratio.X}:{ratio.Y}", ratio, selected.Contains(ratio));
            }

            foreach (var ratio in selected.Where(r => !AspectPresets.Contains(r)))
            {
                AddOption(AspectPanel, $"{ratio.X}:{ratio.Y}", ratio, isChecked: true);
            }
        }

        private void AddOption(WrapPanel panel, string label, object value, bool isChecked)
        {
            panel.Children.Add(new CheckBox
            {
                Content = label,
                Tag = value,
                IsChecked = isChecked,
                Style = (Style)FindResource("OptionCheckBox"),
            });
        }

        private static List<T> CollectChecked<T>(WrapPanel panel) =>
            panel.Children.OfType<CheckBox>()
                .Where(c => c.IsChecked == true)
                .Select(c => (T)c.Tag!)
                .ToList();

        private void AddCustomSize(TextBox input, WrapPanel panel)
        {
            if (!CommandLine.TryParseSize(input.Text, out var size))
            {
                StatusText.Text = "Enter a size like 1920x1080 (both values greater than 0).";
                return;
            }

            var setting = new SizeSetting(size.Width, size.Height);
            if (TryCheckExisting(panel, setting))
            {
                input.Clear();
                return;
            }

            AddOption(panel, $"{setting.Width}×{setting.Height}", setting, isChecked: true);
            input.Clear();
            StatusText.Text = "Added. Press Save to apply.";
        }

        private void AddCustomAspect()
        {
            if (!CommandLine.TryParseAspect(AspectCustomBox.Text, out var x, out var y))
            {
                StatusText.Text = "Enter an aspect ratio like 4:3 (both values greater than 0).";
                return;
            }

            var setting = new AspectRatioSetting(x, y);
            if (TryCheckExisting(AspectPanel, setting))
            {
                AspectCustomBox.Clear();
                return;
            }

            AddOption(AspectPanel, $"{x}:{y}", setting, isChecked: true);
            AspectCustomBox.Clear();
            StatusText.Text = "Added. Press Save to apply.";
        }

        private static bool TryCheckExisting(WrapPanel panel, object value)
        {
            var existing = panel.Children.OfType<CheckBox>().FirstOrDefault(c => Equals(c.Tag, value));
            if (existing is null)
            {
                return false;
            }

            existing.IsChecked = true;
            return true;
        }

        private void AddFitCustom_Click(object sender, RoutedEventArgs e) => AddCustomSize(FitCustomBox, FitPanel);

        private void AddCoverCustom_Click(object sender, RoutedEventArgs e) => AddCustomSize(CoverCustomBox, CoverPanel);

        private void AddAspectCustom_Click(object sender, RoutedEventArgs e) => AddCustomAspect();

        private void FitCustomBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddCustomSize(FitCustomBox, FitPanel);
        }

        private void CoverCustomBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddCustomSize(CoverCustomBox, CoverPanel);
        }

        private void AspectCustomBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddCustomAspect();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _settings.JpegQuality = (int)QualitySlider.Value;
            _settings.FitSizes = CollectChecked<SizeSetting>(FitPanel);
            _settings.CoverSizes = CollectChecked<SizeSetting>(CoverPanel);
            _settings.AspectRatios = CollectChecked<AspectRatioSetting>(AspectPanel);

            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not save settings: {ex.Message}";
                return;
            }

            var exePath = Environment.ProcessPath;
            if (exePath is null)
            {
                StatusText.Text = "Settings saved, but the application path could not be determined for the context menu.";
                return;
            }

            try
            {
                ShellIntegration.Register(exePath, _settings);
                StatusText.Text = "Settings saved and context menu updated.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Settings saved, but updating the context menu failed: {ex.Message}";
            }
        }

        private void Unregister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShellIntegration.Unregister();
                StatusText.Text = "Context menu removed. Press Save to add it again.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Removal failed: {ex.Message}";
            }
        }

        private void UpdateStatus()
        {
            StatusText.Text = ShellIntegration.IsRegistered()
                ? "Context menu is installed for the current user."
                : "Context menu is not installed — press Save to set it up.";
        }
    }
}
