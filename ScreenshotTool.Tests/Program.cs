namespace ScreenshotTool.Tests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            SettingsServiceContractTests.DefaultSettingsShouldEnableClipboardCopy();
            CaptureContractTests.SelectionShouldNormalizeReverseDrag();
            CaptureContractTests.GdiCaptureShouldMatchVirtualScreenSize();
            CaptureContractTests.OversizedWindowSelectionShouldClampToScreen();
            PinViewStateTests.ScaleAndOpacityShouldStayWithinSupportedRange();
            AnnotationContractTests.BasicAnnotationsShouldRetainImageCoordinates();
            AnnotationContractTests.EffectsAndCommandHistoryShouldRemainModelBased();
            TranslationContractTests.DirectionAndSecretStorageShouldWork();
            TranslationContractTests.ProviderCatalogShouldContainCommonProviders();
            TranslationContractTests.LanguagePairShouldSupportCommonLanguagesAndPersist();
            OcrContractTests.ExtractedTextShouldPreserveDetectedLinesAndParagraphs();
            HotkeyContractTests.ConfigurableHotkeysShouldParseAndRejectUnsafeValues();
            Console.WriteLine("Phase 1-10 基础功能契约检查通过。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
