namespace LealInfoPDV;

// Medidas do vão, não medidas de fabricação ou de corte.
public sealed record GlassProject(string Description, decimal WidthMm, decimal HeightMm, int Quantity)
{
    public const string ModelName = "JANELA DE CORRER — 2 FOLHAS";
    public string? ValidationError => WidthMm <= 0 || HeightMm <= 0
        ? "Informe largura e altura maiores que zero, em milímetros."
        : Quantity < 1 ? "Informe quantidade maior que zero." : null;
}
