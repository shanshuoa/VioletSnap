using ScreenshotTool.Core.Security;
using ScreenshotTool.Core.Translation;
using ScreenshotTool.Core.Configuration;
using System.Text.Json;

namespace ScreenshotTool.Tests;

internal static class TranslationContractTests
{
    public static void DirectionAndSecretStorageShouldWork()
    {
        var chinese = LanguageDirection.Detect("这是一个中文测试");
        var english = LanguageDirection.Detect("This is an English test.");
        if (chinese.TargetLanguage != "en" || english.TargetLanguage != "zh-CN")
            throw new InvalidOperationException("中英文翻译方向检测失败。");

        var secrets = new SecretStorageService();
        const string key = "test-key-do-not-store";
        var protectedValue = secrets.Protect(key);
        if (protectedValue == key || secrets.Unprotect(protectedValue) != key)
            throw new InvalidOperationException("DPAPI 密钥保护往返测试失败。");
    }

    public static void ProviderCatalogShouldContainCommonProviders()
    {
        var ids = TranslationProviderCatalog.All.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "OpenAI", "XiaomiMiMo", "DeepSeek", "Kimi", "Qwen", "Doubao", "MiniMax", "Zhipu", "Custom" })
            if (!ids.Contains(required)) throw new InvalidOperationException($"翻译服务商预设缺失：{required}");

        if (TranslationProviderCatalog.All.Where(item => item.Id is not "XiaomiMiMo" and not "Custom")
            .Any(item => !Uri.TryCreate(item.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("翻译服务商预设必须使用有效 HTTPS 地址。");
    }

    public static void LanguagePairShouldSupportCommonLanguagesAndPersist()
    {
        var codes = TranslationLanguageCatalog.All.Select(language => language.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "zh-CN", "zh-TW", "en", "ja", "ko", "fr", "de", "es", "ru", "ar" })
            if (!codes.Contains(required)) throw new InvalidOperationException($"互译语言列表缺失：{required}");

        var settings = new AppSettings();
        settings.Translation.LanguageA = "ja";
        settings.Translation.LanguageB = "fr";
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings));
        if (restored?.Translation.LanguageA != "ja" || restored.Translation.LanguageB != "fr")
            throw new InvalidOperationException("互译语言配置未能持久化往返。");
        if (TranslationLanguageCatalog.Find("ja").OcrLanguageTag != "ja-JP")
            throw new InvalidOperationException("互译语言与 OCR 语言包映射错误。");
    }
}
