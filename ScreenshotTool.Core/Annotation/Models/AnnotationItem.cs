using System.Windows.Media;

namespace ScreenshotTool.Core.Annotation.Models;

public abstract class AnnotationItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeThickness { get; set; } = 3;
    public double Opacity { get; set; } = 1;
    public bool IsSelected { get; set; }
    public abstract AnnotationType Type { get; }
}

public enum AnnotationType { Rectangle, RoundedRectangle, Line, Arrow, Pen, Text, Mosaic, Blur }
