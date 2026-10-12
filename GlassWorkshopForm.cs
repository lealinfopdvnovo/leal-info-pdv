namespace LealInfoPDV;

// Uma Central, um conjunto de campos. Renderizadores e cálculos são os já aprovados.
public sealed class GlassWorkshopForm : Form
{
    private readonly GlassProjectForm project = new();
    private readonly Font titleFont = new("Segoe UI", 20, FontStyle.Bold);
    private readonly Font bodyFont = new("Segoe UI", 10);
    public GlassWorkshopForm(Action<Form>? applyTheme = null)
    {
        Text = "CENTRAL DE VIDRAÇARIA";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1000, 700); MinimumSize = new Size(560, 420);
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        BackColor = Color.FromArgb(8, 18, 36); ForeColor = Color.White;
        Font = bodyFont; ShowInTaskbar = false; MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(new Label { Text = Text, Font = titleFont, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var tabs = new TabControl { Name = "GlassSections", Dock = DockStyle.Fill, Multiline = true, Padding = new Point(14, 10) };
        var pages = new Dictionary<string, TabPage>();
        foreach (var name in new[] { "Projetos", "Materiais", "Medidas", "Cálculos", "Desenho", "Visualização", "Impressão" })
        {
            var page = new TabPage(name) { Name = "Glass" + name, Padding = new Padding(16), BackColor = Color.FromArgb(15, 31, 53), ForeColor = Color.White };
            tabs.TabPages.Add(page); pages.Add(name, page);
        }
        layout.Controls.Add(tabs, 0, 1);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        var status = new Panel { Name = "GlassStatus", Dock = DockStyle.Fill };
        var close = new Button { Name = "CloseGlassWorkshop", Text = "FECHAR", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close(); CancelButton = close;
        footer.Controls.Add(status, 0, 0); footer.Controls.Add(close, 1, 0); layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        var general = Fields(pages["Projetos"]);
        var measures = Fields(pages["Medidas"]);
        var calculations = Fields(pages["Cálculos"]);
        var printing = Fields(pages["Impressão"]);
        AddField(Fields(pages["Materiais"]), "MATERIAIS", new Label { AutoSize = true,
            Text = "Área reservada para a próxima etapa. O cadastro de produtos/materiais existente será utilizado." });
        project.Mount(tabs, general, measures, calculations, pages["Desenho"], pages["Visualização"], printing, this, status);
        AddField(printing, "", new Label { AutoSize = true, Text = "Impressão técnica existente • A4 • sem orçamento financeiro no documento." });
        // Reutiliza a rotina do PDV, sem estilizá-los por aproximação.
        var drawing = Descendants(this).OfType<GlassDrawingView>().Single();
        var drawingBackground = drawing.BackColor; var drawingForeground = drawing.ForeColor;
        applyTheme?.Invoke(this);
        drawing.BackColor = drawingBackground; drawing.ForeColor = drawingForeground;
        Shown += (_, _) => { var area = Screen.FromControl(Owner ?? this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); };
    }
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static TableLayoutPanel Fields(TabPage page)
    {
        var scroll = new Panel { Name = "Fields" + page.Text, Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(12) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); scroll.Controls.Add(fields); page.Controls.Add(scroll); return fields;
    }
    internal static void AddField(TableLayoutPanel fields, string caption, Control control)
    {
        if (!string.IsNullOrWhiteSpace(caption)) fields.Controls.Add(new Label { Text = caption, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 6) });
        control.Dock = DockStyle.Top; control.Margin = new Padding(0, 0, 0, 12);
        if (control is Button) control.Height = 42;
        fields.Controls.Add(control);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { project.Dispose(); titleFont.Dispose(); bodyFont.Dispose(); }
    }
}
