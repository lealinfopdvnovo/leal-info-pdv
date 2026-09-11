using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

// Sexto estilo da Tela de Vendas: verde/branco com fundo marmorizado verde e dourado.
internal static class GreenWhiteSalesThemeBootstrap
{
    private const string SettingKey = "sales_theme_green_white";
    private static readonly HashSet<IntPtr> hookedChoosers = new();
    private static readonly HashSet<IntPtr> themedSales = new();
    private static System.Windows.Forms.Timer? timer;

    [ModuleInitializer]
    internal static void Initialize()
    {
        timer = new System.Windows.Forms.Timer { Interval = 180 };
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    private static void Tick()
    {
        try
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            {
                if (form.IsDisposed || !form.Visible) continue;
                var title = form.Text ?? string.Empty;

                if (title.Contains("Estilo da Tela de Vendas", StringComparison.OrdinalIgnoreCase))
                    EnsureGreenWhiteOption(form);
                else if (title.Contains("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase) && ReadSetting())
                {
                    if (!themedSales.Contains(form.Handle))
                    {
                        ApplyGreenWhite(form);
                        themedSales.Add(form.Handle);
                    }
                }
            }
        }
        catch { }
    }

    private static void EnsureGreenWhiteOption(Form chooser)
    {
        if (hookedChoosers.Contains(chooser.Handle)) return;

        var options = Descendants(chooser).OfType<TableLayoutPanel>()
            .Where(x => x.ColumnCount == 2)
            .OrderByDescending(x => x.Controls.OfType<Button>().Count())
            .FirstOrDefault(x => x.Controls.OfType<Button>().Count() >= 5);
        if (options is null) return;

        if (options.RowCount < 3) options.RowCount = 3;
        while (options.RowStyles.Count < 3)
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));

        foreach (var original in options.Controls.OfType<Button>().ToArray())
            original.Click += (_, _) => WriteSetting(false);

        var green = new Button
        {
            Text = "VERDE E BRANCO\n\nMármore verde + branco + dourado",
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            BackColor = Color.FromArgb(25, 128, 72),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        green.FlatAppearance.BorderColor = Color.FromArgb(218, 178, 70);
        green.FlatAppearance.BorderSize = 3;
        green.BackgroundImage = CreateMarble(360, 170);
        green.BackgroundImageLayout = ImageLayout.Stretch;

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
        green.BringToFront();
        hookedChoosers.Add(chooser.Handle);
        options.PerformLayout();
        chooser.Invalidate(true);
    }

    private static Bitmap CreateMarble(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(249, 252, 246));

        using (var wash = new LinearGradientBrush(new Rectangle(0, 0, width, height),
            Color.FromArgb(225, 245, 222), Color.White, 25f))
            g.FillRectangle(wash, 0, 0, width, height);

        void Wave(Color color, float thickness, int y, int amp, int shift)
        {
            using var p = new Pen(color, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var path = new GraphicsPath();
            path.AddBezier(-30, y, width * .20f, y - amp, width * .38f, y + amp, width * .58f, y);
            path.AddBezier(width * .58f, y, width * .72f, y - amp - shift, width * .88f, y + amp, width + 30, y - shift);
            g.DrawPath(p, path);
        }

        Wave(Color.FromArgb(110, 30, 125, 60), 54, height / 3, height / 4, 18);
        Wave(Color.FromArgb(85, 65, 160, 82), 30, height * 2 / 3, height / 5, -12);
        Wave(Color.FromArgb(120, 14, 100, 45), 10, height / 2, height / 3, 8);
        Wave(Color.FromArgb(220, 205, 164, 48), 4, height / 2 - 8, height / 3, 5);
        Wave(Color.FromArgb(205, 225, 188, 65), 3, height / 4, height / 5, -8);

        using var gold = new SolidBrush(Color.FromArgb(190, 210, 174, 55));
        var rnd = new Random(7419);
        for (int i = 0; i < 45; i++)
        {
            int x = rnd.Next(width); int y = rnd.Next(height); int s = rnd.Next(2, 7);
            g.FillEllipse(gold, x, y, s, s);
        }
        return bmp;
    }

    private static void ApplyGreenWhite(Form sales)
    {
        var greenDark = Color.FromArgb(19, 91, 50);
        var green = Color.FromArgb(30, 145, 78);
        var greenLight = Color.FromArgb(222, 244, 226);
        var textGreen = Color.FromArgb(20, 70, 43);
        var gold = Color.FromArgb(205, 166, 52);
        var white = Color.White;

        sales.BackColor = Color.FromArgb(245, 250, 244);
        sales.BackgroundImage?.Dispose();
        sales.BackgroundImage = CreateMarble(1100, 720);
        sales.BackgroundImageLayout = ImageLayout.Stretch;

        foreach (var panel in Descendants(sales).OfType<Panel>())
        {
            if (panel.Height >= 65 && panel.Dock == DockStyle.Top)
            {
                panel.BackgroundImage = null;
                panel.BackColor = greenDark;
            }
        }

        var layouts = Descendants(sales).OfType<TableLayoutPanel>().ToArray();
        var main = layouts.FirstOrDefault(t => t.ColumnCount == 3 && t.RowCount == 1);
        if (main is not null)
        {
            main.BackgroundImage?.Dispose();
            main.BackgroundImage = CreateMarble(1100, 650);
            main.BackgroundImageLayout = ImageLayout.Stretch;
            main.BackColor = Color.Transparent;

            for (int i = 0; i < 3; i++)
            {
                if (main.GetControlFromPosition(i, 0) is Panel p)
                {
                    p.BackgroundImage?.Dispose();
                    p.BackgroundImage = CreateMarble(520, 650);
                    p.BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
        }

        foreach (var label in Descendants(sales).OfType<Label>())
        {
            var text = label.Text ?? string.Empty;
            if (text.Contains("TOTAL DA VENDA", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("ITENS DA VENDA", StringComparison.OrdinalIgnoreCase))
            {
                label.BackColor = greenDark;
                label.ForeColor = white;
            }
            else if (text.StartsWith("CLIENTE:", StringComparison.OrdinalIgnoreCase))
            {
                label.BackColor = greenLight;
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
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(241, 249, 242);
        }

        foreach (var button in Descendants(sales).OfType<Button>())
        {
            var text = button.Text ?? string.Empty;
            if (text.Contains("FINALIZAR", StringComparison.OrdinalIgnoreCase)) button.BackColor = green;
            else if (text.Contains("ESTILO", StringComparison.OrdinalIgnoreCase)) button.BackColor = gold;
            else if (text.Contains("ADICIONAR", StringComparison.OrdinalIgnoreCase)) button.BackColor = greenDark;
        }

        foreach (var tb in Descendants(sales).OfType<TextBox>()) { tb.BackColor = white; tb.ForeColor = textGreen; }
        foreach (var nud in Descendants(sales).OfType<NumericUpDown>()) { nud.BackColor = white; nud.ForeColor = textGreen; }

        sales.Invalidate(true);
    }

    private static bool ReadSetting()
    {
        try
        {
            using var cn = Database.Open(); using var cmd = cn.CreateCommand();
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
            using var cn = Database.Open(); using var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            cmd.Parameters.AddWithValue("$k", SettingKey);
            cmd.Parameters.AddWithValue("$v", enabled ? "1" : "0"); cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
