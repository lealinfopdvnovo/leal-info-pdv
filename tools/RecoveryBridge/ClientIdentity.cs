using LealInfoPDV.Network;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LealInfoProtection;

public static class ClientIdentity
{
    public static string LocalSerial()
    {
        string source;
        try { using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"); source = key?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName; }
        catch { source = Environment.MachineName; }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO|" + source)));
        return $"LI-{hash[..4]}-{hash[4..8]}-{hash[8..12]}-{hash[12..16]}";
    }
    public static void Validate(string data, string serial, string? verificationKey = null)
    {
        var path = Path.Combine(data, "network.license");
        if (!File.Exists(path)) throw new InvalidDataException("Licença existente não encontrada. Abra a proteção na conta Windows que utiliza o PDV.");
        var license = new NetworkLicense(path, serial, verificationKey);
        license.CheckAccessReadOnly();
        if (license.Terms.ClientCode != "001" || license.Terms.Revision < 1)
            throw new InvalidOperationException("A proteção é exclusiva do cliente 001 com licença assinada e ativa.");
        var config = Path.Combine(data, "network.config");
        if (File.Exists(config) && ProtectedFile.Read<JsonElement>(config).GetProperty("Mode").GetString() == "terminal")
            throw new InvalidOperationException("Ative a proteção somente no servidor 001.");
    }
}
