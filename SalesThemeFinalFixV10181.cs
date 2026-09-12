using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

/// <summary>
/// V10.181: ajustes finais solicitados nos temas nativos da Tela de Vendas.
/// Não altera Futurista Azul, Dark Premium nem Verde e Branco.
/// </summary>
internal static class SalesThemeFinalFixV10181
{
    private static bool _registered;

    [ModuleInitializer]
    internal static void Register()
    {
        if (_registered) return;
        _registered = true;
        Application.Idle += OnIdle;
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        try
        {
            if (ReadSetting("sales_theme_green_white", "0") == "1")
                return;

            var theme = ReadSetting("sales_theme", string.Empty).Trim().ToUpperInvariant();
            if (theme.Length == 0) return;

            foreach (Form form in Application.OpenForms)
            {
                if (form.IsDisposed || !form.Text.Contains("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (theme.Contains("CLEAN"))
                    ApplyCleanPro(form);
                else if (theme.Contains("BLUE") && theme.Contains("RED"))
                    ApplyBlueRed(form);
                else if (theme.Contains("ROSA") || theme.Contains("PINK"))
                    ApplyPink(form);
            }
        }
        catch
        {
            // Ajuste visual nunca deve impedir o funcionamento do PDV.
        }
    }

    private static void ApplyCleanPro(Form form)
    {
        foreach (var label in Descendants<Label>(form))
        {
            var t = Normalize(label.Text);
            if (IsTotalTitle(t) || IsItemsTitle(t))
                label.ForeColor = Color.FromArgb(205, 35, 45);
        }
    }

    private static void ApplyBlueRed(Form form)
    {
        foreach (var label in Descendants<Label>(form))
        {
            var t = Normalize(label.Text);
            if (IsTotalTitle(t) || IsItemsTitle(t))
                label.ForeColor = Color.Black;
        }
    }

    private static void ApplyPink(Form form)
    {
        foreach (var label in Descendants<Label>(form))
        {
            var t = Normalize(label.Text);
            if (!t.Contains("PRESSIONE F2") || !t.Contains("FORMA DE PAGAMENTO"))
                continue;

            label.ForeColor = Color.Black;
            label.BackColor = Color.White;

            if (label.Parent is Control parent)
            {
                try
                {
                    parent.BackgroundImage?.Dispose();
                    parent.BackgroundImage = null;
                }
                catch { parent.BackgroundImage = null; }
                parent.BackColor = Color.White;
            }
        }
    }

    private static bool IsTotalTitle(string text) =>
        text.Contains("TOTAL DA VENDA") || text.Contains("TOTAL DE VENDA") || text.Contains("TOTAL DE VENDAS");

    private static bool IsItemsTitle(string text) =>
        text.Contains("LEAL INFO") && text.Contains("ITENS DA VENDA");

    private static string Normalize(string? text) =>
        (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim().ToUpperInvariant();

    private static System.Collections.Generic.IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static string ReadSetting(string key, string fallback)
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key=$k";
            cmd.Parameters.AddWithValue("$k", key);
            return Convert.ToString(cmd.ExecuteScalar()) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
