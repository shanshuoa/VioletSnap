namespace ScreenshotTool.Core.Translation;

public sealed record TranslationProviderPreset(
    string Id,
    string DisplayName,
    string BaseUrl,
    string SuggestedModel,
    string Hint);

public static class TranslationProviderCatalog
{
    public static IReadOnlyList<TranslationProviderPreset> All { get; } =
    [
        new("OpenAI", "OpenAI", "https://api.openai.com/v1/", "gpt-4.1-mini", "使用 OpenAI 官方兼容接口。"),
        new("XiaomiMiMo", "小米 MiMo", "", "", "请从小米 MiMo 控制台复制兼容接口地址与模型名。"),
        new("DeepSeek", "DeepSeek", "https://api.deepseek.com/", "deepseek-chat", "DeepSeek 官方兼容接口。"),
        new("Kimi", "Kimi（月之暗面）", "https://api.moonshot.ai/v1/", "", "模型名请按 Kimi 控制台当前可用模型填写。"),
        new("Qwen", "通义千问（阿里云百炼）", "https://dashscope.aliyuncs.com/compatible-mode/v1/", "qwen-plus", "使用阿里云百炼北京地域兼容接口。"),
        new("Doubao", "豆包（火山方舟）", "https://ark.cn-beijing.volces.com/api/v3/", "", "模型名通常填写火山方舟推理接入点 ID。"),
        new("MiniMax", "MiniMax", "https://api.minimaxi.com/v1/", "", "使用 MiniMax 国内兼容接口。"),
        new("Zhipu", "智谱 GLM", "https://open.bigmodel.cn/api/paas/v4/", "", "模型名请按智谱开放平台当前可用模型填写。"),
        new("Custom", "自定义兼容接口", "", "", "适用于其他支持 OpenAI Chat Completions 协议的服务。")
    ];

    public static TranslationProviderPreset Find(string? id) =>
        All.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? All.First(item => item.Id == "Custom");
}
