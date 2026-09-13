namespace ScreenshotTool.Core.Translation;

public sealed record TranslationLanguageOption(string Code, string DisplayName, string PromptName, string OcrLanguageTag);

public static class TranslationLanguageCatalog
{
    public static IReadOnlyList<TranslationLanguageOption> All { get; } =
    [
        new("zh-CN", "简体中文", "Simplified Chinese", "zh-Hans"),
        new("zh-TW", "繁体中文", "Traditional Chinese", "zh-Hant"),
        new("en", "英语", "English", "en-US"),
        new("ja", "日语", "Japanese", "ja-JP"),
        new("ko", "韩语", "Korean", "ko-KR"),
        new("fr", "法语", "French", "fr-FR"),
        new("de", "德语", "German", "de-DE"),
        new("es", "西班牙语", "Spanish", "es-ES"),
        new("pt", "葡萄牙语", "Portuguese", "pt-BR"),
        new("it", "意大利语", "Italian", "it-IT"),
        new("ru", "俄语", "Russian", "ru-RU"),
        new("ar", "阿拉伯语", "Arabic", "ar-SA"),
        new("th", "泰语", "Thai", "th-TH"),
        new("vi", "越南语", "Vietnamese", "vi-VN"),
        new("id", "印度尼西亚语", "Indonesian", "id-ID"),
        new("ms", "马来语", "Malay", "ms-MY"),
        new("hi", "印地语", "Hindi", "hi-IN"),
        new("tr", "土耳其语", "Turkish", "tr-TR")
    ];

    public static TranslationLanguageOption Find(string? code) =>
        All.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? All[0];
}
