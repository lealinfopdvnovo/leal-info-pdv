using System.Globalization;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace LealInfoPDV;

internal sealed record ScaleLoadItem(int Plu, string Code, string Description, decimal Price, string Unit, int ValidityDays);

internal static class ScaleLoadService
{
    public static List<ScaleLoadItem> LoadProducts()
    {
        using var cn = Database.Open();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var schema = cn.CreateCommand())
        {
            schema.CommandText = "PRAGMA table_info(products)";
            using var reader = schema.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }

        var unit = columns.Contains("unit") ? "COALESCE(unit,'UNID')" : "'UNID'";
        var expiry = columns.Contains("expiry_date") ? "COALESCE(expiry_date,'')" : "''";
        var eligible = columns.Contains("use_scale")
            ? $"(COALESCE(use_scale,0)=1 OR UPPER({unit})='KG')"
            : $"UPPER({unit})='KG'";

        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT id,COALESCE(barcode,''),name,price,{unit},{expiry} FROM products WHERE active=1 AND id>0 AND {eligible} ORDER BY name";
        using var r = cmd.ExecuteReader();
        var result = new List<ScaleLoadItem>();
        while (r.Read())
        {
            var days = 0;
            if (DateTime.TryParse(r.GetString(5), out var date)) days = Math.Max(0, (date.Date - DateTime.Today).Days);
            result.Add(new ScaleLoadItem(r.GetInt32(0), r.GetString(1), r.GetString(2), Convert.ToDecimal(r.GetDouble(3)), r.GetString(4), days));
        }
        return result;
    }

    public static byte[] Build(IEnumerable<ScaleLoadItem> products, string format)
    {
        var list = products.ToList();
        if (format.Contains("CSV", StringComparison.OrdinalIgnoreCase))
        {
            var lines = new List<string> { "PLU;CODIGO;DESCRICAO;PRECO;UNIDADE;VALIDADE_DIAS" };
            lines.AddRange(list.Select(p => string.Join(';', p.Plu, Csv(p.Code), Csv(Normalize(p.Description, 40)), p.Price.ToString("0.00", CultureInfo.InvariantCulture), Csv(p.Unit), p.ValidityDays)));
            return new UTF8Encoding(true).GetBytes(string.Join("\r\n", lines) + "\r\n");
        }

        var text = new StringBuilder();
        foreach (var p in list)
            text.Append(p.Plu.ToString("D6")).Append('|')
                .Append(Normalize(p.Code, 14).PadRight(14)).Append('|')
                .Append(Normalize(p.Description, 40).PadRight(40)).Append('|')
                .Append(((int)Math.Round(p.Price * 100)).ToString("D8")).Append('|')
                .Append(Normalize(p.Unit, 4).PadRight(4)).Append('|')
                .Append(p.ValidityDays.ToString("D3")).Append("\r\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    public static async Task SendAsync(byte[] payload, CancellationToken ct)
    {
        var mode = EquipmentSettings.Get("scale_mode", "PORTA COM / RS-232");
        if (mode == "REDE TCP/IP")
        {
            var host = EquipmentSettings.Get("scale_host");
            if (string.IsNullOrWhiteSpace(host)) throw new InvalidOperationException("Configure o endereço da balança na aba BALANÇA.");
            var port = int.TryParse(EquipmentSettings.Get("scale_tcp_port", "4001"), out var parsed) ? parsed : 4001;
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            await client.GetStream().WriteAsync(payload, ct);
            await client.GetStream().FlushAsync(ct);
            return;
        }

        if (mode == "PORTA COM / RS-232")
        {
            var portName = EquipmentSettings.Get("scale_com_port");
            if (string.IsNullOrWhiteSpace(portName)) throw new InvalidOperationException("Configure a porta COM da balança na aba BALANÇA.");
            var baud = int.TryParse(EquipmentSettings.Get("scale_baud", "9600"), out var parsed) ? parsed : 9600;
            using var serial = new SerialPort(portName, baud, Parity.None, 8, StopBits.One) { WriteTimeout = 10000 };
            serial.Open();
            await serial.BaseStream.WriteAsync(payload, ct);
            await serial.BaseStream.FlushAsync(ct);
            return;
        }

        throw new InvalidOperationException("O envio direto exige conexão PORTA COM ou REDE TCP/IP. Para USB proprietário, exporte o arquivo e importe no programa do fabricante.");
    }

    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string Normalize(string value, int max)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        var clean = new string(chars).Normalize(NormalizationForm.FormC).Replace("|", " ").Replace(";", " ").Trim().ToUpperInvariant();
        return clean.Length <= max ? clean : clean[..max];
    }
}

public sealed class ScaleLoadForm : Form
{
    private static readonly Color Blue = Color.FromArgb(4, 70, 112);
    private readonly DataGridView grid = new();
    private readonly ComboBox format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Label status = new() { AutoSize = false, Width = 300, Height = 38, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(230, 145, 20), Text = "AGUARDANDO CARGA" };

    public static void Open(IWin32Window owner)
    {
        var current = Application.OpenForms.OfType<ScaleLoadForm>().FirstOrDefault();
        if (current != null) { current.BringToFront(); current.Focus(); return; }
        new ScaleLoadForm().Show(owner);
    }

    public ScaleLoadForm()
    {
        Text = "LEAL INFO PDV - Carga para Balança";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(940, 620);
        MinimumSize = new Size(820, 540);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9);

