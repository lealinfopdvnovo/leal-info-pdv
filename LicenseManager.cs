using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

public enum LicenseEdition
{
    Demo,
    Pro
}

public sealed record LicenseState(
    LicenseEdition Edition,
    bool IsValid,
    DateTime? ExpiresAt,
    string DeviceId,
    string Key)
{
    public bool IsDemo => Edition == LicenseEdition.Demo;
    public bool IsPro => Edition == LicenseEdition.Pro;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}

public static class LicenseManager
{
    private const string LicenseFileName = "license.key";
    private static LicenseState? _current;

    public static LicenseState Current => _current ??= Load();
    public static LicenseEdition Edition => Current.Edition;
    public static bool IsDemo => Current.IsDemo;
    public static bool IsPro => Current.IsPro;
    public static bool IsValid => Current.IsValid && !Current.IsExpired;

    public static string DeviceId()
    {
        var raw = $"{Environment.MachineName}|{Environment.OSVersion.VersionString}|{Environment.ProcessorCount}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16];
    }

    public static LicenseState Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, LicenseFileName);
            if (!File.Exists(path))
                return DemoFallback();

            var key = File.ReadAllText(path).Trim();
            if (TryParse(key, out var state))
                return state;
        }
        catch
        {
            // A licença nunca deve impedir a abertura de uma apresentação.
        }

        return DemoFallback();
    }

    public static void Reload() => _current = Load();

    public static bool TryParse(string key, out LicenseState state)
    {
        state = DemoFallback();
        if (string.IsNullOrWhiteSpace(key)) return false;

        var parts = key.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || !parts[0].Equals("LIPDV", StringComparison.OrdinalIgnoreCase))
            return false;

        var device = DeviceId();
        var editionText = parts[1].ToUpperInvariant();

        if (editionText == "DEMO")
        {
            if (parts.Length != 4 || !DateTime.TryParseExact(parts[2], "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var expires))
                return false;

            if (!parts[3].Equals(device, StringComparison.OrdinalIgnoreCase) && parts[3] != "ANY")
                return false;

            state = new LicenseState(LicenseEdition.Demo, true, expires.Date.AddDays(1).AddTicks(-1).ToUniversalTime(), device, key);
            return true;
        }

        if (editionText == "PRO")
        {
            if (parts.Length != 3) return false;
            if (!parts[2].Equals(device, StringComparison.OrdinalIgnoreCase) && parts[2] != "ANY")
                return false;

            state = new LicenseState(LicenseEdition.Pro, true, null, device, key);
            return true;
        }

        return false;
    }

    private static LicenseState DemoFallback()
        => new(LicenseEdition.Demo, true, DateTime.UtcNow.Date.AddDays(30), DeviceId(), "DEMO-AUTOMATICO");
}
