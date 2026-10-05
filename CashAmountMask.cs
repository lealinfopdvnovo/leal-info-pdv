using System.Globalization;
namespace LealInfoPDV;

// Applied only to the manual cash movement amount field.
internal static class CashAmountMask
{
    internal static void Attach(TextBox field)
    {
        var updating = false;
        var previous = "0,00";
        void Format(string digits)
        {
            digits = digits.TrimStart('0');
            if (digits.Length == 0) digits = "0";
            if (digits.Length > 11) { field.Text = previous; field.SelectionStart = field.TextLength; return; }
            var cents = decimal.Parse(digits, CultureInfo.InvariantCulture);
            updating = true;
            field.Text = (cents / 100m).ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
            previous = field.Text;
            field.SelectionStart = field.TextLength;
            updating = false;
        }
        field.KeyPress += (_, e) =>
        {
            if (char.IsControl(e.KeyChar) && e.KeyChar != '\b') return;
            e.Handled = true;
            var digits = new string(field.Text.Where(c => c >= '0' && c <= '9').ToArray());
            if (e.KeyChar == '\b') Format(field.SelectionLength == field.TextLength ? "0" : digits[..Math.Max(0, digits.Length - 1)]);
            else if (e.KeyChar >= '0' && e.KeyChar <= '9')
                Format((field.SelectionLength == field.TextLength ? "" : digits) + e.KeyChar);
        };
        field.TextChanged += (_, _) =>
        {
            if (updating) return;
            var text = field.Text;
            if (text.Any(c => !(c >= '0' && c <= '9') && c != '.' && c != ','))
            {
                updating = true; field.Text = previous; field.SelectionStart = field.TextLength; updating = false;
                return;
            }
            Format(new string(text.Where(c => c >= '0' && c <= '9').ToArray()));
        };
        Format("0");
    }
}
