using System.Drawing;
using System.Windows.Forms;

namespace LealInfoPDV;

// Metrics are always derived from the approved 1366px layout, never from the
// previous resize. Pixel fonts avoid applying monitor DPI twice to fitted sizes.
internal sealed class SalesResponsiveLayout : IDisposable
{
    private readonly Form form;
    private readonly Control header;
    private readonly StatusStrip footer;
    private readonly Control actions;
    private readonly List<Metric> metrics = new();
    private readonly Dictionary<Control, Font> fonts = new();
    private bool applying;
    private sealed record Metric(Control Control, Padding Padding, Padding Margin, float FontPixels,
        FontStyle Style, string Family, float[] Rows, float[] Columns);
    internal SalesResponsiveLayout(Form form, Control header, StatusStrip footer, Control actions)
    {
        this.form = form; this.header = header; this.footer = footer; this.actions = actions;
        void Capture(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                var table = c as TableLayoutPanel;
                metrics.Add(new(c, c.Padding, c.Margin, (c is DataGridView g ? g.DefaultCellStyle.Font ?? c.Font : c.Font).SizeInPoints * 96f / 72f,
                    c.Font.Style, c.Font.FontFamily.Name,
                    table?.RowStyles.Cast<RowStyle>().Select(r => r.SizeType == SizeType.Absolute ? r.Height : -1).ToArray() ?? Array.Empty<float>(),
                    table?.ColumnStyles.Cast<ColumnStyle>().Select(r => r.SizeType == SizeType.Absolute ? r.Width : -1).ToArray() ?? Array.Empty<float>()));
                Capture(c);
            }
        }
        Capture(form);
        form.MinimumSize = Size.Empty;
        form.SizeChanged += Changed;
        form.DpiChanged += DpiChanged;
        form.Shown += Changed;
        form.FormClosed += Closed;
        form.Tag = this;
        Apply();
    }
    private void Changed(object? sender, EventArgs e) => Apply();
    private void DpiChanged(object? sender, DpiChangedEventArgs e)
    {
        Apply();
    }
    private void Closed(object? sender, FormClosedEventArgs e) => Dispose();
    internal void Apply(int? dpiOverride = null)
    {
        if (applying || form.IsDisposed || form.ClientSize.Width <= 0 || form.ClientSize.Height <= 0) return;
        applying = true;
        form.SuspendLayout();
        try
        {
            float dpi = (dpiOverride ?? form.DeviceDpi) / 96f;
            float fit = Math.Min(form.ClientSize.Width / 1350f, form.ClientSize.Height / 690f);
            float scale = Math.Max(.35f, fit);
            int Px(float value) => value == 0 ? 0 : Math.Max(1, (int)Math.Round(value * scale));
            Padding Pad(Padding p) => new(Px(p.Left), Px(p.Top), Px(p.Right), Px(p.Bottom));
            foreach (var m in metrics)
            {
                var c = m.Control;
                c.SuspendLayout();
                c.Padding = Pad(m.Padding); c.Margin = Pad(m.Margin);
                if (c is TableLayoutPanel table)
                {
                    for (int i = 0; i < m.Rows.Length; i++) if (m.Rows[i] >= 0) table.RowStyles[i].Height = Px(m.Rows[i]);
                    for (int i = 0; i < m.Columns.Length; i++) if (m.Columns[i] >= 0) table.ColumnStyles[i].Width = Px(m.Columns[i]);
                }
                // Prefer DPI sizing when room permits, cap at the available area.
                float pixels = Math.Max(12f, Math.Max(Math.Min(12f * dpi, 16f * scale), m.FontPixels * scale));
                if (!fonts.TryGetValue(c, out var old) || Math.Abs(old.Size - pixels) > .05f)
                {
                    var font = new Font(m.Family, pixels, m.Style, GraphicsUnit.Pixel);
                    c.Font = font; fonts[c] = font; old?.Dispose();
                }
                if (c is SalesVisualButton button) button.LayoutScale = scale;
                if (c is DataGridView grid)
                {
                    grid.DefaultCellStyle.Font = c.Font;
                    grid.ColumnHeadersDefaultCellStyle.Font = c.Font;
                    grid.ColumnHeadersHeight = Math.Max(28, Px(38));
                    grid.RowTemplate.Height = Math.Max(28, Px(32));
                    foreach (DataGridViewRow row in grid.Rows) row.Height = grid.RowTemplate.Height;
                    foreach (DataGridViewColumn column in grid.Columns) column.MinimumWidth = Math.Max(35, Px(45));
                }
                c.ResumeLayout(false);
            }
            header.Height = Math.Max(84, Px(108));
            footer.AutoSize = false; footer.Height = Math.Max(24, Px(30));
            actions.Width = Math.Max(104, (int)(form.ClientSize.Width * .12f));
        }
        finally
        {
            form.ResumeLayout(true);
            foreach (var metric in metrics) metric.Control.PerformLayout();
            form.PerformLayout();
            applying = false;
        }
        form.Invalidate(true);
    }
    public void Dispose()
    {
        form.SizeChanged -= Changed; form.DpiChanged -= DpiChanged;
        form.Shown -= Changed; form.FormClosed -= Closed;
        foreach (var font in fonts.Values) font.Dispose();
        fonts.Clear();
    }
}
