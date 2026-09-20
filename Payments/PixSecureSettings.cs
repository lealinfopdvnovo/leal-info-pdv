using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

internal static class PixSecureSettings
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LEAL-INFO-PDV-PIX-V1");

    public static string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var data = Encoding.UTF8.GetBytes(value.Trim());
        return Convert.ToBase64String(ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser));
    }

    public static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try
        {
            var data = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
        }
        catch { return ""; }
    }
}
