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
            Text = "VERDE E BRANCO\n\nMármore branco + verde fluido + dourado",
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            BackColor = Color.FromArgb(245, 248, 240),
            ForeColor = Color.FromArgb(24, 91, 55),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Tag = "Verde e Branco",
            BackgroundImage = CreateMarble(520, 300),
            BackgroundImageLayout = ImageLayout.Stretch
        };
        card.FlatAppearance.BorderColor = Color.FromArgb(205, 168, 68);
        card.FlatAppearance.BorderSize = 3;
        card.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 243, 229);

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

    private static void SetMarbleBackground(Control c)
    {
        try
        {
            var old = c.BackgroundImage;
            c.BackgroundImage = CreateMarble(Math.Max(420, c.Width), Math.Max(280, c.Height));
            c.BackgroundImageLayout = ImageLayout.Stretch;
            c.BackColor = Color.FromArgb(250, 250, 245);
            old?.Dispose();
        }
        catch
        {
            c.BackColor = Color.FromArgb(248, 249, 243);
        }
    }

    private static void ApplyGreenWhiteTheme(Form form)
    {
        SetMarbleBackground(form);

        foreach (var c in Descendants<Control>(form))
        {
            if (c is DataGridView grid)
            {
                grid.BackgroundColor = Color.FromArgb(253, 253, 248);
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(27, 104, 63);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 252);
                grid.DefaultCellStyle.ForeColor = Color.FromArgb(24, 73, 45);
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 250, 242);
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(201, 229, 202);
                grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(18, 67, 40);
            }
            else if (c is Button b)
            {
                var text = b.Text.ToUpperInvariant();
                if (text.Contains("ESTILO")) b.BackColor = Color.FromArgb(194, 153, 50);
                else if (text.Contains("FINALIZAR")) b.BackColor = Color.FromArgb(38, 151, 82);
                else if (text.Contains("FECHAR")) b.BackColor = Color.FromArgb(31, 104, 64);
                else if (text.Contains("ADICIONAR")) b.BackColor = Color.FromArgb(61, 163, 100);
                else if (text.Contains("LIMPAR")) b.BackColor = Color.FromArgb(95, 138, 104);
                b.ForeColor = Color.White;
            }
            else if (c is TextBox tb)
            {
                tb.BackColor = Color.FromArgb(255, 255, 252);
                tb.ForeColor = Color.FromArgb(22, 83, 50);
            }
            else if (c is NumericUpDown nud)
            {
                nud.BackColor = Color.FromArgb(255, 255, 252);
                nud.ForeColor = Color.FromArgb(22, 83, 50);
            }
            else if (c is Panel or TableLayoutPanel)
            {
                var area = c.Width * c.Height;
                var isWideHeader = c.Width > 650 && c.Height > 35 && c.Height < 115;

                if (isWideHeader)
                {
                    c.BackgroundImage?.Dispose();
                    c.BackgroundImage = null;
                    c.BackColor = Color.FromArgb(24, 103, 63);
                }
                else if (area > 90000 && c.Height > 120)
                {
                    SetMarbleBackground(c);
                }
                else if (c.BackColor.A == 255 && c.BackColor != Color.Transparent)
                {
                    c.BackgroundImage?.Dispose();
                    c.BackgroundImage = null;
                    c.BackColor = Color.FromArgb(246, 249, 241);
                }
            }
        }

        foreach (var lbl in Descendants<Label>(form))
        {
            var parent = lbl.Parent;
            if (parent != null)
            {
                int bright = (parent.BackColor.R + parent.BackColor.G + parent.BackColor.B) / 3;
                lbl.ForeColor = bright < 135 ? Color.White : Color.FromArgb(24, 84, 51);
            }
            else
            {
                lbl.ForeColor = Color.FromArgb(24, 84, 51);
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
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        using (var baseBrush = new LinearGradientBrush(
                   new Point(0, 0),
                   new Point(width, height),
                   Color.FromArgb(255, 255, 251),
                   Color.FromArgb(242, 247, 235)))
        {
            g.FillRectangle(baseBrush, 0, 0, width, height);
        }

        void Ribbon(float y, float bend, float phase)
        {
            using var pale = new Pen(Color.FromArgb(42, 91, 180, 104), Math.Max(74f, width / 12f));
            using var soft = new Pen(Color.FromArgb(54, 63, 158, 84), Math.Max(42f, width / 22f));
            using var mid = new Pen(Color.FromArgb(62, 39, 131, 68), Math.Max(18f, width / 60f));
            using var vein = new Pen(Color.FromArgb(75, 22, 101, 54), Math.Max(3f, width / 340f));
            using var pearl = new Pen(Color.FromArgb(80, 255, 255, 248), Math.Max(16f, width / 65f));

            pale.StartCap = pale.EndCap = LineCap.Round;
            soft.StartCap = soft.EndCap = LineCap.Round;
            mid.StartCap = mid.EndCap = LineCap.Round;
            vein.StartCap = vein.EndCap = LineCap.Round;
            pearl.StartCap = pearl.EndCap = LineCap.Round;

            float x0 = -width * .10f;
            float x1 = width * .30f;
            float x2 = width * .66f;
            float x3 = width * 1.10f;

            float p1 = y - bend * (0.72f + phase);
            float p2 = y + bend * (0.58f - phase * .35f);
            float end = y - bend * .18f;

            g.DrawBezier(pale, x0, y, x1, p1, x2, p2, x3, end);
            g.DrawBezier(soft, x0, y + height * .015f, x1, p1 + height * .012f, x2, p2 + height * .018f, x3, end + height * .01f);
            g.DrawBezier(mid, x0, y + height * .025f, x1, p1 + height * .022f, x2, p2 + height * .025f, x3, end + height * .02f);
            g.DrawBezier(vein, x0, y - height * .018f, x1, p1 - height * .012f, x2, p2 - height * .02f, x3, end - height * .012f);
            g.DrawBezier(pearl, x0, y + height * .045f, x1, p1 + height * .05f, x2, p2 + height * .04f, x3, end + height * .045f);
        }

        Ribbon(height * .18f, height * .22f, .10f);
        Ribbon(height * .52f, height * .25f, -.08f);
        Ribbon(height * .82f, height * .18f, .04f);

        using (var gold = new Pen(Color.FromArgb(205, 203, 165, 60), Math.Max(2.2f, width / 500f)))
        {
            gold.StartCap = gold.EndCap = LineCap.Round;
            g.DrawBezier(gold,
                -width * .05f, height * .31f,
                width * .28f, height * .20f,
                width * .62f, height * .35f,
                width * 1.04f, height * .12f);

            g.DrawBezier(gold,
                -width * .04f, height * .73f,
                width * .30f, height * .91f,
                width * .67f, height * .62f,
                width * 1.05f, height * .69f);
        }

        using (var hairline = new Pen(Color.FromArgb(85, 116, 153, 91), Math.Max(1f, width / 1100f)))
        {
            for (int i = 0; i < 7; i++)
            {
                float y = height * (.10f + i * .13f);
                g.DrawBezier(hairline,
                    -width * .03f, y,
                    width * .28f, y - height * .08f,
                    width * .64f, y + height * .09f,
                    width * 1.03f, y - height * .02f);
            }
        }

        var rnd = new Random(7419);
        using var goldFleck = new SolidBrush(Color.FromArgb(145, 205, 171, 70));
        using var greenMist = new SolidBrush(Color.FromArgb(35, 63, 150, 84));

        for (int i = 0; i < 120; i++)
        {
            int x = rnd.Next(width);
            int y = rnd.Next(height);
            int s = rnd.Next(1, 4);
            if (i % 3 == 0)
                g.FillEllipse(goldFleck, x, y, s + 1, s + 1);
            else
                g.FillEllipse(greenMist, x, y, s, s);
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
