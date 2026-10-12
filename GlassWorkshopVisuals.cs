namespace LealInfoPDV;

// Componentes da tela de vendas real, sem um tema paralelo.
internal static class GlassWorkshopVisuals
{
    internal static Panel Field(Control input)
    {
        var panel = new FramedPanel { Name = "FieldFrame" + input.Name, Dock = DockStyle.Top, Padding = new Padding(14, 10, 14, 10),
            Height = input is TextBox { Multiline: true } ? input.Height + 24 : 54 };
        input.Region?.Dispose(); input.Region = null;
        if (input is TextBox box) box.BorderStyle = BorderStyle.None;
        if (input is NumericUpDown number) number.BorderStyle = BorderStyle.None;
        input.Dock = DockStyle.Fill; input.Margin = Padding.Empty; panel.Controls.Add(input); return panel;
    }
    internal static void Apply(Form form)
    {
        var all = All(form).ToArray();
        var primary = all.OfType<Button>().First(x => x.Name == "Visualize");
        var field = all.OfType<NumericUpDown>().First(x => x.Name == "QuotePrice");
        var palette = new SalesPalette(form.BackColor, all.OfType<Panel>().First(p => p.AutoScroll).BackColor, primary.BackColor, form.ForeColor, field.BackColor, field.ForeColor);
        foreach (var card in all.OfType<FramedPanel>()) { card.Palette = palette; card.Invalidate(); }
        foreach (var panel in all.OfType<Panel>().Where(p => p.AutoScroll))
        {
            panel.Padding = new Padding(18); panel.Paint += (_, e) => SalesVisuals.Frame(e.Graphics, panel.ClientRectangle, palette: palette, texture: form.BackgroundImage);
        }
        foreach (var header in all.OfType<SalesGlowLabel>()) header.GlowColor = palette.Accent;
        var tabs = all.OfType<TabControl>().Single();
        tabs.DrawItem += (_, e) =>
        {
            var state = e.Graphics.Save(); e.Graphics.TranslateTransform(e.Bounds.Left, e.Bounds.Top);
            SalesVisuals.Frame(e.Graphics, new Rectangle(Point.Empty, e.Bounds.Size), e.Index == tabs.SelectedIndex, palette);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, new Rectangle(Point.Empty, e.Bounds.Size), palette.Foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.Graphics.Restore(state);
        };
    }
    private static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    private sealed class FramedPanel : Panel
    {
        internal SalesPalette Palette { get; set; } = SalesPalette.For("Futurista Azul");
        internal FramedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); SalesVisuals.Frame(e.Graphics, ClientRectangle, ContainsFocus, Palette); }
        protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }
    }
}
