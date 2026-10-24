using System.Globalization;
namespace LealInfoPDV;

internal sealed class GlassCutPieceEditor : Form
{
    private readonly TextBox id = new() { Name = "EditPieceId" }, description = new() { Name = "EditPieceDescription" }, material = new() { Name = "EditPieceMaterial" };
    private readonly NumericUpDown width = new() { Name = "EditPieceWidth", Maximum = GlassCutPacking.MaximumDimension, DecimalPlaces = 5 };
    private readonly NumericUpDown height = new() { Name = "EditPieceHeight", Maximum = GlassCutPacking.MaximumDimension, DecimalPlaces = 5 };
    private readonly NumericUpDown quantity = new() { Name = "EditPieceQuantity", Minimum = 1, Maximum = GlassCutPacking.MaximumPieces };
    private bool rotation;
    private readonly HashSet<string> otherIds;
    internal GlassCutPlanForm.PieceRow? Edited { get; private set; }
    internal bool HadPlan { get; }
    internal GlassCutPieceEditor(GlassCutPlanForm.PieceRow source, IEnumerable<string> ids, Color background, Color foreground, bool hadPlan, Action<Form>? applyTheme)
    {
        HadPlan = hadPlan; otherIds = new(ids.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        Name = "GlassCutPieceEditor"; Text = "EDITAR PEÇA"; BackColor = background; ForeColor = foreground;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(510, 660); MinimumSize = new Size(360, 360); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(16) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); scroll.Controls.Add(fields); root.Controls.Add(scroll, 0, 0);
        decimal Read(string text, string unit) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) && v >= 0 && v <= GlassCutPacking.MaximumDimension / GlassDimensionUnits.Factor(unit) ? GlassDimensionUnits.ToMillimetres(v, unit) : 0;
        width.Value = Read(source.Width, source.WidthUnit); height.Value = Read(source.Height, source.HeightUnit);
        if (int.TryParse(source.Quantity, out int q) && q >= 1 && q <= quantity.Maximum) quantity.Value = q;
        id.Text = source.Id; description.Text = source.Description; material.Text = source.Material; rotation = source.Rotation;
        GlassWorkshopForm.AddField(fields, "IDENTIFICAÇÃO", id); GlassWorkshopForm.AddField(fields, "DESCRIÇÃO", description);
        var widthField = new GlassDimensionField(width); var heightField = new GlassDimensionField(height);
        GlassWorkshopForm.AddField(fields, "LARGURA", widthField); GlassWorkshopForm.AddField(fields, "ALTURA", heightField);
        widthField.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<ComboBox>().Single().SelectedItem = source.WidthUnit;
        heightField.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<ComboBox>().Single().SelectedItem = source.HeightUnit;
        GlassWorkshopForm.AddField(fields, "QUANTIDADE", quantity); GlassWorkshopForm.AddField(fields, "MATERIAL", material);
        var rotate = new SalesVisualButton { Name = "EditPieceRotation", Text = "PERMITIR ROTAÇÃO 90°: " + (rotation ? "SIM" : "NÃO") };
        rotate.Click += (_, _) => { rotation = !rotation; rotate.Text = "PERMITIR ROTAÇÃO 90°: " + (rotation ? "SIM" : "NÃO"); };
        GlassWorkshopForm.AddField(fields, "", rotate);
        var warning = new Label { Name = "EditPieceValidation", AutoSize = true }; GlassWorkshopForm.AddField(fields, "", warning);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var save = new SalesVisualButton { Name = "ConfirmPieceEdit", Text = "CONFIRMAR", Dock = DockStyle.Fill };
        var cancel = new SalesVisualButton { Name = "CancelPieceEdit", Text = "CANCELAR", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(id.Text) || otherIds.Contains(id.Text.Trim())) { warning.Text = "Informe uma identificação única."; return; }
            if (width.Value <= 0 || height.Value <= 0) { warning.Text = "Largura e altura devem ser positivas."; return; }
            string wu = widthField.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<ComboBox>().Single().Text;
            string hu = heightField.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<ComboBox>().Single().Text;
            Edited = new() { Id = id.Text.Trim(), Description = description.Text, Width = width.Value.ToString("G29"), Height = height.Value.ToString("G29"), Quantity = quantity.Value.ToString("0"), Material = material.Text, Rotation = rotation };
            Edited.WidthUnit = wu; Edited.HeightUnit = hu;
            DialogResult = DialogResult.OK; Close();
        };
        actions.Controls.Add(save, 0, 0); actions.Controls.Add(cancel, 1, 0); root.Controls.Add(actions, 0, 1); Controls.Add(root);
        CancelButton = cancel;
        // Mesma rotina utilizada pelas telas modernas do PDV.
        applyTheme?.Invoke(this);
        GlassWorkshopVisuals.ApplyPiece(this);
        Shown += (_, _) => { var area = Screen.FromControl(Owner ?? this).WorkingArea; Size = new(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); Location = new(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height))); };
    }
}
