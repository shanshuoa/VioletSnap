using System.Windows.Media.Imaging;
using ScreenshotTool.Core.Annotation.Models;

namespace ScreenshotTool.Core.Annotation;

public interface IAnnotationRenderer
{
    BitmapSource Render(BitmapSource original, IReadOnlyList<AnnotationItem> annotations);
}
