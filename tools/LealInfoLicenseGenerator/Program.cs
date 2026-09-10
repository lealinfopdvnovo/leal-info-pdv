using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace LealInfoLicenseGenerator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (!GeneratorAccess.TryUnlock()) return;
        Application.Run(new GeneratorForm());
    }
}

internal static class GeneratorAccess
{
    private const int Iterations = 210000;
    private static readonly string AccessFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LEAL INFO", "LicenseGenerator", "access.dat");

    internal static bool TryUnlock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AccessFile)!);

        if (!File.Exists(AccessFile))
            return CreatePassword();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var form = new AccessForm(false);
            if (form.ShowDialog() != DialogResult.OK) return false;
            if (Verify(form.Password)) return true;

            MessageBox.Show(
                attempt < 5 ? "Senha incorreta." : "Acesso bloqueado nesta execução.",
                "LEAL INFO • ACESSO RESTRITO",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return false;
    }

    private static bool CreatePassword()
    {
        while (true)
        {
            using var form = new AccessForm(true);
            if (form.ShowDialog() != DialogResult.OK) return false;

            if (form.Password.Length < 6)
            {
                MessageBox.Show("Use uma senha com pelo menos 6 caracteres.", "LEAL INFO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                continue;
            }

            if (!string.Equals(form.Password, form.Confirmation, StringComparison.Ordinal))
            {
                MessageBox.Show("As senhas não conferem.", "LEAL INFO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                continue;
            }

            var salt = RandomNumberGenerator.GetBytes(32);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(form.Password), salt, Iterations, HashAlgorithmName.SHA256, 32);
            File.WriteAllText(AccessFile, $"v1|{Iterations}|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}");
            MessageBox.Show("Senha criada. Guarde-a em local seguro.", "LEAL INFO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
    }

    private static bool Verify(string password)
    {
        try
        {
            var parts = File.ReadAllText(AccessFile).Split('|');
            if (parts.Length != 4 || parts[0] != "v1") return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private sealed class AccessForm : Form
    {
        private readonly TextBox password = new() { UseSystemPasswordChar = true };
        private readonly TextBox confirmation = new() { UseSystemPasswordChar = true };
        internal string Password => password.Text;
        internal string Confirmation => confirmation.Text;

        internal AccessForm(bool firstAccess)
        {
            Text = firstAccess ? "LEAL INFO • Criar senha do Gerador" : "LEAL INFO • Acesso Restrito";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, firstAccess ? 245 : 190);
            BackColor = Color.FromArgb(10, 18, 34);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            Controls.Add(new Label
            {
                Text = firstAccess ? "PRIMEIRO ACESSO • DEFINA A SENHA MESTRE" : "ACESSO RESTRITO • GERADOR DE SERIAL",
                Left = 24, Top = 22, Width = 380, Height = 28,
                Font = new Font("Segoe UI Semibold", 12F), ForeColor = Color.FromArgb(124, 238, 255)
            });

            Controls.Add(new Label { Text = "Senha", Left = 24, Top = 68, Width = 100 });
            password.SetBounds(24, 91, 380, 30);
            Controls.Add(password);

            var buttonTop = 132;
            if (firstAccess)
            {
                Controls.Add(new Label { Text = "Confirmar senha", Left = 24, Top = 128, Width = 150 });
                confirmation.SetBounds(24, 151, 380, 30);
                Controls.Add(confirmation);
                buttonTop = 193;
            }

            var enter = new Button
            {
                Text = firstAccess ? "CRIAR SENHA" : "ENTRAR",
                Left = 24, Top = buttonTop, Width = 150, Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 94, 180), ForeColor = Color.White
            };
            enter.FlatAppearance.BorderSize = 0;
            enter.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(enter);
            AcceptButton = enter;

            var cancel = new Button
            {
                Text = "SAIR", Left = 184, Top = buttonTop, Width = 95, Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(48, 60, 80), ForeColor = Color.White
            };
            cancel.FlatAppearance.BorderSize = 0;
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            CancelButton = cancel;

            Shown += (_, _) => password.Focus();
        }
    }
}

internal sealed class GeneratorForm : Form
{
    private readonly TextBox customer = new() { PlaceholderText = "Nome do cliente / empresa" };
    private readonly ComboBox edition = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown months = new() { Minimum = 1, Maximum = 60, Value = 12 };
    private readonly TextBox device = new() { PlaceholderText = "ID do computador ou ANY" };
    private readonly TextBox serial = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label expires = new() { AutoSize = true };

    internal GeneratorForm()
    {
        Text = "LEAL INFO • Gerador de Serial DEFINITIVO";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 590);
        Size = new Size(860, 650);
        BackColor = Color.FromArgb(10, 18, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        edition.Items.AddRange(new object[] { "STANDARD", "PLUS", "PRO" });
        edition.SelectedIndex = 2;
        device.Text = "ANY";
        months.ValueChanged += (_, _) => UpdateExpiry();
        UpdateExpiry();

        var title = new Label
        {
            Text = "LEAL INFO • GERADOR DE SERIAL DEFINITIVO",
            Font = new Font("Segoe UI Semibold", 20F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };

        var subtitle = new Label
        {
            Text = "Gerador mestre fixo • STANDARD, PLUS e PRO",
            ForeColor = Color.Gainsboro,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 24)
        };

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 5, Padding = new Padding(0), Margin = new Padding(0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(grid, 0, "Cliente / Empresa", customer);
        AddRow(grid, 1, "Plano", edition);
        AddRow(grid, 2, "Validade (meses)", months);

        var devicePanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
        devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        device.Dock = DockStyle.Fill;
        device.Margin = new Padding(0, 5, 8, 5);
        var pasteDevice = Button("COLAR ID", (_, _) => PasteDeviceId());
        pasteDevice.Margin = new Padding(0, 5, 0, 5);
        devicePanel.Controls.Add(device, 0, 0);
        devicePanel.Controls.Add(pasteDevice, 1, 0);
        AddRow(grid, 3, "ID do computador", devicePanel);
        AddRow(grid, 4, "Vencimento", expires);

        var generate = Button("GERAR SERIAL", (_, _) => Generate());
        var copy = Button("COPIAR SERIAL", (_, _) => CopySerial());
        var publicKey = Button("EXPORTAR CHAVE PÚBLICA", (_, _) => ExportPublicKey());
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 22, 0, 12) };
        buttons.Controls.AddRange(new Control[] { generate, copy, publicKey });

        serial.Dock = DockStyle.Fill;
        serial.Font = new Font("Consolas", 10F);
        serial.BackColor = Color.FromArgb(18, 28, 48);
        serial.ForeColor = Color.White;
        serial.BorderStyle = BorderStyle.FixedSingle;

        var serialLabel = new Label { Text = "Serial gerado", AutoSize = true, Font = new Font("Segoe UI Semibold", 10F), Margin = new Padding(0, 12, 0, 8) };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 6 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(subtitle, 0, 1);
        panel.Controls.Add(grid, 0, 2);
        panel.Controls.Add(buttons, 0, 3);
        panel.Controls.Add(serialLabel, 0, 4);
        panel.Controls.Add(serial, 0, 5);
        Controls.Add(panel);
    }

    private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 9, 12, 9) };
        control.Dock = DockStyle.Top;
        if (control.Margin == Padding.Empty) control.Margin = new Padding(0, 5, 0, 5);
        grid.Controls.Add(l, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 38, Padding = new Padding(16, 4, 16, 4), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(20, 94, 180), ForeColor = Color.White, Margin = new Padding(0, 0, 10, 0) };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private void UpdateExpiry() => expires.Text = DateTime.Now.Date.AddMonths((int)months.Value).ToString("dd/MM/yyyy");

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(customer.Text))
        {
            MessageBox.Show("Informe o cliente ou empresa.", "LEAL INFO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            customer.Focus();
            return;
        }

        var expiresAt = DateTime.UtcNow.Date.AddMonths((int)months.Value).AddDays(1).AddTicks(-1);
        serial.Text = LicenseSigner.CreateLicense(edition.SelectedItem?.ToString() ?? "PRO", customer.Text, device.Text, expiresAt);
    }

    private void PasteDeviceId()
    {
        if (!Clipboard.ContainsText()) return;
        var id = Clipboard.GetText().Trim();
        if (string.IsNullOrWhiteSpace(id)) return;
        device.Text = id.ToUpperInvariant();
        device.SelectionStart = device.Text.Length;
    }

    private void CopySerial()
    {
        if (string.IsNullOrWhiteSpace(serial.Text)) return;
        Clipboard.SetText(serial.Text);
        MessageBox.Show("Serial copiado.", "LEAL INFO", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportPublicKey()
    {
        using var save = new SaveFileDialog { Title = "Salvar chave pública do PDV", FileName = "leal-info-license-public-key-master.pem", Filter = "Chave pública (*.pem)|*.pem|Todos os arquivos (*.*)|*.*" };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(save.FileName, LicenseSigner.ExportPublicKeyPem());
        MessageBox.Show("Chave pública mestre exportada.", "LEAL INFO", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
