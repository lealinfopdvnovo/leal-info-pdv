using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoPDV;

public enum LicenseEdition
{
    Standard,
    Plus,
    Pro
}

public sealed record LicenseState(
    LicenseEdition Edition,
    bool IsValid,
    DateTime? ExpiresAt,
    string DeviceId,
    string Key,
    string Customer,
    string LicenseId,
    string? Error)
{
    public bool IsStandard => Edition == LicenseEdition.Standard;
    public bool IsPlus => Edition == LicenseEdition.Plus;
    public bool IsPro => Edition == LicenseEdition.Pro;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}

internal sealed record SignedLicensePayload(
    int Version,
    string Edition,
    string Customer,
    string DeviceId,
    string LicenseId,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

public static class LicenseManager
{
    private const string LicenseFileName = "license.key";
    private const string PublicKeyFileName = "leal-info-license-public-key.pem";
    private static LicenseState? _current;

    private static string LicenseDirectory
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO", "PDV");

    private static string LicensePath => Path.Combine(LicenseDirectory, LicenseFileName);
    private static string PublicKeyPath => Path.Combine(AppContext.BaseDirectory, PublicKeyFileName);

    public static LicenseState Current => _current ??= Load();
    public static LicenseEdition Edition => Current.Edition;
    public static bool IsStandard => Current.IsStandard;
    public static bool IsPlus => Current.IsPlus;
    public static bool IsPro => Current.IsPro;
    public static bool IsValid => Current.IsValid && !Current.IsExpired;

    public static string DeviceId()
    {
        var raw = $"{Environment.MachineName}|{Environment.OSVersion.VersionString}|{Environment.ProcessorCount}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16];
    }

    public static LicenseState Load()
    {
        try
        {
            if (!File.Exists(LicensePath))
                return Invalid("Licença não ativada.");

            var key = File.ReadAllText(LicensePath).Trim();
            if (TryParse(key, out var state))
                return state;

            return state;
        }
        catch (Exception ex)
        {
            return Invalid("Não foi possível carregar a licença: " + ex.Message);
        }
    }

    public static void Reload() => _current = Load();

    public static bool Activate(string key, out LicenseState state)
    {
        if (!TryParse(key, out state) || !state.IsValid || state.IsExpired)
            return false;

        Directory.CreateDirectory(LicenseDirectory);
        File.WriteAllText(LicensePath, key.Trim());
        _current = state;
        return true;
    }

    public static bool TryParse(string key, out LicenseState state)
    {
        state = Invalid("Serial inválido.");
        if (string.IsNullOrWhiteSpace(key)) return false;

        var parts = key.Trim().Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !parts[0].Equals("LIPDV1", StringComparison.Ordinal))
        {
            state = Invalid("Formato de serial inválido.");
            return false;
        }

        if (!File.Exists(PublicKeyPath))
        {
            state = Invalid($"Chave pública de licenciamento não encontrada: {PublicKeyFileName}");
            return false;
        }

        try
        {
            var payloadBytes = FromBase64Url(parts[1]);
            var signature = FromBase64Url(parts[2]);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(File.ReadAllText(PublicKeyPath));
            if (!ecdsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256))
            {
                state = Invalid("Assinatura do serial inválida.");
                return false;
            }

            var payload = JsonSerializer.Deserialize<SignedLicensePayload>(payloadBytes);
            if (payload is null || payload.Version != 1)
            {
                state = Invalid("Conteúdo da licença inválido.");
                return false;
            }

            if (!Enum.TryParse<LicenseEdition>(payload.Edition, true, out var edition))
            {
                state = Invalid("Plano da licença não reconhecido.");
                return false;
            }

            var device = DeviceId();
            var licensedDevice = string.IsNullOrWhiteSpace(payload.DeviceId) ? "ANY" : payload.DeviceId.Trim().ToUpperInvariant();
            if (licensedDevice != "ANY" && !licensedDevice.Equals(device, StringComparison.OrdinalIgnoreCase))
            {
                state = Invalid("Este serial pertence a outro computador.");
                return false;
            }

            var expiresAt = payload.ExpiresAtUtc.ToUniversalTime();
            if (DateTime.UtcNow > expiresAt)
            {
                state = new LicenseState(edition, false, expiresAt, device, key.Trim(), payload.Customer, payload.LicenseId, "Licença vencida.");
                return false;
            }

            state = new LicenseState(edition, true, expiresAt, device, key.Trim(), payload.Customer, payload.LicenseId, null);
            return true;
        }
        catch (Exception ex)
        {
            state = Invalid("Não foi possível validar o serial: " + ex.Message);
            return false;
        }
    }

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s += s.Length % 4 switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }

    private static LicenseState Invalid(string error)
        => new(LicenseEdition.Standard, false, null, DeviceId(), "", "", "", error);
}
