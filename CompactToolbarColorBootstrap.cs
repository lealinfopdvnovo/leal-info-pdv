using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

// Mantem exatamente o tamanho/layout atual da barra e apenas colore os icones.
internal static class CompactToolbarColorBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += ApplyWhenReady;
    }

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        var main = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (main is null || main.IsDisposed || !main.Visible) return;

        Application.Idle -= ApplyWhenReady;
        Apply(main);
    }

    private static void Apply(Control root)
    {
        foreach (var pic in Descendants(root).OfType<PictureBox>())
        {
            if (pic.Width < 24 || pic.Width > 64 || pic.Height < 24 || pic.Height > 64 || pic.Image is null)
                continue;

            var caption = pic.Parent?.Controls.OfType<Label>().FirstOrDefault();
            if (caption is null) continue;

            var text = (caption.Text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim().ToUpperInvariant();
            var accent = AccentFor(text);
            if (accent == Color.Empty) continue;

            pic.Image = Colorize(pic.Image, accent);
        }
    }

    private static Color AccentFor(string text)
    {
        if (text.Contains("PRODUT")) return Color.FromArgb(255, 190, 45);       // amarelo/laranja
        if (text.Contains("CLIENT")) return Color.FromArgb(70, 220, 135);       // verde
        if (text.Contains("FORNEC")) return Color.FromArgb(80, 180, 255);       // azul claro
        if (text.Contains("SERVI")) return Color.FromArgb(190, 120, 255);       // violeta
        if (text.Contains("HIST")) return Color.FromArgb(70, 215, 220);         // turquesa
        if (text.Contains("FLUXO") || text.Contains("CAIXA")) return Color.FromArgb(75, 220, 110); // verde dinheiro
        if (text.Contains("ORDENS") || text.Contains("OS")) return Color.FromArgb(255, 155, 65);    // laranja
        if (text.Contains("ORÇ") || text.Contains("ORC")) return Color.FromArgb(255, 210, 70);       // dourado
        if (text.Contains("TELA") || text.Contains("VENDAS")) return Color.FromArgb(60, 205, 255);  // ciano
        if (text.Contains("RELAT")) return Color.FromArgb(105, 185, 255);       // azul
        if (text.Contains("BACKUP")) return Color.FromArgb(110, 220, 180);      // verde agua
        if (text.Contains("CONFIG")) return Color.FromArgb(185, 190, 205);      // prata
        if (text.Contains("SAIR")) return Color.FromArgb(255, 105, 90);         // vermelho coral
        return Color.Empty;
    }

    private static Bitmap Colorize(Image source, Color accent)
    {
        var bmp = new Bitmap(source.Width, source.Height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var attrs = new System.Drawing.Imaging.ImageAttributes();
        float r = accent.R / 255f;
        float gr = accent.G / 255f;
        float b = accent.B / 255f;
        var matrix = new System.Drawing.Imaging.ColorMatrix(new[]
        {
            new float[] { r, 0, 0, 0, 0 },
            new float[] { 0, gr, 0, 0, 0 },
            new float[] { 0, 0, b, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0, 0, 0, 0, 1 }
        });
        attrs.SetColorMatrix(matrix);
        g.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
        return bmp;
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
