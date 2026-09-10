using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoLicenseGenerator;

internal sealed record LicensePayload(
    int Version,
    string Edition,
    string Customer,
    string DeviceId,
    string LicenseId,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

internal static class LicenseSigner
{
    // Chave mestre fixa do gerador oficial. Não depende mais do computador onde o gerador é executado.
    private const string PrivateKeyPem = """
-----BEGIN PRIVATE KEY-----
MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgARlE7siRnOOLUoYO
sL2QihBd5EjmX5N+Svi/MCairmGhRANCAATUaOlCPHOon2t0A3Eypii81DChArSE
LKNr7fV0FSowfO21Ak0XMWjpUUktBeV8YkuSMg3uDE7xUTEM0gFjng3d
-----END PRIVATE KEY-----
""";

    internal static string CreateLicense(string edition, string customer, string deviceId, DateTime expiresAtUtc)
    {
        using var ecdsa = OpenSigningKey();

        var payload = new LicensePayload(
            1,
            edition.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(customer) ? "CLIENTE" : customer.Trim(),
            string.IsNullOrWhiteSpace(deviceId) ? "ANY" : deviceId.Trim().ToUpperInvariant(),
            Guid.NewGuid().ToString("N").ToUpperInvariant(),
            DateTime.UtcNow,
            expiresAtUtc.ToUniversalTime());

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA256);

        return $"LIPDV1.{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    internal static string ExportPublicKeyPem()
    {
        using var ecdsa = OpenSigningKey();
        return ecdsa.ExportSubjectPublicKeyInfoPem();
    }

    private static ECDsa OpenSigningKey()
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(PrivateKeyPem);
        return ecdsa;
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
