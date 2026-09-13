using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ScreenshotTool.Core.Translation;

public interface ITranslationModelService
{
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);
}

public sealed class TranslationModelService : ITranslationModelService, IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("请先填写有效的 HTTPS Base URL。");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("请先填写 API Key。");

        var endpoint = new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/"), "models");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("API Key 无效，或没有读取模型列表的权限。");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"模型接口返回错误：{(int)response.StatusCode}。该服务商可能不支持自动列出模型，可直接手动填写。 ");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("服务商返回的模型列表格式不兼容，请手动填写模型名。");

        var models = data.EnumerateArray()
            .Select(item => item.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (models.Length == 0) throw new InvalidOperationException("当前 API Key 没有返回任何可用模型。");
        return models;
    }

    public void Dispose() => _client.Dispose();
}
