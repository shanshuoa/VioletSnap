using ScreenshotTool.Core.Pin;

namespace ScreenshotTool.Tests;

public static class PinViewStateTests
{
    public static void ScaleAndOpacityShouldStayWithinSupportedRange()
    {
        var state = new PinViewState();
        for (var index = 0; index < 200; index++)
        {
            state.Zoom(zoomIn: false);
            state.AdjustOpacity(increase: false);
        }

        if (state.Scale != 0.1 || state.Opacity != 0.1)
        {
            throw new InvalidOperationException("贴图缩放或透明度下限不正确。");
        }

        state.Reset();
        if (state.Scale != 1.0 || state.Opacity != 1.0)
        {
            throw new InvalidOperationException("贴图恢复 100% 状态不正确。");
        }
    }
}
