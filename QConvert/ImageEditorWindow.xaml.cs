using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.Win32;

using QConvert.Core;

namespace QConvert
{
    public partial class ImageEditorWindow : Window
    {
        private const double DimmedSourceOpacity = 0.28;
        private const int DefaultSepiaIntensity = 65;
        private const double PreviewMargin = 48;
        private const double PreviewFallbackMaxSide = 1400;

        private readonly string _imagePath;
        private readonly BitmapSource _sourceImage;
        private AppSettings _baseSettings;
        private bool _initialized;
        private TextBox? _dragTextBox;
        private Point _dragStartPoint;
        private double _dragStartValue;
        private bool _suppressPreviewRefresh;
        private bool _cropDragActive;
        private bool _currentPreviewCanDrag;
        private PreviewPlacement _currentPreviewPlacement;
        private Point _cropDragStartPoint;
        private PreviewPlacement _cropDragStartPlacement;
        private BitmapSource? _previewSource;
        private PixelSize _previewSourceSize;
        private readonly Dictionary<PreviewCacheKey, PreviewRenderResult> _previewCache = new();
        private CancellationTokenSource? _previewCancellation;
        private long _previewRequestId;

        public ImageEditorWindow(string imagePath)
        {
            _imagePath = Path.GetFullPath(imagePath);

            InitializeComponent();

            _baseSettings = AppSettings.Load();
            WindowPlacement.Restore(this, _baseSettings.EditorWindowPlacement);
            IcoSizesList.SizesChanged += IcoSizesList_SizesChanged;
            IcoSizesList.StatusChanged += (_, _) => StatusText.Text = IcoSizesList.StatusMessage;
            _sourceImage = ImageConverter.CreateThreadSafeCopy(ImageConverter.LoadPreview(_imagePath));

            InitializeImage();
            InitializeControls();
            SetToolPanel(CurrentToolId());

            _initialized = true;
            RefreshPreview();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var settings = AppSettings.Load();
            WindowPlacement.Save(this, settings.EditorWindowPlacement);

            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not save window placement: {ex.Message}";
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private void InitializeImage()
        {
            Title = $"QConvert Editor - {Path.GetFileName(_imagePath)}";
            ImageTitleText.Text = Path.GetFileName(_imagePath);
            ImagePathText.Text = _imagePath;
            SourceInfoText.Text = $"Source: {_sourceImage.PixelWidth}x{_sourceImage.PixelHeight}";

            OriginalImage.Source = _sourceImage;
            OriginalImage.Width = _sourceImage.PixelWidth;
            OriginalImage.Height = _sourceImage.PixelHeight;
        }

        private void InitializeControls()
        {
            JpegQualitySlider.Value = _baseSettings.JpegQuality;
            WebPQualitySlider.Value = _baseSettings.WebPQuality;
            AvifQualitySlider.Value = _baseSettings.AvifQuality;
            BackgroundBox.Text = _baseSettings.TransparencyBackground;
            SepiaIntensitySlider.Value = DefaultSepiaIntensity;
            SepiaHueSlider.Value = ImageConverter.StandardSepiaHue;
            UpdateSepiaVisuals();
            PopulateIcoSizeList(_baseSettings.IcoSizes);

            FitKeepAspectToggle.IsChecked = true;
            FitWidthBox.Text = _sourceImage.PixelWidth.ToString(CultureInfo.InvariantCulture);
            FitHeightBox.Text = _sourceImage.PixelHeight.ToString(CultureInfo.InvariantCulture);
            FitResultSizeText.Text = $"Result: {_sourceImage.PixelWidth}x{_sourceImage.PixelHeight}";
            CoverWidthBox.Text = _sourceImage.PixelWidth.ToString(CultureInfo.InvariantCulture);
            CoverHeightBox.Text = _sourceImage.PixelHeight.ToString(CultureInfo.InvariantCulture);
            CoverResizeWidthBox.Text = _sourceImage.PixelWidth.ToString(CultureInfo.InvariantCulture);
            CoverResizeHeightBox.Text = _sourceImage.PixelHeight.ToString(CultureInfo.InvariantCulture);
            CoverPositionXBox.Text = "0";
            CoverPositionYBox.Text = "0";
            AvatarSizeBox.Text = Math.Min(_sourceImage.PixelWidth, _sourceImage.PixelHeight).ToString(CultureInfo.InvariantCulture);
            AvatarPositionXBox.Text = "50";
            AvatarPositionYBox.Text = "50";

            var gcd = GreatestCommonDivisor(_sourceImage.PixelWidth, _sourceImage.PixelHeight);
            AspectXBox.Text = (_sourceImage.PixelWidth / gcd).ToString(CultureInfo.InvariantCulture);
            AspectYBox.Text = (_sourceImage.PixelHeight / gcd).ToString(CultureInfo.InvariantCulture);
            AspectPositionXBox.Text = "50";
            AspectPositionYBox.Text = "50";

            SelectConvertTarget(DefaultConvertTarget());
        }

        private void PopulateIcoSizeList(IEnumerable<int> sizes)
        {
            IcoSizesList.Configure(sizes, AppSettings.BuiltInIcoSizes);
        }

        private ConversionTarget DefaultConvertTarget() =>
            Path.GetExtension(_imagePath).ToLowerInvariant() switch
            {
                ".png" => ConversionTarget.Jpeg,
                ".jpg" or ".jpeg" => ConversionTarget.Png,
                ".ico" => ConversionTarget.Png,
                _ => ConversionTarget.Png,
            };

        private void SelectConvertTarget(ConversionTarget target)
        {
            foreach (ComboBoxItem item in ConvertTargetBox.Items)
            {
                if (string.Equals((string?)item.Tag, target.ToString(), StringComparison.Ordinal))
                {
                    ConvertTargetBox.SelectedItem = item;
                    return;
                }
            }

            ConvertTargetBox.SelectedIndex = 0;
        }

        private string CurrentToolId() =>
            ToolList.SelectedItem is ListBoxItem { Tag: string tag } ? tag : "Convert";

        private void SetToolPanel(string toolId)
        {
            if (ConvertPanel is null || ToolTitleText is null)
            {
                return;
            }

            foreach (var panel in new UIElement[]
            {
                ConvertPanel,
                FitPanel,
                CoverPanel,
                AspectPanel,
                AvatarPanel,
                SepiaPanel,
                CompressPanel,
                OptimizePngPanel,
                MetadataPanel,
                FaviconPanel,
            })
            {
                panel.Visibility = Visibility.Collapsed;
            }

            switch (toolId)
            {
                case "Convert":
                    ConvertPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Convert format";
                    ToolDescriptionText.Text = "Choose the exact target format and output settings.";
                    break;
                case "Fit":
                    FitPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Resize to fit";
                    ToolDescriptionText.Text = "Keep aspect to fit proportionally, or turn it off to stretch to the exact size.";
                    break;
                case "Cover":
                    CoverPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Crop to size";
                    ToolDescriptionText.Text = "Select a source crop in pixels, then resize it to the output size.";
                    break;
                case "Aspect":
                    AspectPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Crop to aspect";
                    ToolDescriptionText.Text = "Crop to the target ratio without resizing.";
                    break;
                case "Avatar":
                    AvatarPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Avatar export";
                    ToolDescriptionText.Text = "Create an exact square PNG using the selected crop anchor.";
                    break;
                case "Sepia":
                    SepiaPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Sepia filter";
                    ToolDescriptionText.Text = "Apply a sepia tone with a precise intensity value.";
                    break;
                case "Compress":
                    CompressPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Compress JPG";
                    ToolDescriptionText.Text = "Re-encode to JPEG using the selected JPEG quality.";
                    break;
                case "OptimizePng":
                    OptimizePngPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Optimize PNG";
                    ToolDescriptionText.Text = "Re-encode as PNG and keep the smaller output when possible.";
                    break;
                case "Metadata":
                    MetadataPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Remove metadata";
                    ToolDescriptionText.Text = "Bake orientation into pixels and write a clean copy without metadata.";
                    break;
                case "Favicon":
                    FaviconPanel.Visibility = Visibility.Visible;
                    ToolTitleText.Text = "Favicon bundle";
                    ToolDescriptionText.Text = "Create favicon.ico, PNG icons, and a site.webmanifest in a folder.";
                    break;
            }

            SetSharedOptionVisibility(toolId);
        }

        private void SetSharedOptionVisibility(string toolId)
        {
            if (IcoSizesCard is null || QualityCard is null || BackgroundCard is null)
            {
                return;
            }

            var outputTarget = OutputTargetForTool(toolId);
            var isConvertToIco = toolId == "Convert" && SelectedConvertTarget() == ConversionTarget.Ico;

            IcoSizesCard.Visibility = toolId == "Favicon" || isConvertToIco
                ? Visibility.Visible
                : Visibility.Collapsed;

            JpegQualityRow.Visibility = outputTarget == ConversionTarget.Jpeg ? Visibility.Visible : Visibility.Collapsed;
            WebPQualityRow.Visibility = outputTarget == ConversionTarget.WebP ? Visibility.Visible : Visibility.Collapsed;
            AvifQualityRow.Visibility = outputTarget == ConversionTarget.Avif ? Visibility.Visible : Visibility.Collapsed;

            QualityCard.Visibility =
                JpegQualityRow.Visibility == Visibility.Visible
                || WebPQualityRow.Visibility == Visibility.Visible
                || AvifQualityRow.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Hidden;

            BackgroundCard.Visibility = outputTarget == ConversionTarget.Jpeg
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        private ConversionTarget? OutputTargetForTool(string toolId)
        {
            var sourceTarget = ImageConverter.TargetForSource(Path.GetExtension(_imagePath));
            return toolId switch
            {
                "Convert" => SelectedConvertTarget() is ConversionTarget.Png or ConversionTarget.Ico
                    ? null
                    : SelectedConvertTarget(),
                "Compress" => ConversionTarget.Jpeg,
                "OptimizePng" => null,
                "Avatar" => null,
                "Favicon" => null,
                "Metadata" or "Fit" or "Cover" or "Aspect" or "Sepia" => sourceTarget == ConversionTarget.Jpeg ? ConversionTarget.Jpeg : null,
                _ => null,
            };
        }

        private async void RefreshPreview()
        {
            if (!_initialized)
            {
                return;
            }

            _previewCancellation?.Cancel();
            var requestId = ++_previewRequestId;

            if (!TryBuildOperation(out var operation, out var settings, out var message))
            {
                OutputNameText.Text = "";
                UpdateFitResultSizeText(null);
                ShowImage(preview: null, operation: null);
                StatusText.Text = message;
                return;
            }

            var resultSize = EstimateResultSize(operation, settings);
            UpdateFitResultSizeText(operation is FitOperation ? resultSize : null);
            OutputNameText.Text = $"Output: {DefaultOutputFileName(operation, resultSize.Width, resultSize.Height, SaveAsTarget(operation), settings)}";

            if (PreviewToggle.IsChecked != true)
            {
                ShowImage(preview: null, operation: null);
                StatusText.Text = "Preview off.";
                return;
            }

            var previewSource = GetPreviewSource();
            var previewScale = previewSource.PixelWidth / (double)_sourceImage.PixelWidth;
            var previewOperation = ScaleOperationForPreview(operation, previewScale);
            var cacheKey = PreviewCacheKey.From(previewOperation, settings, _previewSourceSize);

            if (_previewCache.TryGetValue(cacheKey, out var cached))
            {
                ShowImage(cached.Preview, operation, resultSize.Width, resultSize.Height, settings);
                StatusText.Text = "Preview ready.";
                return;
            }

            var cancellation = new CancellationTokenSource();
            _previewCancellation = cancellation;
            StatusText.Text = "Rendering preview...";

            try
            {
                var preview = await Task.Run(
                    () =>
                    {
                        if (cancellation.IsCancellationRequested)
                        {
                            return null;
                        }

                        var rendered = ImageConverter.RenderPreview(previewSource, previewOperation, settings);
                        FreezeIfPossible(rendered);
                        return cancellation.IsCancellationRequested ? null : rendered;
                    });

                if (preview is null || cancellation.IsCancellationRequested || requestId != _previewRequestId)
                {
                    return;
                }

                _previewCache[cacheKey] = new PreviewRenderResult(preview);
                ShowImage(preview, operation, resultSize.Width, resultSize.Height, settings);
                StatusText.Text = "Preview ready.";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (requestId == _previewRequestId)
                {
                    ShowImage(preview: null, operation: null);
                    StatusText.Text = $"Preview failed: {ex.Message}";
                }
            }
            finally
            {
                if (ReferenceEquals(_previewCancellation, cancellation))
                {
                    _previewCancellation = null;
                }
            }
        }

        private void UpdateFitResultSizeText(PixelSize? size)
        {
            if (FitResultSizeText is null)
            {
                return;
            }

            FitResultSizeText.Text = size is PixelSize result
                ? $"Result: {result.Width}x{result.Height}"
                : "";
        }

        private void ShowImage(BitmapSource? preview, Operation? operation, int? resultWidth = null, int? resultHeight = null, AppSettings? settings = null)
        {
            if (preview is null || PreviewToggle.IsChecked != true)
            {
                _currentPreviewCanDrag = false;
                ImagePreviewViewbox.Visibility = Visibility.Visible;
                IconPreviewScroll.Visibility = Visibility.Collapsed;
                IconPreviewPanel.Children.Clear();
                SetCanvasSize(_sourceImage.PixelWidth, _sourceImage.PixelHeight);
                PlaceImage(OriginalImage, _sourceImage, 0, 0, _sourceImage.PixelWidth, _sourceImage.PixelHeight);
                OriginalImage.Opacity = 1;
                OriginalImage.Visibility = Visibility.Visible;
                PreviewImage.Visibility = Visibility.Collapsed;
                PreviewImage.Cursor = Cursors.Arrow;
                HideCropOverlay();
                ResultInfoText.Text = "Result: source";
                return;
            }

            var resultSize = new PixelSize(
                resultWidth ?? preview.PixelWidth,
                resultHeight ?? preview.PixelHeight);

            if (IsIconPreviewOperation(operation))
            {
                ShowIconPreviewStrip(preview, operation, settings);
                ResultInfoText.Text = $"Result: {resultSize.Width}x{resultSize.Height}";
                return;
            }

            ImagePreviewViewbox.Visibility = Visibility.Visible;
            IconPreviewScroll.Visibility = Visibility.Collapsed;
            IconPreviewPanel.Children.Clear();
            var placement = GetPreviewPlacement(operation, resultSize);
            _currentPreviewPlacement = placement;
            _currentPreviewCanDrag = IsPositionedCropOperation(operation)
                && (placement.Width < _sourceImage.PixelWidth || placement.Height < _sourceImage.PixelHeight);
            var canvasWidth = Math.Max(_sourceImage.PixelWidth, placement.RelativeToSource ? _sourceImage.PixelWidth : placement.Width);
            var canvasHeight = Math.Max(_sourceImage.PixelHeight, placement.RelativeToSource ? _sourceImage.PixelHeight : placement.Height);
            SetCanvasSize(canvasWidth, canvasHeight);

            var sourceLeft = (canvasWidth - _sourceImage.PixelWidth) / 2;
            var sourceTop = (canvasHeight - _sourceImage.PixelHeight) / 2;
            PlaceImage(OriginalImage, _sourceImage, sourceLeft, sourceTop, _sourceImage.PixelWidth, _sourceImage.PixelHeight);

            var previewLeft = placement.RelativeToSource
                ? sourceLeft + placement.Left
                : (canvasWidth - placement.Width) / 2;
            var previewTop = placement.RelativeToSource
                ? sourceTop + placement.Top
                : (canvasHeight - placement.Height) / 2;

            PlaceImage(PreviewImage, preview, previewLeft, previewTop, placement.Width, placement.Height);

            OriginalImage.Visibility = placement.ShowDimmedSource ? Visibility.Visible : Visibility.Collapsed;
            OriginalImage.Opacity = placement.ShowDimmedSource ? DimmedSourceOpacity : 1;
            if (IsPositionedCropOperation(operation) && placement.ShowDimmedSource)
            {
                ShowCropOverlay(
                    sourceLeft,
                    sourceTop,
                    _sourceImage.PixelWidth,
                    _sourceImage.PixelHeight,
                    previewLeft,
                    previewTop,
                    placement.Width,
                    placement.Height);
            }
            else
            {
                HideCropOverlay();
            }
            PreviewImage.Visibility = Visibility.Visible;
            PreviewImage.Cursor = _currentPreviewCanDrag ? Cursors.SizeAll : Cursors.Arrow;

            ResultInfoText.Text = $"Result: {resultSize.Width}x{resultSize.Height}";
        }

        private static bool IsPositionedCropOperation(Operation? operation) =>
            operation is CoverOperation or CropResizeOperation or AspectCropOperation or AvatarExportOperation;

        private static bool IsIconPreviewOperation(Operation? operation) =>
            operation is ConvertOperation { Target: ConversionTarget.Ico } or FaviconBundleOperation;

        private void ShowIconPreviewStrip(BitmapSource preview, Operation? operation, AppSettings? settings)
        {
            _currentPreviewCanDrag = false;
            ImagePreviewViewbox.Visibility = Visibility.Collapsed;
            IconPreviewScroll.Visibility = Visibility.Visible;
            IconPreviewPanel.Children.Clear();
            OriginalImage.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewImage.Cursor = Cursors.Arrow;
            HideCropOverlay();

            foreach (var size in IconPreviewSizesFor(operation, settings))
            {
                var item = new StackPanel
                {
                    Margin = new Thickness(0, 0, 18, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var frame = new Border
                {
                    Width = size + 2,
                    Height = size + 2,
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    BorderThickness = new Thickness(1),
                    Child = new Image
                    {
                        Source = preview,
                        Width = size,
                        Height = size,
                        Stretch = Stretch.Fill,
                        SnapsToDevicePixels = true,
                    },
                };

                item.Children.Add(frame);
                item.Children.Add(new TextBlock
                {
                    Text = $"{size}x{size}",
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0),
                });
                IconPreviewPanel.Children.Add(item);
            }
        }

        private static IReadOnlyList<int> IconPreviewSizesFor(Operation? operation, AppSettings? settings)
        {
            var icoSizes = settings?.IcoSizes is { Count: > 0 }
                ? settings.IcoSizes
                : AppSettings.BuiltInIcoSizes;

            return operation switch
            {
                ConvertOperation { Target: ConversionTarget.Ico } => icoSizes.Distinct().OrderBy(size => size).ToList(),
                FaviconBundleOperation => icoSizes
                    .Concat(new[] { 16, 32, 48, 180, 192, 512 })
                    .Distinct()
                    .OrderBy(size => size)
                    .ToList(),
                _ => Array.Empty<int>(),
            };
        }

        private void SetCanvasSize(double width, double height)
        {
            ImageCanvas.Width = Math.Max(1, width);
            ImageCanvas.Height = Math.Max(1, height);
        }

        private static void PlaceImage(Image image, BitmapSource source, double left, double top, double width, double height)
        {
            image.Source = source;
            image.Width = Math.Max(1, width);
            image.Height = Math.Max(1, height);
            Canvas.SetLeft(image, left);
            Canvas.SetTop(image, top);
        }

        private void ShowCropOverlay(
            double sourceLeft,
            double sourceTop,
            double sourceWidth,
            double sourceHeight,
            double cropLeft,
            double cropTop,
            double cropWidth,
            double cropHeight)
        {
            var sourceRight = sourceLeft + sourceWidth;
            var sourceBottom = sourceTop + sourceHeight;
            var cropRight = cropLeft + cropWidth;
            var cropBottom = cropTop + cropHeight;

            SetDimRect(CropDimTop, sourceLeft, sourceTop, sourceWidth, Math.Max(0, cropTop - sourceTop));
            SetDimRect(CropDimBottom, sourceLeft, cropBottom, sourceWidth, Math.Max(0, sourceBottom - cropBottom));
            SetDimRect(CropDimLeft, sourceLeft, cropTop, Math.Max(0, cropLeft - sourceLeft), cropHeight);
            SetDimRect(CropDimRight, cropRight, cropTop, Math.Max(0, sourceRight - cropRight), cropHeight);
        }

        private void HideCropOverlay()
        {
            CropDimTop.Visibility = Visibility.Collapsed;
            CropDimBottom.Visibility = Visibility.Collapsed;
            CropDimLeft.Visibility = Visibility.Collapsed;
            CropDimRight.Visibility = Visibility.Collapsed;
        }

        private static void SetDimRect(System.Windows.Shapes.Rectangle rectangle, double left, double top, double width, double height)
        {
            if (width <= 0.5 || height <= 0.5)
            {
                rectangle.Visibility = Visibility.Collapsed;
                return;
            }

            rectangle.Width = width;
            rectangle.Height = height;
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            rectangle.Visibility = Visibility.Visible;
        }

        private PreviewPlacement GetPreviewPlacement(Operation? operation, PixelSize resultSize)
        {
            var sourceSize = new PixelSize(_sourceImage.PixelWidth, _sourceImage.PixelHeight);

            return operation switch
            {
                AspectCropOperation aspect => PlacementForCrop(ResizeMath.AspectCrop(
                    sourceSize,
                    aspect.RatioX,
                    aspect.RatioY,
                    aspect.PositionX,
                    aspect.PositionY)),
                CropResizeOperation cropResize => PlacementForCrop(cropResize.Crop),
                CoverOperation cover => PlacementForCover(cover.Box, cover.PositionX, cover.PositionY),
                AvatarExportOperation avatar => PlacementForCover(new PixelSize(avatar.Size, avatar.Size), avatar.PositionX, avatar.PositionY),
                FitOperation => new PreviewPlacement(0, 0, resultSize.Width, resultSize.Height, true, true),
                ConvertOperation { Target: ConversionTarget.Ico } => new PreviewPlacement(0, 0, resultSize.Width, resultSize.Height, false, true),
                FaviconBundleOperation => new PreviewPlacement(0, 0, resultSize.Width, resultSize.Height, false, true),
                SepiaOperation => new PreviewPlacement(0, 0, _sourceImage.PixelWidth, _sourceImage.PixelHeight, true, false),
                _ => new PreviewPlacement(0, 0, resultSize.Width, resultSize.Height, true, false),
            };
        }

        private static PreviewPlacement PlacementForCrop(PixelRect rect) =>
            new(rect.X, rect.Y, rect.Width, rect.Height, true, true);

        private PreviewPlacement PlacementForCover(PixelSize box, double positionX, double positionY)
        {
            var source = new PixelSize(_sourceImage.PixelWidth, _sourceImage.PixelHeight);
            var plan = ResizeMath.Cover(source, box, positionX, positionY);
            var scaleX = plan.Scaled.Width / (double)source.Width;
            var scaleY = plan.Scaled.Height / (double)source.Height;
            var left = Math.Clamp(plan.Crop.X / scaleX, 0, source.Width);
            var top = Math.Clamp(plan.Crop.Y / scaleY, 0, source.Height);
            var width = Math.Clamp(box.Width / scaleX, 1, source.Width - left);
            var height = Math.Clamp(box.Height / scaleY, 1, source.Height - top);
            return new PreviewPlacement(left, top, width, height, true, true);
        }

        private PixelSize EstimateResultSize(Operation operation, AppSettings settings)
        {
            var source = new PixelSize(_sourceImage.PixelWidth, _sourceImage.PixelHeight);

            return operation switch
            {
                ConvertOperation { Target: ConversionTarget.Ico } => SquarePreviewSize(settings.IcoSizes, 256),
                ConvertOperation => source,
                FitOperation fit => fit.KeepAspect ? ResizeMath.Fit(source, fit.Box) : fit.Box,
                CoverOperation cover => cover.Box,
                CropResizeOperation cropResize => cropResize.Output,
                AspectCropOperation aspect => SizeOf(ResizeMath.AspectCrop(source, aspect.RatioX, aspect.RatioY, aspect.PositionX, aspect.PositionY)),
                AvatarExportOperation avatar => new PixelSize(avatar.Size, avatar.Size),
                FaviconBundleOperation => new PixelSize(512, 512),
                _ => source,
            };
        }

        private static PixelSize SquarePreviewSize(IReadOnlyCollection<int> sizes, int fallback)
        {
            var size = sizes.Count > 0 ? sizes.Max() : fallback;
            return new PixelSize(size, size);
        }

        private static PixelSize SizeOf(PixelRect rect) => new(rect.Width, rect.Height);

        private static Operation ScaleOperationForPreview(Operation operation, double scale) => operation switch
        {
            FitOperation fit => new FitOperation(ScaleSize(fit.Box, scale), fit.KeepAspect),
            CoverOperation cover => new CoverOperation(ScaleSize(cover.Box, scale), cover.PositionX, cover.PositionY),
            CropResizeOperation cropResize => new CropResizeOperation(ScaleRect(cropResize.Crop, scale), ScaleSize(cropResize.Output, scale)),
            AvatarExportOperation avatar => new AvatarExportOperation(ScaleLength(avatar.Size, scale), avatar.PositionX, avatar.PositionY),
            _ => operation,
        };

        private static PixelSize ScaleSize(PixelSize size, double scale) =>
            new(ScaleLength(size.Width, scale), ScaleLength(size.Height, scale));

        private static PixelRect ScaleRect(PixelRect rect, double scale) =>
            new(ScalePosition(rect.X, scale), ScalePosition(rect.Y, scale), ScaleLength(rect.Width, scale), ScaleLength(rect.Height, scale));

        private static int ScaleLength(int value, double scale) =>
            Math.Max(1, (int)Math.Round(value * scale));

        private static int ScalePosition(int value, double scale) =>
            Math.Max(0, (int)Math.Round(value * scale));

        private BitmapSource GetPreviewSource()
        {
            var previewSize = PreviewSourceSize();
            if (_previewSource is not null && _previewSourceSize == previewSize)
            {
                return _previewSource;
            }

            _previewSourceSize = previewSize;
            if (previewSize.Width == _sourceImage.PixelWidth && previewSize.Height == _sourceImage.PixelHeight)
            {
                _previewSource = _sourceImage;
                return _previewSource;
            }

            var scaled = new TransformedBitmap(
                _sourceImage,
                new ScaleTransform(
                    previewSize.Width / (double)_sourceImage.PixelWidth,
                    previewSize.Height / (double)_sourceImage.PixelHeight));
            _previewSource = ImageConverter.CreateThreadSafeCopy(scaled);
            return _previewSource;
        }

        private PixelSize PreviewSourceSize()
        {
            var availableWidth = PreviewHost.ActualWidth - PreviewMargin;
            var availableHeight = PreviewHost.ActualHeight - PreviewMargin;
            double scale;

            if (availableWidth >= 64 && availableHeight >= 64)
            {
                scale = Math.Min(availableWidth / _sourceImage.PixelWidth, availableHeight / _sourceImage.PixelHeight);
            }
            else
            {
                scale = PreviewFallbackMaxSide / Math.Max(_sourceImage.PixelWidth, _sourceImage.PixelHeight);
            }

            scale = Math.Clamp(scale, 0.01, 1);
            return new PixelSize(
                Math.Max(1, (int)Math.Round(_sourceImage.PixelWidth * scale)),
                Math.Max(1, (int)Math.Round(_sourceImage.PixelHeight * scale)));
        }

        private bool TryBuildOperation(out Operation operation, out AppSettings settings, out string message)
        {
            operation = new StripMetadataOperation();
            settings = BuildSettings();
            message = "";

            switch (CurrentToolId())
            {
                case "Convert":
                    var target = SelectedConvertTarget();
                    operation = new ConvertOperation(target);
                    if (target == ConversionTarget.Ico && !TryApplyIcoSizes(settings, out message))
                    {
                        return false;
                    }
                    return true;

                case "Fit":
                    if (!TryReadSize(FitWidthBox, FitHeightBox, out var fit, out message))
                    {
                        return false;
                    }
                    operation = new FitOperation(fit, FitKeepAspectToggle.IsChecked == true);
                    return true;

                case "Cover":
                    if (!TryReadCropRect(out var crop, out message)
                        || !TryReadSize(CoverResizeWidthBox, CoverResizeHeightBox, out var output, out message))
                    {
                        return false;
                    }
                    operation = new CropResizeOperation(crop, output);
                    return true;

                case "Aspect":
                    if (!TryReadPositiveInt(AspectXBox, "aspect X", out var x, out message)
                        || !TryReadPositiveInt(AspectYBox, "aspect Y", out var y, out message))
                    {
                        return false;
                    }
                    if (!TryReadPercent(AspectPositionXBox, "crop X", out var aspectX, out message)
                        || !TryReadPercent(AspectPositionYBox, "crop Y", out var aspectY, out message))
                    {
                        return false;
                    }
                    operation = new AspectCropOperation(x, y, aspectX, aspectY);
                    return true;

                case "Avatar":
                    if (!TryReadPositiveInt(AvatarSizeBox, "avatar size", out var avatarSize, out message))
                    {
                        return false;
                    }
                    if (!TryReadPercent(AvatarPositionXBox, "crop X", out var avatarX, out message)
                        || !TryReadPercent(AvatarPositionYBox, "crop Y", out var avatarY, out message))
                    {
                        return false;
                    }
                    operation = new AvatarExportOperation(avatarSize, avatarX, avatarY);
                    return true;

                case "Sepia":
                    var intensity = (int)Math.Round(SepiaIntensitySlider.Value);
                    var hue = (int)Math.Round(SepiaHueSlider.Value);
                    operation = new SepiaOperation(intensity, hue);
                    return true;

                case "Compress":
                    operation = new CompressJpegOperation();
                    return true;

                case "OptimizePng":
                    operation = new OptimizePngOperation();
                    return true;

                case "Metadata":
                    operation = new StripMetadataOperation();
                    return true;

                case "Favicon":
                    if (!TryApplyIcoSizes(settings, out message))
                    {
                        return false;
                    }
                    operation = new FaviconBundleOperation();
                    return true;

                default:
                    message = "Select a tool.";
                    return false;
            }
        }

        private AppSettings BuildSettings() => new()
        {
            JpegQuality = (int)JpegQualitySlider.Value,
            WebPQuality = (int)WebPQualitySlider.Value,
            AvifQuality = (int)AvifQualitySlider.Value,
            TransparencyBackground = string.IsNullOrWhiteSpace(BackgroundBox.Text) ? "#ffffff" : BackgroundBox.Text.Trim(),
            IcoSizes = IcoSizeValues(),
            UseSubfolder = _baseSettings.UseSubfolder,
            SubfolderName = _baseSettings.SubfolderName,
            OutputNamePattern = _baseSettings.OutputNamePattern,
            CropAnchor = _baseSettings.CropAnchor,
            PreserveMetadata = _baseSettings.PreserveMetadata,
            AvatarSizes = _baseSettings.AvatarSizes.ToList(),
            SepiaIntensities = _baseSettings.SepiaIntensities.ToList(),
            FitSizes = _baseSettings.FitSizes.ToList(),
            CoverSizes = _baseSettings.CoverSizes.ToList(),
            AspectRatios = _baseSettings.AspectRatios.ToList(),
            EnableConvertToJpg = _baseSettings.EnableConvertToJpg,
            EnableConvertToPng = _baseSettings.EnableConvertToPng,
            EnableConvertToWebP = _baseSettings.EnableConvertToWebP,
            EnableConvertToAvif = _baseSettings.EnableConvertToAvif,
            EnableConvertToIco = _baseSettings.EnableConvertToIco,
            EnableRemoveMetadata = _baseSettings.EnableRemoveMetadata,
            EnableCompressJpeg = _baseSettings.EnableCompressJpeg,
            EnableOptimizePng = _baseSettings.EnableOptimizePng,
            EnableFavicon = _baseSettings.EnableFavicon,
            EnablePastePng = _baseSettings.EnablePastePng,
            EnablePasteJpg = _baseSettings.EnablePasteJpg,
        };

        private bool TryApplyIcoSizes(AppSettings settings, out string message)
        {
            var sizes = IcoSizeValues();
            if (sizes.Count == 0)
            {
                message = "Add at least one icon size.";
                return false;
            }

            settings.IcoSizes = sizes;
            message = "";
            return true;
        }

        private List<int> IcoSizeValues() =>
            IcoSizesList.Sizes.ToList();

        private ConversionTarget SelectedConvertTarget()
        {
            if (ConvertTargetBox.SelectedItem is ComboBoxItem { Tag: string tag }
                && Enum.TryParse<ConversionTarget>(tag, out var target))
            {
                return target;
            }

            return ConversionTarget.Png;
        }

        private static bool TryReadSize(TextBox widthBox, TextBox heightBox, out PixelSize size, out string message)
        {
            size = default;

            if (!TryReadPositiveInt(widthBox, "width", out var width, out message)
                || !TryReadPositiveInt(heightBox, "height", out var height, out message))
            {
                return false;
            }

            size = new PixelSize(width, height);
            return true;
        }

        private bool TryReadCropRect(out PixelRect crop, out string message)
        {
            crop = default;

            if (!TryReadBoundedInt(CoverWidthBox, "crop width", 1, _sourceImage.PixelWidth, out var width, out message)
                || !TryReadBoundedInt(CoverHeightBox, "crop height", 1, _sourceImage.PixelHeight, out var height, out message))
            {
                return false;
            }

            var maxX = Math.Max(0, _sourceImage.PixelWidth - width);
            var maxY = Math.Max(0, _sourceImage.PixelHeight - height);
            if (!TryReadBoundedInt(CoverPositionXBox, "crop X", 0, maxX, out var x, out message)
                || !TryReadBoundedInt(CoverPositionYBox, "crop Y", 0, maxY, out var y, out message))
            {
                return false;
            }

            crop = new PixelRect(x, y, width, height);
            return true;
        }

        private static bool TryReadPercent(TextBox box, string name, out double value, out string message)
        {
            if (double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                && percent is >= 0 and <= 100)
            {
                value = percent / 100.0;
                message = "";
                return true;
            }

            value = 0.5;
            message = $"Enter a {name} from 0 to 100.";
            return false;
        }

        private static bool TryReadPositiveInt(TextBox box, string name, out int value, out string message) =>
            TryReadBoundedInt(box, name, 1, null, out value, out message);

        private static bool TryReadBoundedInt(TextBox box, string name, int min, int? max, out int value, out string message)
        {
            if (int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= min
                && (max is null || value <= max.Value))
            {
                message = "";
                return true;
            }

            message = max is null
                ? $"Enter a {name} of at least {min}."
                : $"Enter a {name} from {min} to {max.Value}.";
            return false;
        }

        private string ExecuteOperation(Operation operation, AppSettings settings) => operation switch
        {
            ConvertOperation convert => ImageConverter.Convert(_imagePath, convert.Target, settings.JpegQuality, settings),
            FitOperation fit => ImageConverter.ResizeToFit(_imagePath, fit.Box, settings.JpegQuality, settings, fit.KeepAspect),
            CoverOperation cover => ImageConverter.CropToSize(_imagePath, cover.Box, cover.PositionX, cover.PositionY, settings.JpegQuality, settings),
            CropResizeOperation cropResize => ImageConverter.CropAndResize(_imagePath, cropResize.Crop, cropResize.Output, settings.JpegQuality, settings),
            AspectCropOperation aspect => ImageConverter.CropToAspect(_imagePath, aspect.RatioX, aspect.RatioY, aspect.PositionX, aspect.PositionY, settings.JpegQuality, settings),
            StripMetadataOperation => ImageConverter.StripMetadata(_imagePath, settings),
            SepiaOperation sepia => ImageConverter.ApplySepia(_imagePath, sepia.Intensity, settings, sepia.Hue),
            CompressJpegOperation => ImageConverter.CompressJpeg(_imagePath, settings),
            OptimizePngOperation => ImageConverter.OptimizePng(_imagePath, settings),
            FaviconBundleOperation => ImageConverter.CreateFaviconBundle(_imagePath, settings),
            AvatarExportOperation avatar => ImageConverter.MakeAvatar(_imagePath, avatar.Size, avatar.PositionX, avatar.PositionY, settings),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                var next = a % b;
                a = b;
                b = next;
            }

            return Math.Max(1, Math.Abs(a));
        }

        private void SaveAs(Operation operation, AppSettings settings)
        {
            var preview = ImageConverter.RenderPreview(_sourceImage, operation, settings);
            var target = SaveAsTarget(operation);
            var defaultPath = DefaultOutputPath(operation, preview, target, settings);
            var dialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckPathExists = true,
                FileName = Path.GetFileName(defaultPath),
                Filter = FilterForTarget(target),
                InitialDirectory = Path.GetDirectoryName(defaultPath),
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            ImageConverter.SaveImage(preview, dialog.FileName, target, settings.JpegQuality, settings);
            StatusText.Text = $"Saved {Path.GetFileName(dialog.FileName)}.";
        }

        private ConversionTarget SaveAsTarget(Operation operation) => operation switch
        {
            ConvertOperation convert => convert.Target,
            CompressJpegOperation => ConversionTarget.Jpeg,
            OptimizePngOperation => ConversionTarget.Png,
            AvatarExportOperation => ConversionTarget.Png,
            FaviconBundleOperation => ConversionTarget.Ico,
            _ => ImageConverter.TargetForSource(Path.GetExtension(_imagePath)),
        };

        private string DefaultOutputPath(Operation operation, BitmapSource preview, ConversionTarget target, AppSettings settings)
        {
            var suffix = operation switch
            {
                ConvertOperation => target.FileExtension(),
                FitOperation or CoverOperation or CropResizeOperation or AspectCropOperation => $".{preview.PixelWidth}x{preview.PixelHeight}{target.FileExtension()}",
                AvatarExportOperation avatar => $".{avatar.Size}x{avatar.Size}.png",
                SepiaOperation sepia => sepia.Hue == ImageConverter.StandardSepiaHue
                    ? $".sepia{sepia.Intensity}{target.FileExtension()}"
                    : $".sepia{sepia.Intensity}h{sepia.Hue}{target.FileExtension()}",
                StripMetadataOperation => $".clean{target.FileExtension()}",
                CompressJpegOperation => ".compressed.jpg",
                OptimizePngOperation => ".optimized.png",
                FaviconBundleOperation => ".ico",
                _ => target.FileExtension(),
            };

            return OutputPathResolver.GetUniquePath(_imagePath, suffix, settings, preview.PixelWidth, preview.PixelHeight);
        }

        private string DefaultOutputFileName(Operation operation, int previewWidth, int previewHeight, ConversionTarget target, AppSettings settings)
        {
            var suffix = operation switch
            {
                ConvertOperation => target.FileExtension(),
                FitOperation or CoverOperation or CropResizeOperation or AspectCropOperation => $".{previewWidth}x{previewHeight}{target.FileExtension()}",
                AvatarExportOperation avatar => $".{avatar.Size}x{avatar.Size}.png",
                SepiaOperation sepia => sepia.Hue == ImageConverter.StandardSepiaHue
                    ? $".sepia{sepia.Intensity}{target.FileExtension()}"
                    : $".sepia{sepia.Intensity}h{sepia.Hue}{target.FileExtension()}",
                StripMetadataOperation => $".clean{target.FileExtension()}",
                CompressJpegOperation => ".compressed.jpg",
                OptimizePngOperation => ".optimized.png",
                FaviconBundleOperation => ".ico",
                _ => target.FileExtension(),
            };

            var baseName = Path.GetFileNameWithoutExtension(_imagePath);
            if (!string.IsNullOrEmpty(settings.OutputNamePattern) && IsSimpleExtension(suffix))
            {
                return OutputPathResolver.ApplyPattern(
                    settings.OutputNamePattern,
                    baseName,
                    suffix,
                    previewWidth,
                    previewHeight);
            }

            return baseName + suffix;
        }

        private static bool IsSimpleExtension(string extension) =>
            extension.StartsWith('.') && !extension.TrimStart('.').Contains('.');

        private static string FilterForTarget(ConversionTarget target) => target switch
        {
            ConversionTarget.Jpeg => "JPEG image (*.jpg)|*.jpg",
            ConversionTarget.Png => "PNG image (*.png)|*.png",
            ConversionTarget.WebP => "WebP image (*.webp)|*.webp",
            ConversionTarget.Avif => "AVIF image (*.avif)|*.avif",
            ConversionTarget.Ico => "Icon file (*.ico)|*.ico",
            _ => "Image file|*.*",
        };

        private void ToolList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetToolPanel(CurrentToolId());
            RefreshPreview();
        }

        private void PreviewToggle_Changed(object sender, RoutedEventArgs e) => RefreshPreview();

        private void RefreshOnToggleChanged(object sender, RoutedEventArgs e) => RefreshPreview();

        private void RefreshOnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_suppressPreviewRefresh)
            {
                RefreshPreview();
            }
        }

