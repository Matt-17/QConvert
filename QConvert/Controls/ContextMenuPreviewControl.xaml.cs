using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using QConvert.Core;

namespace QConvert.Controls
{
    public partial class ContextMenuPreviewControl : UserControl
    {
        private bool _suppressToggleEvent;

        public ContextMenuPreviewControl()
        {
            InitializeComponent();
        }

        public event EventHandler? ContextMenuEnabledChanged;

        public event EventHandler? PreviewExtensionChanged;

        public bool IsContextMenuEnabled
        {
            get => ContextMenuToggle.IsChecked == true;
            set
            {
                if (ContextMenuToggle.IsChecked == value)
                {
                    return;
                }

                _suppressToggleEvent = true;
                try
                {
                    ContextMenuToggle.IsChecked = value;
                }
                finally
                {
                    _suppressToggleEvent = false;
                }
            }
        }

        public string SelectedExtension =>
            PreviewExtensionBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : ".png";

        public void SetContextMenuStatus(string text) =>
            ContextMenuPanelStatusText.Text = text;

        public void Render(
            IReadOnlyList<ShellIntegration.MenuEntry> fileEntries,
            IReadOnlyList<ShellIntegration.MenuEntry> folderEntries,
            bool contextMenuEnabled)
        {
            PreviewFileCaption.Text = $"Right-click photo{SelectedExtension} > QConvert";

            if (!contextMenuEnabled)
            {
                RenderMenu(PreviewFileMenu, Array.Empty<ShellIntegration.MenuEntry>(), "Context menu is off.");
                RenderMenu(PreviewFolderMenu, Array.Empty<ShellIntegration.MenuEntry>(), "Context menu is off.");
                return;
            }

            RenderMenu(PreviewFileMenu, fileEntries, "No entries. This file type will not show a QConvert menu.");
            RenderMenu(PreviewFolderMenu, folderEntries, "No entries. Folders will not show a QConvert menu.");
        }

        private void ContextMenuToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suppressToggleEvent)
            {
                ContextMenuEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PreviewExtensionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            PreviewExtensionChanged?.Invoke(this, EventArgs.Empty);

        private void RenderMenu(StackPanel host, IReadOnlyList<ShellIntegration.MenuEntry> entries, string emptyMessage)
        {
            host.Children.Clear();

            if (entries.Count == 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = emptyMessage,
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 7, 10, 7),
                });
                return;
            }

            int? previousGroup = null;
            foreach (var entry in entries)
            {
                if (previousGroup is not null && previousGroup != entry.Group)
                {
                    host.Children.Add(new Rectangle
                    {
                        Height = 1,
                        Fill = (Brush)FindResource("CardBorderBrush"),
                        Margin = new Thickness(8, 3, 8, 3),
                    });
                }

                host.Children.Add(new TextBlock
                {
                    Text = entry.Label,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextBrush"),
                    Margin = new Thickness(10, 4, 10, 4),
                });

                previousGroup = entry.Group;
            }
        }
    }
}
