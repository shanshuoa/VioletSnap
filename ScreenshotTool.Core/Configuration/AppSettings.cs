namespace ScreenshotTool.Core.Configuration;

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; init; } = new();
    public CaptureSettings Capture { get; init; } = new();
    public PinSettings Pin { get; init; } = new();
    public AnnotationSettings Annotation { get; init; } = new();
    public OcrSettings Ocr { get; init; } = new();
    public TranslationSettings Translation { get; init; } = new();
}

public sealed class HotkeySettings
{
    public string Screenshot { get; set; } = "F1";
    public string Pin { get; set; } = "F3";
}

public sealed class CaptureSettings
{
    public bool ShowCursor { get; init; }
    public bool DefaultCopyToClipboard { get; init; } = true;
}

public sealed class PinSettings
{
    public bool Topmost { get; init; } = true;
    public double DefaultOpacity { get; init; } = 1.0;
}

public sealed class AnnotationSettings
{
    public string DefaultColor { get; set; } = "#FFFF0000";
    public double DefaultThickness { get; set; } = 3;
    public int MosaicPixelSize { get; set; } = 12;
    public double BlurRadius { get; set; } = 8;
}

public sealed class OcrSettings
{
    public string[] PreferredLanguages { get; set; } = ["zh-Hans", "en-US"];
}

public sealed class TranslationSettings
{
    public string Provider { get; set; } = "OpenAI";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string LanguageA { get; set; } = "zh-CN";
    public string LanguageB { get; set; } = "en";
}
