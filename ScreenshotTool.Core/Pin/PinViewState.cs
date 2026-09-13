namespace ScreenshotTool.Core.Pin;

public sealed class PinViewState
{
    public double Scale { get; private set; } = 1.0;
    public double Opacity { get; private set; } = 1.0;

    public void Zoom(bool zoomIn)
    {
        Scale = Math.Clamp(zoomIn ? Scale * 1.05 : Scale / 1.05, 0.1, 5.0);
    }

    public void SetScale(double scale)
    {
        Scale = Math.Clamp(scale, 0.1, 5.0);
    }

    public void AdjustOpacity(bool increase)
    {
        Opacity = Math.Clamp(Opacity + (increase ? 0.05 : -0.05), 0.1, 1.0);
    }

    public void Reset()
    {
        Scale = 1.0;
        Opacity = 1.0;
    }
}
