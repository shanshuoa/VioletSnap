using ScreenshotTool.Core.Capture;

namespace ScreenshotTool.Tests;

public static class CaptureContractTests
{
    public static void SelectionShouldNormalizeReverseDrag()
    {
        var controller = new CaptureSelectionController();
        controller.Begin(100, 80);
        var result = controller.Complete(20, 10);
        if (result != new CaptureRegion(20, 10, 80, 70))
        {
            throw new InvalidOperationException("选区坐标未按物理像素正确归一化。");
        }
    }

    public static void GdiCaptureShouldMatchVirtualScreenSize()
    {
        var service = new GdiCaptureService();
        var bounds = service.GetVirtualScreenBounds();
        var bitmap = service.CaptureVirtualScreen();
        if (bitmap.PixelWidth != bounds.Width || bitmap.PixelHeight != bounds.Height)
        {
            throw new InvalidOperationException("BitBlt 返回的图像尺寸与虚拟屏尺寸不一致。");
        }
    }

    public static void OversizedWindowSelectionShouldClampToScreen()
    {
        var screen = new VirtualScreenBounds(0, 0, 2560, 1440);
        var oversizedWindow = new CaptureRegion(-8, -8, 2616, 1496);
        var result = oversizedWindow.ClampTo(screen);
        if (result != new CaptureRegion(0, 0, 2560, 1440))
            throw new InvalidOperationException($"全屏窗口选区裁剪失败：{result}");

        var secondary = new VirtualScreenBounds(-1920, 0, 1920, 1080);
        var overflow = new CaptureRegion(-1968, -12, 2016, 1128).ClampTo(secondary);
        if (overflow != new CaptureRegion(-1920, 0, 1920, 1080))
            throw new InvalidOperationException($"负坐标显示器选区裁剪失败：{overflow}");
    }
}
