using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV.Network;

public sealed record LicenseTerms(string ServerSerial, int ComputerLimit, string BillingMode,
    DateTimeOffset? ExpiresUtc, string SellerContact, long Revision, string LicenseId, string Plan = "plus", string ClientCode = "", bool Active = true,
    int? AdditionalTerminals = null)
{
    public int TotalComputers => AdditionalTerminals is int extra ? 2 + extra : Math.Max(2, ComputerLimit);
}
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
    private readonly string? legacySerial;
    private Exception? stateFailure;
    private string independentSigned = "";
    internal bool NeedsStateRecovery => stateFailure != null;
    private string SignedPath => path + ".signed";
    public NetworkLicense(string path, string serial, string? verificationKey = null, string? legacySerial = null)
    {
        this.path = path; this.serial = serial; this.legacySerial = legacySerial; publicKey = verificationKey ?? SellerPublicKey.Pem;
        state = new();
        LoadState();
        if (!File.Exists(path) && stateFailure == null) Save();
    }
    public static string LimitNotice(int limit, string contact = "") =>
        "LIMITE DE COMPUTADORES ATINGIDO\n\nSua licença já está utilizando todos os computadores disponíveis.\n\n" +
        "Para adicionar outro caixa ou terminal, adquira uma licença de ponto adicional.\n\nEntre em contato com o vendedor." +
        (string.IsNullOrWhiteSpace(contact) ? "" : $"\n\nContato: {contact}");
    public LicenseTerms Terms
    {
        get { lock (gate) return TermsUnsafe(); }
    }
    public int RegisteredCount { get { lock (gate) { CheckNetworkState(); return 1 + state.Devices.Count; } } }
    internal void CheckNetworkState()
    {
        if (NeedsStateRecovery) throw new InvalidDataException("O cadastro protegido de terminais precisa de recuperação. Nenhum terminal foi apagado ou liberado novamente.", stateFailure);
    }
    private void LoadState()
    {
        independentSigned = File.Exists(SignedPath) ? File.ReadAllText(SignedPath) : "";
        if (independentSigned.Length > 0) _ = Verify(independentSigned);
        try
        {
            state = File.Exists(path) ? ProtectedFile.Read<LicenseState>(path) : new();
            stateFailure = null;
            if (state.SignedLicense.Length > 0)
            {
                _ = Verify(state.SignedLicense);
                if (independentSigned.Length > 0 && Verify(independentSigned) != Verify(state.SignedLicense))
                {
                    var committed = Verify(state.SignedLicense);
                    var previous = Verify(independentSigned);
                    if (committed.ClientCode == previous.ClientCode && committed.Revision > previous.Revision)
                        SaveIndependent(state.SignedLicense); // gravação protegida concluída antes de interrupção
                    else
                        throw new InvalidDataException("Licença independente e cadastro protegido divergentes. Recuperação necessária; nenhum estado foi substituído.");
                }
                if (independentSigned.Length == 0) SaveIndependent(state.SignedLicense);
            }
            else if (independentSigned.Length > 0)
                throw new InvalidDataException("Cadastro protegido não corresponde à licença independente.");
        }
        catch (CryptographicException ex)
        {
            stateFailure = ex;
            // Não substituir nem zerar o arquivo original.
            state = new();
        }
    }
    private LicenseTerms TermsUnsafe()
    {
        if (NeedsStateRecovery)
        {
            if (independentSigned.Length > 0) return Verify(independentSigned);
            throw new InvalidDataException("O arquivo de licença antigo está ilegível e não possui cópia assinada independente. Preserve o original para recuperação assistida.", stateFailure);
        }
        return string.IsNullOrEmpty(state.SignedLicense) ? new(serial, 2, "a_definir", null, "", 0, "BASE") : Verify(state.SignedLicense);
    }
    private LicenseTerms Verify(string text)
    {
        // Só aceita o serial legado deste computador, nunca um serial fornecido pelo usuário.
        try { return Decode(text, serial, publicKey); }
        catch (InvalidDataException) when (legacySerial != null && legacySerial != serial)
        { return Decode(text, legacySerial, publicKey); }
    }
    internal static LicenseTerms Decode(string text, string serial, string publicKey)
    {
        var envelope = JsonSerializer.Deserialize<SignedLicense>(text) ?? throw new InvalidDataException("Licença inválida.");
        var payload = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (!rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new InvalidDataException("A assinatura da licença é inválida. Contate o vendedor.");
        var terms = JsonSerializer.Deserialize<LicenseTerms>(payload) ?? throw new InvalidDataException("Licença inválida.");
        if (terms.ServerSerial != serial || terms.Plan is not ("standard" or "plus" or "pro") ||
            (terms.ClientCode.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(terms.ClientCode, @"^[0-9]{3,9}$")) || terms.ComputerLimit < (terms.Plan == "standard" && terms.AdditionalTerminals == null ? 1 : 2) || terms.ComputerLimit > 1000 || terms.Revision < 1 ||
            terms.AdditionalTerminals is < 0 or > 998 || (terms.AdditionalTerminals != null && terms.ComputerLimit != terms.TotalComputers) ||
            terms.BillingMode is not ("unico" or "mensal") || (terms.BillingMode == "mensal" && terms.ExpiresUtc == null) ||
            (terms.BillingMode == "unico" && terms.ExpiresUtc != null) || string.IsNullOrWhiteSpace(terms.LicenseId))
            throw new InvalidDataException("Esta licença não é válida para este servidor ou modalidade.");
        return terms;
    }
    public void Import(string text)
    {
        lock (gate)
        {
            text = Licensing.LicenseInput.Normalize(text);
            var terms = Verify(text);
            CheckNetworkState();
            if (terms == TermsUnsafe()) { CheckAccess(); return; }
            if (TermsUnsafe().ClientCode.Length>0 && terms.ClientCode!=TermsUnsafe().ClientCode)throw new InvalidDataException("A licença pertence a outro código de cliente.");
            if (terms.Revision <= TermsUnsafe().Revision) throw new InvalidDataException("Esta licença já foi aplicada ou foi substituída por uma mais recente.");
            if (terms.TotalComputers < RegisteredCount) throw new InvalidDataException("A licença possui menos pontos que os computadores já cadastrados.");
            if (terms.ExpiresUtc <= EffectiveNow()) throw new InvalidDataException("A licença está vencida. Contate o vendedor.");
            state.SignedLicense = text; Save();
        }
    }
    // A mesma chave pode concluir uma ativação interrompida; uma revisão substituída nunca volta a valer.
    internal void Activate(string text)
    {
        lock (gate)
        {
            text = Licensing.LicenseInput.Normalize(text);
            var terms = Verify(text);
            if (terms.ClientCode.Length == 0) throw new InvalidDataException("Esta licença antiga não contém o código do cliente. Solicite ao vendedor uma nova chave de ativação para este serial.");
            var current = TermsUnsafe();
            if (terms == current)
            {
                CheckAccess();
                return;
            }
            Import(text);
            CheckAccess();
        }
    }
    private DateTimeOffset EffectiveNow()
    {
        var now = DateTimeOffset.UtcNow;
        if (now > state.LastSeenUtc) state.LastSeenUtc = now;
        return state.LastSeenUtc;
    }
    public void CheckAccess() => CheckAccessCore(true);
    internal void CheckAccessReadOnly() => CheckAccessCore(false);
    private void CheckAccessCore(bool persist)
    {
        lock (gate)
        {
            var terms = TermsUnsafe();
            if (!terms.Active) throw new InvalidOperationException("A licença está inativa. Entre em contato com o vendedor.");
            if (NeedsStateRecovery)
            {
                // Pagamento único pode ser validado pela assinatura sem depender do DPAPI.
                // Mensalidade e cadastro de terminais precisam do relógio/estado protegido.
                if (terms.BillingMode != "unico") CheckNetworkState();
                return;
            }
            var now = EffectiveNow(); if(persist)Save();
            if (terms.ExpiresUtc <= now) throw new InvalidOperationException("A licença mensal está vencida. Entre em contato com o vendedor para renovar." +
                (string.IsNullOrWhiteSpace(terms.SellerContact) ? "" : "\nContato: " + terms.SellerContact));
        }
    }
    internal void Reload() { lock(gate) LoadState(); }
    internal string SignedText { get { lock(gate) return NeedsStateRecovery ? independentSigned : state.SignedLicense; } }
    internal void Register(string deviceSerial, string key, string name)
    {
        lock (gate)
        {
            CheckNetworkState();
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
            if (RegisteredCount >= terms.TotalComputers) throw new InvalidOperationException(LimitNotice(terms.TotalComputers, terms.SellerContact));
            state.Devices.Add(new(deviceSerial, key, name)); Save();
        }
    }
    private void Save()
    {
        CheckNetworkState();
        // A cópia independente só é criada com conteúdo RSA já validado.
        // Se houver falha entre as gravações, a divergência bloqueia, nunca escolhe a revisão antiga.
        ProtectedFile.Write(path, state);
        if (state.SignedLicense.Length > 0) SaveIndependent(state.SignedLicense);
    }
    private void SaveIndependent(string signed)
    {
        _ = Verify(signed);
        var temp = SignedPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(signed);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(SignedPath)) File.Replace(temp, SignedPath, SignedPath + ".previous");
            else File.Move(temp, SignedPath);
            independentSigned = signed;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
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
        // Proteção continua vinculada ao usuário do Windows; nenhum estado é reinicializado.
        var encrypted = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(value), null, DataProtectionScope.CurrentUser);
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                stream.Write(encrypted);
                stream.Flush(flushToDisk: true);
            }
            // Verifica a cópia antes de substituir o estado funcional.
            _ = Read<T>(temp);
            if (File.Exists(path))
                File.Replace(temp, path, path + ".previous", ignoreMetadataErrors: false);
            else
                File.Move(temp, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
