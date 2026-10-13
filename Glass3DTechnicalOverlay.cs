namespace LealInfoPDV;

public sealed record Glass3DDimension(PointF From, PointF To, PointF ObjectFrom, PointF ObjectTo,
    Rectangle Label, string Text, bool DrawLine);
public sealed record Glass3DTechnicalLayout(Glass3DDimension Width, Glass3DDimension Height,
    Rectangle OrientationBounds, string Orientation);

// Camada em coordenadas de tela: texto sempre orientado à câmera, sem mudar a malha.
public static class Glass3DTechnicalOverlay
{
    public static Glass3DTechnicalLayout Layout(Rectangle client, IReadOnlyList<PointF> points, GlassProject project,
        float yaw, float pitch, Font font, Rectangle reserved, int margin)
    {
        string orientation = "FACE " + (MathF.Cos(yaw * MathF.PI / 180) >= 0 ? "A" : "B");
        if (pitch > 8) orientation += " • SUPERIOR"; else if (pitch < -8) orientation += " • INFERIOR";
        float sine = MathF.Sin(yaw * MathF.PI / 180);
        if (sine < -.15f) orientation += " • DIREITA"; else if (sine > .15f) orientation += " • ESQUERDA";
        orientation += "\nFaces A/B ilustrativas; interno/externo não definidos.";
        int available = Math.Max(1, client.Width - margin * 2);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
        var orientationSize = TextRenderer.MeasureText(orientation, font, new Size(available, int.MaxValue), flags);
        var orient = new Rectangle(margin, Math.Max(margin, client.Height - orientationSize.Height - margin), available, orientationSize.Height);
        var legal = new Rectangle(margin, margin, available, Math.Max(1, orient.Top - margin * 2));
        int face = MathF.Cos(yaw * MathF.PI / 180) >= 0 ? 4 : 0;
        var center = new PointF(points.Skip(face).Take(4).Average(p => p.X), points.Skip(face).Take(4).Average(p => p.Y));
        var extent = RectangleF.FromLTRB(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
        var width = Make(points[face], points[face + 1], $"Largura: {project.WidthMm:0.##} mm", 0);
        var height = Make(points[face + 1], points[face + 2], $"Altura: {project.HeightMm:0.##} mm", 1);
        if (width.Label.IntersectsWith(height.Label)) { width = Fallback(width, 0); height = Fallback(height, 1); }
        return new(width, height, orient, orientation);
        Glass3DDimension Make(PointF a, PointF b, string text, int row)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, length = MathF.Sqrt(dx * dx + dy * dy);
            var size = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            float nx = length > 0 ? -dy / length : 0, ny = length > 0 ? dx / length : 1;
            var mid = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            if ((mid.X - center.X) * nx + (mid.Y - center.Y) * ny < 0) { nx = -nx; ny = -ny; }
            float offset = margin * 2.4f + 8;
            var start = new PointF(a.X + nx * offset, a.Y + ny * offset); var end = new PointF(b.X + nx * offset, b.Y + ny * offset);
            float labelDistance = offset + margin + Math.Max(size.Width * MathF.Abs(nx), size.Height * MathF.Abs(ny)) / 2;
            var rect = new Rectangle((int)(mid.X + nx * labelDistance - size.Width / 2f) - 3, (int)(mid.Y + ny * labelDistance - size.Height / 2f) - 2, size.Width + 6, size.Height + 4);
            // A legenda deve ficar fora do objeto. Se o zoom/ângulo não permitir, usar legenda fixa.
            bool visible = length > 50 && legal.Contains(Point.Round(start)) && legal.Contains(Point.Round(end)) && legal.Contains(rect)
                && !rect.IntersectsWith(reserved) && RectangleF.Intersect(rect, extent).IsEmpty;
            var line = new Glass3DDimension(start, end, a, b, rect, text, visible);
            return visible ? line : Fallback(line, row);
        }
        Glass3DDimension Fallback(Glass3DDimension line, int row)
        {
            int lineHeight = font.Height + 6;
            return line with { DrawLine = false, Label = new Rectangle(margin, Math.Max(margin, orient.Top - margin - lineHeight * (2 - row)), Math.Min(available, TextRenderer.MeasureText(line.Text, font).Width + 8), lineHeight) };
        }
    }
    public static void Draw(Graphics g, Glass3DTechnicalLayout layout, Font font)
    {
        using var background = new SolidBrush(Color.FromArgb(9, 22, 39));
        using var pen = new Pen(Color.FromArgb(175, 231, 247), 1.2f);
        foreach (var line in new[] { layout.Width, layout.Height })
        {
            if (line.DrawLine)
            {
                g.DrawLine(pen, line.ObjectFrom, line.From); g.DrawLine(pen, line.ObjectTo, line.To); g.DrawLine(pen, line.From, line.To);
                g.DrawLine(pen, line.From.X - 3, line.From.Y - 3, line.From.X + 3, line.From.Y + 3);
                g.DrawLine(pen, line.To.X - 3, line.To.Y - 3, line.To.X + 3, line.To.Y + 3);
            }
            g.FillRectangle(background, line.Label);
            TextRenderer.DrawText(g, line.Text, font, line.Label, pen.Color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }
        g.FillRectangle(background, layout.OrientationBounds);
        TextRenderer.DrawText(g, layout.Orientation, font, layout.OrientationBounds, pen.Color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
    }
}
