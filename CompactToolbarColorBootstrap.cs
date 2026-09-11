using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

// Mantem exatamente o tamanho/layout atual da barra e troca somente a cor dos icones.
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

            var text = (caption.Text ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim()
                .ToUpperInvariant();

            var accent = AccentFor(text);
            if (accent == Color.Empty) continue;

            var old = pic.Image;
            pic.Image = Colorize(old, accent);
            if (!ReferenceEquals(old, pic.Image)) old.Dispose();
        }
    }

    private static Color AccentFor(string text)
    {
        if (text.Contains("PRODUT")) return Color.FromArgb(255, 185, 35);       // amarelo/laranja
        if (text.Contains("CLIENT")) return Color.FromArgb(45, 220, 105);       // verde vivo
        if (text.Contains("FORNEC")) return Color.FromArgb(60, 170, 255);       // azul claro
        if (text.Contains("SERVI")) return Color.FromArgb(195, 90, 255);        // violeta
        if (text.Contains("HIST")) return Color.FromArgb(0, 215, 205);          // turquesa
        if (text.Contains("FLUXO") || text.Contains("CAIXA")) return Color.FromArgb(70, 220, 75); // verde dinheiro
        if (text.Contains("ORDENS") || text.Contains("OS")) return Color.FromArgb(255, 135, 35);   // laranja
        if (text.Contains("ORÇ") || text.Contains("ORC")) return Color.FromArgb(255, 205, 35);      // dourado
        if (text.Contains("TELA") || text.Contains("VENDAS")) return Color.FromArgb(0, 195, 255);  // ciano
        if (text.Contains("RELAT")) return Color.FromArgb(80, 135, 255);        // azul royal
        if (text.Contains("BACKUP")) return Color.FromArgb(30, 215, 155);       // verde agua
        if (text.Contains("CONFIG")) return Color.FromArgb(205, 205, 215);      // prata
        if (text.Contains("SAIR")) return Color.FromArgb(255, 80, 70);          // vermelho coral
        return Color.Empty;
    }

    private static Bitmap Colorize(Image source, Color accent)
    {
        using var src = new Bitmap(source);
        var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                var p = src.GetPixel(x, y);
                if (p.A == 0)
                {
                    dst.SetPixel(x, y, Color.Transparent);
                    continue;
                }

                // Usa o brilho original apenas para manter volume/sombra,
                // mas substitui de verdade a tonalidade azul pela cor escolhida.
                int brightness = Math.Max(p.R, Math.Max(p.G, p.B));
                double factor = 0.55 + (brightness / 255.0) * 0.45;

                int r = Math.Clamp((int)(accent.R * factor), 0, 255);
                int g = Math.Clamp((int)(accent.G * factor), 0, 255);
                int b = Math.Clamp((int)(accent.B * factor), 0, 255);

                // Realce leve nas partes originalmente quase brancas.
                if (brightness > 225)
                {
                    r = Math.Min(255, r + 28);
                    g = Math.Min(255, g + 28);
                    b = Math.Min(255, b + 28);
                }

                dst.SetPixel(x, y, Color.FromArgb(p.A, r, g, b));
            }
        }

        return dst;
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
