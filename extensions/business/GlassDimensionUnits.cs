using System.Globalization;

namespace LealInfoPDV;

// Adaptador de entrada: medidas técnicas continuam em milímetros.
public static class GlassDimensionUnits
{
    public static readonly string[] Symbols = { "mm", "cm", "m" };
    public static decimal Factor(string unit) => unit switch
    { "mm" => 1m, "cm" => 10m, "m" => 1000m, _ => throw new ArgumentException("Unidade inválida.") };
    public static decimal ToMillimetres(decimal value, string unit) => checked(value * Factor(unit));
    public static decimal Convert(decimal value, string from, string to) => ToMillimetres(value, from) / Factor(to);
    public static string ConvertText(string value, string from, string to)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number))
            throw new ArgumentException("Confira a dimensão antes de trocar a unidade.");
        return Convert(number, from, to).ToString("G29", CultureInfo.CurrentCulture);
    }
}
