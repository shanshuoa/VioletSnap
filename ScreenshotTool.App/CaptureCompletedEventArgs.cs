using ScreenshotTool.Core.Capture;

namespace ScreenshotTool.App;

public sealed class CaptureCompletedEventArgs : EventArgs
{
    public CaptureCompletedEventArgs(CaptureAction action, CaptureRegion region)
    {
        Action = action;
        Region = region;
    }

    public CaptureAction Action { get; }
    public CaptureRegion Region { get; }
}