        Controls.Add(new Label { Text = "CARGA DE PRODUTOS PARA BALANÇA", Dock = DockStyle.Top, Height = 54, BackColor = Blue, ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
        ConfigureGrid();
        Controls.Add(grid);

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 112, ColumnCount = 2, Padding = new Padding(12), BackColor = Color.FromArgb(224, 239, 248) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        buttons.Controls.Add(Button("MARCAR TODOS", SelectAll, Blue));
        buttons.Controls.Add(Button("DESMARCAR", ClearAll, Color.FromArgb(90, 105, 115)));
        buttons.Controls.Add(Button("EXPORTAR ARQUIVO", Export, Color.FromArgb(0, 145, 85)));
        buttons.Controls.Add(Button("ENVIAR COM/TCP", async () => await SendAsync(), Color.FromArgb(230, 145, 20)));
        format.Items.AddRange(new object[] { "UNIVERSAL CSV", "UNIVERSAL TXT", "TOLEDO - ARQUIVO INTERMEDIÁRIO", "FILIZOLA - ARQUIVO INTERMEDIÁRIO", "URANO - ARQUIVO INTERMEDIÁRIO", "BALMAK - ARQUIVO INTERMEDIÁRIO", "RAMUZA - ARQUIVO INTERMEDIÁRIO" });
        format.SelectedIndex = 0;
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        options.Controls.Add(format); options.Controls.Add(status);
        footer.Controls.Add(buttons, 0, 0); footer.Controls.Add(options, 1, 0);
        Controls.Add(footer); footer.BringToFront();
        LoadProducts();
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Blue, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter };
        grid.EnableHeadersVisualStyles = false; grid.RowTemplate.Height = 28;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "selected", HeaderText = "ENVIAR", FillWeight = 45 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "plu", HeaderText = "PLU", ReadOnly = true, FillWeight = 55 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "code", HeaderText = "CÓDIGO", ReadOnly = true, FillWeight = 100 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "description", HeaderText = "DESCRIÇÃO", ReadOnly = true, FillWeight = 220 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "price", HeaderText = "PREÇO/KG", ReadOnly = true, FillWeight = 75 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "unit", HeaderText = "UNID.", ReadOnly = true, FillWeight = 55 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "validity", HeaderText = "VALIDADE", ReadOnly = true, FillWeight = 70 });
    }

    private void LoadProducts()
    {
        grid.Rows.Clear();
        foreach (var p in ScaleLoadService.LoadProducts())
        {
            var index = grid.Rows.Add(true, p.Plu, p.Code, p.Description, p.Price.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")), p.Unit, p.ValidityDays);
            grid.Rows[index].Tag = p;
        }
        status.Text = grid.Rows.Count == 0 ? "NENHUM PRODUTO DE BALANÇA" : $"{grid.Rows.Count} PRODUTO(S) PRONTO(S)";
        status.BackColor = grid.Rows.Count == 0 ? Color.FromArgb(180, 55, 55) : Color.FromArgb(0, 145, 85);
    }

    private List<ScaleLoadItem> Selected()
    {
        grid.EndEdit();
        return grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells["selected"].Value ?? false)).Select(r => (ScaleLoadItem)r.Tag!).ToList();
    }

    private void SelectAll() { foreach (DataGridViewRow row in grid.Rows) row.Cells["selected"].Value = true; }
    private void ClearAll() { foreach (DataGridViewRow row in grid.Rows) row.Cells["selected"].Value = false; }

    private void Export()
    {
        var products = Selected();
        if (products.Count == 0) { Warn("Marque pelo menos um produto para gerar a carga."); return; }
        var csv = format.Text.Contains("CSV", StringComparison.OrdinalIgnoreCase);
        using var dialog = new SaveFileDialog { Title = "Salvar carga para balança", Filter = csv ? "Arquivo CSV (*.csv)|*.csv" : "Arquivo TXT (*.txt)|*.txt", FileName = csv ? "CARGA_BALANCA.csv" : "CARGA_BALANCA.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllBytes(dialog.FileName, ScaleLoadService.Build(products, format.Text));
        status.Text = $"ARQUIVO GERADO • {products.Count} ITEM(NS)"; status.BackColor = Color.FromArgb(0, 145, 85);
        MessageBox.Show(this, "Carga gerada com sucesso.\n\nEm modelos proprietários, importe este arquivo no programa fornecido pelo fabricante da balança.", "Carga para balança", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task SendAsync()
    {
        var products = Selected();
        if (products.Count == 0) { Warn("Marque pelo menos um produto para enviar."); return; }
        if (MessageBox.Show(this, $"Enviar {products.Count} produto(s) pela conexão configurada?\n\nO protocolo da balança deve aceitar carga ASCII delimitada.", "Confirmar carga", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            status.Text = "ENVIANDO CARGA..."; status.BackColor = Color.FromArgb(230, 145, 20);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ScaleLoadService.SendAsync(ScaleLoadService.Build(products, format.Text), timeout.Token);
            status.Text = $"CARGA ENVIADA • {products.Count} ITEM(NS)"; status.BackColor = Color.FromArgb(0, 145, 85);
        }
        catch (Exception ex)
        {
            status.Text = "FALHA NO ENVIO"; status.BackColor = Color.FromArgb(180, 55, 55);
            MessageBox.Show(this, ex.Message, "Carga para balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static Button Button(string text, Action action, Color color)
    {
        var button = new Button { Text = text, Width = 150, Height = 38, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => action(); return button;
    }
    private void Warn(string text) => MessageBox.Show(this, text, "Carga para balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
