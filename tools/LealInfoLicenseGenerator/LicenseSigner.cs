using System.Security.Cryptography;
using System.Text;
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
    private const string KeyName = "LEAL_INFO_PDV_LICENSE_SIGNING_KEY_V1";

    internal static string CreateLicense(string edition, string customer, string deviceId, DateTime expiresAtUtc)
    {
        using var ecdsa = OpenOrCreateSigningKey();

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
        using var ecdsa = OpenOrCreateSigningKey();
        return ecdsa.ExportSubjectPublicKeyInfoPem();
    }

    private static ECDsa OpenOrCreateSigningKey()
    {
        CngKey key;
        if (CngKey.Exists(KeyName))
        {
            key = CngKey.Open(KeyName);
        }
        else
        {
            var options = new CngKeyCreationParameters
            {
                Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                KeyCreationOptions = CngKeyCreationOptions.None,
                KeyUsage = CngKeyUsages.Signing
            };
            key = CngKey.Create(CngAlgorithm.ECDsaP256, KeyName, options);
        }

        return new ECDsaCng(key)
        {
            HashAlgorithm = CngAlgorithm.Sha256
        };
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
