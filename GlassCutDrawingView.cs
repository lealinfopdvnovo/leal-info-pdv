using System.Drawing.Drawing2D;

namespace LealInfoPDV;

public sealed class GlassCutDrawingView : Control
{
    public GlassCutResult? Result { get; set; }
    public int SheetIndex { get; set; }
    public string SelectedPieceId { get; set; } = "";
    public GlassCutDrawingView() { Name = "CutDrawing"; DoubleBuffered = true; ResizeRedraw = true; Dock = DockStyle.Fill; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Result == null || Result.Sheets.Count == 0)
        { TextRenderer.DrawText(e.Graphics, "Informe a chapa, confira as peças e recalcule.", Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak); return; }
        GlassCutPlanRenderer.Draw(e.Graphics, ClientRectangle, Result, Math.Clamp(SheetIndex, 0, Result.Sheets.Count - 1), Font, ForeColor, false, SelectedPieceId);
    }
}

public static class GlassCutPlanRenderer
{
    public static RectangleF SheetBounds(RectangleF area, decimal w, decimal h)
    {
        float availableW = Math.Max(1, area.Width - 56), availableH = Math.Max(1, area.Height - 76);
        float scale = Math.Min(availableW / (float)w, availableH / (float)h);
        return new(area.Left + (area.Width - (float)w * scale) / 2, area.Top + 42 + (availableH - (float)h * scale) / 2, (float)w * scale, (float)h * scale);
    }
    public static void Draw(Graphics g, RectangleF area, GlassCutResult result, int index, Font font, Color foreground, bool paper, string selected = "")
    {
        var sheet = result.Sheets[index]; var bounds = SheetBounds(area, result.SheetWidthMm, result.SheetHeightMm);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(paper ? Color.FromArgb(247, 250, 252) : Color.FromArgb(13, 28, 45));
        using var outline = new Pen(paper ? Color.FromArgb(4, 55, 94) : Color.FromArgb(84, 201, 240), 1.5f);
        using var highlight = new Pen(paper ? Color.DarkBlue : Color.White, 3);
        using var ink = new SolidBrush(foreground);
        g.DrawString($"CHAPA {sheet.Number} • {result.SheetWidthMm:0.##} × {result.SheetHeightMm:0.##} mm", font, ink, new RectangleF(area.X + 8, area.Y + 6, area.Width - 16, 32));
        g.FillRectangle(background, bounds); g.DrawRectangle(outline, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        float scale = bounds.Width / (float)result.SheetWidthMm;
        foreach (var p in sheet.Pieces)
        {
            var r = new RectangleF(bounds.Left + (float)p.X * scale, bounds.Top + (float)p.Y * scale, (float)p.WidthMm * scale, (float)p.HeightMm * scale);
            using var fill = new SolidBrush(paper ? Color.FromArgb(220, 237, 246) : Color.FromArgb(75, 25, 162, 218));
            g.FillRectangle(fill, r); g.DrawRectangle(string.Equals(selected, p.Piece.Id, StringComparison.OrdinalIgnoreCase) ? highlight : outline, r.X, r.Y, r.Width, r.Height);
            string id = p.Piece.Id + (p.Piece.Quantity > 1 ? $" #{p.Instance}" : "");
            string measures = $"{p.WidthMm:0.##} × {p.HeightMm:0.##} mm" + (p.Rotated ? " (90°)" : "");
            if (r.Width > 36 && r.Height > font.Height + 8 && g.MeasureString(id, font).Width < r.Width - 8)
            {
                using var align = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                string text = r.Height > font.Height * 2 + 12 && g.MeasureString(measures, font).Width < r.Width - 8 ? id + "\n" + measures : id;
                g.DrawString(text, font, ink, r, align);
            }
        }
    }
}
