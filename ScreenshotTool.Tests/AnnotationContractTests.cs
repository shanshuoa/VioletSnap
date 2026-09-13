using System.Windows;
using System.Windows.Media;
using ScreenshotTool.Core.Annotation.Models;
using ScreenshotTool.Core.Annotation.UndoRedo;

namespace ScreenshotTool.Tests;

internal static class AnnotationContractTests
{
    public static void BasicAnnotationsShouldRetainImageCoordinates()
    {
        var rectangle = new RectangleAnnotation { X = 12, Y = 24, Width = 160, Height = 80, StrokeColor = Colors.Blue, StrokeThickness = 5 };
        var arrow = new ArrowAnnotation { Start = new Point(10, 20), End = new Point(100, 200) };
        var pen = new PenAnnotation { Points = { new Point(1, 2), new Point(3, 4) } };
        var text = new TextAnnotation { X = 5, Y = 6, Text = "标注" };

        Assert(rectangle.Type == AnnotationType.Rectangle && rectangle.Width == 160 && rectangle.StrokeColor == Colors.Blue);
        Assert(arrow.Type == AnnotationType.Arrow && arrow.End == new Point(100, 200));
        Assert(pen.Type == AnnotationType.Pen && pen.Points.Count == 2);
        Assert(text.Type == AnnotationType.Text && text.Text == "标注");
    }

    public static void EffectsAndCommandHistoryShouldRemainModelBased()
    {
        var items = new List<AnnotationItem>();
        var mosaic = new MosaicAnnotation { X = 10, Y = 20, Width = 100, Height = 60, PixelSize = 12 };
        var blur = new BlurAnnotation { X = 30, Y = 40, Width = 80, Height = 50, Radius = 8 };
        var history = new UndoRedoManager();

        history.Execute(new AddAnnotationCommand(items, mosaic, 0));
        history.Execute(new AddAnnotationCommand(items, blur, 1));
        Assert(items.Count == 2 && mosaic.Type == AnnotationType.Mosaic && blur.Type == AnnotationType.Blur);
        history.Undo();
        Assert(items.Count == 1 && ReferenceEquals(items[0], mosaic));
        history.Redo();
        Assert(items.Count == 2 && ReferenceEquals(items[1], blur));
    }

    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Phase 5 标注模型契约失败。");
    }
}
