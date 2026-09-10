namespace LealInfoPDV;

/// <summary>
/// Matriz comercial de recursos do LEAL INFO PDV.
/// A licença define a edição; esta classe define o que cada edição pode usar.
/// </summary>
public static class LicenseFeatures
{
    public static bool PdvBase => LicenseManager.IsValid;

    // STANDARD: PDV tradicional, sem LIA.
    public static bool LiaEssencial =>
        LicenseManager.IsValid && (LicenseManager.IsPlus || LicenseManager.IsPro);

    // PLUS: LIA Essencial (voz + comandos locais do PDV + conversa limitada por saldo).
    public static bool LiaComandosLocais => LiaEssencial;
    public static bool LiaVoz => LiaEssencial;
    public static bool LiaConversaNatural =>
        LicenseManager.IsValid && (LicenseManager.IsPlus || LicenseManager.IsPro);

    // PRO: LIA completa/expandida, com internet, pesquisa e expansões.
    public static bool LiaExpandida => LicenseManager.IsValid && LicenseManager.IsPro;
    public static bool LiaPesquisaWeb => LiaExpandida;
    public static bool LiaVozNaturalOnline => LiaExpandida;

    public static string NomePlano => LicenseManager.Edition.ToString().ToUpperInvariant();

    public static string ResumoPlano() => LicenseManager.Edition switch
    {
        LicenseEdition.Standard => "STANDARD: PDV tradicional, sem LIA.",
        LicenseEdition.Plus => "PLUS: PDV + LIA Essencial, voz, comandos locais e conversa limitada por saldo.",
        LicenseEdition.Pro => "PRO: PDV + LIA completa, 5 horas de conversa por pacote, pesquisa web e expansões.",
        _ => "Plano não reconhecido."
    };
}
