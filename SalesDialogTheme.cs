namespace LealInfoPDV;

internal static class SalesDialogTheme
{
    internal static void Apply(Form dialog, string theme, Image? sourceTexture)
    {
        var palette = SalesPalette.For(theme);
        var texture = sourceTexture == null ? null : new Bitmap(sourceTexture);
        dialog.Disposed += (_, _) => texture?.Dispose();
        dialog.BackColor = palette.Background;
        dialog.ForeColor = palette.Foreground;
        dialog.BackgroundImage = texture;
        dialog.BackgroundImageLayout = ImageLayout.Stretch;

        void Style(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    case PictureBox:
                        // Fotos e QR Codes mantêm suas imagens e a área de leitura.
                        continue;
                    case DataGridView grid:
                        grid.EnableHeadersVisualStyles = false;
                        grid.BackgroundColor = palette.Background;
                        grid.ForeColor = palette.Foreground;
                        grid.DefaultCellStyle.BackColor = palette.Background;
                        grid.DefaultCellStyle.ForeColor = palette.Foreground;
                        grid.AlternatingRowsDefaultCellStyle.BackColor = palette.Panel;
                        grid.AlternatingRowsDefaultCellStyle.ForeColor = palette.Foreground;
                        grid.ColumnHeadersDefaultCellStyle.BackColor = palette.Panel;
                        grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Foreground;
                        grid.RowHeadersDefaultCellStyle.BackColor = palette.Panel;
                        grid.RowHeadersDefaultCellStyle.ForeColor = palette.Foreground;
                        grid.DefaultCellStyle.SelectionBackColor = palette.Accent;
                        grid.DefaultCellStyle.SelectionForeColor = Color.White;
                        grid.GridColor = ControlPaint.Light(palette.Panel);
                        continue;
                    case TextBoxBase or ComboBox or NumericUpDown or DateTimePicker or ListBox:
                        control.BackColor = palette.Field;
                        control.ForeColor = palette.FieldText;
                        continue;
                    case Button button:
                        var color = button.BackColor;
                        bool semantic = color.G > color.R * 1.2 && color.G > color.B * 1.15
                            || color.R > color.G * 1.5 && color.R > color.B * 1.4;
                        if (!semantic) button.BackColor = button.DialogResult is DialogResult.Cancel or DialogResult.No
                            || button.Text.Contains("CANCEL", StringComparison.OrdinalIgnoreCase)
                            ? palette.Panel : palette.Accent;
                        button.ForeColor = button.BackColor.GetBrightness() > .65f ? palette.FieldText : Color.White;
                        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(button.BackColor);
                        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(button.BackColor);
                        break;
                    case TabControl tabs:
                        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                        tabs.DrawItem += (_, e) =>
                        {
                            bool selected = e.Index == tabs.SelectedIndex;
                            using var brush = new SolidBrush(selected ? palette.Accent : palette.Panel);
                            e.Graphics.FillRectangle(brush, e.Bounds);
                            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                                selected ? Color.White : palette.Foreground,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        };
                        tabs.BackColor = palette.Panel;
                        tabs.ForeColor = palette.Foreground;
                        break;
                    case TabPage page:
                        page.UseVisualStyleBackColor = false;
                        page.BackColor = palette.Background;
                        page.ForeColor = palette.Foreground;
                        page.BackgroundImage = texture;
                        page.BackgroundImageLayout = ImageLayout.Stretch;
                        break;
                    case Panel panel:
                        panel.BackColor = palette.Panel;
                        panel.ForeColor = palette.Foreground;
                        panel.BackgroundImage = texture;
                        panel.BackgroundImageLayout = ImageLayout.Stretch;
                        break;
                    case Label label:
                        label.BackColor = Color.Transparent;
                        label.ForeColor = palette.Foreground;
                        break;
                    default:
                        control.BackColor = palette.Panel;
                        control.ForeColor = palette.Foreground;
                        break;
                }
                if (control.HasChildren) Style(control);
            }
        }
        Style(dialog);
        dialog.Invalidate(true);
    }
}
