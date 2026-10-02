using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV.Network;

public sealed record LicenseTerms(string ServerSerial, int ComputerLimit, string BillingMode,
    DateTimeOffset? ExpiresUtc, string SellerContact, long Revision, string LicenseId, string Plan = "plus", string ClientCode = "", bool Active = true);
public sealed record SignedLicense(string Payload, string Signature);
internal sealed record RegisteredDevice(string Serial, string PublicKey, string Name);
internal sealed class LicenseState
{
    public string SignedLicense { get; set; } = "";
    public List<RegisteredDevice> Devices { get; set; } = new();
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class NetworkLicense
{
    private readonly object gate = new();
    private readonly string path;
    private readonly string serial;
    private readonly string publicKey;
    private LicenseState state;
    public NetworkLicense(string path, string serial, string? verificationKey = null)
    {
        this.path = path; this.serial = serial; publicKey = verificationKey ?? SellerPublicKey.Pem;
        state = File.Exists(path) ? ProtectedFile.Read<LicenseState>(path) : new();
        if (!File.Exists(path)) Save();
    }
    public static string LimitNotice(int limit, string contact = "") =>
        $"Esta licença permite o uso do sistema em somente {limit} computadores (servidor incluído).\n\n" +
        "Para utilizar mais computadores, contrate um ponto adicional. Entre em contato com o vendedor." +
        (string.IsNullOrWhiteSpace(contact) ? "" : $"\n\nContato: {contact}");
    public LicenseTerms Terms
    {
        get { lock (gate) return TermsUnsafe(); }
    }
    public int RegisteredCount { get { lock (gate) return 1 + state.Devices.Count; } }
    private LicenseTerms TermsUnsafe() => string.IsNullOrEmpty(state.SignedLicense)
        ? new(serial, 2, "a_definir", null, "", 0, "BASE") : Verify(state.SignedLicense);
    private LicenseTerms Verify(string text) => Decode(text, serial, publicKey);
    internal static LicenseTerms Decode(string text, string serial, string publicKey)
    {
        var envelope = JsonSerializer.Deserialize<SignedLicense>(text) ?? throw new InvalidDataException("Licença inválida.");
        var payload = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (!rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new InvalidDataException("A assinatura da licença é inválida. Contate o vendedor.");
        var terms = JsonSerializer.Deserialize<LicenseTerms>(payload) ?? throw new InvalidDataException("Licença inválida.");
        if (terms.ServerSerial != serial || terms.Plan is not ("standard" or "plus" or "pro") ||
            (terms.Plan == "standard" && terms.ComputerLimit != 1) ||
            (terms.ClientCode.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(terms.ClientCode, @"^[0-9]{3,9}$")) || terms.ComputerLimit < (terms.Plan == "standard" ? 1 : 2) || terms.ComputerLimit > 1000 || terms.Revision < 1 ||
            terms.BillingMode is not ("unico" or "mensal") || (terms.BillingMode == "mensal" && terms.ExpiresUtc == null) ||
            (terms.BillingMode == "unico" && terms.ExpiresUtc != null) || string.IsNullOrWhiteSpace(terms.LicenseId))
            throw new InvalidDataException("Esta licença não é válida para este servidor ou modalidade.");
        return terms;
    }
    public void Import(string text)
    {
        lock (gate)
        {
            var terms = Verify(text);
            if (TermsUnsafe().ClientCode.Length>0 && terms.ClientCode!=TermsUnsafe().ClientCode)throw new InvalidDataException("A licença pertence a outro código de cliente.");
            if (terms.Revision <= TermsUnsafe().Revision) throw new InvalidDataException("Esta licença já foi aplicada ou foi substituída por uma mais recente.");
            if (terms.ComputerLimit < RegisteredCount) throw new InvalidDataException("A licença possui menos pontos que os computadores já cadastrados.");
            if (terms.ExpiresUtc <= EffectiveNow()) throw new InvalidDataException("A licença está vencida. Contate o vendedor.");
            state.SignedLicense = text; Save();
        }
    }
    private DateTimeOffset EffectiveNow()
    {
        var now = DateTimeOffset.UtcNow;
        if (now > state.LastSeenUtc) state.LastSeenUtc = now;
        return state.LastSeenUtc;
    }
    public void CheckAccess()
    {
        lock (gate)
        {
            var terms = TermsUnsafe();
            if (!terms.Active) throw new InvalidOperationException("A licença está inativa. Entre em contato com o vendedor.");
            var now = EffectiveNow(); Save();
            if (terms.ExpiresUtc <= now) throw new InvalidOperationException("A licença mensal está vencida. Entre em contato com o vendedor para renovar." +
                (string.IsNullOrWhiteSpace(terms.SellerContact) ? "" : "\nContato: " + terms.SellerContact));
        }
    }
    internal void Reload() { lock(gate) state=ProtectedFile.Read<LicenseState>(path); }
    internal string SignedText { get { lock(gate) return state.SignedLicense; } }
    internal void Register(string deviceSerial, string key, string name)
    {
        lock (gate)
        {
            CheckAccess();
            if (string.IsNullOrWhiteSpace(deviceSerial) || string.IsNullOrWhiteSpace(key) || deviceSerial == serial)
                throw new InvalidOperationException("Identificação de computador inválida.");
            var existing = state.Devices.FirstOrDefault(d => d.Serial == deviceSerial);
            if (existing != null)
            {
                if (existing.PublicKey != key) throw new InvalidOperationException("Este computador foi reinstalado ou a identidade mudou. Contate o vendedor.");
                return;
            }
            if (state.Devices.Any(d => d.PublicKey == key)) throw new InvalidOperationException("Identidade já utilizada por outro computador. Contate o vendedor.");
            var terms = TermsUnsafe();
            if (RegisteredCount >= terms.ComputerLimit) throw new InvalidOperationException(LimitNotice(terms.ComputerLimit, terms.SellerContact));
            state.Devices.Add(new(deviceSerial, key, name)); Save();
        }
    }
    private void Save() => ProtectedFile.Write(path, state);
}

internal static class ProtectedFile
{
    // Licença, identidade e cadastro não ficam em configurações editáveis do banco.
    internal static T Read<T>(string path) => JsonSerializer.Deserialize<T>(ProtectedData.Unprotect(
        File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)) ?? throw new InvalidDataException("Arquivo protegido inválido.");
    internal static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(value), null, DataProtectionScope.CurrentUser));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
