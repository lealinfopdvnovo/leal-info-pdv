using System.Drawing;
using System.Windows.Forms;

namespace LealInfoPDV;

internal sealed class LicenseActivationForm : Form
{
    private readonly TextBox serial = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10F)
    };

    private readonly Label status = new()
    {
        AutoSize = true,
        ForeColor = Color.Gainsboro
    };

    internal LicenseActivationForm()
    {
        Text = "LEAL INFO • Ativação";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 510);
        MinimumSize = new Size(680, 470);
        BackColor = Color.FromArgb(10, 18, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "ATIVAÇÃO DO LEAL INFO PDV",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F)
        };

        var subtitle = new Label
        {
            Text = "Cole abaixo o serial fornecido pela LEAL INFO.",
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var device = new Label
        {
            Text = $"ID deste computador: {LicenseManager.DeviceId()}",
            AutoSize = true,
            ForeColor = Color.LightSkyBlue
        };

        var activate = new Button
        {
            Text = "ATIVAR LICENÇA",
            AutoSize = true,
            Height = 42,
            Padding = new Padding(18, 5, 18, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 94, 180),
            ForeColor = Color.White
        };
        activate.FlatAppearance.BorderSize = 0;
        activate.Click += (_, _) => ActivateLicense();

        var close = new Button
        {
            Text = "SAIR",
            AutoSize = true,
            Height = 42,
            Padding = new Padding(18, 5, 18, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 55, 72),
            ForeColor = Color.White
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        buttons.Controls.Add(activate);
        buttons.Controls.Add(close);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 7
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(device, 0, 2);
        layout.Controls.Add(serial, 0, 3);
        layout.Controls.Add(status, 0, 4);
        layout.Controls.Add(buttons, 0, 5);

        Controls.Add(layout);
    }

    private void ActivateLicense()
    {
        if (LicenseManager.Activate(serial.Text, out var license))
        {
            MessageBox.Show(
                $"Licença ativada com sucesso.\n\nPlano: {license.Edition.ToString().ToUpperInvariant()}\nCliente: {license.Customer}\nVencimento: {license.ExpiresAt:dd/MM/yyyy}",
                "LEAL INFO",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        status.Text = license.Error ?? "Não foi possível ativar a licença.";
        status.ForeColor = Color.LightCoral;
    }
}
