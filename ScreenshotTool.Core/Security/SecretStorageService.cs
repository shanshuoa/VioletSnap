using System.Runtime.InteropServices;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.Core.Security;

public sealed class SecretStorageService : ISecretStorageService
{
    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(ProtectOrUnprotect(bytes, protect: true));
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return string.Empty;
        return System.Text.Encoding.UTF8.GetString(ProtectOrUnprotect(Convert.FromBase64String(protectedText), protect: false));
    }

    private static byte[] ProtectOrUnprotect(byte[] input, bool protect)
    {
        var inputBlob = NativeMethods.CreateDataBlob(input);
        try
        {
            var success = protect
                ? NativeMethods.CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output)
                : NativeMethods.CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!success) NativeMethods.ThrowLastWin32Error("无法保护设置中的密钥。");
            try
            {
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally { NativeMethods.LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(inputBlob.Data); }
    }
}
