using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QConvert.Controls
{
    public partial class IcoSizeListControl : UserControl
    {
        private readonly SortedSet<int> _sizes = new();
        private readonly SortedSet<int> _defaultSizes = new();
        private int? _hoveredSize;

        public IcoSizeListControl()
        {
            InitializeComponent();
        }

        public event EventHandler? SizesChanged;

        public event EventHandler? HoveredSizeChanged;

        public event EventHandler? StatusChanged;

        public IReadOnlyList<int> Sizes => _sizes.ToList();

        public int? HoveredSize => _hoveredSize;

        public string StatusMessage { get; private set; } = "";

        public void Configure(IEnumerable<int> sizes, IEnumerable<int>? defaultSizes = null)
        {
            _sizes.Clear();
            foreach (var size in Normalize(sizes))
            {
                _sizes.Add(size);
            }

            _defaultSizes.Clear();
            foreach (var size in Normalize(defaultSizes ?? Array.Empty<int>()))
            {
                _defaultSizes.Add(size);
            }

            ClearHoveredSize(notify: false);
            RenderRows();
        }

        public bool AddSize(int size)
        {
            if (!IsValid(size))
            {
                SetStatus("Enter a size from 1 to 256.");
                return false;
            }

            if (!_sizes.Add(size))
            {
                SetStatus("Icon size already listed.");
                return false;
            }

            RenderRows();
            NotifySizesChanged(_defaultSizes.Contains(size) ? "Icon size enabled." : "Icon size added.");
            return true;
        }

        private void RemoveSize(int size)
        {
            if (_sizes.Count <= 1)
            {
                SetStatus("Keep at least one icon size.");
                return;
            }

            if (!_sizes.Remove(size))
            {
                return;
            }

            if (_hoveredSize == size)
            {
                ClearHoveredSize(notify: true);
            }

            RenderRows();
            NotifySizesChanged("Icon size removed.");
        }

        private void RenderRows()
        {
            RowsHost.Children.Clear();

            var rows = _sizes.Union(_defaultSizes).OrderBy(size => size).ToList();
            if (rows.Count == 0)
            {
                RowsHost.Children.Add(new TextBlock
                {
                    Text = "No sizes.",
                    Style = (Style)FindResource("Hint"),
                    Margin = new Thickness(8, 7, 8, 7),
                });
                return;
            }

            for (var index = 0; index < rows.Count; index++)
            {
                RowsHost.Children.Add(CreateRow(rows[index], index == rows.Count - 1));
            }
        }

        private Border CreateRow(int size, bool isLast)
        {
            var isDefault = _defaultSizes.Contains(size);
            var isActive = _sizes.Contains(size);
            var content = new DockPanel
            {
                LastChildFill = true,
            };

            if (isDefault)
            {
                var toggle = new ToggleButton
                {
                    IsChecked = isActive,
                    Tag = size,
                    Style = (Style)FindResource("SwitchToggle"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                toggle.Checked += DefaultToggle_Changed;
                toggle.Unchecked += DefaultToggle_Changed;
                DockPanel.SetDock(toggle, Dock.Right);
                content.Children.Add(toggle);
            }
            else
            {
                var removeButton = new Button
                {
                    Content = CreateRemoveIcon(),
                    Width = 28,
                    MinWidth = 28,
                    Height = 24,
                    Padding = new Thickness(0),
                    Tag = size,
                    ToolTip = "Remove size",
                    Style = (Style)FindResource("FlatButton"),
                };
                removeButton.Click += RemoveButton_Click;
                DockPanel.SetDock(removeButton, Dock.Right);
                content.Children.Add(removeButton);
            }

            content.Children.Add(new TextBlock
            {
                Text = $"{size} x {size}",
                Style = (Style)FindResource("ToggleRowLabel"),
                Foreground = (Brush)FindResource(isActive ? "TextBrush" : "MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var row = new Border
            {
                Padding = new Thickness(9, 6, 7, 6),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
                Child = content,
                Tag = size,
            };
            row.MouseEnter += Row_MouseEnter;
            row.MouseLeave += Row_MouseLeave;
            return row;
        }

        private FrameworkElement CreateRemoveIcon()
        {
            var stroke = (Brush)FindResource("MutedBrush");
            var canvas = new Canvas
            {
                Width = 18,
                Height = 18,
            };

            canvas.Children.Add(CreateIconPath(stroke, "M6.2,6.8 L6.8,14.1 C6.9,14.9 7.5,15.4 8.3,15.4 L11.7,15.4 C12.5,15.4 13.1,14.9 13.2,14.1 L13.8,6.8"));
            canvas.Children.Add(CreateIconPath(stroke, "M5,6.1 L15,6.1"));
            canvas.Children.Add(CreateIconPath(stroke, "M8,6.1 L8.4,4.8 C8.5,4.3 8.9,4 9.4,4 L10.6,4 C11.1,4 11.5,4.3 11.6,4.8 L12,6.1"));
            canvas.Children.Add(CreateIconPath(stroke, "M9,8.6 L9,13.1"));
            canvas.Children.Add(CreateIconPath(stroke, "M11,8.6 L11,13.1"));

            return new Viewbox
            {
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                Child = canvas,
            };
        }

        private static Path CreateIconPath(Brush stroke, string data) =>
            new()
            {
                Data = Geometry.Parse(data),
                Stroke = stroke,
                StrokeThickness = 1.35,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            };

        private void DefaultToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { Tag: int size } toggle)
            {
                SetDefaultSizeEnabled(size, toggle.IsChecked == true);
            }
        }

        private void SetDefaultSizeEnabled(int size, bool enabled)
        {
            if (!enabled && _sizes.Count <= 1 && _sizes.Contains(size))
            {
                SetStatus("Keep at least one icon size.");
                RenderRows();
                return;
            }

            var changed = enabled
                ? _sizes.Add(size)
                : _sizes.Remove(size);

            if (!changed)
            {
                return;
            }

            if (!enabled && _hoveredSize == size)
            {
                ClearHoveredSize(notify: true);
            }

            RenderRows();
            NotifySizesChanged(enabled ? "Icon size enabled." : "Icon size disabled.");
        }

        private void Add_Click(object sender, RoutedEventArgs e) => AddFromInput();

        private void SizeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddFromInput();
                e.Handled = true;
            }
        }

        private void AddFromInput()
        {
            if (!int.TryParse(SizeBox.Text.Trim(), out var size))
            {
                SetStatus("Enter a size from 1 to 256.");
                return;
            }

            var added = AddSize(size);
            if (added)
            {
                SizeBox.Clear();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int size })
            {
                RemoveSize(size);
            }
        }

        private void Row_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not Border { Tag: int size } || !_sizes.Contains(size) || _hoveredSize == size)
            {
                return;
            }

            _hoveredSize = size;
            HoveredSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Row_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border { Tag: int size } && _hoveredSize == size)
            {
                ClearHoveredSize(notify: true);
            }
        }

        private void NotifySizesChanged(string message)
        {
            StatusMessage = message;
            SizesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetStatus(string message)
        {
            StatusMessage = message;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearHoveredSize(bool notify)
        {
            if (_hoveredSize is null)
            {
                return;
            }

            _hoveredSize = null;
            if (notify)
            {
                HoveredSizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static IEnumerable<int> Normalize(IEnumerable<int> sizes) =>
            sizes.Where(IsValid).Distinct().OrderBy(size => size);

        private static bool IsValid(int size) => size is > 0 and <= 256;
    }
}
