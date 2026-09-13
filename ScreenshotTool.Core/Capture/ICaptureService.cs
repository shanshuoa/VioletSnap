using System.Windows.Media.Imaging;

namespace ScreenshotTool.Core.Capture;

public interface ICaptureService
{
    VirtualScreenBounds GetVirtualScreenBounds();
    BitmapSource CaptureVirtualScreen();
    BitmapSource CaptureRegion(CaptureRegion region);
}
