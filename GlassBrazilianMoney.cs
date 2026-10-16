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
    private string? keyboard;
    private bool writing;
    private TextBox? Editor => Controls.OfType<TextBox>().FirstOrDefault();
    internal GlassMoneyInput()
    {
        DecimalPlaces = 2; Maximum = 1000000000m; ThousandsSeparator = true;
        hint.SetToolTip(this, "Digite centavos: 10050 = R$ 100,50. Também aceita 100,50. Apagar zera o valor.");
    }
    protected override void UpdateEditText()
    {
        writing = true;
        try { ChangingText = true; Text = GlassBrazilianMoney.Format(Value); }
        finally { writing = false; }
    }
    private bool ParseInput(string input, out decimal number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(input)) return true;
        if (Regex.IsMatch(input, @"^[0-9]+$", RegexOptions.CultureInvariant))
            return decimal.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out number) && (number /= 100m) <= Maximum;
        return GlassBrazilianMoney.TryParse(input, out number);
    }
    protected override void ValidateEditText()
    {
        if (UserEdit)
        {
            string input = Text; UserEdit = false; keyboard = null;
            if (ParseInput(input, out var number) && number >= Minimum && number <= Maximum) Value = number;
        }
        UpdateEditText();
    }
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (!writing && string.IsNullOrWhiteSpace(Text))
        { UserEdit = false; keyboard = ""; Value = 0; }
    }
    private void ApplyKeyboard()
    {
        string input = keyboard ?? "";
        string parsed = input.EndsWith(',') ? input + "0" : input;
        if (!ParseInput(parsed, out var number) || number < Minimum || number > Maximum) return;
        UserEdit = false; Value = number; UpdateEditText();
        if (Editor != null) Editor.SelectionStart = Text.Length;
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsDigit(e.KeyChar) || e.KeyChar == ',' || e.KeyChar == '\b')
        {
            bool selectedAll = Editor != null && Editor.SelectionLength == Text.Length;
            if (selectedAll) keyboard = "";
            keyboard ??= decimal.Truncate(Value * 100m).ToString(CultureInfo.InvariantCulture);
            if (e.KeyChar == '\b') keyboard = keyboard.Length > 0 ? keyboard[..^1] : "";
            else if (e.KeyChar == ',') { if (!keyboard.Contains(',')) keyboard += ","; }
            else if (!keyboard.Contains(',') || keyboard.Length - keyboard.IndexOf(',') <= 2) keyboard += e.KeyChar;
            e.Handled = true; ApplyKeyboard(); return;
        }
        base.OnKeyPress(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        { keyboard = ""; ApplyKeyboard(); e.Handled = true; e.SuppressKeyPress = true; return; }
        if (e.Control && e.KeyCode == Keys.A) { Select(0, Text.Length); e.Handled = true; return; }
        base.OnKeyDown(e);
    }
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e); keyboard = null; Select(0, Text.Length);
    }
    protected override void OnLeave(EventArgs e)
    { base.OnLeave(e); keyboard = null; }
    protected override void Dispose(bool disposing)
    {
        if (disposing) hint.Dispose(); base.Dispose(disposing);
    }
}
