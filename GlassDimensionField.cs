namespace LealInfoPDV;

// Uma única fonte: o NumericUpDown canônico já utilizado pelo projeto.
// A entrada visível é uma projeção dele na unidade escolhida por este campo.
internal sealed class GlassDimensionField : TableLayoutPanel
{
    private readonly NumericUpDown canonical;
    private readonly NumericUpDown entry;
    private readonly ComboBox unit;
    private bool syncing;
    internal GlassDimensionField(NumericUpDown source)
    {
        canonical = source; Name = source.Name + "DimensionField";
        Dock = DockStyle.Top; Height = 54; ColumnCount = 2; RowCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        entry = new NumericUpDown { Name = source.Name + "Entry", Minimum = source.Minimum, Maximum = source.Maximum,
            DecimalPlaces = source.DecimalPlaces, Value = source.Value, ThousandsSeparator = false };
        unit = new ComboBox { Name = source.Name + "Unit", DropDownStyle = ComboBoxStyle.DropDownList };
        unit.Items.AddRange(GlassDimensionUnits.Symbols); unit.SelectedIndex = 0;
        Controls.Add(GlassWorkshopVisuals.Field(entry), 0, 0);
        Controls.Add(GlassWorkshopVisuals.Field(unit), 1, 0);
        Controls.Add(canonical); canonical.Visible = false;
        canonical.ValueChanged += CanonicalChanged;
        entry.ValueChanged += (_, _) => { if (!syncing) canonical.Value = GlassDimensionUnits.ToMillimetres(entry.Value, unit.Text); };
        unit.SelectedIndexChanged += (_, _) => RefreshDisplay();
    }
    private void CanonicalChanged(object? sender, EventArgs args) => RefreshDisplay();
    private void RefreshDisplay()
    {
        syncing = true;
        try
        {
            decimal factor = GlassDimensionUnits.Factor(unit.Text);
            // Alterar limites não pode reinterpretar o valor antigo na nova unidade.
            entry.Value = entry.Minimum;
            entry.Minimum = canonical.Minimum / factor;
            entry.Maximum = canonical.Maximum / factor;
            entry.DecimalPlaces = canonical.DecimalPlaces + (unit.Text == "m" ? 3 : unit.Text == "cm" ? 1 : 0);
            entry.Increment = canonical.Increment / factor;
            entry.Value = canonical.Value / factor;
        }
        finally { syncing = false; }
    }
    protected override void Dispose(bool disposing)
    { if (disposing) canonical.ValueChanged -= CanonicalChanged; base.Dispose(disposing); }
}
