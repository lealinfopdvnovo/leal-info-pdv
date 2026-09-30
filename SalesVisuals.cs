using System.Drawing.Drawing2D;

namespace LealInfoPDV;

internal static class SalesVisuals
{
    internal static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    internal static void Frame(Graphics g, Rectangle bounds, bool bright = false)
    {
        if (bounds.Width < 8 || bounds.Height < 8) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(2, 2, bounds.Width - 5, bounds.Height - 5);
        using var path = Rounded(r, 10);
        using var background = new LinearGradientBrush(r, Color.FromArgb(10, 38, 65), Color.FromArgb(1, 11, 23), 90f);
        g.FillPath(background, path);
        for (int i = 5; i >= 1; i--)
        {
            using var glow = new Pen(Color.FromArgb(15 + (5 - i) * 8, 0, 150, 255), i * 2);
            g.DrawPath(glow, path);
        }
        using var rim = new Pen(bright ? Color.FromArgb(130, 235, 255) : Color.FromArgb(50, 155, 225), bright ? 2.2f : 1.2f);
        g.DrawPath(rim, path);
        var inner = RectangleF.Inflate(r, -5, -5);
        using var innerPath = Rounded(inner, 7);
        using var bevel = new LinearGradientBrush(inner, Color.FromArgb(130, 150, 220, 255), Color.FromArgb(5, 20, 65, 120), 90f);
        using var bevelPen = new Pen(bevel, 1.2f);
        g.DrawPath(bevelPen, innerPath);
        using var shine = new LinearGradientBrush(new RectangleF(r.Left, r.Top, r.Width, Math.Min(18, r.Height)), Color.FromArgb(bright ? 110 : 45, 95, 205, 255), Color.Transparent, 90f);
        g.FillRectangle(shine, r.Left + 12, r.Top + 1, r.Width - 24, Math.Min(14, r.Height / 4));
    }
}

internal sealed class SalesGlowLabel : Label
{
    internal SalesGlowLabel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
        for (int radius = 3; radius >= 1; radius--)
        {
            var rect = new Rectangle(radius, 0, Width, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, Color.FromArgb(0, 95 + radius * 30, 245), flags);
            rect.X = -radius;
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, Color.FromArgb(0, 95 + radius * 30, 245), flags);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
    }
}

