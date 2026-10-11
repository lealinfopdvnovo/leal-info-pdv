using System.Text;
namespace LealInfoPDV.Licensing;
internal static class LicenseInput
{
    internal static string Normalize(string text)
    {
        var value = text.Trim().TrimStart('\uFEFF').Trim();
        if (value.StartsWith("LEAL1-", StringComparison.Ordinal))
            value = Encoding.UTF8.GetString(Convert.FromBase64String(value[6..])).Trim().TrimStart('\uFEFF').Trim();
        return value;
    }
}
