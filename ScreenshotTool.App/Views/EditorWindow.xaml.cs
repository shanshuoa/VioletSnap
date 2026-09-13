using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using ScreenshotTool.Core.Annotation;
using ScreenshotTool.Core.Annotation.Models;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;
using Rectangle = System.Windows.Shapes.Rectangle;
using FontFamily = System.Windows.Media.FontFamily;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ScreenshotTool.App.Views;

public partial class EditorWindow : Window
{
    private readonly BitmapSource _original;
    private readonly IAnnotationRenderer _renderer;
    private readonly List<AnnotationItem> _annotations = [];
    private AnnotationItem? _draft;
    private Point _start;
    private double _zoom = 1;
    private EditorTool _tool = EditorTool.Select;
    private Color _color = Colors.Red;
    private double _thickness = 3;
    private TextBox? _textEditor;
    private Button? _activeToolButton;

    public EditorWindow(BitmapSource original, IAnnotationRenderer renderer)
    {
        InitializeComponent();
        _original = original;
        _renderer = renderer;
        OriginalImage.Source = original;
        ApplyZoom();
        StatusText.Text = $"{original.PixelWidth} × {original.PixelHeight}  |  选择工具可选中并按 Delete 删除";
    }

    public event EventHandler<BitmapSource>? CopyRequested;
    public event EventHandler<BitmapSource>? SaveRequested;
    public event EventHandler<BitmapSource>? PinRequested;

    private void OnToolClicked(object sender, RoutedEventArgs e)
    {
        CommitText();
        var button = (Button)sender;
        if (ReferenceEquals(_activeToolButton, button))
        {
            ClearActiveToolSelection();
            _tool = EditorTool.Select;
            StatusText.Text = "未选择绘制工具。";
            return;
        }

        ClearActiveToolSelection();
        _tool = Enum.Parse<EditorTool>((string)button.Tag);
        button.Background = new SolidColorBrush(Color.FromRgb(118, 82, 213));
        button.Foreground = System.Windows.Media.Brushes.White;
        _activeToolButton = button;
        StatusText.Text = _tool == EditorTool.Select ? "选择标注，按 Delete 删除。" : $"当前工具：{button.Content}。拖动以创建标注。";
    }

    private void ClearActiveToolSelection()
    {
        if (_activeToolButton is null) return;
        _activeToolButton.ClearValue(Button.BackgroundProperty);
        _activeToolButton.ClearValue(Button.ForegroundProperty);
        _activeToolButton = null;
    }

    private void OnColorClicked(object sender, RoutedEventArgs e)
    {
        _color = ((Button)sender).Tag?.ToString() switch
        {
            "Blue" => Colors.DodgerBlue,
            "Yellow" => Colors.Gold,
            _ => Colors.Red
        };
    }

