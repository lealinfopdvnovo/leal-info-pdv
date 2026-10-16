using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace LealInfoPDV;

// Presentation only: controls, handlers, bindings, values and geometry remain intact.
internal static class PremiumGlobalTheme
{
    internal static readonly Color Background = Color.FromArgb(7, 18, 36);
    internal static readonly Color Surface = Color.FromArgb(15, 35, 58);
    internal static readonly Color Field = Color.FromArgb(10, 27, 46);
    internal static readonly Color Ink = Color.FromArgb(231, 243, 252);
    internal static readonly Color Cyan = Color.FromArgb(49, 215, 239);
    private static readonly ConditionalWeakTable<Control, Styled> Registered = new();
    private sealed class Styled { }
    private static bool installed;

    internal static void Install()
    {
        if (installed) return;
        installed = true;
        Application.Idle += (_, _) =>
        {
            foreach (var form in Application.OpenForms.Cast<Form>().ToArray()) Apply(form);
        };
    }

    internal static void Apply(Control control)
    {
        if (control.IsDisposed || control is PictureBox or RichTextBox or PrintPreviewControl ||
            control is Form && control.GetType().Name.Contains("Splash", StringComparison.Ordinal) ||
            control.GetType().Namespace?.StartsWith("Microsoft.Web.WebView2", StringComparison.Ordinal) == true) return;
        if (!Registered.TryGetValue(control, out _))
        {
            Registered.Add(control, new Styled());
            Style(control);
            control.ControlAdded += (_, e) =>
            {
                // Form builders often assign their original colors after adding a control.
                if (control.IsHandleCreated && !control.IsDisposed)
                    control.BeginInvoke((Action)(() => { if (!e.Control!.IsDisposed) Apply(e.Control); }));
            };
        }
        foreach (Control child in control.Controls) Apply(child);
    }

    private static bool Semantic(Color c) =>
        c.G > c.R * 1.25 && c.G > c.B * 1.15 || c.R > c.G * 1.5 && c.R > c.B * 1.4 ||
        c.R > 170 && c.G > 80 && c.G < 190 && c.B < 80;

    private static void Style(Control c)
    {
        switch (c)
        {
            case Form form:
                if (form is SplashForm || form.GetType().Name.Contains("Splash", StringComparison.Ordinal)) return;
                form.BackColor = Background;
                form.ForeColor = Ink;
                break;
            case DataGridView grid:
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = Background;
                grid.BorderStyle = BorderStyle.None;
                grid.GridColor = Color.FromArgb(34, 63, 86);
                grid.DefaultCellStyle.BackColor = Field;
                grid.DefaultCellStyle.ForeColor = Ink;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 87, 119);
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Surface;
                grid.AlternatingRowsDefaultCellStyle.ForeColor = Ink;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Cyan;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Cyan;
                grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                grid.RowHeadersDefaultCellStyle.ForeColor = Ink;
                grid.RowHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 87, 119);
                grid.RowHeadersDefaultCellStyle.SelectionForeColor = Color.White;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                break;
            case TextBoxBase or ComboBox or NumericUpDown or DateTimePicker or ListBox or TreeView or ListView:
                c.BackColor = Field;
                c.ForeColor = Ink;
                break;
            case Button b:
                if (b is SalesVisualButton) return;
                if (!Semantic(b.BackColor)) b.BackColor = Color.FromArgb(19, 77, 115);
                b.ForeColor = Color.White;
                b.UseVisualStyleBackColor = false;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Cyan;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(b.BackColor, .2f);
                b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(b.BackColor, .2f);
                // An inset translucent highlight gives depth without covering native text or icons.
                b.Paint += (_, e) =>
                {
                    if (b.Width < 8 || b.Height < 8) return;
                    using var shine = new LinearGradientBrush(new Rectangle(2, 2, b.Width - 4, Math.Max(1, b.Height / 3)),
                        Color.FromArgb(45, Color.White), Color.FromArgb(0, Color.White), 90f);
                    e.Graphics.FillRectangle(shine, 2, 2, b.Width - 4, b.Height / 3);
                    using var shadow = new Pen(Color.FromArgb(90, Color.Black));
                    e.Graphics.DrawLine(shadow, 2, b.Height - 3, b.Width - 3, b.Height - 3);
                };
                break;
            case Label label:
                if (!Semantic(label.ForeColor)) label.ForeColor = label.Font.Bold && label.Font.Size >= 12 ? Cyan : Ink;
                label.BackColor = Color.Transparent;
                break;
            case TabControl tabs:
                tabs.BackColor = Background;
                tabs.ForeColor = Ink;
                if (tabs.DrawMode == TabDrawMode.Normal)
                {
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tabs.DrawItem += (_, e) =>
                    {
                        if (e.Index < 0 || e.Index >= tabs.TabCount) return;
                        using var brush = new SolidBrush(e.Index == tabs.SelectedIndex ? Color.FromArgb(18, 87, 119) : Surface);
                        e.Graphics.FillRectangle(brush, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                            e.Index == tabs.SelectedIndex ? Cyan : Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    };
                }
                break;
            case TabPage page:
                page.UseVisualStyleBackColor = false;
                page.BackColor = Background;
                page.ForeColor = Ink;
                break;
            case ToolStrip strip:
                strip.Renderer = new ToolStripProfessionalRenderer(new PremiumColors());
                strip.BackColor = Surface;
                strip.ForeColor = Ink;
                foreach (ToolStripItem item in strip.Items) StyleItem(item);
                strip.ItemAdded += (_, e) => StyleItem(e.Item!);
                break;
            case Panel panel:
                // Respect the existing custom workshop canvases and sales frame renderers.
                if (panel.GetType() == typeof(Panel) || panel is FlowLayoutPanel or TableLayoutPanel)
                { panel.BackColor = Surface; panel.ForeColor = Ink; }
                break;
            case GroupBox or CheckBox or RadioButton:
                c.ForeColor = Ink;
                c.BackColor = Color.Transparent;
                break;
        }
    }

    private static void StyleItem(ToolStripItem item)
    {
        item.ForeColor = Ink;
        item.BackColor = Surface;
        if (item is ToolStripDropDownItem drop)
            foreach (ToolStripItem child in drop.DropDownItems) StyleItem(child);
    }

    private sealed class PremiumColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuItemSelected => Color.FromArgb(18, 87, 119);
        public override Color MenuItemBorder => Cyan;
        public override Color MenuItemSelectedGradientBegin => Surface;
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(18, 87, 119);
        public override Color MenuItemPressedGradientBegin => Surface;
        public override Color MenuItemPressedGradientEnd => Field;
        public override Color MenuItemPressedGradientMiddle => Surface;
        public override Color MenuBorder => Color.FromArgb(34, 63, 86);
    }
}
