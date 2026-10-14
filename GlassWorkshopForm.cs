namespace LealInfoPDV;

// Uma Central, um conjunto de campos. Renderizadores e cálculos são os já aprovados.
public sealed class GlassWorkshopForm : Form
{
    private readonly GlassProjectForm project = new();
    private readonly GlassMaterialSelection materials = new();
    private GlassCutPlanForm? cutPlan;
    private GlassStoredProject? stored;
    private readonly Label operationsStatus=new(){Name="GlassOperationsStatus",AutoSize=false,Height=100,Text="Financeiro/Agenda: salve o projeto para continuar."};
    private readonly Label saveStatus = new() { Name="ProjectSaveStatus", AutoSize=true };
    private readonly Action<Form>? projectTheme;
    private readonly Font titleFont = new("Segoe UI", 20, FontStyle.Bold);
    private readonly Font bodyFont = new("Segoe UI", 10);
    public GlassWorkshopForm(Action<Form>? applyTheme = null)
    {
        projectTheme = applyTheme;
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
        layout.Controls.Add(new SalesGlowLabel { Text = Text, Font = titleFont, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        var tabs = new TabControl { Name = "GlassSections", Dock = DockStyle.Fill, Multiline = true, Padding = new Point(14, 10) };
        var pages = new Dictionary<string, TabPage>();
        foreach (var name in new[] { "Projetos", "Materiais", "Medidas", "Cálculos", "Desenho", "Visualização", "Impressão" })
        {
            var page = new TabPage(name) { Name = "Glass" + name, Padding = new Padding(16), BackColor = Color.FromArgb(15, 31, 53), ForeColor = Color.White };
            tabs.TabPages.Add(page); pages.Add(name, page);
        }
        layout.Controls.Add(tabs, 0, 1);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        var status = new Panel { Name = "GlassStatus", Dock = DockStyle.Fill };
        var close = new SalesVisualButton { Name = "CloseGlassWorkshop", Text = "FECHAR", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close(); CancelButton = close;
        footer.Controls.Add(status, 0, 0); footer.Controls.Add(close, 1, 0); layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        var general = Fields(pages["Projetos"]);
        var measures = Fields(pages["Medidas"]);
        var calculations = Fields(pages["Cálculos"]);
        var printing = Fields(pages["Impressão"]);
        var materialFields = Fields(pages["Materiais"]);
        materials.Mount(materialFields);
        var openCut = new SalesVisualButton { Name = "OpenCutPlan", Text = "PLANO DE CORTE / APROVEITAMENTO" };
        openCut.Click += (_, _) =>
        {
            EnsureCutPlan();
            cutPlan!.ShowDialog(this);
        };
        AddField(materialFields, "SIMULAÇÃO GEOMÉTRICA EDITÁVEL", openCut);
        project.Mount(tabs, general, measures, calculations, pages["Desenho"], pages["Visualização"], printing, this, status);
        var save = new SalesVisualButton { Name="SaveGlassProject", Text="SALVAR PROJETO" };
        save.Click += (_,_) => RunProjectAction(() => { SaveCurrentProject(); saveStatus.Text="Projeto salvo. " + DateTime.Now.ToString("g"); });
        var open = new SalesVisualButton { Name="OpenGlassProject", Text="ABRIR PROJETO" };
        open.Click += (_,_) => RunProjectAction(() =>
        {
            using var chooser=new GlassSavedProjectBrowser(projectTheme);
            if(chooser.ShowDialog(this)!=DialogResult.OK || chooser.SelectedProjectId==null) return;
            if(HasUnsavedChanges() && MessageBox.Show(this,"Abrir outro projeto descarta alterações não salvas. Continuar?","Abrir projeto",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
            LoadSavedProject(chooser.SelectedProjectId);
        });
        var fresh = new SalesVisualButton { Name="NewSavedGlassProject", Text="LIMPAR / NOVO PROJETO" };
        fresh.Click += (_,_) => RunProjectAction(() =>
        {
            if(MessageBox.Show(this,"Iniciar outro projeto descarta alterações não salvas. Continuar?","Novo projeto",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
            ResetSavedProject();
        });
        AddField(general,"PROJETOS SALVOS",save); AddField(general,"",open); AddField(general,"",fresh); AddField(general,"",saveStatus);
        var finance=new SalesVisualButton{Name="OpenGlassFinance",Text="FINANCEIRO DO PROJETO"};
        finance.Click+=(_,_)=>RunProjectAction(()=>{var saved=OperationsProject(true);OperationsHost().OpenFinanceFromGlass(saved);RefreshOperations();});
        var agenda=new SalesVisualButton{Name="OpenGlassAgenda",Text="AGENDA — MEDIÇÃO / INSTALAÇÃO"};
        agenda.Click+=(_,_)=>RunProjectAction(()=>{var saved=OperationsProject(false);string? id=OperationsHost().OpenAgendaFromGlass(saved);if(id!=null && (!HasUnsavedChanges()||MessageBox.Show(this,"Abrir outro projeto descarta alterações não salvas. Continuar?","Agenda",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes))LoadSavedProject(id);});
        AddField(general,"FINANCEIRO / AGENDA",finance);AddField(general,"",agenda);AddField(general,"",operationsStatus);
        AddField(printing, "", new Label { AutoSize = true, Text = "DESENHO TÉCNICO A4: medidas e desenho, sem valores. Para preços e total, use ORÇAMENTO COMERCIAL A4 / PDF abaixo." });
        var commercial = new SalesVisualButton { Name = "CommercialPreview", Text = "ORÇAMENTO COMERCIAL A4 / PDF" };
        commercial.Click += (_, _) =>
        {
            try { GlassCommercialDocument.Preview(this, project.CommercialSnapshot(materials.Materials)); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Orçamento comercial", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        AddField(printing, "DOCUMENTO COMERCIAL", commercial);
        AddField(printing, "RECIBO", new SalesVisualButton { Name = "GlassReceiptPending", Text = "PENDENTE: VÍNCULO COM VENDA/PAGAMENTO", Enabled = false });
        // Reutiliza a rotina do PDV, sem estilizá-los por aproximação.
        var drawing = Descendants(this).OfType<GlassDrawingView>().Single();
        var drawingBackground = drawing.BackColor; var drawingForeground = drawing.ForeColor;
        applyTheme?.Invoke(this);
        drawing.BackColor = drawingBackground; drawing.ForeColor = drawingForeground;
        GlassWorkshopVisuals.Apply(this);
        Shown += (_, _) => { var area = Screen.FromControl(Owner ?? this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); };
    }
    private void EnsureCutPlan()
    {
        if(cutPlan!=null && !cutPlan.IsDisposed) return;
        cutPlan=new GlassCutPlanForm(() =>
        {
            var client=Descendants(this).OfType<ComboBox>().Single(c=>c.Name=="QuoteCustomer");
            return new GlassCutContext(project.CutReferenceProject,client.SelectedIndex>0?client.Text:"Cliente não selecionado",materials.Materials);
        },projectTheme);
    }
    internal GlassSavedProject CaptureSavedProject()=>project.CaptureSaved(materials.Materials.ToArray(),cutPlan?.CaptureSaved());
    internal GlassStoredProject SaveCurrentProject()
    {
        ValidateChildren();
        var snapshot=CaptureSavedProject();
        stored=GlassProjectStore.Save(snapshot,stored);
        RefreshOperations();return stored;
    }
    internal void LoadSavedProject(string id)
    {
        var loaded=GlassProjectStore.Load(id);
        // Valida antes de alterar os controles. O registro não é modificado ao abrir.
        var prior=CaptureSavedProject();
        try { ApplySavedProject(loaded.Data); stored=loaded; saveStatus.Text="Aberto: " + loaded.Data.Description;RefreshOperations(); }
        catch { ApplySavedProject(prior); throw; }
    }
    private void ApplySavedProject(GlassSavedProject saved)
    {
        project.RestoreSaved(saved); materials.RestoreSaved(saved.Materials);
        cutPlan?.Dispose(); cutPlan=null;
        if(saved.Cut!=null) { EnsureCutPlan(); cutPlan!.RestoreSaved(saved.Cut); }
    }
    internal void ResetSavedProject()
    {
        ApplySavedProject(new(1,GlassProject.ModelName,"Projeto de teste",1200,1000,"mm","mm",1,
            new(0,"","",0,0,0,0),Array.Empty<GlassMaterialLink>(),null,false));
        stored=null;saveStatus.Text="Novo projeto — ainda não salvo.";RefreshOperations();
    }
    private MainForm OperationsHost()
    {
        Form? host=Owner;while(host!=null && host is not MainForm)host=host.Owner;
        return host as MainForm??throw new InvalidOperationException("Abra a Vidraçaria pela tela principal do PDV.");
    }
    internal GlassStoredProject OperationsProject(bool needsCustomer)
    {
        ValidateChildren();
        if(stored==null||HasUnsavedChanges())throw new InvalidOperationException("Salve o projeto e suas alterações antes de abrir Financeiro/Agenda.");
        if(needsCustomer && stored.Data.Quote.CustomerId<=0)throw new InvalidOperationException("Selecione e salve o cliente do projeto antes de abrir o financeiro.");
        return stored;
    }
    private void RefreshOperations()
    {
        if(stored==null){operationsStatus.Text="Financeiro/Agenda: salve o projeto para continuar.";return;}
        try{operationsStatus.Text=GlassOperationsStore.Read(stored).Display;}
        catch(InvalidOperationException){operationsStatus.Text="Projeto alterado por outra sessão: reabra para atualizar o financeiro.";}
    }
    private bool HasUnsavedChanges()=>stored==null || System.Text.Json.JsonSerializer.Serialize(CaptureSavedProject())!=System.Text.Json.JsonSerializer.Serialize(stored.Data);
    private void RunProjectAction(Action action)
    {
        try { action(); }
        catch(Exception error) when(error is not OutOfMemoryException)
        { MessageBox.Show(this,"Não foi possível concluir. Os projetos já salvos foram preservados.\r\n"+error.Message,"Projetos da Vidraçaria",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
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
        fields.Controls.Add(control is TextBox or ComboBox or NumericUpDown ? GlassWorkshopVisuals.Field(control) : control);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { cutPlan?.Dispose(); materials.Dispose(); project.Dispose(); titleFont.Dispose(); bodyFont.Dispose(); }
    }
}
