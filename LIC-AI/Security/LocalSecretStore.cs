using System.Security.Cryptography;
using System.Text;

namespace LicAi.Security;

public sealed class LocalSecretStore
{
    private readonly string _path;

    public LocalSecretStore(string path)
    {
        _path = path;
    }

    public string? GetApiKey()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var encrypted = File.ReadAllBytes(_path);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    public void SaveApiKey(string apiKey)
    {
        apiKey = (apiKey ?? string.Empty).Trim();
        if (apiKey.Length == 0) throw new ArgumentException("Chave vazia.", nameof(apiKey));
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        var plain = Encoding.UTF8.GetBytes(apiKey);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, encrypted);
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
