namespace ScreenshotTool.Core.Security;

public interface ISecretStorageService
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