internal sealed class SalesVisualButton : Button
{
    private bool hovered;
    private bool pressed;
    internal SalesVisualButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; base.OnMouseDown(e); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 8 || Height < 8) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(2, 2, Width - 5, Height - 5);
        using var path = SalesVisuals.Rounded(r, 9);
        Color top = ControlPaint.Light(BackColor, hovered ? .65f : .35f);
        Color bottom = ControlPaint.Dark(BackColor, pressed ? .45f : .25f);
        using var gradient = new LinearGradientBrush(r, top, bottom, 90f);
        g.FillPath(gradient, path);
        using var glow = new Pen(Color.FromArgb(75, 0, 155, 255), 5);
        g.DrawPath(glow, path);
        using var edge = new Pen(Color.FromArgb(175, 195, 235, 255), 1.2f);
        g.DrawPath(edge, path);
        var shineRect = RectangleF.Inflate(r, -3, -3);
        shineRect.Height = Math.Max(4, shineRect.Height * .3f);
        using var shine = new LinearGradientBrush(shineRect, Color.FromArgb(90, Color.White), Color.FromArgb(0, Color.White), 90f);
        using var shinePath = SalesVisuals.Rounded(shineRect, 6);
        g.FillPath(shine, shinePath);
        string label = Text;
        bool method = label is "Dinheiro" or "PIX" or "Cartão" or "Múltiplo";
        var textRect = Rectangle.Inflate(ClientRectangle, -7, -5);
        using var textFont = new Font("Segoe UI", method ? Math.Clamp(Height / 6f, 11, 16) : label.Contains("Finalizar") ? Math.Clamp(Width * .74f / 10f, 12, 20) : label.Contains("Remover") ? 10 : label.Contains("Cancelar") || label.Contains("Comprovante") ? Math.Clamp(Width / 12f, 8.5f, 11) : Font.Size, FontStyle.Bold);
        if (method)
        {
            float size = Math.Clamp(Height * .33f, 20, 35);
            DrawIcon(g, label, new RectangleF((Width - size) / 2, Height * .15f, size, size));
            textRect.Y = (int)(Height * .53f); textRect.Height = Height - textRect.Y - 7;
        }
        else if (label.Contains("Finalizar"))
        {
            DrawIcon(g, "check", new RectangleF(Width * .08f, Height * .34f, Height * .3f, Height * .3f));
            textRect.X = (int)(Width * .24f); textRect.Width = Width - textRect.X - 10;
            using var shortcutFont = new Font("Segoe UI", Math.Clamp(Height / 5f, 13, 22), FontStyle.Bold);
            using var titleFont = new Font("Segoe UI", Math.Clamp(textRect.Width / 12f, 10, 18), FontStyle.Bold);
            TextRenderer.DrawText(g, "F4", shortcutFont, new Rectangle(textRect.X, 7, textRect.Width, (int)(Height * .42f)), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Finalizar Venda", titleFont, new Rectangle(textRect.X, (int)(Height * .42f), textRect.Width, (int)(Height * .48f)), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            return;
        }
        else if (label.Contains("Comprovante") || label.Contains("Cancelar") || label == "SAIR")
        {
            float size = Math.Clamp(Height * .32f, 18, 28);
            DrawIcon(g, label.Contains("Comprovante") ? "print" : label == "SAIR" ? "exit" : "cancel", new RectangleF((Width - size) / 2, Height * .13f, size, size));
            textRect.Y = (int)(Height * .5f); textRect.Height = Height - textRect.Y - 5;
        }
        TextRenderer.DrawText(g, label, textFont, textRect, Enabled ? Color.White : Color.Silver,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -6, -6), Color.White, Color.Transparent);
    }
    private static void DrawIcon(Graphics g, string icon, RectangleF r)
    {
        var state = g.Save();
        g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 32, r.Height / 32);
        using var white = new SolidBrush(icon == "exit" ? Color.FromArgb(255, 55, 50) : Color.White);
        using var pen = new Pen(white.Color, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (icon)
        {
            case "PIX":
                foreach (var point in new[] { new PointF(16, 6), new PointF(6, 16), new PointF(26, 16), new PointF(16, 26) })
                    g.FillPolygon(white, new[] { new PointF(point.X, point.Y - 6), new PointF(point.X + 6, point.Y), new PointF(point.X, point.Y + 6), new PointF(point.X - 6, point.Y) });
                break;
            case "Dinheiro":
                g.FillRectangle(white, 1, 6, 30, 21);
                using (var dark = new SolidBrush(Color.FromArgb(0, 135, 40)))
                { g.FillEllipse(dark, 9, 5, 15, 23); using var f = new Font("Segoe UI", 14, FontStyle.Bold); g.DrawString("$", f, white, 10, 4); }
                break;
            case "Cartão":
                using (var card = SalesVisuals.Rounded(new RectangleF(1, 5, 30, 23), 4)) g.FillPath(white, card);
                using (var dark = new Pen(Color.FromArgb(50, 65, 90), 4)) g.DrawLine(dark, 3, 13, 29, 13);
                break;
            case "Múltiplo":
                g.DrawLine(pen, 16, 8, 6, 24); g.DrawLine(pen, 16, 8, 26, 24); g.DrawLine(pen, 6, 24, 26, 24);
                g.FillEllipse(white, 11, 1, 10, 10); g.FillEllipse(white, 0, 21, 11, 11); g.FillEllipse(white, 21, 21, 11, 11);
                break;
            case "check": using (var checkPen = new Pen(Color.White, 6)) g.DrawLines(checkPen, new[] { new PointF(2, 17), new PointF(12, 27), new PointF(31, 5) }); break;
            case "cancel": g.FillEllipse(white, 1, 1, 30, 30); using (var dark = new Pen(Color.FromArgb(20, 45, 70), 3)) { g.DrawLine(dark, 11, 11, 21, 21); g.DrawLine(dark, 21, 11, 11, 21); } break;
            case "print": g.DrawRectangle(pen, 6, 1, 20, 10); g.FillRectangle(white, 1, 10, 30, 15); g.DrawRectangle(pen, 7, 22, 18, 9); break;
            case "exit": g.DrawLines(pen, new[] { new PointF(12, 4), new PointF(3, 4), new PointF(3, 29), new PointF(12, 29) }); g.DrawLine(pen, 11, 15, 29, 15); g.DrawLines(pen, new[] { new PointF(23, 8), new PointF(30, 15), new PointF(23, 22) }); break;
        }
        g.Restore(state);
    }
}

internal sealed class SalesItemsGrid : DataGridView
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (RowCount != 0) return;
        using var font = new Font("Segoe UI", 12, FontStyle.Regular);
        using var brush = new SolidBrush(Color.LightSteelBlue);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString("Nenhum item adicionado.\nUse F5 para buscar um produto.", font, brush,
            new RectangleF(0, ColumnHeadersHeight, Width, Math.Max(0, Height - ColumnHeadersHeight)), format);
    }
}
