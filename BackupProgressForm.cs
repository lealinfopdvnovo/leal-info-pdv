using System.Drawing;
using System.Windows.Forms;
namespace LealInfoPDV;

// Only included in the Loja/Casa candidate builds.
internal sealed class BackupProgressForm : Form
{
    private readonly Label status = new() { Dock = DockStyle.Top, Height = 32, ForeColor = Color.White, Text = "PREPARANDO BACKUP...", TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProgressBar bar = new() { Dock = DockStyle.Top, Height = 30, Minimum = 0, Maximum = 100 };
    private readonly Label percent = new() { Dock = DockStyle.Top, Height = 25, ForeColor = Color.White, Text = "0% — etapas do backup", TextAlign = ContentAlignment.MiddleCenter };
    public BackupProgressForm()
    {
        Text = "Backup"; ClientSize = new Size(424, 220); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.FromArgb(20, 112, 157); Font = new Font("Segoe UI", 10, FontStyle.Bold);
        var title = new Label { Text = "AGUARDE...", Dock = DockStyle.Top, Height = 58, BackColor = Color.Red, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8) };
        var close = new Button { Text = "FECHAR", BackColor = Color.FromArgb(240, 108, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Bottom, Height = 42 };
        close.Click += (_, _) => Hide(); // Closing the display never cancels the backup.
        body.Controls.Add(close); body.Controls.Add(percent); body.Controls.Add(bar); body.Controls.Add(status);
        Controls.Add(body); Controls.Add(title);
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }
    public void Report(int value)
    {
        if (IsDisposed) return;
        bar.Value = Math.Clamp(value, 0, 100); percent.Text = $"{bar.Value}% — etapas do backup";
        status.Text = value switch { >= 100 => "BACKUP CONCLUÍDO", >= 70 => "ENVIANDO POR E-MAIL...", >= 40 => "COMPACTANDO BACKUP...", >= 10 => "COPIANDO BANCO DE DADOS...", _ => "AGUARDANDO BACKUP..." };
    }
}
