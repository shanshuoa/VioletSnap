using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Interop;
using ScreenshotTool.Core.Annotation;
using ScreenshotTool.Core.Annotation.Models;
using ScreenshotTool.Core.Annotation.UndoRedo;
using ScreenshotTool.Core.Capture;
using ScreenshotTool.Core.Pin;
using ScreenshotTool.Core.Win32;
using ScreenshotTool.Core.Ocr;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Rectangle = System.Windows.Shapes.Rectangle;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows.Threading;
using System.Windows.Media.Effects;
using System.Globalization;

namespace ScreenshotTool.App.Views;

public partial class PinWindow : Window
{
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(183, 156, 255));
    private static readonly Brush SelectedToolBrush = new SolidColorBrush(Color.FromRgb(118, 82, 213));
    private static readonly Brush ToolbarTextBrush = new SolidColorBrush(Color.FromRgb(36, 40, 44));
    private static readonly Brush ActionBackgroundBrush = new SolidColorBrush(Color.FromRgb(241, 237, 255));
    private static readonly Brush ActionForegroundBrush = new SolidColorBrush(Color.FromRgb(94, 61, 181));
    private static readonly Typeface ResultTextTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private readonly BitmapSource _image;
    private readonly IAnnotationRenderer _annotationRenderer;
    private readonly PinViewState _viewState = new();
    private readonly CaptureRegion? _initialRegion;
    private readonly bool _initialAnnotationMode;
    private readonly List<AnnotationItem> _annotations = [];
    private readonly UndoRedoManager _history = new();
    private double _dpiScaleX = 1;
    private double _dpiScaleY = 1;
    private AnnotationTool _tool = AnnotationTool.Select;
    private AnnotationItem? _draft;
    private Point _start;
    private Color _color = Colors.Red;
    private double _thickness = 3;
    private double _fontSize = 20;
    private string _fontFamily = "Microsoft YaHei";
    private FontWeight _fontWeight = FontWeights.Normal;
    private double _effectStrength = 12;
    private TextBox? _textEditor;
    private TextAnnotation? _editingText;
    private AnnotationSnapshot? _beforeTextEdit;
    private AnnotationItem? _transformItem;
    private AnnotationSnapshot? _beforeTransform;
    private Point _transformStart;
    private ResizeCorner? _resizeCorner;
    private Button? _activeToolButton;
    private OcrResult? _translationOcrResult;
    private string _translatedText = string.Empty;
    private bool _showTranslatedText;
    private bool _translationLoading;

    public PinWindow(BitmapSource image, IAnnotationRenderer annotationRenderer, CaptureRegion? initialRegion = null, bool initialAnnotationMode = false)
    {
        InitializeComponent();
        _image = image;
        _annotationRenderer = annotationRenderer;
        _initialRegion = initialRegion;
        _initialAnnotationMode = initialAnnotationMode;
        PinnedImage.Source = image;
        WindowStartupLocation = initialRegion is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.Manual;
        SourceInitialized += (_, _) =>
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX; _dpiScaleY = dpi.DpiScaleY;
            var workArea = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            _viewState.SetScale(Math.Min(1, Math.Min(workArea.Width * .7 / image.PixelWidth, workArea.Height * .7 / image.PixelHeight)));
            ApplyViewState(); ApplyInitialPosition();
        };
        Loaded += (_, _) =>
        {
            ApplyInitialPosition();
            SetAnnotationMode(_initialAnnotationMode, notify: false);
            PinActionToolbar.IsOpen = true;
            Dispatcher.BeginInvoke(RepositionToolbar, DispatcherPriority.Loaded);
        };
        LocationChanged += (_, _) => RepositionToolbar();
        SizeChanged += (_, _) => RepositionToolbar();
        IsVisibleChanged += (_, _) =>
        {
            PinActionToolbar.IsOpen = IsVisible;
            AnnotationToolbar.IsOpen = IsVisible && AnnotationMenuItem.IsChecked;
        };
        Closed += (_, _) => { PinActionToolbar.IsOpen = false; AnnotationToolbar.IsOpen = false; };
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _dpiScaleX = newDpi.DpiScaleX; _dpiScaleY = newDpi.DpiScaleY;
        if (IsLoaded) ApplyViewState();
    }

    public BitmapSource Image => _image;
    public Action<PinWindow> CaptureRestoreState()
    {
        CommitText();
        var annotations = _annotations.ToArray();
        var result = _translationOcrResult;
        var text = _translatedText;
        var show = _showTranslatedText;
        var scale = _viewState.Scale;
        return restored =>
        {
            foreach (var item in annotations)
                restored._history.Execute(new AddAnnotationCommand(restored._annotations, item, restored._annotations.Count));
            restored._viewState.SetScale(scale);
            if (result is not null && text.Length > 0)
            {
                restored.ShowTranslationResult(result, text);
                restored.SetTranslationVisibility(show);
            }
            restored.ApplyViewState();
        };
    }
    public BitmapSource RecognitionImage => _annotations.Count == 0 ? _image : _annotationRenderer.Render(_image, _annotations);
    public BitmapSource RenderedImage => _showTranslatedText && _translationOcrResult is not null && !string.IsNullOrWhiteSpace(_translatedText)
        ? RenderTranslationToBitmap(RecognitionImage)
        : RecognitionImage;
    public event EventHandler? CopyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? OcrRequested;
    public event EventHandler? TranslateRequested;
    public event EventHandler<bool>? AnnotationModeChanged;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (LockPositionMenuItem.IsChecked) return;
        if (AnnotationMenuItem.IsChecked) return;
        if (eventArgs.ClickCount >= 2) { Close(); return; }
        if (eventArgs.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) _viewState.AdjustOpacity(eventArgs.Delta > 0);
        else _viewState.Zoom(eventArgs.Delta > 0);
        ApplyViewState(); eventArgs.Handled = true;
    }

    private void ApplyViewState()
    {
        Width = Math.Max(1, _image.PixelWidth * _viewState.Scale / _dpiScaleX);
        var imageHeight = Math.Max(1, _image.PixelHeight * _viewState.Scale / _dpiScaleY);
        var translatedHeight = _showTranslatedText && _translationOcrResult is not null
            ? RenderTranslationToBitmap(RecognitionImage).PixelHeight * Width / RenderTranslationToBitmap(RecognitionImage).PixelWidth : 0;
        Height = _translationLoading
            ? imageHeight + 60
            : _showTranslatedText && _translationOcrResult is not null ? imageHeight + translatedHeight + 8 : imageHeight;
        TranslationGapRow.Height = _translationLoading || (_showTranslatedText && _translationOcrResult is not null)
            ? new GridLength(8)
            : new GridLength(0);
        TranslationRow.Height = _translationLoading
            ? new GridLength(52)
            : _showTranslatedText && _translationOcrResult is not null ? new GridLength(translatedHeight) : new GridLength(0);
        ScaleStatus.Text = $"{_viewState.Scale:P0}";
        Opacity = _viewState.Opacity;
        RedrawAnnotations();
        RedrawTranslation();
    }

    private void ApplyInitialPosition()
    {
        if (_initialRegion is not { } region) return;
        var handle = new WindowInteropHelper(this).Handle;
        var width = (int)Math.Round(_image.PixelWidth * _viewState.Scale);
        var height = (int)Math.Round(_image.PixelHeight * _viewState.Scale);
        if (!NativeMethods.SetWindowPos(handle, new IntPtr(-1), region.X, region.Y, width, height, NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow))
            NativeMethods.ThrowLastWin32Error("无法定位贴图窗口。");
    }

    private void OnAnnotationClicked(object sender, RoutedEventArgs eventArgs) => SetAnnotationMode(AnnotationMenuItem.IsChecked, notify: true);

    private void SetAnnotationMode(bool enabled, bool notify)
    {
        AnnotationMenuItem.IsChecked = enabled;
        AnnotationCanvas.IsHitTestVisible = enabled;
        AnnotationToolbar.IsOpen = enabled;
        SetActionButtonSelected(PinnedAnnotationButton, enabled);
        if (!enabled)
        {
            CommitText();
            ClearActiveToolSelection();
            _tool = AnnotationTool.Select;
            foreach (var item in _annotations) item.IsSelected = false;
        }
        if (enabled)
        {
            Dispatcher.BeginInvoke(RepositionToolbar, DispatcherPriority.Loaded);
        }
        RedrawAnnotations();
        if (notify) AnnotationModeChanged?.Invoke(this, enabled);
    }

    private void OnToolClicked(object sender, RoutedEventArgs eventArgs)
    {
        CommitText();
        if (!AnnotationMenuItem.IsChecked) SetAnnotationMode(true, notify: true);
        var button = (Button)sender;
        if (ReferenceEquals(_activeToolButton, button))
        {
            ClearActiveToolSelection();
            _tool = AnnotationTool.Select;
            foreach (var item in _annotations) item.IsSelected = false;
            RedrawAnnotations();
            return;
        }
        _tool = Enum.Parse<AnnotationTool>((string)button.Tag);
        ClearActiveToolSelection();
        button.Background = SelectedToolBrush;
        button.Foreground = Brushes.White;
        _activeToolButton = button;
    }

    private void ClearActiveToolSelection()
    {
        if (_activeToolButton is null) return;
        _activeToolButton.Background = Brushes.Transparent;
        _activeToolButton.Foreground = ToolbarTextBrush;
        _activeToolButton = null;
    }

    private static void SetActionButtonSelected(Button button, bool selected)
    {
        button.Background = selected ? SelectedToolBrush : ActionBackgroundBrush;
        button.Foreground = selected ? Brushes.White : ActionForegroundBrush;
        button.BorderBrush = selected ? SelectedToolBrush : new SolidColorBrush(Color.FromRgb(216, 204, 250));
        button.BorderThickness = selected ? new Thickness(0) : new Thickness(1);
    }

    private void OnPinnedAnnotationClicked(object sender, RoutedEventArgs eventArgs)
    {
        SetAnnotationMode(!AnnotationMenuItem.IsChecked, notify: true);
    }

    private void OnColorClicked(object sender, RoutedEventArgs eventArgs)
    {
        if (((Button)sender).Tag?.ToString() == "Custom")
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            _color = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        }
        else
        _color = ((Button)sender).Tag?.ToString() switch
        {
            "Blue" => Colors.DodgerBlue,
            "Yellow" => Colors.Gold,
            "Purple" => Color.FromRgb(118, 82, 213),
            "Black" => Colors.Black,
            "White" => Colors.White,
            _ => Colors.Red
        };
        var selected = _annotations.LastOrDefault(item => item.IsSelected);
        if (selected is null) return;
        var before = AnnotationSnapshot.Capture(selected);
        selected.StrokeColor = _color;
        if (selected is TextAnnotation text) text.TextColor = _color;
        RecordChange(selected, before);
        RedrawAnnotations();
    }

    private void OnColorChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((System.Windows.Controls.ComboBox)sender).SelectedItem is ComboBoxItem item)
            OnColorClicked(new Button { Tag = item.Tag }, new RoutedEventArgs());
    }

    private void OnThicknessChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (ThicknessBox.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Content.ToString()?.Split(' ')[0], out var value))
        {
            _thickness = value;
            var selected = _annotations.LastOrDefault(annotation => annotation.IsSelected);
            if (selected is not null)
            {
                var before = AnnotationSnapshot.Capture(selected);
                selected.StrokeThickness = value;
                RecordChange(selected, before);
                RedrawAnnotations();
            }
        }
    }

    private void OnAnnotationMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        CommitText();
        var point = ToImagePoint(eventArgs.GetPosition(AnnotationCanvas));
        if (_tool == AnnotationTool.Select)
        {
            var alreadySelected = _annotations.LastOrDefault(item => item.IsSelected);
            var corner = alreadySelected is null ? null : HitResizeCorner(alreadySelected, point);
            var selected = corner is null ? SelectAt(point) : alreadySelected;
            if (eventArgs.ClickCount == 2 && selected is TextAnnotation text)
            {
                StartTextEditor(point, text);
                eventArgs.Handled = true;
                return;
            }
            if (selected is not null)
            {
                _transformItem = selected;
                _beforeTransform = AnnotationSnapshot.Capture(selected);
                _transformStart = point;
                _resizeCorner = corner;
                AnnotationCanvas.CaptureMouse();
            }
            else
            {
                if (LockPositionMenuItem.IsChecked) { eventArgs.Handled = true; return; }
                AnnotationCanvas.IsHitTestVisible = false;
                try
                {
                    DragMove();
                }
                finally
                {
                    AnnotationCanvas.IsHitTestVisible = true;
                    RepositionToolbar();
                }
            }
            eventArgs.Handled = true;
            return;
        }
        if (_tool == AnnotationTool.Text) { StartTextEditor(point); eventArgs.Handled = true; return; }
        _start = point;
        _draft = CreateDraft(point);
        _annotations.Add(_draft);
        AnnotationCanvas.CaptureMouse();
        RedrawAnnotations();
        eventArgs.Handled = true;
    }

    private void OnAnnotationMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.LeftButton != MouseButtonState.Pressed) return;
        var point = ToImagePoint(eventArgs.GetPosition(AnnotationCanvas));
        if (_draft is not null)
        {
            UpdateDraft(_draft, point);
            RedrawAnnotations();
        }
        else if (_transformItem is not null && _beforeTransform is not null)
        {
            _beforeTransform.Apply(_transformItem);
            if (_resizeCorner is { } corner) ResizeItem(_transformItem, _beforeTransform, corner, point);
            else MoveItem(_transformItem, point - _transformStart);
            RedrawAnnotations();
        }
    }

    private void OnAnnotationMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (_transformItem is not null && _beforeTransform is not null)
        {
            AnnotationCanvas.ReleaseMouseCapture();
            RecordChange(_transformItem, _beforeTransform);
            _transformItem = null; _beforeTransform = null; _resizeCorner = null;
            RedrawAnnotations();
            return;
        }
        if (_draft is null) return;
        AnnotationCanvas.ReleaseMouseCapture();
        var draft = _draft; _draft = null;
        UpdateDraft(draft, ToImagePoint(eventArgs.GetPosition(AnnotationCanvas)));
        if (IsEmpty(draft)) _annotations.Remove(draft);
        else _history.RecordExecuted(new AddAnnotationCommand(_annotations, draft, _annotations.IndexOf(draft)));
        RedrawAnnotations();
    }

    private AnnotationItem CreateDraft(Point point) => _tool switch
    {
        AnnotationTool.Rectangle => new RectangleAnnotation(),
        AnnotationTool.RoundedRectangle => new RoundedRectangleAnnotation(),
        AnnotationTool.Line => new LineAnnotation { Start = point, End = point },
        AnnotationTool.Arrow => new ArrowAnnotation { Start = point, End = point },
        AnnotationTool.Pen => new PenAnnotation { Points = { point } },
        AnnotationTool.Mosaic => new MosaicAnnotation { PixelSize = (int)_effectStrength },
        AnnotationTool.Blur => new BlurAnnotation { Radius = _effectStrength },
        _ => throw new InvalidOperationException()
    };

    private void UpdateDraft(AnnotationItem item, Point point)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var delta = point - _start;
            if (item is RectangleAnnotation or RoundedRectangleAnnotation)
            {
                var size = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
                point = _start + new Vector(delta.X < 0 ? -size : size, delta.Y < 0 ? -size : size);
            }
            else if (item is LineAnnotation or ArrowAnnotation)
            {
                var angle = Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4)) * Math.PI / 4;
                point = _start + new Vector(Math.Cos(angle) * delta.Length, Math.Sin(angle) * delta.Length);
            }
        }
        item.StrokeColor = _color; item.StrokeThickness = _thickness;
        switch (item)
        {
            case LineAnnotation line: line.End = point; break;
            case ArrowAnnotation arrow: arrow.End = point; break;
            case PenAnnotation pen when pen.Points.Count == 0 || (pen.Points[^1] - point).Length >= 1: pen.Points.Add(point); break;
            case PenAnnotation: break;
            default:
                item.X = Math.Min(_start.X, point.X); item.Y = Math.Min(_start.Y, point.Y);
                item.Width = Math.Abs(point.X - _start.X); item.Height = Math.Abs(point.Y - _start.Y); break;
        }
    }

    private void StartTextEditor(Point point, TextAnnotation? existing = null)
    {
        _editingText = existing;
        _beforeTextEdit = existing is null ? null : AnnotationSnapshot.Capture(existing);
        var origin = existing is null ? point : new Point(existing.X, existing.Y);
        _textEditor = new TextBox { Text = existing?.Text ?? string.Empty, Width = Math.Max(180, (existing?.Width ?? 0) * ViewScale), MinHeight = 28, FontSize = (existing?.FontSize ?? _fontSize) * ViewScale, FontFamily = new FontFamily(existing?.FontFamilyName ?? _fontFamily), FontWeight = existing?.FontWeight ?? _fontWeight, Foreground = new SolidColorBrush(existing?.TextColor ?? _color), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Canvas.SetLeft(_textEditor, origin.X * ViewScale); Canvas.SetTop(_textEditor, origin.Y * ViewScale);
        _textEditor.LostKeyboardFocus += (_, _) => CommitText();
        AnnotationCanvas.Children.Add(_textEditor);
        _textEditor.Focus();
    }

    private void CommitText()
    {
        if (_textEditor is null) return;
        var editor = _textEditor; _textEditor = null;
        var text = editor.Text.Trim();
        var point = ToImagePoint(new Point(Canvas.GetLeft(editor), Canvas.GetTop(editor)));
        AnnotationCanvas.Children.Remove(editor);
        if (_editingText is { } existing)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                var index = _annotations.IndexOf(existing);
                _history.Execute(new DeleteAnnotationCommand(_annotations, existing, index));
            }
            else
            {
                existing.X = point.X; existing.Y = point.Y; existing.Text = text;
                RecordChange(existing, _beforeTextEdit!);
            }
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            var created = new TextAnnotation { X = point.X, Y = point.Y, Text = text, TextColor = _color, StrokeColor = _color, FontSize = _fontSize, FontFamilyName = _fontFamily, FontWeight = _fontWeight, StrokeThickness = _thickness };
            _annotations.Add(created);
            _history.RecordExecuted(new AddAnnotationCommand(_annotations, created, _annotations.Count - 1));
        }
        _editingText = null; _beforeTextEdit = null;
        RedrawAnnotations();
    }

    private void RedrawAnnotations()
    {
        if (UndoButton is not null) UndoButton.IsEnabled = _history.CanUndo;
        if (RedoButton is not null) RedoButton.IsEnabled = _history.CanRedo;
        if (!IsLoaded || _textEditor is not null) return;
        AnnotationCanvas.Children.Clear();
        foreach (var item in _annotations)
        {
            AnnotationCanvas.Children.Add(CreateVisual(item));
            if (item.IsSelected) AddSelectionVisuals(item);
        }
    }

    private UIElement CreateVisual(AnnotationItem item)
    {
        if (item is MosaicAnnotation or BlurAnnotation)
        {
            return CreateEffectVisual(item);
        }
        Shape? shape = item switch
        {
            RectangleAnnotation => new Rectangle { Width = item.Width * ViewScale, Height = item.Height * ViewScale },
            RoundedRectangleAnnotation rounded => new Rectangle { Width = item.Width * ViewScale, Height = item.Height * ViewScale, RadiusX = rounded.Radius * ViewScale, RadiusY = rounded.Radius * ViewScale },
            LineAnnotation line => new Line { X1 = line.Start.X * ViewScale, Y1 = line.Start.Y * ViewScale, X2 = line.End.X * ViewScale, Y2 = line.End.Y * ViewScale },
            ArrowAnnotation arrow => MakeArrow(arrow),
            PenAnnotation pen => new Polyline { Points = new PointCollection(pen.Points.Select(p => new Point(p.X * ViewScale, p.Y * ViewScale))) },
            _ => null
        };
        if (item is TextAnnotation text)
        {
            var block = new TextBlock { Text = text.Text, FontFamily = new FontFamily(text.FontFamilyName), FontSize = text.FontSize * ViewScale, FontWeight = text.FontWeight, Foreground = new SolidColorBrush(text.TextColor), IsHitTestVisible = false };
            Canvas.SetLeft(block, text.X * ViewScale); Canvas.SetTop(block, text.Y * ViewScale); return block;
        }
        shape!.Stroke = new SolidColorBrush(item.StrokeColor); shape.StrokeThickness = item.StrokeThickness * ViewScale;
        shape.StrokeLineJoin = PenLineJoin.Round; shape.StrokeStartLineCap = PenLineCap.Round; shape.StrokeEndLineCap = PenLineCap.Round; shape.IsHitTestVisible = false;
        if (item.IsSelected) shape.StrokeDashArray = [3, 2];
        if (item is RectangleAnnotation or RoundedRectangleAnnotation) { Canvas.SetLeft(shape, item.X * ViewScale); Canvas.SetTop(shape, item.Y * ViewScale); }
        return shape;
    }

    private UIElement CreateEffectVisual(AnnotationItem item)
    {
        var region = ClampRegion(item);
        BitmapSource source = new CroppedBitmap(_image, region);
        var image = new Image { Width = item.Width * ViewScale, Height = item.Height * ViewScale, Stretch = Stretch.Fill, IsHitTestVisible = false };
        if (item is MosaicAnnotation mosaic)
        {
            var scale = 1d / Math.Max(2, mosaic.PixelSize);
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        }
        else if (item is BlurAnnotation blur)
        {
            image.Effect = new BlurEffect { Radius = blur.Radius * ViewScale, KernelType = KernelType.Gaussian };
        }
        image.Source = source;
        var container = new Grid { Width = image.Width, Height = image.Height, IsHitTestVisible = false };
        container.Children.Add(image);
        if (item is MosaicAnnotation && AnnotationMenuItem.IsChecked)
        {
            container.Children.Add(new Rectangle
            {
                Stroke = AccentBrush,
                StrokeThickness = 1.5,
                StrokeDashArray = [5, 3],
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            });
        }
        Canvas.SetLeft(container, item.X * ViewScale);
        Canvas.SetTop(container, item.Y * ViewScale);
        return container;
    }

    private Int32Rect ClampRegion(AnnotationItem item)
    {
        var x = Math.Clamp((int)Math.Floor(item.X), 0, _image.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Floor(item.Y), 0, _image.PixelHeight - 1);
        var width = Math.Clamp((int)Math.Ceiling(item.Width), 1, _image.PixelWidth - x);
        var height = Math.Clamp((int)Math.Ceiling(item.Height), 1, _image.PixelHeight - y);
        return new Int32Rect(x, y, width, height);
    }

    private void AddSelectionVisuals(AnnotationItem item)
    {
        var bounds = Bounds(item);
        var border = new Rectangle
        {
            Width = Math.Max(1, bounds.Width * ViewScale), Height = Math.Max(1, bounds.Height * ViewScale),
            Stroke = AccentBrush, StrokeThickness = 1, StrokeDashArray = [4, 3], IsHitTestVisible = false
        };
        Canvas.SetLeft(border, bounds.X * ViewScale); Canvas.SetTop(border, bounds.Y * ViewScale);
        AnnotationCanvas.Children.Add(border);
        foreach (var point in CornerPoints(bounds))
        {
            var handle = new Rectangle { Width = 9, Height = 9, Fill = Brushes.White, Stroke = AccentBrush, StrokeThickness = 1.5, IsHitTestVisible = false };
            Canvas.SetLeft(handle, point.X * ViewScale - 4.5); Canvas.SetTop(handle, point.Y * ViewScale - 4.5);
            AnnotationCanvas.Children.Add(handle);
        }
    }

    private Polyline MakeArrow(ArrowAnnotation arrow)
    {
        var vector = arrow.Start - arrow.End; if (vector.Length > 0) vector.Normalize();
        Vector Rotate(Vector value, double degrees) { var radians = degrees * Math.PI / 180; return new Vector(value.X * Math.Cos(radians) - value.Y * Math.Sin(radians), value.X * Math.Sin(radians) + value.Y * Math.Cos(radians)); }
        var left = arrow.End + Rotate(vector, arrow.ArrowHeadAngle) * arrow.ArrowHeadLength;
        var right = arrow.End + Rotate(vector, -arrow.ArrowHeadAngle) * arrow.ArrowHeadLength;
        return new Polyline { Points = new PointCollection([new Point(arrow.Start.X * ViewScale, arrow.Start.Y * ViewScale), new Point(arrow.End.X * ViewScale, arrow.End.Y * ViewScale), new Point(left.X * ViewScale, left.Y * ViewScale), new Point(arrow.End.X * ViewScale, arrow.End.Y * ViewScale), new Point(right.X * ViewScale, right.Y * ViewScale)]) };
    }

    private ResizeCorner? HitResizeCorner(AnnotationItem item, Point point)
    {
        var tolerance = 9 / ViewScale;
        var corners = CornerPoints(Bounds(item));
        for (var index = 0; index < corners.Length; index++)
            if ((corners[index] - point).Length <= tolerance) return (ResizeCorner)index;
        return null;
    }

    private static Point[] CornerPoints(Rect bounds) =>
        [bounds.TopLeft, bounds.TopRight, bounds.BottomLeft, bounds.BottomRight];

    private static void MoveItem(AnnotationItem item, Vector delta)
    {
        switch (item)
        {
            case LineAnnotation line: line.Start += delta; line.End += delta; break;
            case ArrowAnnotation arrow: arrow.Start += delta; arrow.End += delta; break;
            case PenAnnotation pen:
                for (var index = 0; index < pen.Points.Count; index++) pen.Points[index] += delta;
                break;
            default: item.X += delta.X; item.Y += delta.Y; break;
        }
    }

    private static void ResizeItem(AnnotationItem item, AnnotationSnapshot original, ResizeCorner corner, Point point)
    {
        var bounds = original.Bounds;
        var opposite = corner switch
        {
            ResizeCorner.TopLeft => bounds.BottomRight,
            ResizeCorner.TopRight => bounds.BottomLeft,
            ResizeCorner.BottomLeft => bounds.TopRight,
            _ => bounds.TopLeft
        };
        var resized = new Rect(opposite, point);
        if (resized.Width < 2 || resized.Height < 2) return;
        switch (item)
        {
            case LineAnnotation line:
                line.Start = resized.TopLeft; line.End = resized.BottomRight; break;
            case ArrowAnnotation arrow:
                arrow.Start = resized.TopLeft; arrow.End = resized.BottomRight; break;
            case PenAnnotation pen:
                for (var index = 0; index < pen.Points.Count; index++)
                {
                    var source = original.Points[index];
                    var xRatio = bounds.Width <= 0 ? 0 : (source.X - bounds.X) / bounds.Width;
                    var yRatio = bounds.Height <= 0 ? 0 : (source.Y - bounds.Y) / bounds.Height;
                    pen.Points[index] = new Point(resized.X + xRatio * resized.Width, resized.Y + yRatio * resized.Height);
                }
                break;
            case TextAnnotation text:
                text.X = resized.X; text.Y = resized.Y;
                text.FontSize = Math.Max(8, original.FontSize * resized.Height / Math.Max(1, bounds.Height));
                break;
            default:
                item.X = resized.X; item.Y = resized.Y; item.Width = resized.Width; item.Height = resized.Height; break;
        }
    }

    private void RecordChange(AnnotationItem item, AnnotationSnapshot before)
    {
        var after = AnnotationSnapshot.Capture(item);
        _history.RecordExecuted(new ChangeAnnotationCommand(() => { before.Apply(item); RedrawAnnotations(); }, () => { after.Apply(item); RedrawAnnotations(); }));
    }

    private AnnotationItem? SelectAt(Point point)
    {
        foreach (var item in _annotations) item.IsSelected = false;
        var selected = _annotations.LastOrDefault(annotation =>
        {
            var bounds = Bounds(annotation);
            bounds.Inflate(Math.Max(5, annotation.StrokeThickness), Math.Max(5, annotation.StrokeThickness));
            return bounds.Contains(point);
        });
        if (selected is not null) selected.IsSelected = true;
        RedrawAnnotations();
        return selected;
    }

    private static Rect Bounds(AnnotationItem item) => item switch
    {
        LineAnnotation line => new Rect(line.Start, line.End),
        ArrowAnnotation arrow => new Rect(arrow.Start, arrow.End),
        PenAnnotation pen when pen.Points.Count > 0 => new Rect(new Point(pen.Points.Min(point => point.X), pen.Points.Min(point => point.Y)), new Point(pen.Points.Max(point => point.X), pen.Points.Max(point => point.Y))),
        TextAnnotation text => new Rect(text.X, text.Y, Math.Max(80, text.Text.Length * text.FontSize), text.FontSize * 1.5),
        _ => new Rect(item.X, item.Y, item.Width, item.Height)
    };
    private static bool IsEmpty(AnnotationItem item) => item is PenAnnotation pen ? pen.Points.Count < 2 : Bounds(item).Width < 1 && Bounds(item).Height < 1;
    private double ViewScale => ActualWidth > 0 ? ActualWidth / _image.PixelWidth : _viewState.Scale / _dpiScaleX;
    private Point ToImagePoint(Point view) => new(view.X / ViewScale, view.Y / ViewScale);

    private void OnKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Delete && AnnotationMenuItem.IsChecked && _textEditor is null)
        {
            var selected = _annotations.Where(item => item.IsSelected).ToList();
            foreach (var item in selected)
                _history.Execute(new DeleteAnnotationCommand(_annotations, item, _annotations.IndexOf(item)));
            RedrawAnnotations(); eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CommitText(); eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CompleteAnnotationAndCopy();
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _history.Undo(); RedrawAnnotations(); eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _history.Redo(); RedrawAnnotations(); eventArgs.Handled = true;
        }
    }

    private void OnFinishAnnotationClicked(object sender, RoutedEventArgs eventArgs)
    {
        CompleteAnnotationAndCopy();
    }

    private void CompleteAnnotationAndCopy()
    {
        CommitText();
        SetAnnotationMode(false, notify: true);
        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMoveHandleMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (LockPositionMenuItem.IsChecked) { eventArgs.Handled = true; return; }
        DragMove();
        RepositionToolbar();
        eventArgs.Handled = true;
    }

    private void RepositionToolbar()
    {
        var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var availableWidth = Math.Max(220, area.Width / _dpiScaleX - 32);
        AnnotationToolsPanel.Width = Math.Min(620, availableWidth);
        ActionToolsPanel.MaxWidth = availableWidth;
        if (PinActionToolbar.IsOpen)
        {
            var actionWidth = PinActionToolbar.Child?.RenderSize.Width ?? 0;
            var rightOffset = actionWidth > 0 ? Math.Max(0, ActualWidth - actionWidth) : 0;
            PinActionToolbar.HorizontalOffset = rightOffset + .1;
            PinActionToolbar.HorizontalOffset = rightOffset;
        }
        if (AnnotationToolbar.IsOpen)
        {
            var toolbarWidth = AnnotationToolbar.Child?.RenderSize.Width ?? 0;
            var centeredOffset = toolbarWidth > 0 ? (ActualWidth - toolbarWidth) / 2 : 0;
            AnnotationToolbar.HorizontalOffset = centeredOffset + .1;
            AnnotationToolbar.HorizontalOffset = centeredOffset;
        }
    }

    private void OnUndoClicked(object sender, RoutedEventArgs eventArgs)
    {
        CommitText();
        _history.Undo();
        RedrawAnnotations();
    }

    private void OnRedoClicked(object sender, RoutedEventArgs eventArgs)
    {
        _history.Redo();
        RedrawAnnotations();
    }

    private void OnCopyClicked(object sender, RoutedEventArgs eventArgs) { CommitText(); CopyRequested?.Invoke(this, EventArgs.Empty); }
    private void OnSaveClicked(object sender, RoutedEventArgs eventArgs) { CommitText(); SaveRequested?.Invoke(this, EventArgs.Empty); }
    private void OnOcrClicked(object sender, RoutedEventArgs eventArgs) { CommitText(); OcrRequested?.Invoke(this, EventArgs.Empty); }
    private void OnTranslateClicked(object sender, RoutedEventArgs eventArgs) { CommitText(); TranslateRequested?.Invoke(this, EventArgs.Empty); }
    private async void OnCopyTranslationClicked(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await ScreenshotTool.Core.Clipboard.TextClipboard.CopyAsync(_translatedText);
            CopyTranslationButton.ToolTip = "译文已复制";
        }
        catch { CopyTranslationButton.ToolTip = "复制失败，请重试"; }
    }

    private void OnShowOriginalClicked(object sender, RoutedEventArgs eventArgs) => SetTranslationVisibility(false);
    private void OnShowTranslationClicked(object sender, RoutedEventArgs eventArgs) => SetTranslationVisibility(true);

    private void SetTranslationVisibility(bool showTranslation)
    {
        _showTranslatedText = showTranslation;
        _translationLoading = false;
        if (showTranslation)
        {
            TranslatedImageBorder.Visibility = Visibility.Visible;
            RedrawTranslation();
            SetTranslationToggleSelection(TranslatedViewButton, true);
            SetTranslationToggleSelection(OriginalViewButton, false);
        }
        else
        {
            TranslatedImageBorder.Visibility = Visibility.Collapsed;
            SetTranslationToggleSelection(OriginalViewButton, true);
            SetTranslationToggleSelection(TranslatedViewButton, false);
        }
        ApplyViewState();
    }

    private static void SetTranslationToggleSelection(Button button, bool selected)
    {
        button.Background = selected ? SelectedToolBrush : Brushes.White;
        button.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(94, 83, 108));
        button.BorderBrush = selected ? SelectedToolBrush : new SolidColorBrush(Color.FromRgb(216, 209, 227));
    }

    public void ShowTranslationLoading()
    {
        CancelTranslationButton.Visibility = Visibility.Visible;
        TranslatedImageBorder.Visibility = Visibility.Collapsed;
        TranslationTogglePanel.Visibility = Visibility.Collapsed;
        TranslateActionButton.Visibility = Visibility.Collapsed;
        _showTranslatedText = false;
        _translationLoading = true;
        TranslationStatusText.Text = "正在提取并翻译…";
        TranslationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(64, 54, 80));
        TranslationStatusPanel.Visibility = Visibility.Visible;
        ApplyViewState();
    }

    public void ShowTranslationResult(OcrResult ocrResult, string translatedText)
    {
        CancelTranslationButton.Visibility = Visibility.Collapsed;
        _translationBitmap = null;
        _translationOcrResult = ocrResult;
        _translatedText = translatedText.Trim();
        _showTranslatedText = true;
        _translationLoading = false;
        TranslationStatusPanel.Visibility = Visibility.Collapsed;
        TranslationTogglePanel.Visibility = Visibility.Visible;
        TranslateActionButton.Visibility = Visibility.Collapsed;
        Dispatcher.BeginInvoke(RepositionToolbar, DispatcherPriority.Loaded);
        SetTranslationVisibility(true);
    }

    public void ShowTranslationError(string message)
    {
        CancelTranslationButton.Visibility = Visibility.Collapsed;
        TranslatedImageBorder.Visibility = Visibility.Collapsed;
        TranslationTogglePanel.Visibility = Visibility.Collapsed;
        TranslateActionButton.Visibility = Visibility.Visible;
        _showTranslatedText = false;
        _translationLoading = true;
        TranslationStatusText.Text = message;
        TranslationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(176, 54, 78));
        TranslationStatusPanel.Visibility = Visibility.Visible;
        ApplyViewState();
    }

    private void RedrawTranslation()
    {
        if (!_showTranslatedText || _translationOcrResult is null || string.IsNullOrWhiteSpace(_translatedText) || !IsLoaded) return;
        TranslatedPinnedImage.Source = RenderTranslationToBitmap(RecognitionImage);
        TranslatedImageBorder.Visibility = Visibility.Visible;
    }

    private BitmapSource? _translationBitmap;
    private BitmapSource RenderTranslationToBitmap(BitmapSource source)
    {
        if (_translationBitmap is not null) return _translationBitmap;
        var width = Math.Max(360, source.PixelWidth);
        var fontSize = Math.Clamp(width / 48d, 18, 26);
        var formatted = new FormattedText(_translatedText, CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight, ResultTextTypeface, fontSize,
            new SolidColorBrush(Color.FromRgb(38, 31, 48)), 1)
        {
            MaxTextWidth = width - 48,
            LineHeight = fontSize * 1.55
        };
        var height = Math.Max(96, (int)Math.Ceiling(formatted.Height + 48));
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            drawing.DrawText(formatted, new Point(24, 24));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return _translationBitmap = bitmap;
    }

    public event EventHandler? CancelTranslationRequested;
    private void OnCancelTranslation(object sender, RoutedEventArgs e) => CancelTranslationRequested?.Invoke(this, EventArgs.Empty);
    private void OnCopyOriginal(object sender, RoutedEventArgs e) { CommitText(); CopyExplicitAsync(RecognitionImage); }
    private void OnCopyComparison(object sender, RoutedEventArgs e)
    {
        CommitText();
        if (_translationOcrResult is null) { CopyExplicitAsync(RecognitionImage); return; }
        var original = RecognitionImage;
        var translated = RenderTranslationToBitmap(original);
        var width = Math.Max(original.PixelWidth, translated.PixelWidth);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, original.PixelHeight + translated.PixelHeight + 8));
            drawing.DrawImage(original, new Rect(0, 0, original.PixelWidth, original.PixelHeight));
            drawing.DrawImage(translated, new Rect(0, original.PixelHeight + 8, translated.PixelWidth, translated.PixelHeight));
        }
        var bitmap = new RenderTargetBitmap(width, original.PixelHeight + translated.PixelHeight + 8, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); CopyExplicitAsync(bitmap);
    }
    private async void CopyExplicitAsync(BitmapSource bitmap)
    {
        try { await new ScreenshotTool.Core.Clipboard.ClipboardService().SetImageAsync(bitmap); }
        catch { System.Windows.MessageBox.Show("剪贴板暂时被占用，请重试。", "复制失败"); }
    }
    private void OnTopmostClicked(object sender, RoutedEventArgs eventArgs) => Topmost = TopmostMenuItem.IsChecked;
    private void OnBorderClicked(object sender, RoutedEventArgs eventArgs) => ImageBorder.BorderThickness = BorderMenuItem.IsChecked ? new Thickness(1) : new Thickness(0);
    private void OnResetClicked(object sender, RoutedEventArgs eventArgs) { _viewState.Reset(); ApplyViewState(); }
    private void OnFitClicked(object sender, RoutedEventArgs e)
    {
        var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var fullHeight = _image.PixelHeight + (_showTranslatedText && _translationOcrResult is not null ? RenderTranslationToBitmap(RecognitionImage).PixelHeight : 0);
        _viewState.SetScale(Math.Min((area.Width - 64d) / _image.PixelWidth, (area.Height - 160d) / fullHeight));
        ApplyViewState();
    }
    private void OnCloseClicked(object sender, RoutedEventArgs eventArgs) => Close();
    private void OnTextStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontSizeBox is null || FontFamilyBox is null) return;
        if (FontSizeBox.SelectedItem is ComboBoxItem size && double.TryParse(size.Content.ToString(), out var value)) _fontSize = value;
        if (FontFamilyBox.SelectedItem is ComboBoxItem family) _fontFamily = family.Content.ToString()!;
        ApplyTextStyle();
    }
    private void OnBoldClicked(object sender, RoutedEventArgs e)
    {
        _fontWeight = _fontWeight == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;
        SetActionButtonSelected((Button)sender, _fontWeight == FontWeights.Bold);
        ApplyTextStyle();
    }
    private void OnEffectStrengthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((System.Windows.Controls.ComboBox)sender).SelectedItem is not ComboBoxItem option || !double.TryParse(option.Content.ToString(), out var value)) return;
        _effectStrength = value;
        var selected = _annotations.LastOrDefault(item => item.IsSelected);
        if (selected is not MosaicAnnotation && selected is not BlurAnnotation) return;
        var before = AnnotationSnapshot.Capture(selected);
        if (selected is MosaicAnnotation mosaic) mosaic.PixelSize = (int)value;
        if (selected is BlurAnnotation blur) blur.Radius = value;
        RecordChange(selected, before); RedrawAnnotations();
    }
    private void ApplyTextStyle()
    {
        if (_annotations.LastOrDefault(item => item.IsSelected) is not TextAnnotation text) return;
        var before = AnnotationSnapshot.Capture(text);
        text.FontSize = _fontSize; text.FontFamilyName = _fontFamily; text.FontWeight = _fontWeight;
        RecordChange(text, before); RedrawAnnotations();
    }
    private enum AnnotationTool { Select, Rectangle, RoundedRectangle, Line, Arrow, Pen, Text, Mosaic, Blur }
    private enum ResizeCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    private sealed class AnnotationSnapshot
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public Color StrokeColor { get; init; }
        public double StrokeThickness { get; init; }
        public Point Start { get; init; }
        public Point End { get; init; }
        public Point[] Points { get; init; } = [];
        public string Text { get; init; } = string.Empty;
        public double FontSize { get; init; }
        public string FontFamilyName { get; init; } = "Microsoft YaHei";
        public FontWeight FontWeight { get; init; }
        public double EffectStrength { get; init; }
        public Color TextColor { get; init; }
        public Rect Bounds { get; init; }

        public static AnnotationSnapshot Capture(AnnotationItem item) => new()
        {
            X = item.X, Y = item.Y, Width = item.Width, Height = item.Height,
            StrokeColor = item.StrokeColor, StrokeThickness = item.StrokeThickness,
            Start = item is LineAnnotation line ? line.Start : item is ArrowAnnotation arrow ? arrow.Start : default,
            End = item is LineAnnotation lineEnd ? lineEnd.End : item is ArrowAnnotation arrowEnd ? arrowEnd.End : default,
            Points = item is PenAnnotation pen ? pen.Points.ToArray() : [],
            Text = item is TextAnnotation text ? text.Text : string.Empty,
            FontSize = item is TextAnnotation font ? font.FontSize : 20,
            FontFamilyName = item is TextAnnotation family ? family.FontFamilyName : "Microsoft YaHei",
            FontWeight = item is TextAnnotation weight ? weight.FontWeight : FontWeights.Normal,
            EffectStrength = item is MosaicAnnotation mosaic ? mosaic.PixelSize : item is BlurAnnotation blur ? blur.Radius : 0,
            TextColor = item is TextAnnotation color ? color.TextColor : item.StrokeColor,
            Bounds = PinWindow.Bounds(item)
        };

        public void Apply(AnnotationItem item)
        {
            item.X = X; item.Y = Y; item.Width = Width; item.Height = Height;
            item.StrokeColor = StrokeColor; item.StrokeThickness = StrokeThickness;
            if (item is LineAnnotation line) { line.Start = Start; line.End = End; }
            if (item is ArrowAnnotation arrow) { arrow.Start = Start; arrow.End = End; }
            if (item is PenAnnotation pen) { pen.Points.Clear(); pen.Points.AddRange(Points); }
            if (item is TextAnnotation text) { text.Text = Text; text.FontSize = FontSize; text.TextColor = TextColor; text.FontFamilyName = FontFamilyName; text.FontWeight = FontWeight; }
            if (item is MosaicAnnotation mosaic) mosaic.PixelSize = (int)EffectStrength;
            if (item is BlurAnnotation blur) blur.Radius = EffectStrength;
        }
    }
}
