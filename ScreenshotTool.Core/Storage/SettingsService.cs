using System.IO;
using System.Text.Json;
using ScreenshotTool.Core.Configuration;
using ScreenshotTool.Core.Infrastructure;

namespace ScreenshotTool.Core.Storage;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;
    private readonly IAppLogger _logger;

    public SettingsService(string applicationDataDirectory, IAppLogger logger)
    {
        _settingsPath = Path.Combine(applicationDataDirectory, "settings.json");
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            if (!File.Exists(_settingsPath))
            {
                SaveDefaults();
                return;
            }

            var json = File.ReadAllText(_settingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Current = new AppSettings();
            _logger.Error("配置加载失败，已使用默认配置。", exception);
        }
    }

    private void SaveDefaults()
    {
        var json = JsonSerializer.Serialize(Current, SerializerOptions);
        File.WriteAllText(_settingsPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(Current, SerializerOptions);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(true));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("配置保存失败。", exception);
            throw;
        }
    }
}
