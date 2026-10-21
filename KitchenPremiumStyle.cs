using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
namespace LealInfoPDV;

internal static class KitchenPremiumStyle
{
    internal static readonly Color Background = Color.FromArgb(4, 8, 12);
    internal static readonly Color Surface = Color.FromArgb(13, 22, 30);
    internal static readonly Color Gold = Color.FromArgb(246, 197, 69);
    private static readonly ConditionalWeakTable<Button, object> styled = new();
    // Kitchen owns its presentation; the shared idle-time shell must not repaint it.
    internal static bool Owns(Control control)
    {
        var form = control as Form ?? control.FindForm();
        return form is KitchenForm || form?.Name is "KitchenOrderEntry" or "AllKitchenTables" or "KitchenPdvSelection";
    }
    internal static void DrawFittedText(Graphics g, string text, Rectangle bounds, Color color, float preferredPixels, bool wrap = false)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
            FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap
        };
        float size = preferredPixels;
        using var ink = new SolidBrush(color);
        while (true)
        {
            using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            var measured = g.MeasureString(text, font, new SizeF(wrap ? bounds.Width : 10000, 10000), format);
            if ((measured.Width <= bounds.Width + .5f && measured.Height <= bounds.Height + .5f) || size <= 6)
            {
                g.DrawString(text, font, ink, bounds, format); return;
            }
            size -= .5f;
        }
    }
    internal static GraphicsPath Round(Rectangle box, int radius)
    {
        var path = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(box.Width, box.Height));
        if (d < 2) { path.AddRectangle(box); return path; }
        path.AddArc(box.Left, box.Top, d, d, 180, 90); path.AddArc(box.Right - d, box.Top, d, d, 270, 90);
        path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90); path.AddArc(box.Left, box.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    internal static void Frame(Control panel)
    {
        panel.Paint += (_, e) =>
        {
            if (panel.Width < 4 || panel.Height < 4) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Round(new Rectangle(1, 1, panel.Width - 3, panel.Height - 3), 10);
            using var edge = new Pen(Color.FromArgb(155, Gold)); e.Graphics.DrawPath(edge, shape);
        };
    }
    internal static void Apply(Control root)
    {
        if (root is KitchenTableButton or KitchenMoreTablesButton) return;
        root.BackColor = root is TextBox or NumericUpDown or ComboBox ? Surface : Background;
        root.ForeColor = Color.FromArgb(235, 242, 249);
        if (root is Button b)
        {
            b.FlatStyle = FlatStyle.Flat; b.UseVisualStyleBackColor = false; b.BackColor = Surface; b.ForeColor = Gold;
            b.FlatAppearance.BorderColor = Color.FromArgb(180, Gold); b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand; b.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            if (!styled.TryGetValue(b, out _))
            {
                styled.Add(b, new object());
                b.Paint += (_, e) =>
                {
                    if (b.Width < 4 || b.Height < 4) return;
                    var bounds = new Rectangle(2, 2, b.Width - 5, b.Height - 5);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var shape = Round(bounds, 7);
                    bool selected=b.AccessibleDescription=="selecionado";
                    Color top = selected ? Gold : b.Focused ? Color.FromArgb(97,73,18) : Color.FromArgb(27,39,49);
                    using var fill = new LinearGradientBrush(bounds, top, selected?Color.FromArgb(104,73,9):Color.FromArgb(3,9,13), 90f);
                    e.Graphics.Clear(Background); e.Graphics.FillPath(fill, shape);
                    using var edge = new Pen(b.Enabled ? Gold : Color.DimGray, b.Focused ? 2 : 1); e.Graphics.DrawPath(edge, shape);
                    if (b.Focused) { using var light = new Pen(Color.FromArgb(70, Gold), 5); e.Graphics.DrawPath(light, shape); }
                    DrawFittedText(e.Graphics, b.Text, new Rectangle(7, 4, b.Width - 14, b.Height - 8),
                        b.Enabled ? selected ? Color.FromArgb(20,17,4) : b.ForeColor : Color.Gray,
                        b.Font.SizeInPoints * e.Graphics.DpiY / 72f, !b.Name.StartsWith("KitchenCategory_",StringComparison.Ordinal));
                };
                b.MouseEnter += (_, _) => b.Invalidate(); b.MouseLeave += (_, _) => b.Invalidate();
            }
        }
        if (root is DataGridView grid)
        {
            grid.BackgroundColor = Background; grid.GridColor = Surface;
            grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(75, 60, 21); grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(17, 26, 35);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = Gold;
        }
        foreach (Control child in root.Controls) Apply(child);
    }
}



internal sealed class KitchenCenteredLabel : Label
{
    internal KitchenCenteredLabel() { TextAlign=ContentAlignment.MiddleCenter; DoubleBuffered=true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        KitchenPremiumStyle.DrawFittedText(e.Graphics,Text,new Rectangle(2,1,Math.Max(1,Width-4),Math.Max(1,Height-2)),ForeColor,Font.SizeInPoints*e.Graphics.DpiY/72f);
    }
}
