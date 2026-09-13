using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IO;
using ScreenshotTool.Core.Infrastructure;
using ScreenshotTool.Core.Security;

namespace ScreenshotTool.Core.Translation;

public sealed class OpenAiCompatibleTranslationService : ITranslationService, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ISecretStorageService _secrets;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public OpenAiCompatibleTranslationService(ISettingsService settings, ISecretStorageService secrets)
    {
        _settings = settings;
        _secrets = secrets;
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        var config = _settings.Current.Translation;
        if (string.IsNullOrWhiteSpace(config.BaseUrl) || string.IsNullOrWhiteSpace(config.Model) || string.IsNullOrWhiteSpace(config.ApiKeyEncrypted))
            throw new InvalidOperationException("翻译服务尚未配置，请先打开设置填写地址、模型和 API Key。");
        var endpoint = new Uri(new Uri(config.BaseUrl.EndsWith('/') ? config.BaseUrl : config.BaseUrl + "/"), "chat/completions");
        var languageA = TranslationLanguageCatalog.Find(config.LanguageA);
        var languageB = TranslationLanguageCatalog.Find(config.LanguageB);
        var direction = $"The configured bidirectional language pair is {languageA.PromptName} ({languageA.Code}) and {languageB.PromptName} ({languageB.Code}). " +
                        $"Detect which of these two languages the input primarily uses. If it is {languageA.PromptName}, translate it to {languageB.PromptName}; " +
                        $"if it is {languageB.PromptName}, translate it to {languageA.PromptName}.";
        var body = JsonSerializer.Serialize(new
        {
            model = config.Model,
            temperature = 0.1,
            messages = new[]
            {
                new { role = "system", content = "You are a bidirectional translation engine. Follow the configured language pair exactly. Translate faithfully. Do not explain or summarize. Preserve line breaks and technical terms. Return only the translated text." },
                new { role = "user", content = direction + "\n\n" + request.Text }
            }
        });
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secrets.Unprotect(config.ApiKeyEncrypted));
        message.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException("翻译服务鉴权失败，请检查 API Key。");
        if ((int)response.StatusCode == 429) throw new InvalidOperationException("翻译服务请求过于频繁，请稍后重试。");
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"翻译服务返回错误：{(int)response.StatusCode}。");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("翻译服务响应格式异常。");
        return new TranslationResult { Text = text.Trim() };
    }

    public void Dispose() => _client.Dispose();
}
