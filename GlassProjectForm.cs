namespace LealInfoPDV;

public sealed class GlassProjectForm : Form
{
    private readonly TextBox description = new() { Name = "Description", MaxLength = 160, Text = "Projeto de teste", Dock = DockStyle.Fill };
    private readonly NumericUpDown widthMm = Number("WidthMm", 1200, 0, 1000000, 2);
    private readonly NumericUpDown heightMm = Number("HeightMm", 1000, 0, 1000000, 2);
    private readonly NumericUpDown quantity = Number("Quantity", 1, 1, 9999, 0);
    private readonly GlassDrawingView drawing = new() { Name = "Drawing" };
    private readonly Label message = new() { Name = "ValidationMessage", AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 210, 130) };
    private readonly Button visualize = Button("Visualize", "VISUALIZAR");
    private readonly Button print = Button("Preview", "PRÉVIA / IMPRIMIR A4");
    private readonly Panel surface = new() { Name = "DrawingSurface", Dock = DockStyle.Fill };
    private readonly Button mode2D = new SalesVisualButton() { Name = "Mode2D", Text = "2D", Width = 54, Height = 30 };
    private readonly Button mode3D = new SalesVisualButton() { Name = "Mode3D", Text = "3D", Width = 54, Height = 30, Enabled = false };
    private readonly Button reset3D = new SalesVisualButton() { Name = "Reset3D", Text = "RESETAR VISÃO", Width = 160, Height = 30, Visible = false };
    private readonly Button measurements3D = new SalesVisualButton { Name = "Toggle3DMeasurements", Text = "MOSTRAR MEDIDAS", Width = 210, Height = 30, Visible = false };
    private readonly Label viewHint = new() { Name = "ViewHint", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(174, 201, 220) };
    private GlassBox3DView? drawing3D;
    private bool unavailable3D;
    private GlassQuoteForm? quote;
    private IWin32Window? printOwner;
    private readonly Font heading = new("Segoe UI", 13, FontStyle.Bold);
    private readonly Font body = new("Segoe UI", 10);

    public GlassProjectForm()
    {
        Text = "NOVO PROJETO — VIDRAÇARIA";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 680);
        MinimumSize = new Size(560, 420);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = body;
        BackColor = Color.FromArgb(8, 18, 36);
        ForeColor = Color.White;
        ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 14, 0), Name = "Parameters" };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control c) { c.Margin = new Padding(0, 4, 0, 8); fields.Controls.Add(c); }
        Add(new Label { Text = "NOVO PROJETO", Font = heading, AutoSize = true, ForeColor = Color.FromArgb(115, 233, 255) });
        Add(new Label { Text = "MODELO DISPONÍVEL", AutoSize = true });
        var thumbnail = new GlassDrawingView { ShowDimensions = false, Height = 78, Dock = DockStyle.Top, Project = new GlassProject("", 1200, 1000, 1) };
        Add(thumbnail);
        Add(new Label { Text = GlassProject.ModelName, AutoSize = true, MaximumSize = new Size(255, 0) });
        Add(new Label { Text = "DESCRIÇÃO DO PROJETO", AutoSize = true }); Add(description);
        Add(new Label { Text = "LARGURA DO VÃO", AutoSize = true }); Add(new GlassDimensionField(widthMm));
        Add(new Label { Text = "ALTURA DO VÃO", AutoSize = true }); Add(new GlassDimensionField(heightMm));
        Add(new Label { Text = "QUANTIDADE", AutoSize = true }); Add(quantity);
        Add(visualize); Add(print);
        var commercial = Button("GlassQuote", "ORÇAMENTO DA VIDRAÇARIA");
        Add(commercial);
        commercial.Click += (_, _) =>
        {
            if (quote == null || quote.IsDisposed)
            {
                quote = new GlassQuoteForm(CurrentProject());
                quote.Show(this);
            }
            else { quote.UpdateProject(CurrentProject()); quote.Activate(); }
        };
        Add(new Button { Name = "SaveProject", Text = "SALVAR — PRÓXIMA ETAPA", Enabled = false, Dock = DockStyle.Top, Height = 36 });
        Add(message);
        scroll.Controls.Add(fields);
        var graph = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        graph.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        graph.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        graph.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        graph.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        graph.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        graph.Controls.Add(new Label { Text = GlassProject.ModelName, Font = heading, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(115, 233, 255) }, 0, 0);
        var modes = new FlowLayoutPanel { Name = "ViewModes", Dock = DockStyle.Fill, WrapContents = false };
        foreach (var button in new[] { mode2D, mode3D, reset3D }) { button.FlatStyle = FlatStyle.Flat; button.ForeColor = Color.White; button.BackColor = Color.FromArgb(11, 113, 161); modes.Controls.Add(button); }
        mode2D.Click += (_, _) => Show2D();
        mode3D.Click += (_, _) => Show3D();
        reset3D.Click += (_, _) => drawing3D?.ResetView();
        measurements3D.Click += (_, _) =>
        {
            if (drawing3D == null || drawing3D.IsUnavailable || !drawing3D.CanShowTechnicalDimensions) return;
            drawing3D.ShowTechnicalDimensions = !drawing3D.ShowTechnicalDimensions;
            measurements3D.Text = drawing3D.ShowTechnicalDimensions ? "OCULTAR MEDIDAS" : "MOSTRAR MEDIDAS";
        };
        graph.Controls.Add(modes, 0, 1);
        surface.Controls.Add(drawing);
        graph.Controls.Add(surface, 0, 2);
        graph.Controls.Add(viewHint, 0, 3);
        Show2D();
        layout.Controls.Add(scroll, 0, 0); layout.Controls.Add(graph, 1, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var close = Button("CloseProject", "FECHAR"); close.Width = 130; close.Height = 34; close.Dock = DockStyle.None;
        close.DialogResult = DialogResult.Cancel; close.Click += (_, _) => Close();
        footer.Controls.Add(close); layout.Controls.Add(footer, 0, 1); layout.SetColumnSpan(footer, 2);
        Controls.Add(layout); CancelButton = close;
        print.Enabled = false;
        visualize.Click += (_, _) => UpdateDrawing();
        print.Click += (_, _) => { if (print.Enabled && drawing.Project != null) GlassProjectPrint.Preview(printOwner ?? this, drawing.Project); };
        void Changed(object? sender, EventArgs e) { quote?.UpdateProject(CurrentProject()); print.Enabled = false; if (drawing.Project != null) message.Text = "Medidas/dados alterados. Clique em ATUALIZAR DESENHO."; }
        widthMm.ValueChanged += Changed; heightMm.ValueChanged += Changed; quantity.ValueChanged += Changed; description.TextChanged += Changed;
        Shown += (_, _) => { var area = Screen.FromControl(Owner ?? this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); };
    }
    // A Central hospeda os controles já existentes; esta instância não abre janela.
    internal void Mount(TabControl tabs, TableLayoutPanel projects, TableLayoutPanel measures,
        TableLayoutPanel calculations, TabPage design, TabPage visualization, TableLayoutPanel printing,
        Form owner, Control status)
    {
        GlassWorkshopForm.AddField(projects, "MODELO", new Label { Text = GlassProject.ModelName, AutoSize = true });
        GlassWorkshopForm.AddField(projects, "DESCRIÇÃO DO PROJETO", description);
        GlassWorkshopForm.AddField(projects, "QUANTIDADE", quantity);
        quote = new GlassQuoteForm(CurrentProject());
        quote.Mount(projects, calculations);
        GlassWorkshopForm.AddField(measures, "LARGURA DO VÃO", widthMm.Parent!);
        GlassWorkshopForm.AddField(measures, "ALTURA DO VÃO", heightMm.Parent!);
        GlassWorkshopForm.AddField(measures, "", visualize);
        GlassWorkshopForm.AddField(measures, "", message);
        GlassWorkshopForm.AddField(printing, "IMPRESSÃO TÉCNICA A4", print);
        printOwner = owner;
        status.Controls.Add(viewHint); viewHint.Dock = DockStyle.Fill;
        void MoveSurface(bool three)
        {
            if (three) visualization.Controls.Add(surface); else design.Controls.Add(surface);
            surface.Dock = DockStyle.Fill; surface.BringToFront();
            if (three) Show3D(); else Show2D();
        }
        var viewFooter = new Panel { Dock = DockStyle.Bottom, Height = 44 };
        visualization.Controls.Add(viewFooter);
        reset3D.Dock = DockStyle.Left; viewFooter.Controls.Add(reset3D);
        measurements3D.Dock = DockStyle.Right; viewFooter.Controls.Add(measurements3D);
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab == visualization) MoveSurface(true);
            else { Show2D(); if (tabs.SelectedTab == design) MoveSurface(false); }
        };
        MoveSurface(false);
        void DataChanged(object? sender, EventArgs args)
        {
            UpdateDrawing();
            if (tabs.SelectedTab == visualization) Show3D();
        }
        widthMm.ValueChanged += DataChanged; heightMm.ValueChanged += DataChanged;
        quantity.ValueChanged += DataChanged; description.TextChanged += DataChanged;
        UpdateDrawing();
    }
    internal GlassCommercialSnapshot CommercialSnapshot(IReadOnlyList<GlassMaterialLink> materials)
        => (quote ?? throw new InvalidOperationException("Projeto não disponível.")).CommercialSnapshot(materials);
    internal GlassProject CutReferenceProject => CurrentProject();
    private GlassProject CurrentProject() => new(description.Text.Trim(), widthMm.Value, heightMm.Value, (int)quantity.Value);
    public void UpdateDrawing()
    {
        // ValidateEditText é executado pelo NumericUpDown quando perde foco antes do clique.
        var project = new GlassProject(description.Text.Trim(), widthMm.Value, heightMm.Value, (int)quantity.Value);
        if (project.ValidationError != null) { message.Text = project.ValidationError; print.Enabled = false; drawing.Project = null; drawing.Invalidate(); if (drawing3D != null) drawing3D.Project = null; mode3D.Enabled = false; Show2D(); return; }
        drawing.Project = project; drawing.Invalidate(); if (drawing3D != null) drawing3D.Project = project; mode3D.Enabled = !unavailable3D; print.Enabled = true; visualize.Text = "ATUALIZAR DESENHO";
        message.Text = "Desenho atualizado. SALVAR pendente da definição da persistência de projetos.";
    }
    private void Show2D()
    {
        if (drawing3D != null) drawing3D.Visible = false;
        drawing.Visible = true; drawing.BringToFront(); reset3D.Visible = false;
        measurements3D.Visible = false;
        mode2D.BackColor = Color.FromArgb(20, 149, 194); mode3D.BackColor = Color.FromArgb(11, 113, 161);
        viewHint.Text = unavailable3D ? "3D indisponível neste equipamento. O desenho 2D continua disponível."
            : "Esquema do vão • medidas em mm • sem regras de fabricação/corte";
    }
    private void Show3D()
    {
        if (unavailable3D || drawing.Project == null) return;
        try
        {
            if (drawing3D == null)
            {
                drawing3D = new GlassBox3DView();
                drawing3D.RenderingUnavailable += (_, _) => Disable3D();
                drawing3D.SizeChanged += (_, _) => UpdateMeasurementAccess();
                drawing3D.FontChanged += (_, _) => UpdateMeasurementAccess();
                surface.Controls.Add(drawing3D);
            }
            drawing3D.Project = drawing.Project;
            drawing.Visible = false; drawing3D.Visible = true; drawing3D.BringToFront(); drawing3D.Focus();
            reset3D.Visible = true;
            measurements3D.Visible = true;
            mode3D.BackColor = Color.FromArgb(20, 149, 194); mode2D.BackColor = Color.FromArgb(11, 113, 161);
            viewHint.Text = "3D técnico • arraste para girar • rodinha: zoom • profundidade apenas ilustrativa";
            UpdateMeasurementAccess();
        }
        catch (Exception) { Disable3D(); }
    }
    private void Disable3D()
    {
        unavailable3D = true; mode3D.Enabled = false; Show2D();
    }
    private void UpdateMeasurementAccess()
    {
        if (drawing3D == null || !drawing3D.Visible || !measurements3D.Visible) return;
        measurements3D.Enabled = drawing3D.CanShowTechnicalDimensions && !drawing3D.IsUnavailable;
        viewHint.Text = drawing3D.CanShowTechnicalDimensions
            ? "3D técnico • arraste para girar • rodinha: zoom • profundidade apenas ilustrativa"
            : "Amplie o visor para cotas. As dimensões fixas continuam disponíveis.";
    }
    private static NumericUpDown Number(string name, decimal value, decimal min, decimal max, int decimals) => new()
    { Name = name, Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = value, Dock = DockStyle.Fill, ThousandsSeparator = false };
    private static Button Button(string name, string text) => new SalesVisualButton() { Name = name, Text = text, Dock = DockStyle.Top,
        Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(11, 113, 161), ForeColor = Color.White };
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) { quote?.Dispose(); heading.Dispose(); body.Dispose(); } }
}