        private void RefreshOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetSharedOptionVisibility(CurrentToolId());
            RefreshPreview();
        }

        private void RefreshOnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => RefreshPreview();

        private void SepiaControl_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSepiaVisuals();
            if (!_suppressPreviewRefresh)
            {
                RefreshPreview();
            }
        }

        private void SepiaDefault_Click(object sender, RoutedEventArgs e)
        {
            _suppressPreviewRefresh = true;
            try
            {
                SepiaIntensitySlider.Value = DefaultSepiaIntensity;
                SepiaHueSlider.Value = ImageConverter.StandardSepiaHue;
            }
            finally
            {
                _suppressPreviewRefresh = false;
            }

            UpdateSepiaVisuals();
            RefreshPreview();
        }

        private void CenterCrop_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentToolId() == "Cover")
            {
                var cropSize = CurrentCoverCropSize();
                SetCurrentCropPixelPosition(
                    Math.Max(0, (_sourceImage.PixelWidth - cropSize.Width) / 2),
                    Math.Max(0, (_sourceImage.PixelHeight - cropSize.Height) / 2));
                return;
            }

            SetCurrentCropPosition(0.5, 0.5);
        }

        private void UpdateSepiaVisuals()
        {
            if (SepiaIntensityValueText is null || SepiaHueValueText is null || SepiaHueSwatch is null)
            {
                return;
            }

            var intensity = (int)Math.Round(SepiaIntensitySlider.Value);
            var hue = (int)Math.Round(SepiaHueSlider.Value);
            SepiaIntensityValueText.Text = $"{intensity}%";
            SepiaHueValueText.Text = hue == ImageConverter.StandardSepiaHue
                ? "Classic"
                : hue.ToString(CultureInfo.InvariantCulture);
            SepiaHueSwatch.Background = new SolidColorBrush(ColorFromSepiaTone(hue));
        }

        private static Color ColorFromSepiaTone(int hue)
        {
            const double classicHue = 0.091;
            const double classicSaturation = 0.44;
            const double classicLightness = 0.58;
            var offset = (NormalizeHue(hue) - ImageConverter.StandardSepiaHue) / 360.0;
            var (r, g, b) = HslToRgb(NormalizeUnitHue(classicHue + offset), classicSaturation, classicLightness);

            return Color.FromRgb(
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255));
        }

        private static int NormalizeHue(int hue) =>
            ((hue % 360) + 360) % 360;

        private static double NormalizeUnitHue(double hue)
        {
            hue %= 1;
            return hue < 0 ? hue + 1 : hue;
        }

        private static (double R, double G, double B) HslToRgb(double hue, double saturation, double lightness)
        {
            if (saturation <= 0)
            {
                return (lightness, lightness, lightness);
            }

            var q = lightness < 0.5
                ? lightness * (1 + saturation)
                : lightness + saturation - lightness * saturation;
            var p = 2 * lightness - q;

            return (
                HueToRgb(p, q, hue + 1.0 / 3.0),
                HueToRgb(p, q, hue),
                HueToRgb(p, q, hue - 1.0 / 3.0));
        }

        private static double HueToRgb(double p, double q, double hue)
        {
            if (hue < 0) hue += 1;
            if (hue > 1) hue -= 1;
            if (hue < 1.0 / 6.0) return p + (q - p) * 6 * hue;
            if (hue < 1.0 / 2.0) return q;
            if (hue < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - hue) * 6;
            return p;
        }

        private void NumericLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBlock { Tag: TextBox textBox })
            {
                return;
            }

            if (!double.TryParse(textBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                value = 0;
            }

            _dragTextBox = textBox;
            _dragStartPoint = e.GetPosition(this);
            _dragStartValue = value;
            Mouse.OverrideCursor = Cursors.SizeWE;
            ((TextBlock)sender).CaptureMouse();
            e.Handled = true;
        }

        private void NumericLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragTextBox is null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var delta = e.GetPosition(this).X - _dragStartPoint.X;
            var multiplier = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            var value = _dragStartValue + delta * multiplier;
            var (min, max) = NumericRangeFor(_dragTextBox);
            value = Math.Max(min, value);
            if (max is not null)
            {
                value = Math.Min(max.Value, value);
            }

            _dragTextBox.Text = IsPositionTextBox(_dragTextBox)
                ? value.ToString("0.0", CultureInfo.InvariantCulture)
                : Math.Round(value).ToString(CultureInfo.InvariantCulture);
        }

        private void NumericLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock label)
            {
                label.ReleaseMouseCapture();
            }

            _dragTextBox = null;
            Mouse.OverrideCursor = null;
            e.Handled = true;
        }

        private static (int Min, int? Max) NumericRangeFor(TextBox textBox) =>
            textBox.Name switch
            {
                nameof(CoverPositionXBox) or nameof(CoverPositionYBox) => (0, null),
                nameof(AspectPositionXBox) or nameof(AspectPositionYBox) => (0, 100),
                nameof(AvatarPositionXBox) or nameof(AvatarPositionYBox) => (0, 100),
                _ => (1, null),
            };

        private static bool IsPositionTextBox(TextBox textBox) =>
            textBox.Name is nameof(AspectPositionXBox) or nameof(AspectPositionYBox)
                or nameof(AvatarPositionXBox) or nameof(AvatarPositionYBox);

        private void IcoSizesList_SizesChanged(object? sender, EventArgs e)
        {
            StatusText.Text = IcoSizesList.StatusMessage;
            RefreshPreview();
        }

        private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_currentPreviewCanDrag)
            {
                return;
            }

            _cropDragActive = true;
            _cropDragStartPoint = e.GetPosition(ImageCanvas);
            _cropDragStartPlacement = _currentPreviewPlacement;
            PreviewImage.CaptureMouse();
            Mouse.OverrideCursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_cropDragActive || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var point = e.GetPosition(ImageCanvas);
            var deltaX = point.X - _cropDragStartPoint.X;
            var deltaY = point.Y - _cropDragStartPoint.Y;
            var maxLeft = Math.Max(0, _sourceImage.PixelWidth - _cropDragStartPlacement.Width);
            var maxTop = Math.Max(0, _sourceImage.PixelHeight - _cropDragStartPlacement.Height);
            var left = Math.Clamp(_cropDragStartPlacement.Left + deltaX, 0, maxLeft);
            var top = Math.Clamp(_cropDragStartPlacement.Top + deltaY, 0, maxTop);
            if (CurrentToolId() == "Cover")
            {
                SetCurrentCropPixelPosition((int)Math.Round(left), (int)Math.Round(top));
            }
            else
            {
                var positionX = maxLeft > 0 ? left / maxLeft : 0.5;
                var positionY = maxTop > 0 ? top / maxTop : 0.5;
                SetCurrentCropPosition(positionX, positionY);
            }
            e.Handled = true;
        }

        private void PreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_cropDragActive)
            {
                return;
            }

            _cropDragActive = false;
            PreviewImage.ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
            e.Handled = true;
        }

        private void SetCurrentCropPosition(double positionX, double positionY)
        {
            var xText = (Math.Clamp(positionX, 0, 1) * 100).ToString("0.0", CultureInfo.InvariantCulture);
            var yText = (Math.Clamp(positionY, 0, 1) * 100).ToString("0.0", CultureInfo.InvariantCulture);

            _suppressPreviewRefresh = true;
            try
            {
                switch (CurrentToolId())
                {
                    case "Aspect":
                        AspectPositionXBox.Text = xText;
                        AspectPositionYBox.Text = yText;
                        break;
                    case "Avatar":
                        AvatarPositionXBox.Text = xText;
                        AvatarPositionYBox.Text = yText;
                        break;
                }
            }
            finally
            {
                _suppressPreviewRefresh = false;
            }

            RefreshPreview();
        }

        private void SetCurrentCropPixelPosition(int x, int y)
        {
            var cropSize = CurrentCoverCropSize();
            var maxX = Math.Max(0, _sourceImage.PixelWidth - cropSize.Width);
            var maxY = Math.Max(0, _sourceImage.PixelHeight - cropSize.Height);

            _suppressPreviewRefresh = true;
            try
            {
                CoverPositionXBox.Text = Math.Clamp(x, 0, maxX).ToString(CultureInfo.InvariantCulture);
                CoverPositionYBox.Text = Math.Clamp(y, 0, maxY).ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                _suppressPreviewRefresh = false;
            }

            RefreshPreview();
        }

        private PixelSize CurrentCoverCropSize()
        {
            if (!TryReadBoundedInt(CoverWidthBox, "crop width", 1, _sourceImage.PixelWidth, out var width, out _))
            {
                width = _sourceImage.PixelWidth;
            }

            if (!TryReadBoundedInt(CoverHeightBox, "crop height", 1, _sourceImage.PixelHeight, out var height, out _))
            {
                height = _sourceImage.PixelHeight;
            }

            return new PixelSize(width, height);
        }

        private void PreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _previewSource = null;
            _previewCache.Clear();
            if (_initialized)
            {
                RefreshPreview();
            }
        }

        private void SaveDropButton_Click(object sender, RoutedEventArgs e)
        {
            SaveDropMenu.PlacementTarget = SaveDropButton;
            SaveDropMenu.IsOpen = true;
        }

        private void SaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildOperation(out var operation, out var settings, out var message))
            {
                StatusText.Text = message;
                return;
            }

            try
            {
                var output = ExecuteOperation(operation, settings);
                StatusText.Text = Directory.Exists(output)
                    ? $"Created {Path.GetFileName(output)}."
                    : $"Saved {Path.GetFileName(output)}.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Save failed: {ex.Message}";
            }
        }

        private void SaveAsResult_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildOperation(out var operation, out var settings, out var message))
            {
                StatusText.Text = message;
                return;
            }

            try
            {
                SaveAs(operation, settings);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Save as failed: {ex.Message}";
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var window = new MainWindow
            {
                Owner = this,
            };

            window.ShowDialog();
            _baseSettings = AppSettings.Load();
            PopulateIcoSizeList(_baseSettings.IcoSizes);
            RefreshPreview();
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow
            {
                Owner = this,
            }.ShowDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private static void FreezeIfPossible(BitmapSource source)
        {
            if (source.CanFreeze && !source.IsFrozen)
            {
                source.Freeze();
            }
        }

        private readonly record struct PreviewRenderResult(BitmapSource Preview);

        private readonly record struct PreviewCacheKey(
            Operation Operation,
            int JpegQuality,
            int WebPQuality,
            int AvifQuality,
            string Background,
            string IcoSizes,
            PixelSize PreviewSourceSize)
        {
            public static PreviewCacheKey From(Operation operation, AppSettings settings, PixelSize previewSourceSize) =>
                new(
                    operation,
                    settings.JpegQuality,
                    settings.WebPQuality,
                    settings.AvifQuality,
                    settings.TransparencyBackground,
                    string.Join(",", settings.IcoSizes),
                    previewSourceSize);
        }

        private readonly record struct PreviewPlacement(
            double Left,
            double Top,
            double Width,
            double Height,
            bool RelativeToSource,
            bool ShowDimmedSource);
    }
}
