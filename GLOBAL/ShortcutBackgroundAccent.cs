using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace LealInfoPDV;

internal static class ShortcutBackgroundAccent
{
    private static readonly ConditionalWeakTable<Control, object> Applied = new();

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += (_, _) => ApplyToOpenMainForms();
    }

    private static void ApplyToOpenMainForms()
    {
        foreach (Form form in Application.OpenForms)
        {
            if (form is MainForm)
                ApplyRecursive(form);
        }
    }

    private static void ApplyRecursive(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Panel card)
            {
                var caption = card.Controls.OfType<Label>().FirstOrDefault();
                var normalized = (caption?.Text ?? string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", " ")
                    .Trim();

                if (normalized.Equals("PRODUTOS", StringComparison.OrdinalIgnoreCase))
                    AttachAccent(card, AccentKind.Products);
                else if (normalized.Equals("TELA DE VENDAS", StringComparison.OrdinalIgnoreCase))
                    AttachAccent(card, AccentKind.Sales);
            }

            if (control.HasChildren)
                ApplyRecursive(control);
        }
    }

    private static void AttachAccent(Panel card, AccentKind kind)
    {
        if (Applied.TryGetValue(card, out _)) return;
        Applied.Add(card, new object());

        card.Paint += (_, e) => PaintBackgroundOnly(card, e, kind);
        card.Invalidate();
    }

    private static void PaintBackgroundOnly(Panel card, PaintEventArgs e, AccentKind kind)
    {
        if (card.Width < 20 || card.Height < 20) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Mantem a borda/glow original do botao intacta: pinta somente o miolo.
        var rect = new Rectangle(7, 7, card.Width - 15, card.Height - 15);
        const int radius = 15;
        using var path = RoundedPath(rect, radius);

        bool hover = card.ClientRectangle.Contains(card.PointToClient(Cursor.Position));
        Color top;
        Color bottom;

        if (kind == AccentKind.Products)
        {
            top = hover ? Color.FromArgb(255, 176, 55) : Color.FromArgb(255, 140, 20);
            bottom = hover ? Color.FromArgb(220, 92, 0) : Color.FromArgb(175, 65, 0);
        }
        else
        {
            top = hover ? Color.FromArgb(255, 82, 82) : Color.FromArgb(225, 42, 42);
            bottom = hover ? Color.FromArgb(185, 18, 18) : Color.FromArgb(135, 5, 5);
        }

        using var brush = new LinearGradientBrush(rect, top, bottom, 90f);
        e.Graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private enum AccentKind
    {
        Products,
        Sales
    }
}
