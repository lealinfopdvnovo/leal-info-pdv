using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

internal static class SafeVisualEnhancements
{
    private static readonly HashSet<PictureBox> ColoredPictures = new();
    private static readonly HashSet<Button> HookedThemeButtons = new();
    private static readonly HashSet<Form> GreenAppliedForms = new();
    private static bool _registered;

    [ModuleInitializer]
    internal static void Register()
    {
        try
        {
            if (_registered) return;
            _registered = true;
            Application.Idle += OnIdle;
        }
        catch
        {
            // Nunca impedir a abertura do PDV por causa de uma melhoria visual.
        }
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        try
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form.IsDisposed) continue;

                if (form is MainForm)
                    ApplyColoredToolbar(form);

                if (string.Equals(form.Text, "Estilo da Tela de Vendas", StringComparison.OrdinalIgnoreCase))
                    EnsureGreenWhiteCard(form);

                if (form.Text.Contains("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase))
                {
                    if (ReadSetting("sales_theme_green_white", "0") == "1" && !GreenAppliedForms.Contains(form))
                    {
                        ApplyGreenWhiteTheme(form);
                        GreenAppliedForms.Add(form);
                    }
                }
            }

            GreenAppliedForms.RemoveWhere(f => f.IsDisposed);
        }
        catch
        {
            // Falha visual não pode afetar o funcionamento do sistema.
        }
    }

    private static void ApplyColoredToolbar(Control root)
    {
        foreach (var pic in Descendants<PictureBox>(root))
        {
            if (ColoredPictures.Contains(pic) || pic.Image == null) continue;
            if (pic.Width < 24 || pic.Width > 64 || pic.Height < 24 || pic.Height > 64) continue;
            if (pic.Parent == null) continue;

            Label? caption = null;
            foreach (Control sibling in pic.Parent.Controls)
            {
                if (sibling is Label lbl && !string.IsNullOrWhiteSpace(lbl.Text))
                {
                    caption = lbl;
                    break;
                }
            }
            if (caption == null) continue;

            var accent = AccentFor(caption.Text);
            if (!accent.HasValue) continue;

            pic.Image = TintIcon(pic.Image, accent.Value);
            ColoredPictures.Add(pic);
        }
    }

    private static Color? AccentFor(string label)
    {
        var t = label.Trim().ToUpperInvariant();
        if (t.Contains("PRODUT")) return Color.FromArgb(255, 205, 35);
        if (t.Contains("CLIENT")) return Color.FromArgb(55, 220, 95);
        if (t.Contains("FORNECED")) return Color.FromArgb(255, 145, 35);
        if (t.Contains("SERVI")) return Color.FromArgb(205, 95, 255);
        if (t.Contains("HIST")) return Color.FromArgb(35, 220, 210);
        if (t.Contains("FLUXO")) return Color.FromArgb(70, 225, 80);
        if (t.Contains("ORDENS") || t == "OS") return Color.FromArgb(255, 105, 55);
        if (t.Contains("ORÇAMENT") || t.Contains("ORCAMENT")) return Color.FromArgb(255, 190, 30);
        if (t.Contains("TELA DE VENDAS")) return Color.FromArgb(0, 205, 255);
        if (t.Contains("RELAT")) return Color.FromArgb(115, 135, 255);
        if (t.Contains("BACKUP")) return Color.FromArgb(45, 225, 165);
        if (t.Contains("CONFIG")) return Color.FromArgb(235, 185, 45);
        if (t.Contains("SAIR")) return Color.FromArgb(255, 70, 65);
        return null;
    }

    private static Bitmap TintIcon(Image source, Color accent)
    {
        var bmp = new Bitmap(source.Width, source.Height);
        using var g = Graphics.FromImage(bmp);
        g.DrawImage(source, 0, 0, source.Width, source.Height);

        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.A == 0) continue;

            var lum = (p.R * 0.299 + p.G * 0.587 + p.B * 0.114) / 255.0;
            var boost = 0.48 + lum * 0.72;
            int r = Math.Clamp((int)(accent.R * boost), 0, 255);
            int gr = Math.Clamp((int)(accent.G * boost), 0, 255);
            int b = Math.Clamp((int)(accent.B * boost), 0, 255);
            bmp.SetPixel(x, y, Color.FromArgb(p.A, r, gr, b));
        }
        return bmp;
    }

    private static void EnsureGreenWhiteCard(Form chooser)
    {
        var table = FindThemeTable(chooser);
        if (table == null) return;

        foreach (Control c in table.Controls)
            if (c is Button existing && string.Equals(Convert.ToString(existing.Tag), "Verde e Branco", StringComparison.OrdinalIgnoreCase))
                return;

        foreach (Control c in table.Controls)
        {
            if (c is not Button b || HookedThemeButtons.Contains(b)) continue;
            HookedThemeButtons.Add(b);
            b.Click += (_, _) => WriteSetting("sales_theme_green_white", "0");
        }

        var card = new Button
        {
            Text = "VERDE E BRANCO\n\nMármore verde + branco + dourado",
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            BackColor = Color.FromArgb(28, 120, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Tag = "Verde e Branco",
            BackgroundImage = CreateMarble(520, 300),
            BackgroundImageLayout = ImageLayout.Stretch
        };
        card.FlatAppearance.BorderColor = Color.FromArgb(200, 165, 75);
        card.FlatAppearance.BorderSize = 3;
        card.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 145, 92);

        card.Click += (_, _) =>
        {
            try
            {
                WriteSetting("sales_theme_green_white", "1");
                WriteSetting("sales_theme", "Futurista Azul");
                foreach (Form open in Application.OpenForms)
                {
                    if (open.Text.Contains("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase))
                    {
                        GreenAppliedForms.Remove(open);
                        ApplyGreenWhiteTheme(open);
                        GreenAppliedForms.Add(open);
                        break;
                    }
                }
                chooser.Close();
            }
            catch
            {
                chooser.Close();
            }
        };

        table.Controls.Add(card, 1, 2);
    }

    private static TableLayoutPanel? FindThemeTable(Control root)
    {
        foreach (var table in Descendants<TableLayoutPanel>(root))
            if (table.ColumnCount == 2 && table.RowCount >= 3)
                return table;
        return null;
    }

    private static void ApplyGreenWhiteTheme(Form form)
    {
        var marble = CreateMarble(Math.Max(900, form.ClientSize.Width), Math.Max(650, form.ClientSize.Height));
        var previous = form.BackgroundImage;
        form.BackgroundImage = marble;
        form.BackgroundImageLayout = ImageLayout.Stretch;
        previous?.Dispose();
        form.BackColor = Color.FromArgb(244, 248, 242);

        foreach (var c in Descendants<Control>(form))
        {
            if (c is DataGridView grid)
            {
                grid.BackgroundColor = Color.FromArgb(250, 252, 248);
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 92, 58);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.DefaultCellStyle.BackColor = Color.FromArgb(253, 254, 251);
                grid.DefaultCellStyle.ForeColor = Color.FromArgb(20, 65, 42);
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(196, 225, 203);
                grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(12, 60, 36);
            }
            else if (c is Button b)
            {
                var text = b.Text.ToUpperInvariant();
                if (text.Contains("ESTILO")) b.BackColor = Color.FromArgb(184, 145, 48);
                else if (text.Contains("FINALIZAR")) b.BackColor = Color.FromArgb(26, 135, 76);
                else if (text.Contains("FECHAR")) b.BackColor = Color.FromArgb(25, 82, 58);
                b.ForeColor = Color.White;
            }
            else if (c is TextBox tb)
            {
                tb.BackColor = Color.FromArgb(255, 255, 252);
                tb.ForeColor = Color.FromArgb(20, 75, 45);
            }
            else if (c is Label lbl)
            {
                if (lbl.ForeColor != Color.White)
                    lbl.ForeColor = Color.FromArgb(20, 78, 48);
            }
            else if (c is Panel or TableLayoutPanel)
            {
                if (c.BackColor.A == 255 && c.BackColor != Color.Transparent)
                {
                    var brightness = (c.BackColor.R + c.BackColor.G + c.BackColor.B) / 3;
                    c.BackColor = brightness < 120
                        ? Color.FromArgb(22, 93, 60)
                        : Color.FromArgb(238, 246, 237);
                }
            }
        }

        form.Invalidate(true);
    }

    private static Bitmap CreateMarble(int width, int height)
    {
        width = Math.Max(300, width);
        height = Math.Max(220, height);
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var baseBrush = new LinearGradientBrush(new Point(0, 0), new Point(width, height),
                   Color.FromArgb(252, 252, 247), Color.FromArgb(222, 239, 222)))
            g.FillRectangle(baseBrush, 0, 0, width, height);

        using var softGreen = new Pen(Color.FromArgb(72, 54, 148, 91), Math.Max(40, width / 18f));
        using var midGreen = new Pen(Color.FromArgb(90, 20, 113, 68), Math.Max(16, width / 55f));
        using var darkGreen = new Pen(Color.FromArgb(105, 10, 86, 52), Math.Max(5, width / 180f));
        using var gold = new Pen(Color.FromArgb(175, 190, 150, 55), Math.Max(2, width / 420f));

        for (int i = -1; i < 5; i++)
        {
            float y = height * (0.12f + i * 0.22f);
            g.DrawBezier(softGreen, -width * .08f, y, width * .28f, y - height * .24f, width * .68f, y + height * .22f, width * 1.08f, y - height * .08f);
            g.DrawBezier(midGreen, -width * .08f, y + 20, width * .34f, y - height * .18f, width * .70f, y + height * .16f, width * 1.08f, y);
            g.DrawBezier(darkGreen, -width * .08f, y + 3, width * .37f, y - height * .15f, width * .72f, y + height * .12f, width * 1.08f, y - 5);
            g.DrawBezier(gold, -width * .08f, y - 4, width * .36f, y - height * .16f, width * .71f, y + height * .13f, width * 1.08f, y - 8);
        }

        var rnd = new Random(7419);
        using var fleck = new SolidBrush(Color.FromArgb(120, 196, 157, 67));
        for (int i = 0; i < 160; i++)
        {
            int x = rnd.Next(width);
            int y = rnd.Next(height);
            int s = rnd.Next(1, 4);
            g.FillEllipse(fleck, x, y, s, s);
        }
        return bmp;
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T match) yield return match;
            foreach (var nested in Descendants<T>(c)) yield return nested;
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
