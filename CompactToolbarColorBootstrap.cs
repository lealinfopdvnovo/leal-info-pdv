using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

// Mantem exatamente o tamanho/layout atual e troca somente a cor visual dos icones.
internal static class CompactToolbarColorBootstrap
{
    private static System.Windows.Forms.Timer? timer;

    [ModuleInitializer]
    internal static void Initialize()
    {
        timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) => ApplyWhenReady();
        timer.Start();
    }

    private static void ApplyWhenReady()
    {
        var main = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (main is null || main.IsDisposed || !main.Visible) return;
        Apply(main);
        timer?.Stop();
        timer?.Dispose();
        timer = null;
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

            var old = pic.Image;
            pic.Image = Colorize(old, accent);
            old.Dispose();
            pic.Invalidate();
        }
    }

    private static Color AccentFor(string text)
    {
        if (text.Contains("PRODUT")) return Color.FromArgb(255, 205, 35);       // amarelo
        if (text.Contains("CLIENT")) return Color.FromArgb(55, 220, 95);       // verde
        if (text.Contains("FORNEC")) return Color.FromArgb(255, 145, 35);      // laranja
        if (text.Contains("SERVI")) return Color.FromArgb(205, 95, 255);       // roxo
        if (text.Contains("HIST")) return Color.FromArgb(35, 220, 210);        // turquesa
        if (text.Contains("FLUXO")) return Color.FromArgb(70, 225, 80);        // verde dinheiro
        if (text.Contains("ORDENS")) return Color.FromArgb(255, 105, 55);      // laranja vermelho
        if (text.Contains("ORÇ") || text.Contains("ORC")) return Color.FromArgb(255, 190, 30); // dourado
        if (text.Contains("TELA")) return Color.FromArgb(0, 205, 255);         // ciano
        if (text.Contains("RELAT")) return Color.FromArgb(115, 135, 255);      // azul violeta
        if (text.Contains("BACKUP")) return Color.FromArgb(45, 225, 165);      // verde agua
        if (text.Contains("CONFIG")) return Color.FromArgb(235, 185, 45);      // ouro
        if (text.Contains("SAIR")) return Color.FromArgb(255, 70, 65);         // vermelho
        return Color.Empty;
    }

    private static Bitmap Colorize(Image source, Color accent)
    {
        using var src = new Bitmap(source);
        var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        for (int y = 0; y < src.Height; y++)
        for (int x = 0; x < src.Width; x++)
        {
            var p = src.GetPixel(x, y);
            if (p.A < 8) { dst.SetPixel(x, y, Color.Transparent); continue; }

            int lum = (p.R * 30 + p.G * 59 + p.B * 11) / 100;
            double shade = 0.72 + (lum / 255.0) * 0.38;
            int r = Math.Clamp((int)(accent.R * shade), 0, 255);
            int g = Math.Clamp((int)(accent.G * shade), 0, 255);
            int b = Math.Clamp((int)(accent.B * shade), 0, 255);
            dst.SetPixel(x, y, Color.FromArgb(p.A, r, g, b));
        }
        return dst;
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
