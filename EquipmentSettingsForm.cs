using System.Drawing.Printing;
using System.Globalization;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace LealInfoPDV;

internal static class EquipmentSettings
{
    public static string Get(string key, string fallback = "")
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key=$key";
            cmd.Parameters.AddWithValue("$key", key);
            return Convert.ToString(cmd.ExecuteScalar()) ?? fallback;
        }
        catch { return fallback; }
    }

    public static void Set(string key, object? value)
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
        cmd.ExecuteNonQuery();
    }
}

public static class ThermalPrinterService
{
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(EquipmentSettings.Get("printer_name"));
    public static bool AutoPrintEnabled => EquipmentSettings.Get("printer_auto_print") == "1";

    public static bool TryPrintConfigured(string text, IWin32Window? owner = null, bool showErrors = true)
    {
        var printer = EquipmentSettings.Get("printer_name");
        if (string.IsNullOrWhiteSpace(printer) || !PrinterSettings.InstalledPrinters.Cast<string>().Contains(printer))
        {
            if (showErrors) MessageBox.Show(owner, "Selecione uma impressora instalada na Central de Equipamentos.", "Impressora", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            using var document = CreateDocument(printer, text);
            document.Print();
            return true;
        }
        catch (Exception ex)
        {
            if (showErrors) MessageBox.Show(owner, "Não foi possível imprimir.\n\n" + ex.Message, "Impressora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    public static bool PrintReceipt(string text, IWin32Window? owner = null)
    {
        var installed = PrinterSettings.InstalledPrinters.Cast<string>()
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (installed.Length == 0)
        {
            MessageBox.Show(owner, "Nenhuma impressora foi encontrada no Windows.", "Impressora", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var selected = installed.Length == 1 ? installed[0] : ChoosePrinter(installed, owner);
        if (string.IsNullOrWhiteSpace(selected)) return false;

        try
        {
            using var document = CreateDocument(selected, text);
            document.Print();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "Não foi possível imprimir.\n\n" + ex.Message, "Impressora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    public static void PrintAutomaticallyIfEnabled(string text, IWin32Window? owner = null)
    {
        if (AutoPrintEnabled) PrintReceipt(text, owner);
    }

    private static string? ChoosePrinter(string[] installed, IWin32Window? owner)
    {
        using var form = new Form
        {
            Text = "Escolher impressora",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(520, 210),
            BackColor = Color.FromArgb(224, 239, 248),
            Font = new Font("Segoe UI", 9.5f)
        };
        var title = new Label { Text = "ONDE DESEJA IMPRIMIR?", Dock = DockStyle.Top, Height = 54, BackColor = Color.FromArgb(4, 70, 112), ForeColor = Color.White, Font = new Font("Segoe UI", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        var combo = new ComboBox { Left = 28, Top = 78, Width = 464, Height = 32, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        combo.Items.AddRange(installed);
        var preferred = EquipmentSettings.Get("printer_name");
        combo.SelectedItem = installed.FirstOrDefault(x => string.Equals(x, preferred, StringComparison.OrdinalIgnoreCase)) ?? installed[0];
        var print = new Button { Text = "IMPRIMIR", Left = 272, Top = 132, Width = 140, Height = 42, BackColor = Color.FromArgb(0, 145, 85), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "CANCELAR", Left = 112, Top = 132, Width = 140, Height = 42, BackColor = Color.FromArgb(90, 105, 115), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), DialogResult = DialogResult.Cancel };
        print.FlatAppearance.BorderSize = 0; cancel.FlatAppearance.BorderSize = 0;
        form.Controls.Add(combo); form.Controls.Add(print); form.Controls.Add(cancel); form.Controls.Add(title);
        form.AcceptButton = print; form.CancelButton = cancel;
        return form.ShowDialog(owner) == DialogResult.OK ? combo.SelectedItem?.ToString() : null;
    }

    private static PrintDocument CreateDocument(string printer, string text)
    {
        var widthMm = int.TryParse(EquipmentSettings.Get("printer_paper_width_mm", "80"), out var parsed) ? parsed : 80;
        var copies = short.TryParse(EquipmentSettings.Get("printer_copies", "1"), out var count) ? count : (short)1;
        var widthHundredths = Math.Max(100, (int)Math.Round(widthMm / 25.4 * 100));
        var doc = new PrintDocument { DocumentName = "LEAL INFO PDV - Cupom" };
        doc.PrinterSettings.PrinterName = printer;
        doc.PrinterSettings.Copies = (short)Math.Clamp(copies, (short)1, (short)9);
        doc.DefaultPageSettings.PaperSize = new PaperSize($"Bobina {widthMm} mm", widthHundredths, 3000);
        doc.DefaultPageSettings.Margins = new Margins(6, 6, 6, 6);
        doc.PrintPage += (_, e) =>
        {
            using var font = new Font("Consolas", widthMm <= 58 ? 7.2f : 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            var bounds = new RectangleF(e.MarginBounds.Left, e.MarginBounds.Top, e.MarginBounds.Width, e.MarginBounds.Height);
            e.Graphics.DrawString(text, font, Brushes.Black, bounds);
            e.HasMorePages = false;
        };
        return doc;
    }
}

public sealed class EquipmentSettingsForm : Form
{
    private static readonly Color Blue = Color.FromArgb(4, 70, 112);
    private static readonly Color LightBlue = Color.FromArgb(224, 239, 248);
    private readonly ComboBox printer = DropDown();
    private readonly ComboBox paper = DropDown();
    private readonly NumericUpDown customWidth = new() { Minimum = 40, Maximum = 120, Value = 80, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly NumericUpDown copies = new() { Minimum = 1, Maximum = 9, Value = 1, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly CheckBox autoPrint = new() { Text = "IMPRIMIR AUTOMATICAMENTE AO FINALIZAR A VENDA", AutoSize = true, ForeColor = Blue, Font = new Font("Segoe UI", 9, FontStyle.Bold), Anchor = AnchorStyles.Left };
    private readonly ComboBox scaleMode = DropDown();
    private readonly ComboBox scaleProfile = DropDown();
    private readonly ComboBox port = DropDown();
    private readonly ComboBox baud = DropDown();
    private readonly TextBox host = Field();
    private readonly NumericUpDown tcpPort = new() { Minimum = 1, Maximum = 65535, Value = 4001, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly TextBox command = Field();
    private readonly TextBox regex = Field();
    private readonly Label scaleStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(230, 145, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold), Text = "AGUARDANDO TESTE", Margin = new Padding(4, 6, 4, 6) };

    public static void Open(IWin32Window owner)
    {
        var current = Application.OpenForms.OfType<EquipmentSettingsForm>().FirstOrDefault();
        if (current != null) { current.BringToFront(); current.Focus(); return; }
        var form = new EquipmentSettingsForm();
        form.Show(owner);
    }

    public EquipmentSettingsForm()
    {
        Text = "LEAL INFO PDV - Balança e Impressora";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 540);
        Size = new Size(860, 620);
        MaximizeBox = false;
        BackColor = LightBlue;
        Font = new Font("Segoe UI", 9);

        Controls.Add(new Label { Text = "CENTRAL DE EQUIPAMENTOS", Dock = DockStyle.Top, Height = 54, BackColor = Blue, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16, FontStyle.Bold) });
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Padding = new Point(14, 6) };
        tabs.TabPages.Add(BuildPrinterTab());
        tabs.TabPages.Add(BuildScaleTab());
        tabs.TabPages.Add(BuildTutorialTab());
        Controls.Add(tabs);
        tabs.BringToFront();
        LoadSettings();
    }

    private TabPage BuildPrinterTab()
    {
        var tab = NewTab("IMPRESSORA TÉRMICA");
        var grid = Grid(8);
        tab.Controls.Add(grid);
        printer.Items.AddRange(PrinterSettings.InstalledPrinters.Cast<string>().ToArray());
        paper.Items.AddRange(new object[] { "58 mm", "76 mm", "80 mm", "PERSONALIZADA" });
        paper.SelectedIndexChanged += (_, _) => customWidth.Enabled = paper.Text == "PERSONALIZADA";
        AddRow(grid, 0, "Impressora instalada no Windows", printer);
        AddRow(grid, 1, "Modelo de bobina", paper);
        AddRow(grid, 2, "Largura personalizada (mm)", customWidth);
        AddRow(grid, 3, "Quantidade de vias", copies);
        grid.Controls.Add(autoPrint, 1, 4);
        var hint = new Label { Text = "Compatível com impressoras instaladas no Windows: USB, rede, Bluetooth ou porta virtual. Corte e gaveta dependem do driver do fabricante.", Dock = DockStyle.Fill, ForeColor = Blue, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleLeft };
        grid.Controls.Add(hint, 0, 5); grid.SetColumnSpan(hint, 2);
        var buttons = Buttons();
        buttons.Controls.Add(Action("SALVAR CONFIGURAÇÃO", SavePrinter));
        buttons.Controls.Add(Action("IMPRIMIR TESTE", TestPrinter, Color.FromArgb(0, 145, 85)));
        buttons.Controls.Add(Action("ATUALIZAR LISTA", RefreshPrinters, Color.FromArgb(230, 145, 20)));
        grid.Controls.Add(buttons, 0, 6); grid.SetColumnSpan(buttons, 2);
        return tab;
    }

    private TabPage BuildScaleTab()
    {
        var tab = NewTab("BALANÇA");
        var grid = Grid(10);
        tab.Controls.Add(grid);
        scaleMode.Items.AddRange(new object[] { "PORTA COM / RS-232", "REDE TCP/IP", "TECLADO / HID", "ETIQUETA COM CÓDIGO DE BARRAS" });
        scaleProfile.Items.AddRange(new object[] { "GENÉRICO", "TOLEDO", "FILIZOLA", "URANO", "BALMAK", "RAMUZA", "PERSONALIZADO" });
        baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        port.Items.AddRange(SerialPort.GetPortNames().OrderBy(x => x).Cast<object>().ToArray());
        scaleMode.SelectedIndexChanged += (_, _) => UpdateScaleFields();
        scaleProfile.SelectedIndexChanged += (_, _) => ApplyProfile();
        AddRow(grid, 0, "Tipo de conexão", scaleMode);
        AddRow(grid, 1, "Perfil do equipamento", scaleProfile);
        AddRow(grid, 2, "Porta COM", port);
        AddRow(grid, 3, "Velocidade (baud rate)", baud);
        AddRow(grid, 4, "Endereço IP / nome de rede", host);
        AddRow(grid, 5, "Porta TCP", tcpPort);
        AddRow(grid, 6, "Comando de leitura (opcional)", command);
        AddRow(grid, 7, "Expressão para localizar o peso", regex);
        var buttons = Buttons();
        buttons.Controls.Add(Action("SALVAR CONFIGURAÇÃO", SaveScale));
        buttons.Controls.Add(Action("TESTAR LEITURA", async () => await TestScaleAsync(), Color.FromArgb(0, 145, 85)));
        buttons.Controls.Add(Action("ATUALIZAR PORTAS", RefreshPorts, Color.FromArgb(230, 145, 20)));
        grid.Controls.Add(buttons, 0, 8); grid.SetColumnSpan(buttons, 2);
        grid.Controls.Add(scaleStatus, 0, 9); grid.SetColumnSpan(scaleStatus, 2);
        return tab;
    }

    private TabPage BuildTutorialTab()
    {
        var tab = NewTab("COMO INSTALAR");
        var text = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White,
            ForeColor = Color.FromArgb(20, 45, 65), Font = new Font("Segoe UI", 9.5f), Margin = new Padding(12),
            Text = """
            IMPRESSORA TÉRMICA NÃO FISCAL

            1. Conecte a impressora por USB, rede ou Bluetooth e instale o driver oficial do fabricante.
            2. No Windows, abra Configurações > Bluetooth e dispositivos > Impressoras e scanners e imprima uma página de teste.
            3. Volte à aba IMPRESSORA TÉRMICA, clique em ATUALIZAR LISTA e selecione a impressora.
            4. Escolha a bobina: 58 mm (compacta), 76 mm (alguns modelos antigos) ou 80 mm (mais comum e legível). Use PERSONALIZADA somente se o manual indicar outra largura.
            5. Clique em IMPRIMIR TESTE. Se o conteúdo cortar, confirme a largura também nas Preferências de Impressão do Windows.

            BALANÇA

            1. Instale o driver do fabricante. Em balanças USB/serial, confirme a porta em Gerenciador de Dispositivos > Portas (COM e LPT).
            2. Consulte no manual o protocolo, baud rate e formato enviado. Selecione um perfil de marca como ponto de partida e ajuste conforme o manual do modelo.
            3. Para RS-232/USB virtual, escolha PORTA COM; para conversores Ethernet, escolha REDE TCP/IP.
            4. Em TECLADO/HID a balança digita o peso como um teclado. Em ETIQUETA, o leitor envia o código impresso pela balança.
            5. Com a plataforma vazia e depois com um produto, clique em TESTAR LEITURA. Só salve após aparecer um peso coerente e estável.

            IMPORTANTE

            Não existe um único protocolo usado por todos os fabricantes. O LEAL INFO trabalha com perfis configuráveis para atender modelos diferentes. Para um modelo não reconhecido, informe marca, modelo e uma foto da etiqueta/ página de protocolo do manual para criarmos o perfil correto sem alterar o restante do PDV.
            """
        };
        tab.Controls.Add(text);
        return tab;
    }

    private void LoadSettings()
    {
        Select(printer, EquipmentSettings.Get("printer_name"));
        var width = EquipmentSettings.Get("printer_paper_width_mm", "80");
        paper.SelectedItem = width is "58" or "76" or "80" ? width + " mm" : "PERSONALIZADA";
        if (decimal.TryParse(width, out var w)) customWidth.Value = Math.Clamp(w, customWidth.Minimum, customWidth.Maximum);
        if (decimal.TryParse(EquipmentSettings.Get("printer_copies", "1"), out var c)) copies.Value = Math.Clamp(c, copies.Minimum, copies.Maximum);
        autoPrint.Checked = EquipmentSettings.Get("printer_auto_print") == "1";
        Select(scaleMode, EquipmentSettings.Get("scale_mode", "PORTA COM / RS-232"));
        Select(scaleProfile, EquipmentSettings.Get("scale_profile", "GENÉRICO"));
        Select(port, EquipmentSettings.Get("scale_com_port"));
        Select(baud, EquipmentSettings.Get("scale_baud", "9600"));
        host.Text = EquipmentSettings.Get("scale_host");
        if (decimal.TryParse(EquipmentSettings.Get("scale_tcp_port", "4001"), out var p)) tcpPort.Value = Math.Clamp(p, tcpPort.Minimum, tcpPort.Maximum);
        command.Text = EquipmentSettings.Get("scale_command");
        regex.Text = EquipmentSettings.Get("scale_regex", @"[-+]?\d+[\.,]?\d*");
        UpdateScaleFields();
    }

    private void SavePrinter()
    {
        if (string.IsNullOrWhiteSpace(printer.Text)) { Warn("Selecione uma impressora instalada no Windows."); return; }
        var width = paper.Text == "PERSONALIZADA" ? (int)customWidth.Value : int.Parse(paper.Text.Split(' ')[0]);
        EquipmentSettings.Set("printer_name", printer.Text);
        EquipmentSettings.Set("printer_paper_width_mm", width);
        EquipmentSettings.Set("printer_copies", (int)copies.Value);
        EquipmentSettings.Set("printer_auto_print", autoPrint.Checked ? "1" : "0");
        MessageBox.Show(this, "Impressora e bobina configuradas com sucesso.", "Central de Equipamentos", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void TestPrinter()
    {
        SavePrinter();
        var sample = $"LEAL INFO PDV\nTESTE DE IMPRESSÃO\n{DateTime.Now:dd/MM/yyyy HH:mm:ss}\n--------------------------------\nBobina: {EquipmentSettings.Get("printer_paper_width_mm")} mm\nImpressora: {printer.Text}\n--------------------------------\nIMPRESSÃO CONFIGURADA\n\n\n";
        if (ThermalPrinterService.TryPrintConfigured(sample, this)) MessageBox.Show(this, "Teste enviado para a impressora.", "Impressora", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveScale()
    {
        if (scaleMode.Text == "PORTA COM / RS-232" && string.IsNullOrWhiteSpace(port.Text)) { Warn("Selecione a porta COM da balança."); return; }
        if (scaleMode.Text == "REDE TCP/IP" && string.IsNullOrWhiteSpace(host.Text)) { Warn("Informe o endereço de rede da balança ou conversor."); return; }
        EquipmentSettings.Set("scale_mode", scaleMode.Text); EquipmentSettings.Set("scale_profile", scaleProfile.Text);
        EquipmentSettings.Set("scale_com_port", port.Text); EquipmentSettings.Set("scale_baud", baud.Text);
        EquipmentSettings.Set("scale_host", host.Text.Trim()); EquipmentSettings.Set("scale_tcp_port", (int)tcpPort.Value);
        EquipmentSettings.Set("scale_command", command.Text); EquipmentSettings.Set("scale_regex", regex.Text);
        EquipmentSettings.Set("scale_enabled", "1");
        MessageBox.Show(this, "Configuração da balança salva.", "Central de Equipamentos", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task TestScaleAsync()
    {
        try
        {
            scaleStatus.Text = "LENDO BALANÇA..."; scaleStatus.BackColor = Color.FromArgb(230, 145, 20);
            string raw;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (scaleMode.Text == "PORTA COM / RS-232") raw = await ReadSerialAsync(timeout.Token);
            else if (scaleMode.Text == "REDE TCP/IP") raw = await ReadNetworkAsync(timeout.Token);
            else { scaleStatus.Text = scaleMode.Text.StartsWith("TECLADO") ? "MODO TECLADO: TESTE NO CAMPO DE VENDA" : "MODO ETIQUETA: TESTE COM O LEITOR"; scaleStatus.BackColor = Color.FromArgb(0, 145, 85); return; }
            var match = Regex.Match(raw, string.IsNullOrWhiteSpace(regex.Text) ? @"[-+]?\d+[\.,]?\d*" : regex.Text);
            if (!match.Success) throw new InvalidOperationException("A balança respondeu, mas o peso não foi localizado. Resposta: " + DisplayRaw(raw));
            scaleStatus.Text = "COMUNICAÇÃO OK • PESO: " + match.Value; scaleStatus.BackColor = Color.FromArgb(0, 145, 85);
        }
        catch (Exception ex)
        {
            scaleStatus.Text = "FALHA NA COMUNICAÇÃO"; scaleStatus.BackColor = Color.FromArgb(180, 55, 55);
            MessageBox.Show(this, ex.Message, "Teste da balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task<string> ReadSerialAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(port.Text)) throw new InvalidOperationException("Selecione a porta COM.");
        using var serial = new SerialPort(port.Text, int.TryParse(baud.Text, out var rate) ? rate : 9600, Parity.None, 8, StopBits.One) { ReadTimeout = 4500, WriteTimeout = 1000, NewLine = "\r\n" };
        serial.Open();
        if (!string.IsNullOrEmpty(command.Text)) serial.Write(DecodeCommand(command.Text));
        var buffer = new byte[256];
        var count = await serial.BaseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
        return Encoding.ASCII.GetString(buffer, 0, count);
    }

    private async Task<string> ReadNetworkAsync(CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host.Text.Trim(), (int)tcpPort.Value, ct);
        using var stream = client.GetStream();
        if (!string.IsNullOrEmpty(command.Text)) await stream.WriteAsync(Encoding.ASCII.GetBytes(DecodeCommand(command.Text)), ct);
        var buffer = new byte[256];
        var count = await stream.ReadAsync(buffer, ct);
        return Encoding.ASCII.GetString(buffer, 0, count);
    }

    private void ApplyProfile()
    {
        if (!IsHandleCreated) return;
        baud.SelectedItem = scaleProfile.Text switch { "TOLEDO" => "9600", "FILIZOLA" => "9600", "URANO" => "9600", "BALMAK" => "9600", "RAMUZA" => "9600", _ => baud.SelectedItem ?? "9600" };
        regex.Text = @"[-+]?\d+[\.,]?\d*";
    }

    private void UpdateScaleFields()
    {
        var serial = scaleMode.Text == "PORTA COM / RS-232";
        var network = scaleMode.Text == "REDE TCP/IP";
        port.Enabled = baud.Enabled = serial;
        host.Enabled = tcpPort.Enabled = network;
        command.Enabled = regex.Enabled = serial || network;
    }

    private void RefreshPrinters() { var selected = printer.Text; printer.Items.Clear(); printer.Items.AddRange(PrinterSettings.InstalledPrinters.Cast<string>().ToArray()); Select(printer, selected); }
    private void RefreshPorts() { var selected = port.Text; port.Items.Clear(); port.Items.AddRange(SerialPort.GetPortNames().OrderBy(x => x).Cast<object>().ToArray()); Select(port, selected); }
    private static string DecodeCommand(string value) => value.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");
    private static string DisplayRaw(string value) => value.Replace("\r", "<CR>").Replace("\n", "<LF>").Trim();
    private void Warn(string text) => MessageBox.Show(this, text, "Central de Equipamentos", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static ComboBox DropDown() => new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private static TextBox Field() => new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private static TabPage NewTab(string text) => new(text) { BackColor = Color.White, Padding = new Padding(14) };
    private static TableLayoutPanel Grid(int rows)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows, Padding = new Padding(8), BackColor = Color.White };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        return grid;
    }
    private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Blue, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold) }, 0, row);
        control.Margin = new Padding(4, 5, 4, 5); grid.Controls.Add(control, 1, row);
    }
    private static FlowLayoutPanel Buttons() => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
    private static Button Action(string text, Action action, Color? color = null)
    {
        var button = new Button { Text = text, Width = 178, Height = 38, BackColor = color ?? Color.FromArgb(0, 163, 224), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(3, 5, 3, 3) };
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => action(); return button;
    }
    private static void Select(ComboBox combo, string value)
    {
        var item = combo.Items.Cast<object>().FirstOrDefault(x => string.Equals(Convert.ToString(x), value, StringComparison.OrdinalIgnoreCase));
        if (item != null) combo.SelectedItem = item; else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }
}
