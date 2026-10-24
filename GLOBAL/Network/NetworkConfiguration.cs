using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoPDV.Network;

public sealed class NetworkConfiguration
{
    public string Mode { get; set; } = "local";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 47821;
    public string CertificateHash { get; set; } = "";
    public string PairingSecret { get; set; } = "";
    private static string PathName => Path.Combine(Database.AppFolder, "network.config");
    public static NetworkConfiguration Current { get; private set; } = new();
    public static void Load()
    {
        Current = File.Exists(PathName) ? ProtectedFile.Read<NetworkConfiguration>(PathName) : new();
        if (Current.Mode is not ("local" or "server" or "terminal") || Current.Port is < 1024 or > 65535)
            throw new InvalidDataException("Configuração de rede inválida.");
    }
    public static void Save(NetworkConfiguration value) => ProtectedFile.Write(PathName, value);
    public static NetworkConfiguration NewServer(int port) => new() { Mode = "server", Port = port,
        PairingSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
}
internal sealed record DeviceKey(string PrivateKey, string PublicKey);
internal static class NetworkIdentity
{
    private static readonly Lazy<DeviceKey> key = new(() =>
    {
        var path = Path.Combine(Database.AppFolder, "network.identity");
        if (File.Exists(path)) return ProtectedFile.Read<DeviceKey>(path);
        using var rsa = RSA.Create(3072);
        var value = new DeviceKey(rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
        ProtectedFile.Write(path, value); return value;
    });
    internal static DeviceKey Key => key.Value;
    internal static string Sign(string challenge)
    {
        using var rsa = RSA.Create(); rsa.ImportFromPem(Key.PrivateKey);
        return Convert.ToBase64String(rsa.SignData(Convert.FromBase64String(challenge), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }
}
