using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

// Adiciona o tema Verde e Branco sem mexer na estrutura/logica da tela de vendas.
internal static class GreenWhiteSalesThemeBootstrap
{
    private const string SettingKey = "sales_theme_green_white";
    private static readonly HashSet<IntPtr> hookedChoosers = new();
    private static readonly HashSet<IntPtr> themedSales = new();

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += (_, _) => Tick();
    }

    private static void Tick()
    {
        try
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form.IsDisposed || !form.Visible) continue;

                if (form.Text.Contains("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase))
                {
                    if (ReadSetting() && !themedSales.Contains(form.Handle))
                    {
                        ApplyGreenWhite(form);
                        themedSales.Add(form.Handle);
                    }
                }
                else if (form.Text.Contains("Estilo da Tela de Vendas", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureGreenWhiteOption(form);
                }
            }
        }
        catch
        {
            // Visual opcional nunca deve interromper o PDV.
        }
    }

    private static void EnsureGreenWhiteOption(Form chooser)
    {
        if (hookedChoosers.Contains(chooser.Handle)) return;

        var options = Descendants(chooser)
            .OfType<TableLayoutPanel>()
            .FirstOrDefault(x => x.ColumnCount == 2 && x.RowCount == 3);
        if (options is null) return;

        // Ao escolher qualquer tema original, desativa o Verde e Branco persistente.
        foreach (var original in options.Controls.OfType<Button>())
            original.Click += (_, _) => WriteSetting(false);

        var green = new Button
        {
            Text = "VERDE E BRANCO\n\nVerde clean + branco profissional",
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            BackColor = Color.FromArgb(24, 142, 82),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Tag = "Verde e Branco"
        };
        green.FlatAppearance.BorderColor = Color.White;
        green.FlatAppearance.BorderSize = 3;

        green.Click += (_, _) =>
        {
            WriteSetting(true);
            if (chooser.Owner is Form sales && !sales.IsDisposed)
            {
                ApplyGreenWhite(sales);
                themedSales.Add(sales.Handle);
            }
            chooser.Close();
        };

        options.Controls.Add(green, 1, 2);
        hookedChoosers.Add(chooser.Handle);
    }

    private static void ApplyGreenWhite(Form sales)
    {
        var greenDark = Color.FromArgb(19, 105, 61);
        var green = Color.FromArgb(28, 155, 88);
        var greenLight = Color.FromArgb(218, 242, 226);
        var greenPale = Color.FromArgb(235, 248, 239);
        var textGreen = Color.FromArgb(22, 76, 48);
        var white = Color.White;

        sales.BackColor = greenPale;

        var header = sales.Controls.OfType<Panel>()
            .FirstOrDefault(p => p.Dock == DockStyle.Top && p.Height >= 70 && p.Height <= 110);
        if (header is not null)
        {
            header.BackgroundImage = null;
            header.BackColor = greenDark;
            foreach (var lbl in Descendants(header).OfType<Label>())
                lbl.ForeColor = white;

            var line = header.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Bottom && p.Height <= 8);
            if (line is not null) line.BackColor = Color.FromArgb(70, 215, 125);
        }

        var body = sales.Controls.OfType<TableLayoutPanel>()
            .FirstOrDefault(t => t.ColumnCount == 3 && t.RowCount == 1);
        if (body is not null)
        {
            body.BackgroundImage = null;
            body.BackColor = greenPale;

            var photo = body.GetControlFromPosition(0, 0) as Panel;
            var entry = body.GetControlFromPosition(1, 0) as Panel;
            var receipt = body.GetControlFromPosition(2, 0) as Panel;

            if (photo is not null)
            {
                photo.BackgroundImage = null;
                photo.BackColor = greenDark;
                foreach (var lbl in Descendants(photo).OfType<Label>())
                {
                    if (!lbl.Text.Contains("Selecione um produto", StringComparison.OrdinalIgnoreCase))
                        lbl.ForeColor = white;
                }
            }

            if (entry is not null)
            {
                entry.BackgroundImage = null;
                entry.BackColor = Color.FromArgb(31, 125, 75);
                foreach (var lbl in Descendants(entry).OfType<Label>())
                    lbl.ForeColor = white;
                foreach (var tb in Descendants(entry).OfType<TextBox>())
                {
                    tb.BackColor = white;
                    tb.ForeColor = textGreen;
                }
                foreach (var nud in Descendants(entry).OfType<NumericUpDown>())
                {
                    nud.BackColor = white;
                    nud.ForeColor = textGreen;
                }
            }

            if (receipt is not null)
            {
                receipt.BackgroundImage = null;
                receipt.BackColor = white;
            }
        }

        foreach (var label in Descendants(sales).OfType<Label>())
        {
            var text = label.Text ?? string.Empty;
            if (text.Contains("TOTAL DA VENDA", StringComparison.OrdinalIgnoreCase))
            {
                if (label.Parent is Panel p) p.BackColor = greenDark;
                label.ForeColor = white;
            }
            else if (text.Contains("ITENS DA VENDA", StringComparison.OrdinalIgnoreCase))
            {
                label.BackColor = green;
                label.ForeColor = white;
            }
            else if (text.StartsWith("CLIENTE:", StringComparison.OrdinalIgnoreCase))
            {
                label.BackColor = greenLight;
                label.ForeColor = textGreen;
            }
            else if (text.Contains("Pressione F2", StringComparison.OrdinalIgnoreCase))
            {
                label.ForeColor = textGreen;
            }
        }

        foreach (var grid in Descendants(sales).OfType<DataGridView>())
        {
            grid.BackgroundColor = white;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = greenLight;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = textGreen;
            grid.DefaultCellStyle.SelectionBackColor = green;
            grid.DefaultCellStyle.SelectionForeColor = white;
            grid.AlternatingRowsDefaultCellStyle.BackColor = greenPale;
        }

        foreach (var button in Descendants(sales).OfType<Button>())
        {
            var text = button.Text ?? string.Empty;
            if (text.Contains("ADICIONAR ITEM", StringComparison.OrdinalIgnoreCase))
                button.BackColor = green;
            else if (text.Contains("LIMPAR", StringComparison.OrdinalIgnoreCase))
                button.BackColor = Color.FromArgb(52, 120, 81);
            else if (text.Contains("FINALIZAR VENDA", StringComparison.OrdinalIgnoreCase))
                button.BackColor = Color.FromArgb(15, 165, 88);
            else if (text == "FECHAR")
                button.BackColor = greenDark;
        }

        // Moldura do status (CAIXA LIVRE / item adicionado).
        var status = Descendants(sales).OfType<Label>()
            .FirstOrDefault(l => string.Equals(l.Text, "CAIXA LIVRE", StringComparison.OrdinalIgnoreCase));
        if (status?.Parent is Panel inner)
        {
            inner.BackColor = greenDark;
            if (inner.Parent is Panel frame) frame.BackColor = green;
        }

        sales.Invalidate(true);
    }

    private static bool ReadSetting()
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key=$k";
            cmd.Parameters.AddWithValue("$k", SettingKey);
            return string.Equals(Convert.ToString(cmd.ExecuteScalar()), "1", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private static void WriteSetting(bool enabled)
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            cmd.Parameters.AddWithValue("$k", SettingKey);
            cmd.Parameters.AddWithValue("$v", enabled ? "1" : "0");
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
