using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

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

        private static readonly int[] DefaultAvatarSizes = { 64, 128, 256, 512 };
        private static readonly int[] DefaultSepiaIntensities = { 35, 65, 100 };

        private readonly AppSettings _settings;
        private readonly bool _initialized;
        private bool _contextMenuEnabled;

        public MainWindow() : this(null)
        {
        }

        public MainWindow(string? selectedImagePath)
        {
            SelectedImagePath = selectedImagePath;
            InitializeComponent();
            _settings = AppSettings.Load();
            WindowPlacement.Restore(this, _settings.SettingsWindowPlacement);

            // Quality sliders.
            QualitySlider.Value = _settings.JpegQuality;
            WebPQualitySlider.Value = _settings.WebPQuality;
            AvifQualitySlider.Value = _settings.AvifQuality;

            // Background color.
            BackgroundBox.Text = _settings.TransparencyBackground;
            PreserveMetadataBox.IsChecked = _settings.PreserveMetadata;

            // ICO sizes (content of .ico files, not menu entries).
            PopulateIcoPanel();

            // Crop anchor.
            foreach (CropAnchor anchor in Enum.GetValues<CropAnchor>())
            {
                CropAnchorBox.Items.Add(anchor);
            }
            CropAnchorBox.SelectedItem = _settings.CropAnchor;

            // Subfolder.
            UseSubfolderBox.IsChecked = _settings.UseSubfolder;
            SubfolderNameBox.Text = _settings.SubfolderName;

            // Output pattern.
            OutputPatternBox.Text = _settings.OutputNamePattern;

            // Per-entry menu toggles.
            PopulateAvatarPanel();
            PopulateSepiaPanel();
            PopulateSizePanel(FitPanel, _settings.FitSizes);
            PopulateSizePanel(CoverPanel, _settings.CoverSizes);
            PopulateAspectPanel(_settings.AspectRatios);

            ConvertJpgToggle.IsChecked = _settings.EnableConvertToJpg;
            ConvertPngToggle.IsChecked = _settings.EnableConvertToPng;
            ConvertWebPToggle.IsChecked = _settings.EnableConvertToWebP;
            ConvertAvifToggle.IsChecked = _settings.EnableConvertToAvif;
            ConvertIcoToggle.IsChecked = _settings.EnableConvertToIco;
            CompressToggle.IsChecked = _settings.EnableCompressJpeg;
            OptimizePngToggle.IsChecked = _settings.EnableOptimizePng;
            RemoveMetadataToggle.IsChecked = _settings.EnableRemoveMetadata;
            FaviconToggle.IsChecked = _settings.EnableFavicon;
            PastePngToggle.IsChecked = _settings.EnablePastePng;
            PasteJpgToggle.IsChecked = _settings.EnablePasteJpg;
            _contextMenuEnabled = _settings.ContextMenuEnabled;
            ContextMenuPreview.IsContextMenuEnabled = _contextMenuEnabled;
            SendToToggle.IsChecked = SendToIntegration.IsInstalled();

            foreach (var toggle in new[]
            {
                ConvertJpgToggle, ConvertPngToggle, ConvertWebPToggle, ConvertAvifToggle, ConvertIcoToggle,
                CompressToggle, OptimizePngToggle, RemoveMetadataToggle, FaviconToggle,
                PastePngToggle, PasteJpgToggle,
            })
            {
                WireContextMenuApply(toggle);
            }

            SendToToggle.Checked += (_, _) => ApplySendToChange();
            SendToToggle.Unchecked += (_, _) => ApplySendToChange();
            WireSettingsPersistence();
            ContextMenuPreview.PreviewExtensionChanged += (_, _) => RefreshPreview();
            ContextMenuPreview.ContextMenuEnabledChanged += ContextMenuPreview_ContextMenuEnabledChanged;

            UpdateSendToStatus();
            UpdateStatus();

            _initialized = true;
            if (!_contextMenuEnabled && ShellIntegration.IsRegistered())
            {
                ApplyContextMenuChange("Context menu is off.");
            }
            else
            {
                RefreshPreview();
                RefreshDiagnostics();
            }
        }

        public string? SelectedImagePath { get; }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_initialized)
            {
                CollectInto(_settings);
                WindowPlacement.Save(this, _settings.SettingsWindowPlacement);
                try
                {
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Could not save settings: {ex.Message}";
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }

        // -- Master/detail navigation -------------------------------------------

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Fires during InitializeComponent before the detail pane exists.
            if (DetailHost is null) return;
            if (NavList.SelectedItem is not ListBoxItem { Tag: string panelName }) return;

            foreach (var panel in DetailHost.Children.OfType<UIElement>())
            {
                panel.Visibility = Visibility.Collapsed;
            }

            if (FindName(panelName) is UIElement selected)
            {
                selected.Visibility = Visibility.Visible;
            }

            if (panelName == nameof(PanelDiagnostics))
            {
                RefreshDiagnostics();
            }

            DetailScroll.ScrollToTop();
        }

        // -- Panel population ---------------------------------------------------

        private void PopulateSizePanel(StackPanel panel, List<SizeSetting> selected)
        {
            foreach (var (name, size) in SizePresets)
            {
                AddToggleRow(panel, $"{name} ({size.Width}x{size.Height})", size, selected.Contains(size));
            }

            foreach (var size in selected.Where(s => !SizePresets.Any(p => p.Size == s)))
            {
                AddToggleRow(panel, $"{size.Width}x{size.Height}", size, isChecked: true);
            }
        }

        private void PopulateAspectPanel(List<AspectRatioSetting> selected)
        {
            foreach (var ratio in AspectPresets)
            {
                AddToggleRow(AspectPanel, $"{ratio.X}:{ratio.Y}", ratio, selected.Contains(ratio));
            }

            foreach (var ratio in selected.Where(r => !AspectPresets.Contains(r)))
            {
                AddToggleRow(AspectPanel, $"{ratio.X}:{ratio.Y}", ratio, isChecked: true);
            }
        }

        private void PopulateAvatarPanel()
        {
            foreach (var value in DefaultAvatarSizes)
            {
                AddToggleRow(AvatarSizesPanel, $"{value}x{value}", value, _settings.AvatarSizes.Contains(value));
            }

            foreach (var value in _settings.AvatarSizes.Where(v => !DefaultAvatarSizes.Contains(v)))
            {
                AddToggleRow(AvatarSizesPanel, $"{value}x{value}", value, isChecked: true);
            }
        }

        private void PopulateSepiaPanel()
        {
            foreach (var value in DefaultSepiaIntensities)
            {
                AddToggleRow(SepiaPanel, $"Sepia {value}%", value, _settings.SepiaIntensities.Contains(value));
            }

            foreach (var value in _settings.SepiaIntensities.Where(v => !DefaultSepiaIntensities.Contains(v)))
            {
                AddToggleRow(SepiaPanel, $"Sepia {value}%", value, isChecked: true);
            }
        }

        private void PopulateIcoPanel()
        {
            IcoSizesList.Configure(_settings.IcoSizes, AppSettings.BuiltInIcoSizes);
        }

        /// <summary>Adds one menu-entry row (label + on/off switch) to a feature page.</summary>
        private void AddToggleRow(StackPanel panel, string label, object value, bool isChecked)
        {
            var toggle = new ToggleButton
            {
                IsChecked = isChecked,
                Tag = value,
                Style = (Style)FindResource("SwitchToggle"),
            };
            WireContextMenuApply(toggle);
            DockPanel.SetDock(toggle, Dock.Right);

            var content = new DockPanel();
            if (IsUserAddedValue(panel, value))
            {
                var removeButton = CreateRemoveButton("Remove value");
                removeButton.Click += (_, _) =>
                {
                    panel.Children.Remove((UIElement)removeButton.Tag);
                    ApplyContextMenuChange("Value removed and context menu updated.");
                };
                DockPanel.SetDock(removeButton, Dock.Right);
                content.Children.Add(removeButton);
            }

            content.Children.Add(toggle);
            content.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("ToggleRowLabel"),
            });

            var row = new Border
            {
                Style = (Style)FindResource("ToggleRow"),
                Child = content,
                Tag = toggle,
            };
            if (content.Children[0] is Button button)
            {
                button.Tag = row;
            }

            panel.Children.Add(row);
        }

        private static IEnumerable<ToggleButton> RowToggles(StackPanel panel) =>
            panel.Children.OfType<Border>().Select(row => (ToggleButton)row.Tag!);

        private static List<T> CollectToggled<T>(StackPanel panel) =>
            RowToggles(panel)
                .Where(t => t.IsChecked == true)
                .Select(t => (T)t.Tag!)
                .ToList();

        private void WireContextMenuApply(ToggleButton toggle)
        {
            toggle.Checked += (_, _) => ApplyContextMenuChange("Context menu updated.");
            toggle.Unchecked += (_, _) => ApplyContextMenuChange("Context menu updated.");
        }

        private void WireSettingsPersistence()
        {
            QualitySlider.ValueChanged += (_, _) => ApplySettingsOnly("JPEG quality saved.");
            WebPQualitySlider.ValueChanged += (_, _) => ApplySettingsOnly("WebP quality saved.");
            AvifQualitySlider.ValueChanged += (_, _) => ApplySettingsOnly("AVIF quality saved.");
            CropAnchorBox.SelectionChanged += (_, _) => ApplySettingsOnly("Crop position saved.");
            IcoSizesList.SizesChanged += (_, _) => ApplySettingsOnly(IcoSizesList.StatusMessage);
            IcoSizesList.StatusChanged += (_, _) => StatusText.Text = IcoSizesList.StatusMessage;

            UseSubfolderBox.Checked += (_, _) => ApplySettingsOnly("Output folder setting saved.");
            UseSubfolderBox.Unchecked += (_, _) => ApplySettingsOnly("Output folder setting saved.");
            PreserveMetadataBox.Checked += (_, _) => ApplySettingsOnly("Metadata setting saved.");
            PreserveMetadataBox.Unchecked += (_, _) => ApplySettingsOnly("Metadata setting saved.");

            BackgroundBox.LostFocus += (_, _) => ApplySettingsOnly("JPEG background saved.");
            SubfolderNameBox.LostFocus += (_, _) => ApplySettingsOnly("Output folder setting saved.");
            OutputPatternBox.LostFocus += (_, _) => ApplySettingsOnly("Output name pattern saved.");

            BackgroundBox.KeyDown += ApplySettingsOnEnter;
            SubfolderNameBox.KeyDown += ApplySettingsOnEnter;
            OutputPatternBox.KeyDown += ApplySettingsOnEnter;
        }

        private void ApplySettingsOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplySettingsOnly("Settings saved.");
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private Button CreateRemoveButton(string toolTip) =>
            new()
            {
                Style = (Style)FindResource("FlatButton"),
                Width = 28,
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = toolTip,
                Content = new TextBlock
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12,
                    Text = "\uE74D",
                },
            };

        private static bool IsUserAddedValue(StackPanel panel, object value) =>
            panel.Name switch
            {
                nameof(FitPanel) or nameof(CoverPanel) => value is SizeSetting size && !SizePresets.Any(p => p.Size == size),
                nameof(AspectPanel) => value is AspectRatioSetting ratio && !AspectPresets.Contains(ratio),
                nameof(AvatarSizesPanel) => value is int avatarSize && !DefaultAvatarSizes.Contains(avatarSize),
                nameof(SepiaPanel) => value is int sepia && !DefaultSepiaIntensities.Contains(sepia),
                _ => false,
            };

        // -- Custom entry helpers -----------------------------------------------

        private void AddCustomSize(TextBox input, StackPanel panel)
        {
            if (!CommandLine.TryParseSize(input.Text, out var size))
            {
                StatusText.Text = "Enter a size like 1920x1080 (both values greater than 0).";
                return;
            }

            var setting = new SizeSetting(size.Width, size.Height);
            if (TryEnableExisting(panel, setting))
            {
                input.Clear();
                ApplyContextMenuChange("Context menu updated.");
                return;
            }

            AddToggleRow(panel, $"{setting.Width}x{setting.Height}", setting, isChecked: true);
            input.Clear();
            ApplyContextMenuChange("Size added and context menu updated.");
        }

        private void AddCustomAspect()
        {
            if (!CommandLine.TryParseAspect(AspectCustomBox.Text, out var x, out var y))
            {
                StatusText.Text = "Enter an aspect ratio like 4:3 (both values greater than 0).";
                return;
            }

            var setting = new AspectRatioSetting(x, y);
            if (TryEnableExisting(AspectPanel, setting))
            {
                AspectCustomBox.Clear();
                ApplyContextMenuChange("Context menu updated.");
                return;
            }

            AddToggleRow(AspectPanel, $"{x}:{y}", setting, isChecked: true);
            AspectCustomBox.Clear();
            ApplyContextMenuChange("Aspect ratio added and context menu updated.");
        }

        private void AddCustomAvatarSize()
        {
            if (!int.TryParse(AvatarSizeCustomBox.Text.Trim(), out var size) || size <= 0)
            {
                StatusText.Text = "Enter a positive pixel size.";
                return;
            }

            if (TryEnableExisting(AvatarSizesPanel, size))
            {
                AvatarSizeCustomBox.Clear();
                ApplyContextMenuChange("Context menu updated.");
                return;
            }

            AddToggleRow(AvatarSizesPanel, $"{size}x{size}", size, isChecked: true);
            AvatarSizeCustomBox.Clear();
            ApplyContextMenuChange("Avatar size added and context menu updated.");
        }

        private void AddCustomSepia()
        {
            if (!int.TryParse(SepiaCustomBox.Text.Trim(), out var intensity) || intensity is < 0 or > 100)
            {
                StatusText.Text = "Enter a sepia strength from 0 to 100.";
                return;
            }

            if (TryEnableExisting(SepiaPanel, intensity))
            {
                SepiaCustomBox.Clear();
                ApplyContextMenuChange("Context menu updated.");
                return;
            }

            AddToggleRow(SepiaPanel, $"Sepia {intensity}%", intensity, isChecked: true);
            SepiaCustomBox.Clear();
            ApplyContextMenuChange("Sepia strength added and context menu updated.");
        }

        private static bool TryEnableExisting(StackPanel panel, object value)
        {
            var existing = RowToggles(panel).FirstOrDefault(t => Equals(t.Tag, value));
            if (existing is null) return false;
            existing.IsChecked = true;
            return true;
        }

        // -- Live preview -------------------------------------------------------

        /// <summary>Reads the current (possibly unsaved) UI state into a settings object.</summary>
        private void CollectInto(AppSettings settings)
        {
            settings.JpegQuality = (int)QualitySlider.Value;
            settings.WebPQuality = (int)WebPQualitySlider.Value;
            settings.AvifQuality = (int)AvifQualitySlider.Value;
            settings.TransparencyBackground = BackgroundBox.Text.Trim();
            settings.PreserveMetadata = PreserveMetadataBox.IsChecked == true;
            settings.IcoSizes = IcoSizesList.Sizes.ToList();
            settings.CropAnchor = (CropAnchor)(CropAnchorBox.SelectedItem ?? CropAnchor.Center);
            settings.UseSubfolder = UseSubfolderBox.IsChecked == true;
            settings.SubfolderName = SubfolderNameBox.Text.Trim();
            settings.OutputNamePattern = OutputPatternBox.Text.Trim();
            settings.AvatarSizes = CollectToggled<int>(AvatarSizesPanel);
            settings.SepiaIntensities = CollectToggled<int>(SepiaPanel);
            settings.FitSizes = CollectToggled<SizeSetting>(FitPanel);
            settings.CoverSizes = CollectToggled<SizeSetting>(CoverPanel);
            settings.AspectRatios = CollectToggled<AspectRatioSetting>(AspectPanel);

            settings.EnableConvertToJpg = ConvertJpgToggle.IsChecked == true;
            settings.EnableConvertToPng = ConvertPngToggle.IsChecked == true;
            settings.EnableConvertToWebP = ConvertWebPToggle.IsChecked == true;
            settings.EnableConvertToAvif = ConvertAvifToggle.IsChecked == true;
            settings.EnableConvertToIco = ConvertIcoToggle.IsChecked == true;
            settings.ContextMenuEnabled = _contextMenuEnabled;
            settings.EnableCompressJpeg = CompressToggle.IsChecked == true;
            settings.EnableOptimizePng = OptimizePngToggle.IsChecked == true;
            settings.EnableRemoveMetadata = RemoveMetadataToggle.IsChecked == true;
            settings.EnableFavicon = FaviconToggle.IsChecked == true;
            settings.EnablePastePng = PastePngToggle.IsChecked == true;
            settings.EnablePasteJpg = PasteJpgToggle.IsChecked == true;
        }

        private void ApplyContextMenuChange(string successMessage)
        {
            if (!_initialized)
            {
                return;
            }

            CollectInto(_settings);
            if (!TrySaveSettings())
            {
                return;
            }

            RefreshPreview();
            RefreshDiagnostics();

            if (!_contextMenuEnabled)
            {
                try
                {
                    ShellIntegration.Unregister();
                    UpdateStatus(updateMessage: false);
                    RefreshDiagnostics();
                    StatusText.Text = successMessage == "Context menu removed."
                        ? successMessage
                        : "Settings saved. Context menu is off.";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Removal failed: {ex.Message}";
                }
                return;
            }

            RebuildContextMenu(successMessage);
            RefreshDiagnostics();
        }

        private void ApplySettingsOnly(string successMessage)
        {
            if (!_initialized)
            {
                return;
            }

            CollectInto(_settings);
            if (!TrySaveSettings())
            {
                return;
            }

            RefreshPreview();
            RefreshDiagnostics();
            StatusText.Text = successMessage;
        }

        private bool TrySaveSettings()
        {
            try
            {
                _settings.Save();
                return true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not save settings: {ex.Message}";
                return false;
            }
        }

        private void RebuildContextMenu(string successMessage)
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null)
            {
                StatusText.Text = "Settings saved, but the application path could not be determined for the context menu.";
                return;
            }

            var cliPath = CliExecutablePath(exePath);
            if (cliPath is null)
            {
                StatusText.Text = "Settings saved, but the CLI path could not be determined for the context menu.";
                return;
            }

            try
            {
                ShellIntegration.Register(exePath, cliPath, _settings);
                UpdateStatus(updateMessage: false);
                StatusText.Text = successMessage;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Settings saved, but updating the context menu failed: {ex.Message}";
            }
        }

        private void RefreshPreview()
        {
            if (!_initialized) return;

            var snapshot = new AppSettings();
            CollectInto(snapshot);

            var extension = ContextMenuPreview.SelectedExtension;

            if (!_contextMenuEnabled)
            {
                ContextMenuPreview.Render(
                    Array.Empty<ShellIntegration.MenuEntry>(),
                    Array.Empty<ShellIntegration.MenuEntry>(),
                    contextMenuEnabled: false);
                return;
            }

            ContextMenuPreview.Render(
                ShellIntegration.MenuEntries(extension, snapshot).ToList(),
                ShellIntegration.FolderMenuEntries(snapshot).ToList(),
                contextMenuEnabled: true);

        }


        // -- Event handlers -----------------------------------------------------

        private void RefreshDiagnostics()
        {
            if (DiagnosticsTextBox is null)
            {
                return;
            }

            var guiPath = Environment.ProcessPath;
            var diagnostics = ShellIntegration.Diagnose(guiPath, CliExecutablePath(guiPath));
            var builder = new StringBuilder();
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"GUI executable path: {diagnostics.GuiExecutablePath ?? "<unavailable>"}");
            builder.AppendLine($"CLI executable path: {diagnostics.CliExecutablePath ?? "<unavailable>"}");
            builder.AppendLine($"Current user install path: {diagnostics.InstallFolder ?? "<not registered>"}");
            builder.AppendLine($"Context menu switch: {(_contextMenuEnabled ? "on" : "off")}");
            builder.AppendLine($"Any registered key: {(ShellIntegration.IsRegistered() ? "yes" : "no")}");
            builder.AppendLine();

            foreach (var location in diagnostics.Locations)
            {
                builder.AppendLine($"[{location.Name}]");
                builder.AppendLine($"Key: {location.KeyPath}");
                builder.AppendLine($"Exists: {(location.Exists ? "yes" : "no")}");

                if (!string.IsNullOrWhiteSpace(location.Error))
                {
                    builder.AppendLine($"Error: {location.Error}");
                }

                if (location.Exists)
                {
                    builder.AppendLine($"MUIVerb: {location.MuiVerb ?? "<missing>"}");
                    builder.AppendLine($"Icon: {location.Icon ?? "<missing>"}");
                    builder.AppendLine($"SubCommands: {FormatRegistryValue(location.SubCommands)}");

                    if (location.Commands.Count == 0)
                    {
                        builder.AppendLine("Commands: <none>");
                    }
                    else
                    {
                        builder.AppendLine("Commands:");
                        foreach (var command in location.Commands)
                        {
                            builder.AppendLine($"  {command.KeyName}: {command.MuiVerb ?? "<missing verb>"}");
                            builder.AppendLine($"    command: {command.Command ?? "<missing>"}");
                            builder.AppendLine($"    expected exe: {command.ExpectedExecutablePath ?? "<not checked>"}");
                            builder.AppendLine($"    uses expected exe: {FormatCommandMatch(command.CommandUsesExecutable)}");
                        }
                    }
                }

                builder.AppendLine();
            }

            DiagnosticsTextBox.Text = builder.ToString();

            var missing = diagnostics.Locations.Count(location => !location.Exists);
            var mismatchedCommands = diagnostics.Locations
                .SelectMany(location => location.Commands)
                .Count(command => command.CommandUsesExecutable == false);
            var errors = diagnostics.Locations.Count(location => !string.IsNullOrWhiteSpace(location.Error));

            DiagnosticsStatusText.Text = errors > 0
                ? $"{errors} registry location(s) could not be read. See report below."
                : mismatchedCommands > 0
                    ? $"{mismatchedCommands} command(s) do not point at the running executable."
                    : missing > 0
                        ? $"{missing} registry location(s) are missing."
                        : "All expected registry locations are present.";
        }

        private static string FormatRegistryValue(string? value) =>
            value is null ? "<missing>" : string.IsNullOrEmpty(value) ? "<empty>" : value;

        private static string? CliExecutablePath(string? guiExePath)
        {
            if (string.IsNullOrWhiteSpace(guiExePath))
            {
                return null;
            }

            var directory = System.IO.Path.GetDirectoryName(guiExePath);
            return directory is null
                ? null
                : System.IO.Path.Combine(directory, "QConvert.Cli.exe");
        }

        private static string FormatCommandMatch(bool? matches) => matches switch
        {
            true => "yes",
            false => "no",
            null => "not checked",
        };

        private void AddFitCustom_Click(object sender, RoutedEventArgs e) => AddCustomSize(FitCustomBox, FitPanel);
        private void AddCoverCustom_Click(object sender, RoutedEventArgs e) => AddCustomSize(CoverCustomBox, CoverPanel);
        private void AddAspectCustom_Click(object sender, RoutedEventArgs e) => AddCustomAspect();
        private void AddAvatarSize_Click(object sender, RoutedEventArgs e) => AddCustomAvatarSize();
        private void AddSepia_Click(object sender, RoutedEventArgs e) => AddCustomSepia();

        private void FitCustomBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddCustomSize(FitCustomBox, FitPanel); }
        private void CoverCustomBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddCustomSize(CoverCustomBox, CoverPanel); }
        private void AspectCustomBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddCustomAspect(); }
        private void AvatarSizeCustomBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddCustomAvatarSize(); }
        private void SepiaCustomBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddCustomSepia(); }

        private void RecreateContextMenu_Click(object sender, RoutedEventArgs e)
        {
            RepairContextMenu("Context menu recreated.");
        }

        private void RepairContextMenu_Click(object sender, RoutedEventArgs e) =>
            RepairContextMenu("Context menu repaired and recreated.");

        private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            RefreshDiagnostics();
            StatusText.Text = "Diagnostics refreshed.";
        }

        private void RepairContextMenu(string successMessage)
        {
            SetContextMenuEnabled(true);
            ApplyContextMenuChange(successMessage);
            RefreshDiagnostics();
        }

        private void ContextMenuPreview_ContextMenuEnabledChanged(object? sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SetContextMenuEnabled(ContextMenuPreview.IsContextMenuEnabled);
            ApplyContextMenuChange(_contextMenuEnabled
                ? "Context menu enabled and rebuilt."
                : "Context menu removed.");
        }

        private void SetContextMenuEnabled(bool enabled)
        {
            _contextMenuEnabled = enabled;
            ContextMenuPreview.IsContextMenuEnabled = enabled;
        }

        private void ApplySendToChange()
        {
            if (!_initialized)
            {
                return;
            }

            var exePath = Environment.ProcessPath;
            if (exePath is null)
            {
                StatusText.Text = "The application path could not be determined for the Send To menu.";
                return;
            }

            try
            {
                if (SendToToggle.IsChecked == true)
                {
                    SendToIntegration.Install(exePath);
                    StatusText.Text = "Send To menu installed.";
                }
                else
                {
                    SendToIntegration.Uninstall();
                    StatusText.Text = "Send To menu removed.";
                }

                UpdateSendToStatus();
            }
            catch (Exception ex)
            {
                SendToStatus.Text = $"Send To update failed: {ex.Message}";
            }
        }

        private void Unregister_Click(object sender, RoutedEventArgs e)
        {
            SetContextMenuEnabled(false);
            ApplyContextMenuChange("Context menu removed.");
        }

        private void PasteClipboardAsPng_Click(object sender, RoutedEventArgs e) =>
            PasteClipboardImage(ConversionTarget.Png);

        private void PasteClipboardAsJpg_Click(object sender, RoutedEventArgs e) =>
            PasteClipboardImage(ConversionTarget.Jpeg);

        private void PasteClipboardImage(ConversionTarget target)
        {
            if (!Clipboard.ContainsImage())
            {
                StatusText.Text = "Clipboard does not contain an image.";
                return;
            }

            var image = GetClipboardImage();
            if (image is null)
            {
                StatusText.Text = "Clipboard image could not be read.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckPathExists = true,
                FileName = $"clipboard-{DateTime.Now:yyyy-MM-ddTHH-mm-ss}{target.FileExtension()}",
                Filter = target == ConversionTarget.Jpeg
                    ? "JPEG image (*.jpg)|*.jpg"
                    : "PNG image (*.png)|*.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                ImageConverter.SaveBitmap(image, dialog.FileName, target, (int)QualitySlider.Value, _settings);
                StatusText.Text = $"Saved {System.IO.Path.GetFileName(dialog.FileName)}.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Clipboard save failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Reads an image from the clipboard robustly. WPF's <see cref="Clipboard.GetImage"/>
        /// returns an InteropBitmap over the raw DIB and frequently loses the alpha channel,
        /// producing an all-transparent ("empty") image for pasted PNGs. We therefore prefer
        /// the real "PNG" clipboard format and only fall back to GetImage as a last resort.
        /// </summary>
        private static System.Windows.Media.Imaging.BitmapSource? GetClipboardImage()
        {
            // 1. Preferred: apps (browsers, screenshot tools) often place the original
            //    PNG bytes on the clipboard. Decode them directly so alpha is preserved.
            foreach (var format in new[] { "PNG", "image/png" })
            {
                try
                {
                    if (Clipboard.ContainsData(format) &&
                        Clipboard.GetData(format) is System.IO.MemoryStream stream &&
                        stream.Length > 0)
                    {
                        stream.Position = 0;
                        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                            stream,
                            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0)
                        {
                            var frame = decoder.Frames[0];
                            frame.Freeze();
                            return frame;
                        }
                    }
                }
                catch
                {
                    // Ignore and try the next strategy.
                }
            }

            // 2. Fall back to the standard (lossy) accessor for sources that only
            //    provide a DIB/bitmap without transparency.
            try
            {
                return Clipboard.GetImage();
            }
            catch
            {
                return null;
            }
        }

        private void UpdateSendToStatus()
        {
            var installed = SendToIntegration.IsInstalled();
            SendToStatus.Text = installed
                ? "Currently installed."
                : "Currently not installed.";
            SendToStatusText.Text = installed
                ? "Send To: installed"
                : "Send To: not installed";
        }

        private void UpdateContextMenuStatusBadge(bool registered)
        {
            var background = registered
                ? Color.FromRgb(0xD1, 0xFA, 0xE5)
                : _contextMenuEnabled
                    ? Color.FromRgb(0xFE, 0xE2, 0xE2)
                    : Color.FromRgb(0xFE, 0xF3, 0xC7);
            var foreground = registered
                ? Color.FromRgb(0x06, 0x5F, 0x46)
                : _contextMenuEnabled
                    ? Color.FromRgb(0x99, 0x1B, 0x1B)
                    : Color.FromRgb(0x92, 0x45, 0x00);

            ContextMenuStatusBadge.Background = new SolidColorBrush(background);
            ContextMenuStatusText.Foreground = new SolidColorBrush(foreground);
        }
        private void UpdateStatus(bool updateMessage = true)
        {
            var registered = ShellIntegration.IsRegistered();
            ContextMenuStatusText.Text = _contextMenuEnabled
                ? registered ? "Context menu: installed" : "Context menu: missing"
                : "Context menu: off";
            UpdateContextMenuStatusBadge(registered);
            var contextMenuPanelStatus = _contextMenuEnabled
                ? registered ? "Installed for Explorer." : "Missing. Recreate from File menu."
                : "Off. Preview is hidden.";
            ContextMenuPreview.SetContextMenuStatus(contextMenuPanelStatus);

            if (updateMessage)
            {
                StatusText.Text = _contextMenuEnabled
                    ? registered
                        ? "Context menu is installed for the current user."
                        : "Context menu is on, but Explorer entries are missing. Use Recreate context menu."
                    : "Context menu is off.";
            }
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow
            {
                Owner = this,
            }.ShowDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    }
}
