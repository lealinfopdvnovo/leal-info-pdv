using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfoPDV.Network;
namespace LealInfoPDV.Licensing;
internal sealed record LicenseMigrationRequest(int Version, string Kind, string SignedLicense, string LicenseDigest,
    string FromSerial, string ToHardwareSerial, string ClientCode, string LicenseId, long Revision,
    string? SourceProtectedSha256, LicenseState? Snapshot, string StateAvailability);
internal static class LicenseMigration
{
    internal const string RequestKind = "LEAL-LICENSE-MIGRATION-REQUEST";
    internal const string AuthorizationKind = "LEAL-LICENSE-MIGRATION-AUTHORIZATION";
    internal static string Digest(string signed)
    {
        var envelope = JsonSerializer.Deserialize<SignedLicense>(LicenseInput.Normalize(signed)) ?? throw new InvalidDataException("Envelope inválido.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.Payload + "." + envelope.Signature)));
    }
    internal static LicenseMigrationRequest CreateRequest(string signed, string path, string hardwareSerial, string legacySerial, string publicKey)
    {
        signed = LicenseInput.Normalize(signed);
        if (!hardwareSerial.StartsWith("LH-", StringComparison.Ordinal))
            throw new InvalidDataException("UUID de hardware adequado indisponível. Migração automática não autorizada.");
        LicenseTerms terms;
        try { terms = NetworkLicense.Decode(signed, hardwareSerial, publicKey); }
        catch(InvalidDataException)
        {
            try { terms = NetworkLicense.Decode(signed,legacySerial,publicKey); }
            catch(InvalidDataException)
            {
                var proofPath=path+".migration";
                if(!File.Exists(proofPath))throw;
                var prior=ValidateAuthorization(File.ReadAllText(proofPath),hardwareSerial,publicKey);
                if(prior.LicenseDigest!=Digest(signed))throw new InvalidDataException("Prova anterior não corresponde à licença.");
                terms=NetworkLicense.Decode(signed,prior.FromSerial,publicKey);
            }
        }
        LicenseState? snapshot = null; string? sourceHash = null;
        var availability = "AUSENTE";
        if (File.Exists(path))
        {
            var original = File.ReadAllBytes(path);
            sourceHash = Convert.ToHexString(SHA256.HashData(original));
            try
            {
                snapshot = ProtectedFile.Read<LicenseState>(path);
                var storedTerms = NetworkLicense.Decode(snapshot.SignedLicense, terms.ServerSerial, publicKey);
                if (storedTerms != terms) throw new InvalidDataException("A chave não corresponde ao estado atual; não assine uma migração de revisão antiga.");
                availability = "RECUPERADO";
            }
            catch(Exception ex) when(ex is CryptographicException or JsonException) { snapshot = null; availability = Encoding.UTF8.GetString(original).StartsWith("LEAL-LICENSE-STATE-UNAVAILABLE-V1:",StringComparison.Ordinal) ? "INDISPONIVEL_APOS_INSTALACAO_LIMPA" : "ILEGIVEL_SEM_CHECKPOINT"; }
            if (!original.SequenceEqual(File.ReadAllBytes(path))) throw new IOException("Arquivo mudou durante a leitura. Repita com o PDV fechado.");
        }
        return new(1,RequestKind,signed,Digest(signed),terms.ServerSerial,hardwareSerial,terms.ClientCode,terms.LicenseId,terms.Revision,sourceHash,snapshot,availability);
    }
    internal static LicenseMigrationRequest ValidateAuthorization(string signedAuthorization, string hardwareSerial, string publicKey)
    {
        var envelope = JsonSerializer.Deserialize<SignedLicense>(LicenseInput.Normalize(signedAuthorization)) ?? throw new InvalidDataException("Autorização inválida.");
        var bytes = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (!rsa.VerifyData(bytes,Convert.FromBase64String(envelope.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))
            throw new InvalidDataException("Assinatura da autorização inválida.");
        var request = JsonSerializer.Deserialize<LicenseMigrationRequest>(bytes) ?? throw new InvalidDataException("Autorização inválida.");
        if (request.Version != 1 || request.Kind != AuthorizationKind || request.ToHardwareSerial != hardwareSerial || !hardwareSerial.StartsWith("LH-",StringComparison.Ordinal))
            throw new InvalidDataException("A autorização não pertence a este hardware.");
        var terms = NetworkLicense.Decode(request.SignedLicense,request.FromSerial,publicKey);
        if (request.LicenseDigest != Digest(request.SignedLicense) || terms.ClientCode != request.ClientCode || terms.LicenseId != request.LicenseId || terms.Revision != request.Revision)
            throw new InvalidDataException("Autorização não corresponde à licença original.");
        if (!terms.Active || terms.ExpiresUtc <= DateTimeOffset.UtcNow) throw new InvalidDataException("Licença inativa ou vencida não pode ser migrada.");
        if (request.Snapshot == null && terms.BillingMode != "unico") throw new InvalidDataException("Mensalidade exige checkpoint autorizado do relógio.");
        if (request.Snapshot is { } state)
        {
            if (Digest(state.SignedLicense) != request.LicenseDigest || state.Devices == null || state.Devices.Count + 1 > terms.TotalComputers || state.LastSeenUtc > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new InvalidDataException("Checkpoint incompatível com licença/pontos/relógio.");
            if (state.Devices.Any(d=>string.IsNullOrWhiteSpace(d.Serial) || string.IsNullOrWhiteSpace(d.PublicKey) || (d.Serial==terms.ServerSerial || d.Serial==request.ToHardwareSerial)) ||
                state.Devices.Select(d=>d.Serial).Distinct().Count()!=state.Devices.Count || state.Devices.Select(d=>d.PublicKey).Distinct().Count()!=state.Devices.Count)
                throw new InvalidDataException("Checkpoint de terminais inválido.");
            if (terms.ExpiresUtc <= state.LastSeenUtc) throw new InvalidDataException("Checkpoint indica mensalidade vencida.");
        }
        if (request.SourceProtectedSha256 is { } hash && (hash.Length!=64 || !hash.All(Uri.IsHexDigit))) throw new InvalidDataException("Hash de origem inválido.");
        return request;
    }
    internal static bool IsAuthorization(string text)
    {
        try { var e=JsonSerializer.Deserialize<SignedLicense>(LicenseInput.Normalize(text));using var d=JsonDocument.Parse(Convert.FromBase64String(e!.Payload));return d.RootElement.TryGetProperty("Kind",out var k)&&k.GetString()==AuthorizationKind; }
        catch { return false; }
    }
    internal static void WriteDurable(string path, string text)
    {
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using(var s=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)) {s.Write(Encoding.UTF8.GetBytes(text));s.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
