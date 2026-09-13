using System.Windows;
using System.Windows.Input;
using ScreenshotTool.Core.Hotkeys;
using ScreenshotTool.Core.Infrastructure;
using ScreenshotTool.Core.Security;
using ScreenshotTool.Core.Translation;

namespace ScreenshotTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly ISecretStorageService _secrets;
    private readonly ITranslationModelService _modelService;
    private readonly IHotkeyManager _hotkeyManager;
    private bool _initializing;
    private bool _synchronizingKey;
    private System.Threading.CancellationTokenSource? _modelCancellation;

    public SettingsWindow(ISettingsService settings, ISecretStorageService secrets, ITranslationModelService modelService, IHotkeyManager hotkeyManager)
    {
        InitializeComponent();
        FixedWindowLayout.Fit(this, 760, 800);
        _settings = settings; _secrets = secrets; _modelService = modelService; _hotkeyManager = hotkeyManager;
        _initializing = true;
        ScreenshotHotkeyBox.Text = settings.Current.Hotkeys.Screenshot;
        PinHotkeyBox.Text = settings.Current.Hotkeys.Pin;
        ProviderBox.ItemsSource = TranslationProviderCatalog.All;
        ProviderBox.SelectedItem = TranslationProviderCatalog.Find(settings.Current.Translation.Provider);
        LanguageABox.ItemsSource = TranslationLanguageCatalog.All;
        LanguageBBox.ItemsSource = TranslationLanguageCatalog.All;
        LanguageABox.SelectedItem = TranslationLanguageCatalog.Find(settings.Current.Translation.LanguageA);
        LanguageBBox.SelectedItem = TranslationLanguageCatalog.Find(settings.Current.Translation.LanguageB);
        BaseUrlBox.Text = settings.Current.Translation.BaseUrl;
        ModelBox.Text = settings.Current.Translation.Model;
        if (!string.IsNullOrWhiteSpace(settings.Current.Translation.ApiKeyEncrypted))
        {
            try
            {
                var apiKey = _secrets.Unprotect(settings.Current.Translation.ApiKeyEncrypted);
                ApiKeyBox.Password = apiKey;
                ApiKeyVisibleBox.Text = apiKey;
            }
            catch
            {
                ModelStatusText.Text = "已保存的密钥无法解密，请重新输入。";
                ModelStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 68, 93));
            }
        }
        _initializing = false;
        UpdateProviderHint();
        UpdateLanguagePairText();
        ShowSettingsPage(showLanguagePage: false);
        Closed += (_, _) => _modelCancellation?.Cancel();
        BaseUrlBox.TextChanged += (_, _) => InvalidateModels();
    }

    private void OnProviderChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initializing || ProviderBox.SelectedItem is not TranslationProviderPreset preset) return;
        BaseUrlBox.Text = preset.BaseUrl;
        ModelBox.Text = preset.SuggestedModel;
        UpdateProviderHint();
    }

    private void UpdateProviderHint()
    {
        if (ProviderBox.SelectedItem is not TranslationProviderPreset preset) return;
        ProviderHint.Text = preset.Hint + "\nAPI Key 留空表示保留已保存的密钥；所有请求均由本机直接发送给所选服务商。";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var snapshot = System.Text.Json.JsonSerializer.Serialize(_settings.Current);
        try { SaveSettings(); }
        catch
        {
            var previous = System.Text.Json.JsonSerializer.Deserialize<ScreenshotTool.Core.Configuration.AppSettings>(snapshot)!;
            var current = _settings.Current;
            current.Hotkeys.Screenshot = previous.Hotkeys.Screenshot;
            current.Hotkeys.Pin = previous.Hotkeys.Pin;
            current.Translation.Provider = previous.Translation.Provider;
            current.Translation.BaseUrl = previous.Translation.BaseUrl;
            current.Translation.Model = previous.Translation.Model;
            current.Translation.ApiKeyEncrypted = previous.Translation.ApiKeyEncrypted;
            current.Translation.LanguageA = previous.Translation.LanguageA;
            current.Translation.LanguageB = previous.Translation.LanguageB;
            current.Ocr.PreferredLanguages = previous.Ocr.PreferredLanguages;
            _hotkeyManager.Register(current.Hotkeys.Screenshot, current.Hotkeys.Pin);
            System.Windows.MessageBox.Show(this, "设置未保存成功，请检查配置目录权限后重试。", "保存失败");
        }
    }

    private void SaveSettings()
    {
        if (!TryValidateHotkeys(out var screenshotHotkey, out var pinHotkey)) return;
        if (LanguageABox.SelectedItem is not TranslationLanguageOption languageA ||
            LanguageBBox.SelectedItem is not TranslationLanguageOption languageB ||
            string.Equals(languageA.Code, languageB.Code, StringComparison.OrdinalIgnoreCase))
        {
            ShowSettingsPage(showLanguagePage: true);
            System.Windows.MessageBox.Show(this, "请选择两种不同的互译语言。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var baseUrl = BaseUrlBox.Text.Trim();
        if (baseUrl.Length > 0 && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps))
        {
            ShowSettingsPage(showLanguagePage: false);
            System.Windows.MessageBox.Show(this, "请填写有效的 HTTPS Base URL。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            BaseUrlBox.Focus();
            return;
        }
        if (GetCurrentApiKey().Length > 0 && (baseUrl.Length == 0 || string.IsNullOrWhiteSpace(ModelBox.Text)))
        {
            ShowSettingsPage(showLanguagePage: false);
            System.Windows.MessageBox.Show(this, "请填写模型名或推理接入点 ID。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            ModelBox.Focus();
            return;
        }
        var previousScreenshotHotkey = _settings.Current.Hotkeys.Screenshot;
        var previousPinHotkey = _settings.Current.Hotkeys.Pin;
        var registration = _hotkeyManager.Register(screenshotHotkey, pinHotkey);
        if (!registration.Success)
        {
            _hotkeyManager.Register(previousScreenshotHotkey, previousPinHotkey);
            ShowSettingsPage(showLanguagePage: false);
            HotkeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 68, 93));
            HotkeyStatusText.Text = string.Join(" ", registration.Errors) + " 原快捷键已恢复，请换一个组合键。";
            return;
        }

        _settings.Current.Hotkeys.Screenshot = screenshotHotkey;
        _settings.Current.Hotkeys.Pin = pinHotkey;
        _settings.Current.Translation.Provider = (ProviderBox.SelectedItem as TranslationProviderPreset)?.Id ?? "Custom";
        _settings.Current.Translation.BaseUrl = baseUrl;
        _settings.Current.Translation.Model = ModelBox.Text.Trim();
        _settings.Current.Translation.LanguageA = languageA.Code;
        _settings.Current.Translation.LanguageB = languageB.Code;
        _settings.Current.Ocr.PreferredLanguages = [languageA.OcrLanguageTag, languageB.OcrLanguageTag];
        var apiKey = GetCurrentApiKey();
        if (!string.IsNullOrWhiteSpace(apiKey))
            _settings.Current.Translation.ApiKeyEncrypted = _secrets.Protect(apiKey);
        _settings.Save();
        DialogResult = true;
    }
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnShowGeneralPage(object sender, RoutedEventArgs e) => ShowSettingsPage(showLanguagePage: false);
    private void OnExportSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "设置文件 (*.json)|*.json", FileName = "ZhouTianCapture.settings.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var exported = new ScreenshotTool.Core.Configuration.AppSettings();
            exported.Hotkeys.Screenshot = ScreenshotHotkeyBox.Text;
            exported.Hotkeys.Pin = PinHotkeyBox.Text;
            exported.Translation.Provider = (ProviderBox.SelectedItem as TranslationProviderPreset)?.Id ?? "Custom";
            exported.Translation.BaseUrl = BaseUrlBox.Text;
            exported.Translation.Model = ModelBox.Text;
            exported.Translation.LanguageA = (LanguageABox.SelectedItem as TranslationLanguageOption)?.Code ?? "zh-CN";
            exported.Translation.LanguageB = (LanguageBBox.SelectedItem as TranslationLanguageOption)?.Code ?? "en";
            System.IO.File.WriteAllText(dialog.FileName, System.Text.Json.JsonSerializer.Serialize(exported, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            ModelStatusText.Text = "设置已导出，不包含 API Key。";
        }
        catch { System.Windows.MessageBox.Show(this, "无法导出，请检查保存位置。", "导出失败"); }
    }

    private void OnImportSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "设置文件 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new System.IO.FileInfo(dialog.FileName).Length > 1024 * 1024) throw new InvalidOperationException();
            var imported = System.Text.Json.JsonSerializer.Deserialize<ScreenshotTool.Core.Configuration.AppSettings>(System.IO.File.ReadAllText(dialog.FileName)) ?? throw new InvalidOperationException();
            ScreenshotHotkeyBox.Text = imported.Hotkeys.Screenshot;
            PinHotkeyBox.Text = imported.Hotkeys.Pin;
            ProviderBox.SelectedItem = TranslationProviderCatalog.Find(imported.Translation.Provider);
            BaseUrlBox.Text = imported.Translation.BaseUrl;
            ModelBox.Text = imported.Translation.Model;
            LanguageABox.SelectedItem = TranslationLanguageCatalog.Find(imported.Translation.LanguageA);
            LanguageBBox.SelectedItem = TranslationLanguageCatalog.Find(imported.Translation.LanguageB);
            ModelStatusText.Text = "已载入设置，请检查后保存；本机 Key 未变更。";
        }
        catch { System.Windows.MessageBox.Show(this, "设置文件无法读取或格式不正确。", "导入失败"); }
    }
    private void OnShowLanguagePage(object sender, RoutedEventArgs e) => ShowSettingsPage(showLanguagePage: true);

    private void ShowSettingsPage(bool showLanguagePage)
    {
        GeneralSettingsPage.Visibility = showLanguagePage ? Visibility.Collapsed : Visibility.Visible;
        LanguageSettingsPage.Visibility = showLanguagePage ? Visibility.Visible : Visibility.Collapsed;
        SetPageButtonSelected(GeneralPageButton, !showLanguagePage);
        SetPageButtonSelected(LanguagePageButton, showLanguagePage);
    }

    private static void SetPageButtonSelected(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
        button.Foreground = selected
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(94, 61, 181))
            : System.Windows.Media.Brushes.White;
        button.BorderBrush = selected
            ? System.Windows.Media.Brushes.White
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(85, 255, 255, 255));
    }

    private void OnSwapLanguages(object sender, RoutedEventArgs e)
    {
        (LanguageABox.SelectedItem, LanguageBBox.SelectedItem) = (LanguageBBox.SelectedItem, LanguageABox.SelectedItem);
        UpdateLanguagePairText();
    }

    private void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initializing) UpdateLanguagePairText();
    }

    private void UpdateLanguagePairText()
    {
        if (LanguageABox.SelectedItem is TranslationLanguageOption languageA && LanguageBBox.SelectedItem is TranslationLanguageOption languageB)
            LanguagePairText.Text = $"{languageA.DisplayName}  ⇄  {languageB.DisplayName}";
    }

    private void OnHotkeyPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.TextBox box) return;
        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        if (key is Key.Tab) return;
        eventArgs.Handled = true;

        if (key is Key.Delete or Key.Back)
        {
            box.Text = string.Empty;
            HotkeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(129, 117, 143));
            HotkeyStatusText.Text = $"{(Equals(box.Tag, "Screenshot") ? "截图" : "贴图")}快捷键已设为停用，保存后生效。";
            return;
        }

        if (HotkeyGesture.IsModifierKey(key))
        {
            HotkeyStatusText.Text = "请继续按下一个非修饰键。";
            return;
        }

        if (HotkeyGesture.TryCreate(key, Keyboard.Modifiers, out var gesture, out var error))
        {
            box.Text = gesture.DisplayText;
            HotkeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 65, 198));
            HotkeyStatusText.Text = $"已录入 {gesture.DisplayText}，点击保存后立即生效。";
        }
        else
        {
            HotkeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 68, 93));
            HotkeyStatusText.Text = error;
        }
    }

    private bool TryValidateHotkeys(out string screenshotHotkey, out string pinHotkey)
    {
        screenshotHotkey = ScreenshotHotkeyBox.Text.Trim();
        pinHotkey = PinHotkeyBox.Text.Trim();
        if (!HotkeyGesture.TryParse(screenshotHotkey, out var screenshot, out var screenshotError))
        {
            ShowSettingsPage(showLanguagePage: false);
            HotkeyStatusText.Text = "截图快捷键：" + screenshotError;
            ScreenshotHotkeyBox.Focus();
            return false;
        }
        if (!HotkeyGesture.TryParse(pinHotkey, out var pin, out var pinError))
        {
            ShowSettingsPage(showLanguagePage: false);
            HotkeyStatusText.Text = "贴图快捷键：" + pinError;
            PinHotkeyBox.Focus();
            return false;
        }
        if (!string.IsNullOrWhiteSpace(screenshotHotkey) && !string.IsNullOrWhiteSpace(pinHotkey) &&
            screenshot.Modifiers == pin.Modifiers && screenshot.VirtualKey == pin.VirtualKey)
        {
            ShowSettingsPage(showLanguagePage: false);
            HotkeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 68, 93));
            HotkeyStatusText.Text = "截图和贴图不能使用相同快捷键。";
            return false;
        }

        screenshotHotkey = string.IsNullOrWhiteSpace(screenshotHotkey) ? string.Empty : screenshot.DisplayText;
        pinHotkey = string.IsNullOrWhiteSpace(pinHotkey) ? string.Empty : pin.DisplayText;
        return true;
    }

    private string GetCurrentApiKey() => ApiKeyVisibleBox.Visibility == Visibility.Visible
        ? ApiKeyVisibleBox.Text.Trim()
        : ApiKeyBox.Password.Trim();

    private void OnToggleKeyVisibility(object sender, RoutedEventArgs e)
    {
        _synchronizingKey = true;
        if (ApiKeyVisibleBox.Visibility == Visibility.Visible)
        {
            ApiKeyBox.Password = ApiKeyVisibleBox.Text;
            ApiKeyVisibleBox.Visibility = Visibility.Collapsed;
            ApiKeyBox.Visibility = Visibility.Visible;
            ToggleKeyButton.Content = "显示";
            ApiKeyBox.Focus();
        }
        else
        {
            ApiKeyVisibleBox.Text = ApiKeyBox.Password;
            ApiKeyBox.Visibility = Visibility.Collapsed;
            ApiKeyVisibleBox.Visibility = Visibility.Visible;
            ToggleKeyButton.Content = "隐藏";
            ApiKeyVisibleBox.Focus();
            ApiKeyVisibleBox.CaretIndex = ApiKeyVisibleBox.Text.Length;
        }
        _synchronizingKey = false;
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_synchronizingKey) return;
        InvalidateModels();
        _synchronizingKey = true;
        ApiKeyVisibleBox.Text = ApiKeyBox.Password;
        _synchronizingKey = false;
    }

    private void OnApiKeyTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_synchronizingKey) return;
        InvalidateModels();
        _synchronizingKey = true;
        ApiKeyBox.Password = ApiKeyVisibleBox.Text;
        _synchronizingKey = false;
    }

    private void OnClearApiKey(object sender, RoutedEventArgs e)
    {
        var previous = _settings.Current.Translation.ApiKeyEncrypted;
        try
        {
            _settings.Current.Translation.ApiKeyEncrypted = string.Empty;
            _settings.Save();
        }
        catch
        {
            _settings.Current.Translation.ApiKeyEncrypted = previous;
            ModelStatusText.Text = "清除未成功，请检查配置目录权限后重试。";
            return;
        }
        InvalidateModels();
        _synchronizingKey = true;
        ApiKeyBox.Clear();
        ApiKeyVisibleBox.Clear();
        _synchronizingKey = false;

        ModelBox.ItemsSource = null;
        ModelStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 65, 198));
        ModelStatusText.Text = "API Key 已从输入框和本机配置中清除。";
    }

    private async void OnLoadModels(object sender, RoutedEventArgs e)
    {
        InvalidateModels();
        using var cancellation = new System.Threading.CancellationTokenSource();
        _modelCancellation = cancellation;
        LoadModelsButton.IsEnabled = false;
        ModelStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 65, 198));
        ModelStatusText.Text = "正在验证 API Key 并读取可用模型…";
        try
        {
            var selectedModel = ModelBox.Text.Trim();
            var models = await _modelService.GetAvailableModelsAsync(BaseUrlBox.Text.Trim(), GetCurrentApiKey(), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            ModelBox.ItemsSource = models;
            ModelBox.Text = models.Contains(selectedModel, StringComparer.OrdinalIgnoreCase) ? selectedModel : models[0];
            ModelStatusText.Text = $"验证成功，当前 Key 可用模型 {models.Count} 个。";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            ModelStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 68, 93));
            ModelStatusText.Text = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(_modelCancellation, cancellation))
            {
                _modelCancellation = null;
                LoadModelsButton.IsEnabled = true;
            }
        }
    }

    private void InvalidateModels()
    {
        _modelCancellation?.Cancel();
        _modelCancellation = null;
        if (LoadModelsButton is null || ModelBox is null) return;
        LoadModelsButton.IsEnabled = true;
        var model = ModelBox.Text;
        ModelBox.ItemsSource = null;
        ModelBox.Text = model;
        ModelStatusText.Text = "配置已更新，可重新读取模型。";
    }
}
