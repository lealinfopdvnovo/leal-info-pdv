using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
namespace LealInfoPDV;

// Scoped to kitchen controls: never changes the application theme or client settings.
internal static class KitchenPremiumStyle
{
    internal static readonly Color Background = Color.FromArgb(12, 15, 21);
    internal static readonly Color Surface = Color.FromArgb(23, 29, 38);
    private static readonly ConditionalWeakTable<Button, object> styled = new();
    internal static void Apply(Control root)
    {
        root.BackColor = root is TextBox or NumericUpDown or ComboBox ? Surface : Background;
        root.ForeColor = Color.FromArgb(235, 242, 249);
        if (root is Button b)
        {
            b.FlatStyle = FlatStyle.Flat; b.UseVisualStyleBackColor = false;
            b.BackColor = Color.FromArgb(15, 88, 111); b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = Color.FromArgb(25, 201, 225); b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand; b.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            if (!styled.TryGetValue(b, out _))
            {
                styled.Add(b, new object());
                b.Paint += (_, e) =>
                {
                    if (b.ClientSize.Width < 3 || b.ClientSize.Height < 3) return;
                    var bounds = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
                    Color color = b.Enabled ? b.BackColor : Color.FromArgb(45, 49, 55);
                    using var fill = new LinearGradientBrush(bounds, ControlPaint.Light(color, .35f), ControlPaint.Dark(color, .35f), 90f);
                    e.Graphics.FillRectangle(fill, bounds);
                    using var edge = new Pen(b.Enabled ? ControlPaint.Light(color, .9f) : Color.Gray);
                    e.Graphics.DrawRectangle(edge, bounds);
                    using var light = new Pen(Color.FromArgb(110, Color.White));
                    e.Graphics.DrawLine(light, 2, 2, b.Width - 3, 2);
                    TextRenderer.DrawText(e.Graphics, b.Text, b.Font, new Rectangle(5, 4, b.Width - 10, b.Height - 8),
                        b.Enabled ? b.ForeColor : Color.Silver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                };
                b.MouseEnter += (_, _) => b.Invalidate(); b.MouseLeave += (_, _) => b.Invalidate();
            }
        }
        if (root is DataGridView grid)
        {
            grid.BackgroundColor = Background; grid.GridColor = Surface;
            grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 93, 114);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(28, 35, 45);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        }
        foreach (Control child in root.Controls) Apply(child);
    }
}
