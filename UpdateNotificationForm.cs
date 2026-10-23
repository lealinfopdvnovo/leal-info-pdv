using System.Drawing;
using System.Windows.Forms;

namespace LealInfoPDV;

internal sealed class UpdateNotificationForm : Form
{
    private readonly Form main;
    private bool applying;
    internal bool IsApplying => applying;
    private readonly Button now;
    private readonly Button later;
    protected override bool ShowWithoutActivation => true;

    internal UpdateNotificationForm(Form owner, string version, Func<Task> apply)
    {
        main = owner;
        Text = "Nova atualização";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(360, 160);
        BackColor = Color.FromArgb(20, 30, 46);
        ForeColor = Color.White;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.Controls.Add(new Label { Text = "Nova atualização", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13, FontStyle.Bold) }, 0, 0);
        layout.Controls.Add(new Label { Text = $"Versão {version} disponível para seu PDV.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) }, 0, 1);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        now = new Button { Text = "Atualizar agora", Dock = DockStyle.Fill, BackColor = Color.FromArgb(218, 174, 72), ForeColor = Color.Black, UseVisualStyleBackColor = false };
        later = new Button { Text = "Depois", Dock = DockStyle.Fill };
        actions.Controls.Add(now, 0, 0); actions.Controls.Add(later, 1, 0);
        layout.Controls.Add(actions, 0, 2); Controls.Add(layout);
        later.Click += (_, _) => Close();
        now.Click += async (_, _) =>
        {
            if (applying) return;
            applying = true; now.Enabled = later.Enabled = false; now.Text = "Preparando…";
            try { await apply(); if (!IsDisposed) Close(); }
            catch (Exception ex)
            {
                if (!IsDisposed) MessageBox.Show(this, "Não foi possível atualizar agora.\n\n" + ex.Message, "Atualização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { if (!IsDisposed) { applying = false; now.Enabled = later.Enabled = true; now.Text = "Atualizar agora"; } }
        };
        main.LocationChanged += Reposition;
        main.SizeChanged += Reposition;
        Shown += Reposition;
        FormClosed += (_, _) => { main.LocationChanged -= Reposition; main.SizeChanged -= Reposition; };
    }

    private void Reposition(object? sender, EventArgs e)
    {
        if (main.IsDisposed || IsDisposed) return;
        var area = Rectangle.Intersect(main.RectangleToScreen(main.ClientRectangle), Screen.FromControl(main).WorkingArea);
        if (area.IsEmpty) area = Screen.FromControl(main).WorkingArea;
        int gap = (int)(16 * DeviceDpi / 96f);
        Location = new Point(Math.Max(area.Left, area.Right - Width - gap), Math.Max(area.Top, area.Bottom - Height - gap));
    }
}
