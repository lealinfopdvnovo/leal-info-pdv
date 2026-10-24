using System.ComponentModel;
using System.Globalization;

namespace LealInfoPDV;

public sealed record GlassCutContext(GlassProject Project, string CustomerName, IReadOnlyList<GlassMaterialLink> Materials);

public sealed class GlassCutPlanForm : Form
{
    public sealed class PieceRow : INotifyPropertyChanged
    {
        private string id = "", description = "", width = "0", height = "0", quantity = "1", material = "";
        private bool rotation = true;
        private string widthUnit = "mm", heightUnit = "mm";
        public string WidthUnit { get => widthUnit; set => ChangeUnit(ref widthUnit, value, true); }
        public string HeightUnit { get => heightUnit; set => ChangeUnit(ref heightUnit, value, false); }
        private void ChangeUnit(ref string field, string value, bool horizontal)
        {
            if (field == value) return;
            string converted = GlassDimensionUnits.ConvertText(horizontal ? Width : Height, field, value);
            field = value;
            if (horizontal) Width = converted; else Height = converted;
            PropertyChanged?.Invoke(this, new(horizontal ? nameof(WidthUnit) : nameof(HeightUnit)));
        }
        public string Id { get => id; set => Set(ref id, value, nameof(Id)); }
        public string Description { get => description; set => Set(ref description, value, nameof(Description)); }
        public string Width { get => width; set => Set(ref width, value, nameof(Width)); }
        public string Height { get => height; set => Set(ref height, value, nameof(Height)); }
        public string Quantity { get => quantity; set => Set(ref quantity, value, nameof(Quantity)); }
        public string Material { get => material; set => Set(ref material, value, nameof(Material)); }
        public bool Rotation { get => rotation; set { if (rotation != value) { rotation = value; PropertyChanged?.Invoke(this, new(nameof(Rotation))); } } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Set(ref string field, string value, string name) { if (field != value) { field = value ?? ""; PropertyChanged?.Invoke(this, new(name)); } }
    }
    private readonly Func<GlassCutContext> context;
    private readonly Action<Form>? theme;
    private readonly BindingList<PieceRow> rows = new();
    private readonly NumericUpDown width = new() { Name = "SheetWidth", Minimum = 0, Maximum = GlassCutPacking.MaximumDimension, DecimalPlaces = 2 };
    private readonly NumericUpDown height = new() { Name = "SheetHeight", Minimum = 0, Maximum = GlassCutPacking.MaximumDimension, DecimalPlaces = 2 };
    private readonly ComboBox material = new() { Name = "CutMaterialSuggestion", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView grid = new() { Name = "CutPieces", AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, Height = 280, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None };
    private readonly GlassCutDrawingView view = new() { Height = 340 };
    private readonly ComboBox sheet = new() { Name = "CutSheetSelector", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox summary = new() { Name = "CutSummary", ReadOnly = true, Multiline = true, Height = 165, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = new() { Name = "CutStatus", AutoSize = true };
    private readonly SalesVisualButton rotation = new() { Name = "CutAllowRotation", Text = "PERMITIR ROTAÇÃO 90°: NÃO" };
    private readonly SalesVisualButton preview = new() { Name = "CutPreview", Text = "PRÉVIA / IMPRIMIR A4", Enabled = false };
    private bool allowRotation;
    private readonly Font bodyFont = new("Segoe UI", 10);
    private readonly Font titleFont = new("Segoe UI", 18, FontStyle.Bold);
    private int sequence;
    public GlassCutResult? Result { get; private set; }
    public GlassCutPlanForm(Func<GlassCutContext> source, Action<Form>? applyTheme = null)
    {
        context = source; theme = applyTheme; Text = "PLANO DE CORTE — SIMULAÇÃO GEOMÉTRICA"; Name = "GlassCutPlan";
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96); Font = bodyFont;
        ClientSize = new Size(1100, 740); MinimumSize = new Size(640, 440); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.Controls.Add(new SalesGlowLabel { Text = "PLANO DE CORTE", Font = titleFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        layout.Controls.Add(new Label { Text = GlassCutPacking.Disclaimer, Dock = DockStyle.Fill, AutoSize = false }, 0, 1);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var left = Fields(out var leftScroll); var right = Fields(out var rightScroll);
        columns.Controls.Add(leftScroll, 0, 0); columns.Controls.Add(rightScroll, 1, 0); layout.Controls.Add(columns, 0, 2);
        GlassWorkshopForm.AddField(left, "LARGURA REAL DA CHAPA", new GlassDimensionField(width));
        GlassWorkshopForm.AddField(left, "ALTURA REAL DA CHAPA", new GlassDimensionField(height));
        GlassWorkshopForm.AddField(left, "MATERIAL SUGERIDO — CADASTRO JÁ VINCULADO", material);
        rotation.Click += (_, _) => { allowRotation = !allowRotation; rotation.Text = "PERMITIR ROTAÇÃO 90°: " + (allowRotation ? "SIM" : "NÃO"); Dirty(); };
        GlassWorkshopForm.AddField(left, "Só gira se esta opção E a coluna 90° da peça permitirem.", rotation);
        foreach (var (property, title, size) in new[] { ("Id", "Peça / ID", 90), ("Description", "Descrição", 160), ("Width", "Largura", 100), ("Height", "Altura", 100), ("Quantity", "Qtd.", 55), ("Material", "Material", 170) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, DataPropertyName = property, HeaderText = title, Width = size, ValueType = typeof(string), MaxInputLength = property is "Description" or "Material" ? 200 : 80 });
        foreach (var axis in new[] { "Width", "Height" })
        {
            var column = new DataGridViewComboBoxColumn { Name = axis + "Unit", DataPropertyName = axis + "Unit", HeaderText = "Unidade", Width = 72, ValueType = typeof(string) };
            column.Items.AddRange(GlassDimensionUnits.Symbols); grid.Columns.Insert(grid.Columns[axis].Index + 1, column);
        }
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Rotation", DataPropertyName = "Rotation", HeaderText = "90° peça", Width = 75, ValueType = typeof(bool) });
        grid.DataSource = rows; grid.SelectionChanged += (_, _) => { view.SelectedPieceId = (grid.CurrentRow?.DataBoundItem as PieceRow)?.Id ?? ""; view.Invalidate(); };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        AddButton(left, "EditCutPiece", "EDITAR PEÇA", EditSelected);
        GlassWorkshopForm.AddField(left, "PEÇAS EDITÁVEIS • use a rolagem horizontal para as demais colunas", grid);
        AddButton(left, "AddCutPiece", "ADICIONAR PEÇA", () => rows.Add(NewRow()));
        AddButton(left, "AddCutReference", "COPIAR MEDIDAS DO PROJETO", AddReference);
        AddButton(left, "RemoveCutPiece", "REMOVER PEÇA SELECIONADA", () => { if (grid.CurrentRow?.DataBoundItem is PieceRow row) rows.Remove(row); });
        AddButton(left, "RecalculateCut", "RECALCULAR PLANO", () => Recalculate());
        GlassWorkshopForm.AddField(left, "", status);
        GlassWorkshopForm.AddField(left, "", new Label { Text = "Edição independente: não altera medidas do projeto, orçamento ou estoque. O plano é incluído ao SALVAR PROJETO na Central.", AutoSize = true });
        GlassWorkshopForm.AddField(right, "CHAPA / MATERIAL", sheet);
        sheet.SelectedIndexChanged += (_, _) => { view.SheetIndex = Math.Max(0, sheet.SelectedIndex); view.Invalidate(); };
        GlassWorkshopForm.AddField(right, "DISTRIBUIÇÃO 2D PROPORCIONAL", view);
        AddButton(right, "CutPrevious", "CHAPA ANTERIOR", () => { if (sheet.SelectedIndex > 0) sheet.SelectedIndex--; });
        AddButton(right, "CutNext", "PRÓXIMA CHAPA", () => { if (sheet.SelectedIndex + 1 < sheet.Items.Count) sheet.SelectedIndex++; });
        GlassWorkshopForm.AddField(right, "APROVEITAMENTO GEOMÉTRICO", summary);
        preview.Click += (_, _) => { if (Result != null) GlassCutPlanPrint.Preview(this, context(), Result); };
        GlassWorkshopForm.AddField(right, "", preview);
        var close = new SalesVisualButton { Name = "CloseCut", Text = "FECHAR", Dock = DockStyle.Right, Width = 150, DialogResult = DialogResult.Cancel }; close.Click += (_, _) => Close(); CancelButton = close;
        layout.Controls.Add(close, 0, 3); Controls.Add(layout);
        rows.ListChanged += (_, _) => Dirty(); width.ValueChanged += (_, _) => Dirty(); height.ValueChanged += (_, _) => Dirty();
        RefreshMaterials(); AddReference();
        applyTheme?.Invoke(this); GlassWorkshopVisuals.ApplyCut(this);
        view.BackColor = BackColor; view.ForeColor = ForeColor;
        Shown += (_, _) =>
        {
            RefreshMaterials(); var area = Screen.FromControl(Owner ?? this).WorkingArea;
            Size = new(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        };
    }
    internal GlassSavedCut CaptureSaved()
    {
        if(!grid.EndEdit()) throw new InvalidOperationException("Conclua a edição da peça antes de salvar.");
        var pieces=new List<GlassSavedPiece>();
        foreach(var r in rows)
        {
            if(!decimal.TryParse(r.Width,NumberStyles.Number,CultureInfo.CurrentCulture,out var w) ||
                !decimal.TryParse(r.Height,NumberStyles.Number,CultureInfo.CurrentCulture,out var h) ||
                !int.TryParse(r.Quantity,out var q)) throw new InvalidDataException("Confira os valores da peça " + r.Id + " antes de salvar.");
            pieces.Add(new(r.Id,r.Description,GlassDimensionUnits.ToMillimetres(w,r.WidthUnit),
                GlassDimensionUnits.ToMillimetres(h,r.HeightUnit),r.WidthUnit,r.HeightUnit,q,r.Material,r.Rotation));
        }
        return new(width.Value,height.Value,GlassSavedControls.Unit(width),GlassSavedControls.Unit(height),
            allowRotation,material.Text,sequence,pieces.ToArray(),Result != null);
    }
    internal void RestoreSaved(GlassSavedCut saved)
    {
        rows.Clear(); sequence=saved.Sequence;
        GlassSavedControls.Set(width,saved.WidthMm,saved.WidthUnit);
        GlassSavedControls.Set(height,saved.HeightMm,saved.HeightUnit);
        allowRotation=saved.AllowRotation;
        rotation.Text="PERMITIR ROTAÇÃO 90°: " + (allowRotation ? "SIM" : "NÃO");
        RefreshMaterials();
        if(!material.Items.Contains(saved.MaterialSuggestion)) material.Items.Add(saved.MaterialSuggestion);
        material.SelectedItem=saved.MaterialSuggestion;
        foreach(var p in saved.Pieces)
            rows.Add(new PieceRow { Id=p.Id,Description=p.Description,WidthUnit=p.WidthUnit,HeightUnit=p.HeightUnit,
                Width=GlassDimensionUnits.Convert(p.WidthMm,"mm",p.WidthUnit).ToString("G29",CultureInfo.CurrentCulture),
                Height=GlassDimensionUnits.Convert(p.HeightMm,"mm",p.HeightUnit).ToString("G29",CultureInfo.CurrentCulture),
                Quantity=p.Quantity.ToString(CultureInfo.InvariantCulture),Material=p.Material,Rotation=p.Rotation });
        Dirty();
        if(saved.WasCalculated && !Recalculate()) throw new InvalidDataException("Não foi possível reconstruir o plano salvo.");
    }
    private static TableLayoutPanel Fields(out Panel scroll)
    {
        scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(6) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); scroll.Controls.Add(fields); return fields;
    }
    private static void AddButton(TableLayoutPanel parent, string name, string title, Action action)
    { var button = new SalesVisualButton { Name = name, Text = title }; button.Click += (_, _) => action(); GlassWorkshopForm.AddField(parent, "", button); }
    private void EditSelected()
    {
        grid.EndEdit();
        if (grid.CurrentRow?.DataBoundItem is not PieceRow selected) { status.Text = "Selecione uma peça para editar."; return; }
        using var editor = new GlassCutPieceEditor(selected, rows.Where(r => r != selected).Select(r => r.Id), BackColor, ForeColor, Result != null, theme);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Edited != null)
        {
            var replacement = editor.Edited;
            int index = rows.IndexOf(selected);
            rows[index] = replacement;
            grid.CurrentCell = grid.Rows[index].Cells["Id"];
            if (editor.HadPlan) Recalculate();
        }
    }
    private PieceRow NewRow()
    {
        string id; do { id = $"PEÇA {++sequence:00}"; } while (rows.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)));
        return new() { Id = id, Material = material.SelectedIndex > 0 ? material.Text : "" };
    }
    private void AddReference()
    {
        var p = context().Project; var row = NewRow(); row.Description = p.Description + " (referência do vão — conferir corte)";
        row.Width = p.WidthMm.ToString("0.##"); row.Height = p.HeightMm.ToString("0.##"); row.Quantity = p.Quantity.ToString(); rows.Add(row);
    }
    private void RefreshMaterials()
    {
        string selected = material.Text; material.Items.Clear(); material.Items.Add("Sem material associado");
        foreach (var m in context().Materials) material.Items.Add($"ID {m.ProductId} — {m.Name}");
        material.SelectedIndex = Math.Max(0, material.Items.IndexOf(selected));
    }
    private void Dirty()
    {
        Result = null; view.Result = null; view.Invalidate(); sheet.Items.Clear(); preview.Enabled = false;
        status.Text = "Dados alterados. RECALCULAR PLANO."; summary.Text = "Resultado anterior descartado; recalcule após editar.";
    }
    public bool Recalculate()
    {
        grid.EndEdit(); Dirty();
        try
        {
            var pieces = new List<GlassCutPiece>();
            foreach (var row in rows)
            {
                if (!decimal.TryParse(row.Width, NumberStyles.Number, CultureInfo.CurrentCulture, out var w) || !decimal.TryParse(row.Height, NumberStyles.Number, CultureInfo.CurrentCulture, out var h) || !int.TryParse(row.Quantity, out int q))
                    throw new ArgumentException("Confira os valores numéricos de " + row.Id);
                pieces.Add(new(row.Id.Trim(), row.Description, GlassDimensionUnits.ToMillimetres(w, row.WidthUnit), GlassDimensionUnits.ToMillimetres(h, row.HeightUnit), q, row.Material, row.Rotation));
            }
            Result = GlassCutPacking.Calculate(width.Value, height.Value, pieces, allowRotation);
            view.Result = Result;
            foreach (var s in Result.Sheets) sheet.Items.Add($"CHAPA {s.Number} — " + (s.Material == "" ? "material não informado" : s.Material));
            if (sheet.Items.Count > 0) sheet.SelectedIndex = 0;
            summary.Text = $"CHAPAS: {Result.Sheets.Count}\r\nÁREA TOTAL: {Result.TotalArea:0.######} m²\r\nUTILIZADA: {Result.UsedArea:0.######} m²\r\nNÃO UTILIZADA: {Result.UnusedArea:0.######} m²\r\nAPROVEITAMENTO: {Result.Utilization:0.##}%\r\nDESPERDÍCIO GEOMÉTRICO: {Result.Waste:0.##}%\r\nNÃO ACOMODADAS: {Result.RejectedQuantity}";
            foreach (var p in Result.Rejected) summary.AppendText($"\r\n{p.Piece.Id} ({p.Piece.Quantity} un.): {p.Reason}");
            status.Text = "Simulação recalculada. Materiais diferentes usam chapas separadas. Heurística sem garantia de ótimo global."; preview.Enabled = true; view.Invalidate(); return true;
        }
        catch (Exception e) when (e is ArgumentException or OverflowException) { status.Text = e.Message; summary.Text = "Plano inválido. Nenhuma medida do projeto foi alterada."; return false; }
    }
    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) { bodyFont.Dispose(); titleFont.Dispose(); } }
}

