using LealInfoPDV.Network;
namespace LealInfoPDV.Licensing;
internal static class InstallationLicense
{
    internal static NetworkLicense? Store { get; private set; }
    internal static LicenseTerms? Current { get; private set; }
    internal static string Edition => Current?.Plan.ToUpperInvariant() ?? "NAO ATIVADO";
    internal static bool HasLia => Current is { Active: true, Plan: "plus" or "pro" };
    internal static bool HasNetwork => Current is { Active: true, Plan: "standard" or "plus" or "pro" };
    internal static bool IsActivated(LicenseTerms terms) => terms.Active && terms.ClientCode.Length > 0 && terms.Revision > 0;
    internal static void LoadLocal()
    {
        Store = null;
        Current = null;
        try
        {
            Store = new NetworkLicense(Path.Combine(Database.AppFolder, "network.license"), MachineLicenseIdentity.Current.Serial, legacySerial: MachineLicenseIdentity.Current.LegacySerial);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new InvalidDataException("Não foi possível abrir a licença protegida deste usuário do Windows. " +
                "O arquivo foi preservado. Reaplicar a chave não recupera o cadastro protegido. " +
                "Abra com o mesmo usuário da ativação ou solicite diagnóstico ao suporte.", ex);
        }
        Current = Store.Terms;
    }
    internal static void RefreshLocal() { Store?.Reload(); Current = Store?.Terms; Store?.CheckAccess(); if(Current==null || !IsActivated(Current))throw new InvalidOperationException("Ative a licença do cliente antes de abrir o PDV."); }
    internal static void AcceptServer(string signed, string serial, string? verificationKey = null)
    {
        var terms = NetworkLicense.Decode(signed, serial, verificationKey ?? SellerPublicKey.Pem);
        if(!IsActivated(terms) || terms.ExpiresUtc <= DateTimeOffset.UtcNow)
            throw new NetworkAccessException("O servidor precisa de uma licença ativa Standard, Plus ou Pro. Contate o vendedor.");
        Current = terms;
    }
    internal static bool EnsureActivated()
    {
        try { LoadLocal(); if(IsActivated(Current!)) { Store!.CheckAccess(); return true; } }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Licença do PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        using var activation = new ActivationForm();
        return activation.ShowDialog() == DialogResult.OK;
    }
}
