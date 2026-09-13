namespace ScreenshotTool.Core.Capture;

public sealed class CaptureSelectionController
{
    private (int X, int Y)? _start;

    public CaptureRegion? Selection { get; private set; }

    public void Begin(int x, int y)
    {
        _start = (x, y);
        Selection = new CaptureRegion(x, y, 0, 0);
    }

    public CaptureRegion Update(int x, int y)
    {
        if (_start is not { } start)
        {
            return Selection ?? default;
        }

        Selection = CaptureRegion.FromPoints(start.X, start.Y, x, y);
        return Selection.Value;
    }

    public CaptureRegion? Complete(int x, int y)
    {
        var result = Update(x, y);
        _start = null;
        return result.IsEmpty ? null : result;
    }
}