    private void OnThicknessChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThicknessBox.SelectedItem is ComboBoxItem item && double.TryParse(item.Content.ToString()?.Split(' ')[0], out var thickness)) _thickness = thickness;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        CommitText();
        var point = ToImagePoint(e.GetPosition(AnnotationCanvas));
        if (_tool == EditorTool.Select)
        {
            SelectAt(point);
            return;
        }
        if (_tool == EditorTool.Text)
        {
            StartTextEditor(point);
            return;
        }
        _start = point;
        _draft = CreateDraft(point);
        _annotations.Add(_draft);
        AnnotationCanvas.CaptureMouse();
        RedrawAnnotations();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_draft is null || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateDraft(_draft, ToImagePoint(e.GetPosition(AnnotationCanvas)));
        RedrawAnnotations();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draft is null) return;
        AnnotationCanvas.ReleaseMouseCapture();
        var draft = _draft;
        _draft = null;
        UpdateDraft(draft, ToImagePoint(e.GetPosition(AnnotationCanvas)));
        if (IsEmpty(draft)) _annotations.Remove(draft);
        RedrawAnnotations();
    }

    private AnnotationItem CreateDraft(Point point) => _tool switch
    {
        EditorTool.Rectangle => new RectangleAnnotation(),
        EditorTool.RoundedRectangle => new RoundedRectangleAnnotation(),
        EditorTool.Line => new LineAnnotation { Start = point, End = point },
        EditorTool.Arrow => new ArrowAnnotation { Start = point, End = point },
        EditorTool.Pen => new PenAnnotation { Points = { point } },
        _ => throw new InvalidOperationException()
    };

    private void UpdateDraft(AnnotationItem item, Point point)
    {
        item.StrokeColor = _color;
        item.StrokeThickness = _thickness;
        switch (item)
        {
            case LineAnnotation line: line.End = point; break;
            case ArrowAnnotation arrow: arrow.End = point; break;
            case PenAnnotation pen:
                if (pen.Points.Count == 0 || (pen.Points[^1] - point).Length >= 1) pen.Points.Add(point);
                break;
            default:
                item.X = Math.Min(_start.X, point.X); item.Y = Math.Min(_start.Y, point.Y);
                item.Width = Math.Abs(point.X - _start.X); item.Height = Math.Abs(point.Y - _start.Y); break;
        }
    }

    private void StartTextEditor(Point point)
    {
        _textEditor = new TextBox { Width = 220, MinHeight = 30, FontSize = 20, Foreground = new SolidColorBrush(_color), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Canvas.SetLeft(_textEditor, point.X * _zoom); Canvas.SetTop(_textEditor, point.Y * _zoom);
        AnnotationCanvas.Children.Add(_textEditor); _textEditor.Focus();
    }

    private void CommitText()
    {
        if (_textEditor is null) return;
        var text = _textEditor.Text.Trim();
        var point = ToImagePoint(new Point(Canvas.GetLeft(_textEditor), Canvas.GetTop(_textEditor)));
        AnnotationCanvas.Children.Remove(_textEditor);
        _textEditor = null;
        if (!string.IsNullOrWhiteSpace(text)) _annotations.Add(new TextAnnotation { X = point.X, Y = point.Y, Text = text, TextColor = _color, StrokeColor = _color, FontSize = 20, StrokeThickness = _thickness });
        RedrawAnnotations();
    }

    private void RedrawAnnotations()
    {
        AnnotationCanvas.Children.Clear();
        foreach (var item in _annotations) AnnotationCanvas.Children.Add(CreateVisual(item));
    }

    private UIElement CreateVisual(AnnotationItem item)
    {
        Shape? shape = item switch
        {
            RectangleAnnotation => new Rectangle { Width = item.Width * _zoom, Height = item.Height * _zoom },
            RoundedRectangleAnnotation rounded => new Rectangle { Width = item.Width * _zoom, Height = item.Height * _zoom, RadiusX = rounded.Radius * _zoom, RadiusY = rounded.Radius * _zoom },
            LineAnnotation line => MakeLine(line.Start, line.End),
            ArrowAnnotation arrow => MakeArrow(arrow),
            PenAnnotation pen => MakePen(pen),
            _ => null
        };
        if (item is TextAnnotation text)
        {
            var block = new TextBlock { Text = text.Text, FontFamily = new FontFamily(text.FontFamilyName), FontSize = text.FontSize * _zoom, FontWeight = text.FontWeight, Foreground = new SolidColorBrush(text.TextColor), IsHitTestVisible = false };
            Canvas.SetLeft(block, text.X * _zoom); Canvas.SetTop(block, text.Y * _zoom); return block;
        }
        shape!.Stroke = new SolidColorBrush(item.StrokeColor); shape.StrokeThickness = item.StrokeThickness * _zoom; shape.StrokeLineJoin = PenLineJoin.Round; shape.StrokeStartLineCap = PenLineCap.Round; shape.StrokeEndLineCap = PenLineCap.Round; shape.Opacity = item.Opacity; shape.IsHitTestVisible = false;
        if (item.IsSelected) shape.StrokeDashArray = [3, 2];
        if (item is RectangleAnnotation or RoundedRectangleAnnotation) { Canvas.SetLeft(shape, item.X * _zoom); Canvas.SetTop(shape, item.Y * _zoom); }
        return shape;
    }

    private Line MakeLine(Point start, Point end) => new() { X1 = start.X * _zoom, Y1 = start.Y * _zoom, X2 = end.X * _zoom, Y2 = end.Y * _zoom };
    private Polyline MakePen(PenAnnotation pen) => new() { Points = new PointCollection(pen.Points.Select(p => new Point(p.X * _zoom, p.Y * _zoom))) };
    private Polyline MakeArrow(ArrowAnnotation arrow)
    {
        var vector = arrow.Start - arrow.End; if (vector.Length > 0) vector.Normalize();
        Vector Rotate(Vector v, double degrees) { var r = degrees * Math.PI / 180; return new Vector(v.X * Math.Cos(r) - v.Y * Math.Sin(r), v.X * Math.Sin(r) + v.Y * Math.Cos(r)); }
        var a = arrow.End + Rotate(vector, arrow.ArrowHeadAngle) * arrow.ArrowHeadLength;
        var b = arrow.End + Rotate(vector, -arrow.ArrowHeadAngle) * arrow.ArrowHeadLength;
        return new Polyline { Points = new PointCollection([new Point(arrow.Start.X * _zoom, arrow.Start.Y * _zoom), new Point(arrow.End.X * _zoom, arrow.End.Y * _zoom), new Point(a.X * _zoom, a.Y * _zoom), new Point(arrow.End.X * _zoom, arrow.End.Y * _zoom), new Point(b.X * _zoom, b.Y * _zoom)]) };
    }

    private void SelectAt(Point point)
    {
        foreach (var item in _annotations) item.IsSelected = false;
        var selected = _annotations.LastOrDefault(item => Bounds(item).Contains(point));
        if (selected is not null) selected.IsSelected = true;
        StatusText.Text = selected is null ? "未选中标注。" : "已选中标注，按 Delete 删除。";
        RedrawAnnotations();
    }

    private static Rect Bounds(AnnotationItem item) => item switch
    {
        LineAnnotation line => new Rect(line.Start, line.End), ArrowAnnotation arrow => new Rect(arrow.Start, arrow.End),
        PenAnnotation pen when pen.Points.Count > 0 => new Rect(new Point(pen.Points.Min(p => p.X), pen.Points.Min(p => p.Y)), new Point(pen.Points.Max(p => p.X), pen.Points.Max(p => p.Y))),
        TextAnnotation text => new Rect(text.X, text.Y, Math.Max(80, text.Text.Length * text.FontSize), text.FontSize * 1.5),
        _ => new Rect(item.X, item.Y, item.Width, item.Height)
    };
    private static bool IsEmpty(AnnotationItem item) => item is PenAnnotation pen ? pen.Points.Count < 2 : Bounds(item).Width < 1 && Bounds(item).Height < 1;
    private Point ToImagePoint(Point view) => new(view.X / _zoom, view.Y / _zoom);
    private void ApplyZoom() { ImageHost.Width = _original.PixelWidth * _zoom; ImageHost.Height = _original.PixelHeight * _zoom; OriginalImage.Width = ImageHost.Width; OriginalImage.Height = ImageHost.Height; AnnotationCanvas.Width = ImageHost.Width; AnnotationCanvas.Height = ImageHost.Height; ZoomText.Text = $"{_zoom:P0}"; RedrawAnnotations(); }
    private void OnZoomInClicked(object sender, RoutedEventArgs e) { _zoom = Math.Min(3, _zoom * 1.25); ApplyZoom(); }
    private void OnZoomOutClicked(object sender, RoutedEventArgs e) { _zoom = Math.Max(.25, _zoom / 1.25); ApplyZoom(); }
    private BitmapSource Render() { CommitText(); return _renderer.Render(_original, _annotations); }
    private void OnCopyClicked(object sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, Render());
    private void OnSaveClicked(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, Render());
    private void OnPinClicked(object sender, RoutedEventArgs e) => PinRequested?.Invoke(this, Render());
    private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Delete && _textEditor is null) { _annotations.RemoveAll(item => item.IsSelected); RedrawAnnotations(); } else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) CommitText(); }
    private enum EditorTool { Select, Rectangle, RoundedRectangle, Line, Arrow, Pen, Text }
}
