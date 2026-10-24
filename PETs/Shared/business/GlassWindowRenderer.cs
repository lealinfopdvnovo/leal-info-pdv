using System.Drawing.Drawing2D;
using System.Globalization;

namespace LealInfoPDV;

public static class GlassWindowRenderer
{
    public static string Millimeters(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR")) + " mm";

    // Uma única escala para os dois eixos. Cotas ficam fora da geometria do vão.
    public static RectangleF WindowBounds(RectangleF area, GlassProject project)
    {
        if (project.ValidationError != null) throw new ArgumentException(project.ValidationError);
        float margin = Math.Min(area.Width, area.Height) * .14f;
        float width = Math.Max(1, area.Width - 2 * margin);
        float height = Math.Max(1, area.Height - 2 * margin);
        double scale = Math.Min(width / (double)project.WidthMm, height / (double)project.HeightMm);
        float w = (float)((double)project.WidthMm * scale), h = (float)((double)project.HeightMm * scale);
        return new RectangleF(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);
    }

    public static void Draw(Graphics g, RectangleF area, GlassProject project, bool paper = false, bool dimensions = true)
    {
        var old = g.Save();
        try
        {
            g.SetClip(area);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = WindowBounds(area, project);
            float unit = Math.Max(1, Math.Min(area.Width, area.Height) / 400);
            Color ink = paper ? Color.FromArgb(22, 45, 65) : Color.FromArgb(115, 224, 251);
            using var frame = new Pen(ink, 2 * unit);
            using var detail = new Pen(ink, unit);
            using var glass = new SolidBrush(paper ? Color.FromArgb(233, 244, 249) : Color.FromArgb(18, 59, 84));
            using var text = new SolidBrush(paper ? Color.Black : Color.White);
            using var font = new Font("Segoe UI", Math.Clamp(12 * unit, 9, 22), FontStyle.Regular, GraphicsUnit.Pixel);
            using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.FillRectangle(glass, r);
            g.DrawRectangle(frame, r.X, r.Y, r.Width, r.Height);
            // Espessura gráfica ilustrativa, sem desconto técnico/fórmula de fabricante.
            float inset = Math.Min(r.Width, r.Height) * .035f;
            var inner = RectangleF.Inflate(r, -inset, -inset);
            g.DrawRectangle(detail, inner.X, inner.Y, inner.Width, inner.Height);
            float mid = r.Left + r.Width / 2;
            g.DrawLine(frame, mid, inner.Top, mid, inner.Bottom);
            float arrowY = r.Top + r.Height * .6f;
            Arrow(g, detail, r.Left + r.Width * .16f, r.Left + r.Width * .4f, arrowY, unit);
            Arrow(g, detail, r.Left + r.Width * .84f, r.Left + r.Width * .6f, arrowY, unit);
            if (dimensions)
            {
                float offset = Math.Min(area.Width, area.Height) * .065f;
                float y = r.Top - offset, x = r.Right + offset;
                g.DrawLine(detail, r.Left, r.Top, r.Left, y - 4 * unit);
                g.DrawLine(detail, r.Right, r.Top, r.Right, y - 4 * unit);
                Dimension(g, detail, new(r.Left, y), new(r.Right, y), unit);
                g.DrawString(Millimeters(project.WidthMm), font, text,
                    new RectangleF(r.Left, y - 22 * unit, r.Width, 20 * unit), centered);
                g.DrawLine(detail, r.Right, r.Top, x + 4 * unit, r.Top);
                g.DrawLine(detail, r.Right, r.Bottom, x + 4 * unit, r.Bottom);
                Dimension(g, detail, new(x, r.Top), new(x, r.Bottom), unit);
                var rotation = g.Save();
                g.TranslateTransform(x + 14 * unit, r.Top + r.Height / 2);
                g.RotateTransform(-90);
                g.DrawString(Millimeters(project.HeightMm), font, text,
                    new RectangleF(-r.Height / 2, -10 * unit, r.Height, 20 * unit), centered);
                g.Restore(rotation);
            }
        }
        finally { g.Restore(old); }
    }

    private static void Arrow(Graphics g, Pen pen, float from, float to, float y, float unit)
    {
        g.DrawLine(pen, from, y, to, y);
        float head = Math.Min(Math.Abs(to - from) * .2f, 7 * unit), direction = Math.Sign(to - from);
        g.DrawLine(pen, to, y, to - direction * head, y - head * .65f);
        g.DrawLine(pen, to, y, to - direction * head, y + head * .65f);
    }
    private static void Dimension(Graphics g, Pen pen, PointF a, PointF b, float unit)
    {
        g.DrawLine(pen, a, b);
        float tick = 4 * unit;
        g.DrawLine(pen, a.X - tick, a.Y + tick, a.X + tick, a.Y - tick);
        g.DrawLine(pen, b.X - tick, b.Y + tick, b.X + tick, b.Y - tick);
    }
}

public sealed class GlassDrawingView : Control
{
    public GlassProject? Project { get; set; }
    public bool ShowDimensions { get; set; } = true;
    public GlassDrawingView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(9, 22, 39);
        Dock = DockStyle.Fill;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Project != null && Width > 20 && Height > 20)
            GlassWindowRenderer.Draw(e.Graphics, ClientRectangle, Project, dimensions: ShowDimensions);
        else TextRenderer.DrawText(e.Graphics, "Informe as medidas e clique em VISUALIZAR.", Font,
            ClientRectangle, Color.FromArgb(174, 201, 220), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}
