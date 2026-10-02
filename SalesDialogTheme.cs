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
        dialog.Font = new Font("Segoe UI", 10f);

        static bool IsStatusColor(Color color) =>
            color.R > 155 && color.R > color.G * 1.45 && color.R > color.B * 1.25
            || color.G > 120 && color.G > color.R * 1.25 && color.G > color.B * 1.08;

        static bool IsNeutral(Color color) =>
            color == Color.Empty || color == SystemColors.Control || color == SystemColors.ControlText
            || color == Color.Black || color == Color.White || color == Color.Transparent
            || Math.Abs(color.R - color.G) < 12 && Math.Abs(color.G - color.B) < 12;

        void Round(Control control, int radius)
        {
            void Apply()
            {
                if (control.IsDisposed || control.Width < 4 || control.Height < 4) return;
                int d = Math.Min(Math.Min(radius * 2, control.Width), control.Height);
                var rect = new Rectangle(0, 0, control.Width, control.Height);
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
                path.AddArc(rect.Right - d - 1, rect.Top, d, d, 270, 90);
                path.AddArc(rect.Right - d - 1, rect.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                control.Region?.Dispose();
                control.Region = new Region(path);
            }
            control.HandleCreated += (_, _) => Apply();
            control.Resize += (_, _) => Apply();
            if (control.IsHandleCreated) Apply();
        }

        void Style(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    case PictureBox:
                        // Fotos, logotipos e QR Codes mantêm suas imagens originais.
                        continue;
                    case DataGridView grid:
                        grid.EnableHeadersVisualStyles = false;
                        grid.BorderStyle = BorderStyle.None;
                        grid.BackgroundColor = palette.Panel;
                        grid.ForeColor = palette.FieldText;
                        grid.GridColor = ControlPaint.Light(palette.Panel, .18f);
                        grid.DefaultCellStyle.BackColor = palette.Field;
                        grid.DefaultCellStyle.ForeColor = palette.FieldText;
                        grid.DefaultCellStyle.SelectionBackColor = palette.Accent;
                        grid.DefaultCellStyle.SelectionForeColor = palette.Accent.GetBrightness() > .62f ? palette.FieldText : Color.White;
                        grid.AlternatingRowsDefaultCellStyle.BackColor = ControlPaint.Light(palette.Field, .045f);
                        grid.AlternatingRowsDefaultCellStyle.ForeColor = palette.FieldText;
                        grid.ColumnHeadersDefaultCellStyle.BackColor = palette.Panel;
                        grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Foreground;
                        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = palette.Panel;
                        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.Foreground;
                        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                        grid.RowHeadersDefaultCellStyle.BackColor = palette.Panel;
                        grid.RowHeadersDefaultCellStyle.ForeColor = palette.Foreground;
                        grid.RowHeadersDefaultCellStyle.SelectionBackColor = palette.Panel;
                        grid.RowHeadersDefaultCellStyle.SelectionForeColor = palette.Foreground;
                        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 34);
                        Round(grid, 12);
                        continue;
                    case TextBoxBase or ComboBox or NumericUpDown or DateTimePicker or ListBox:
                        control.BackColor = palette.Field;
                        control.ForeColor = palette.FieldText;
                        Round(control, 9);
                        break;
                    case Button button:
                        bool semantic = IsStatusColor(button.BackColor);
                        if (!semantic)
                            button.BackColor = button.DialogResult is DialogResult.Cancel or DialogResult.No
                                || button.Text.Contains("CANCEL", StringComparison.OrdinalIgnoreCase)
                                || button.Text.Contains("FECHAR", StringComparison.OrdinalIgnoreCase)
                                || button.Text.Contains("VOLTAR", StringComparison.OrdinalIgnoreCase)
                                ? palette.Panel : palette.Accent;
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderSize = 0;
                        button.FlatAppearance.BorderColor = ControlPaint.Light(button.BackColor, .22f);
                        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(button.BackColor, .16f);
                        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(button.BackColor, .12f);
                        button.ForeColor = button.BackColor.GetBrightness() > .62f ? palette.FieldText : Color.White;
                        button.Font = new Font("Segoe UI", Math.Max(9f, button.Font.Size), FontStyle.Bold);
                        button.Cursor = Cursors.Hand;
                        Round(button, 10);
                        break;
                    case TabControl tabs:
                        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                        tabs.BackColor = palette.Panel;
                        tabs.ForeColor = palette.Foreground;
                        tabs.DrawItem += (_, e) =>
                        {
                            bool selected = e.Index == tabs.SelectedIndex;
                            using var brush = new SolidBrush(selected ? palette.Accent : palette.Panel);
                            e.Graphics.FillRectangle(brush, e.Bounds);
                            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                                selected ? (palette.Accent.GetBrightness() > .62f ? palette.FieldText : Color.White) : palette.Foreground,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                        };
                        break;
                    case TabPage page:
                        page.UseVisualStyleBackColor = false;
                        page.BackColor = palette.Background;
                        page.ForeColor = palette.Foreground;
                        page.BackgroundImage = null;
                        break;
                    case Panel panel:
                        panel.BackColor = palette.Panel;
                        panel.ForeColor = palette.Foreground;
                        panel.BackgroundImage = null;
                        Round(panel, 14);
                        break;
                    case LinkLabel link:
                        link.BackColor = Color.Transparent;
                        link.LinkColor = palette.Accent;
                        link.ActiveLinkColor = ControlPaint.Light(palette.Accent);
                        link.VisitedLinkColor = palette.Accent;
                        break;
                    case Label label:
                        label.BackColor = Color.Transparent;
                        if (IsNeutral(label.ForeColor) || !IsStatusColor(label.ForeColor)) label.ForeColor = palette.Foreground;
                        break;
                    case CheckBox or RadioButton:
                        control.BackColor = palette.Panel;
                        if (IsNeutral(control.ForeColor)) control.ForeColor = palette.Foreground;
                        break;
                    case GroupBox box:
                        box.BackColor = palette.Panel;
                        box.ForeColor = palette.Foreground;
                        break;
                    default:
                        if (IsNeutral(control.BackColor)) control.BackColor = palette.Panel;
                        if (IsNeutral(control.ForeColor)) control.ForeColor = palette.Foreground;
                        break;
                }

                if (control.HasChildren) Style(control);
            }
        }

        Style(dialog);
        dialog.Invalidate(true);
    }
}
