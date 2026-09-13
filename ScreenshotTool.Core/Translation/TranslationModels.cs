namespace ScreenshotTool.Core.Translation;

public sealed class TranslationRequest
{
    public string Text { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = "auto";
    public string TargetLanguage { get; init; } = "zh-CN";
}

public sealed class TranslationResult
{
    public string Text { get; init; } = string.Empty;
    public string? DetectedSourceLanguage { get; init; }
}

public readonly record struct LanguageDirection(string SourceLanguage, string TargetLanguage)
{
    public static LanguageDirection Detect(string text)
    {
        var meaningful = text.Count(character => !char.IsWhiteSpace(character));
        var chinese = text.Count(character => character is >= '\u4e00' and <= '\u9fff');
        return meaningful > 0 && chinese / (double)meaningful > .2
            ? new LanguageDirection("zh-CN", "en")
            : new LanguageDirection("auto", "zh-CN");
    }
}
