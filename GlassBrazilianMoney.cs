using System.Globalization;
using System.Text.RegularExpressions;

namespace LealInfoPDV;

// Formatação/parsing de moeda, sem alterar o cálculo comercial ou a cultura do PDV.
internal static class GlassBrazilianMoney
{
    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");
    internal static string Format(decimal value) => value.ToString("C2", Culture).Replace('\u00a0', ' ');
    internal static bool TryParse(string text, out decimal value)
    {
        value = 0;
        string clean = text.Trim().Replace("\u00a0", " ");
        if (clean.StartsWith("R$", StringComparison.OrdinalIgnoreCase)) clean = clean[2..].Trim();
        // Pontos só podem separar grupos de milhares; centavos usam vírgula.
        // Impede interpretar 100,50 como 10050, e recusa 100.50 ambíguo.
        if (!Regex.IsMatch(clean, @"^(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]{1,2})?$", RegexOptions.CultureInvariant)) return false;
        return decimal.TryParse(clean, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, Culture, out value);
    }
}

internal sealed class GlassMoneyInput : NumericUpDown
{
    private readonly ToolTip hint = new();
    internal GlassMoneyInput()
    {
        DecimalPlaces = 2; Maximum = 1000000000m; ThousandsSeparator = true;
        hint.SetToolTip(this, "Valor em reais. Use vírgula para centavos: 100,50 ou R$ 100,50.");
    }
    protected override void UpdateEditText()
    {
        ChangingText = true;
        Text = GlassBrazilianMoney.Format(Value);
    }
    protected override void ValidateEditText()
    {
        if (UserEdit)
        {
            string input = Text;
            UserEdit = false;
            if (GlassBrazilianMoney.TryParse(input, out var number) && number >= Minimum && number <= Maximum)
                Value = number;
            // Entrada inválida conserva o último valor; nunca remove a vírgula
            // nem grava um valor financeiro inventado.
        }
        UpdateEditText();
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.KeyChar is ',' or '.') e.Handled = false;
    }
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Select(0, Text.Length);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) hint.Dispose();
        base.Dispose(disposing);
    }
}
