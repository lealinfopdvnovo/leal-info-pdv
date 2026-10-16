namespace LealInfoPDV;

// Cálculo comercial do vão. Não define consumo, corte ou fabricação.
public sealed record GlassQuote(GlassProject Project, decimal PricePerSquareMeter,
    decimal ServiceValue, decimal Discount, decimal Addition)
{
    public decimal Area => Project.WidthMm / 1000m * (Project.HeightMm / 1000m);
    public decimal TotalArea => Area * Project.Quantity;
    public decimal MaterialValue => decimal.Round(TotalArea * PricePerSquareMeter, 2, MidpointRounding.AwayFromZero);
    public decimal BaseValue => MaterialValue + ServiceValue;
    public decimal Total => BaseValue - Discount + Addition;
    public string? ValidationError => Project.ValidationError
        ?? (PricePerSquareMeter < 0 || ServiceValue < 0 || Discount < 0 || Addition < 0
            ? "Valores comerciais não podem ser negativos."
            : Total < 0 ? "O desconto não pode exceder o valor base mais o acréscimo." : null);
}
