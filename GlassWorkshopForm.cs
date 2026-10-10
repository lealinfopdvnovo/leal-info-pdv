namespace LealInfoPDV;

// Central inicial exclusiva da base 003. Não grava dados nem define regras técnicas.
public sealed class GlassWorkshopForm : Form
{
    private readonly Font titleFont = new("Segoe UI", 20, FontStyle.Bold);
    private readonly Font sectionFont = new("Segoe UI", 16, FontStyle.Bold);
    private readonly Font bodyFont = new("Segoe UI", 10);

    private static readonly (string Title, string Description)[] Sections =
    {
        ("Projetos", "Área reservada para os projetos de vidraçaria e seu fluxo de trabalho."),
        ("Materiais", "Área reservada para os materiais. Tipos, propriedades e cadastro serão definidos após levantamento."),
        ("Medidas", "Área reservada para medidas. Unidades, campos e validações ainda serão levantados."),
        ("Cálculos", "Área reservada para cálculos. Nenhuma fórmula, tolerância ou regra técnica foi implementada."),
        ("Desenho", "Área reservada para desenho de projetos. Ferramentas e geometria serão definidas em etapa posterior."),
        ("Visualização", "Área reservada para a visualização dos projetos, após definição do modelo de desenho."),
        ("Impressão", "Área reservada para impressão. Formatos e documentos serão definidos posteriormente.")
    };

    public GlassWorkshopForm()
    {
        Text = "CENTRAL DE VIDRAÇARIA";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 580);
        MinimumSize = new Size(560, 420);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        BackColor = Color.FromArgb(8, 18, 36);
        ForeColor = Color.White;
        Font = bodyFont;
        ShowInTaskbar = false;
        MinimizeBox = false;
        KeyPreview = true;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.Controls.Add(new Label { Text = "CENTRAL DE VIDRAÇARIA", Dock = DockStyle.Fill, Font = titleFont, ForeColor = Color.FromArgb(115, 233, 255), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(new Label { Text = "ESTRUTURA INICIAL • DESENVOLVIMENTO 003", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(168, 197, 222), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);

        var tabs = new TabControl { Name = "GlassSections", Dock = DockStyle.Fill, Multiline = true, Padding = new Point(14, 10) };
        foreach (var section in Sections)
        {
            var page = new TabPage(section.Title) { BackColor = Color.FromArgb(15, 31, 53), ForeColor = Color.White, AutoScroll = true, Padding = new Padding(20) };
            var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.Controls.Add(new Label { Text = section.Title.ToUpperInvariant(), Font = sectionFont, ForeColor = Color.FromArgb(104, 222, 255), Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 12, 0, 22) }, 0, 0);
            content.Controls.Add(new Label { Text = section.Description, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 22) }, 0, 1);
            content.Controls.Add(new Label { Text = "Disponível em etapa futura, após definição e validação dos requisitos.", Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(170, 192, 212), Margin = new Padding(0, 0, 0, 12) }, 0, 2);
            page.Controls.Add(content);
            tabs.TabPages.Add(page);
        }
        layout.Controls.Add(tabs, 0, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
        var close = new Button { Text = "FECHAR", Width = 130, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(11, 113, 161), ForeColor = Color.White, DialogResult = DialogResult.Cancel };
        close.FlatAppearance.BorderColor = Color.FromArgb(85, 204, 238);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        CancelButton = close;
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(Owner ?? this).WorkingArea;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { titleFont.Dispose(); sectionFont.Dispose(); bodyFont.Dispose(); }
    }
}
