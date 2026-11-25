using System.Threading;

namespace ScreenshotTool.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(false, @"Local\ScreenshotTool.SingleInstance", out var firstInstance);
        if (firstInstance) App.Main();
    }
}
