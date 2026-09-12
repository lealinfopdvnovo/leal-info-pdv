using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

/// <summary>
/// V10.180: impede que o mármore Verde e Branco permaneça nos temas nativos
/// e corrige textos claros que ficam sem contraste após a troca de estilo.
/// </summary>
internal static class ThemeIsolationFixV10180
{
    private static readonly HashSet<Button> Hooked = new();
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
            foreach (Form form in Application.OpenForms)
            {
                if (form.IsDisposed) continue;
                if (!string.Equals(form.Text, "Estilo da Tela de Vendas", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var button in Descendants<Button>(form))
                {
                    if (Hooked.Contains(button)) continue;
                    if (string.Equals(Convert.ToString(button.Tag), "Verde e Branco", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Hooked.Add(button);
                    button.Click += (_, _) =>
                    {
                        WriteSetting("sales_theme_green_white", "0");

                        foreach (Form open in Application.OpenForms)
                        {
                            if (!open.Text.Contains("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (open.IsHandleCreated)
                                open.BeginInvoke(new Action(() => RemoveGreenResidue(open)));
                            break;
                        }
                    };
                }
            }
        }
        catch
        {
            // Ajuste visual nunca deve impedir o PDV de funcionar.
        }
    }

    private static void RemoveGreenResidue(Form sales)
    {
        try
        {
            ClearBackgroundImage(sales);

            foreach (var control in Descendants<Control>(sales))
            {
                ClearBackgroundImage(control);

                if (control is Label label && label.Parent is not null)
                {
                    var bg = label.Parent.BackColor;
                    var fg = label.ForeColor;
                    int bgBrightness = (bg.R + bg.G + bg.B) / 3;
                    int fgBrightness = (fg.R + fg.G + fg.B) / 3;

                    // Evita branco/quase branco sobre áreas claras, especialmente no PDV Rosa.
                    if (bgBrightness >= 205 && fgBrightness >= 220)
                        label.ForeColor = Color.FromArgb(18, 55, 85);
                }
            }

            sales.Invalidate(true);
        }
        catch
        {
        }
    }

    private static void ClearBackgroundImage(Control control)
    {
        try
        {
            if (control.BackgroundImage is null) return;
            var old = control.BackgroundImage;
            control.BackgroundImage = null;
            old.Dispose();
        }
        catch
        {
            control.BackgroundImage = null;
        }
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void WriteSetting(string key, string value)
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }
        catch
        {
        }
    }
}
