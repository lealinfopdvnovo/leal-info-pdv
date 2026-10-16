using System.Drawing.Drawing2D;

namespace LealInfoPDV;

internal static class GlassWorkshopIcon
{
    // Recurso vetorial local: esquadria com duas folhas de vidro, sem arquivos remotos.
    internal static Bitmap Create()
    {
        var bitmap = new Bitmap(256, 256);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        var outline = new PointF[] { new(39, 52), new(193, 27), new(218, 47), new(218, 199), new(65, 229), new(39, 207) };
        using var shadow = new SolidBrush(Color.FromArgb(70, 0, 14, 42));
        g.FillPolygon(shadow, outline.Select(p => new PointF(p.X + 8, p.Y + 8)).ToArray());
        using var frame = new LinearGradientBrush(new Point(40, 35), new Point(200, 225), Color.FromArgb(209, 248, 255), Color.FromArgb(8, 57, 124));
        g.FillPolygon(frame, outline);
        var glass = new PointF[] { new(52, 62), new(185, 42), new(185, 188), new(52, 213) };
        using var pane = new LinearGradientBrush(new Point(55, 50), new Point(180, 215), Color.FromArgb(190, 21, 203, 247), Color.FromArgb(230, 4, 34, 79));
        g.FillPolygon(pane, glass);
        using var edge = new Pen(Color.FromArgb(115, 235, 255), 5);
        g.DrawPolygon(edge, glass);
        g.DrawLine(edge, 119, 52, 119, 200);
        using var side = new Pen(Color.FromArgb(25, 108, 181), 8);
        g.DrawLine(side, 202, 55, 202, 195);
        using var reflection = new Pen(Color.FromArgb(150, 222, 252, 255), 6);
        g.DrawLine(reflection, 64, 106, 102, 78);
        g.DrawLine(reflection, 133, 148, 170, 120);
        using var handle = new Pen(Color.FromArgb(228, 249, 255), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(handle, 132, 119, 132, 139);
        return bitmap;
    }
}
