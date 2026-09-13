using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Core.Annotation.Models;

public sealed class RectangleAnnotation : AnnotationItem
{
    public Color? FillColor { get; set; }
    public override AnnotationType Type => AnnotationType.Rectangle;
}

public sealed class RoundedRectangleAnnotation : AnnotationItem
{
    public double Radius { get; set; } = 8;
    public override AnnotationType Type => AnnotationType.RoundedRectangle;
}

public sealed class LineAnnotation : AnnotationItem
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public override AnnotationType Type => AnnotationType.Line;
}

public sealed class ArrowAnnotation : AnnotationItem
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public double ArrowHeadLength { get; set; } = 14;
    public double ArrowHeadAngle { get; set; } = 28;
    public override AnnotationType Type => AnnotationType.Arrow;
}

public sealed class PenAnnotation : AnnotationItem
{
    public List<Point> Points { get; } = [];
    public override AnnotationType Type => AnnotationType.Pen;
}

public sealed class TextAnnotation : AnnotationItem
{
    public string Text { get; set; } = string.Empty;
    public string FontFamilyName { get; set; } = "Microsoft YaHei UI";
    public double FontSize { get; set; } = 20;
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    public Color TextColor { get; set; } = Colors.Red;
    public Color? BackgroundColor { get; set; }
    public override AnnotationType Type => AnnotationType.Text;
}

public sealed class MosaicAnnotation : AnnotationItem
{
    public int PixelSize { get; set; } = 12;
    public override AnnotationType Type => AnnotationType.Mosaic;
}

public sealed class BlurAnnotation : AnnotationItem
{
    public double Radius { get; set; } = 8;
    public override AnnotationType Type => AnnotationType.Blur;
}
