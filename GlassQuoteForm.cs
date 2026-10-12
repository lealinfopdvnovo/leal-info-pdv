using System.Globalization;

namespace LealInfoPDV;

// Prévia em memória. Usa o cadastro existente, sem modificar banco ou produtos.
public sealed class GlassQuoteForm : Form
{
    private GlassProject project;
    private readonly ComboBox customer = new() { Name = "QuoteCustomer", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox service = new() { Name = "QuoteService", Dock = DockStyle.Fill, MaxLength = 240 };
    private readonly NumericUpDown price = Money("QuotePrice");
    private readonly NumericUpDown serviceValue = Money("QuoteServiceValue");
    private readonly NumericUpDown discount = Money("QuoteDiscount");
    private readonly NumericUpDown addition = Money("QuoteAddition");
    private readonly TextBox summary = new() { Name = "QuoteSummary", ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, MinimumSize = new Size(100, 270) };
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private sealed record Customer(long Id, string Name) { public override string ToString() => Name; }

    public GlassQuoteForm(GlassProject source)
    {
        project = source;
        Text = "ORÇAMENTO DA VIDRAÇARIA — PRÉVIA";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 700);
        MinimumSize = new Size(420, 360);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        BackColor = Color.FromArgb(8, 18, 36); ForeColor = Color.White;
        ShowInTaskbar = false;
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(string caption, Control control)
        {
            fields.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 8, 0, 4) });
            control.Margin = new Padding(0, 0, 0, 6); fields.Controls.Add(control);
        }
        customer.Items.Add(new Customer(0, "Selecionar cliente cadastrado")); customer.SelectedIndex = 0;
        Add("CLIENTE (cadastro existente)", customer);
        var load = new Button { Text = "CARREGAR CLIENTES", AutoSize = true };
        load.Click += (_, _) => LoadCustomers(); fields.Controls.Add(load);
        Add("DESCRIÇÃO DO SERVIÇO (opcional)", service);
        Add("VALOR POR m² (R$)", price);
        Add("SERVIÇO ADICIONAL — VALOR TOTAL (R$)", serviceValue);
        Add("DESCONTO — VALOR TOTAL (R$)", discount);
        Add("ACRÉSCIMO — VALOR TOTAL (R$)", addition);
        Add("PRÉVIA COMERCIAL", summary);
        fields.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill,
            Text = "Prévia em memória. Salvar, PDF, impressão comercial, WhatsApp e recibo serão conectados em etapa posterior." });
        scroll.Controls.Add(fields);
        var close = new Button { Text = "FECHAR", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close(); CancelButton = close;
        Controls.Add(scroll); Controls.Add(close);
        foreach (var value in new[] { price, serviceValue, discount, addition }) value.ValueChanged += (_, _) => RefreshQuote();
        customer.SelectedIndexChanged += (_, _) => RefreshQuote(); service.TextChanged += (_, _) => RefreshQuote();
        Shown += (_, _) => { var area = Screen.FromControl(this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); };
        RefreshQuote();
    }
    public void UpdateProject(GlassProject source)
    {
        if (IsDisposed) return;
        project = source; RefreshQuote();
    }
    private void LoadCustomers()
    {
        try
        {
            var selected = (customer.SelectedItem as Customer)?.Id;
            var items = new List<Customer>();
            using var connection = Database.Open(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT id,name FROM customers ORDER BY name,id";
            using var reader = command.ExecuteReader();
            while (reader.Read()) items.Add(new Customer(reader.GetInt64(0), reader.GetString(1)));
            customer.Items.Clear(); customer.Items.Add(new Customer(0, "Selecionar cliente cadastrado"));
            foreach (var item in items) customer.Items.Add(item);
            customer.SelectedIndex = Math.Max(0, customer.Items.Cast<Customer>().ToList().FindIndex(c => c.Id == selected));
        }
        catch (Exception) { MessageBox.Show(this, "Não foi possível consultar o cadastro de clientes. Nenhum dado foi alterado.", Text); }
    }
    private void RefreshQuote()
    {
        var quote = new GlassQuote(project, price.Value, serviceValue.Value, discount.Value, addition.Value);
        if (quote.ValidationError != null) { summary.Text = quote.ValidationError + "\r\nTotal indisponível até corrigir os dados."; return; }
        string MoneyText(decimal value) => value.ToString("C2", Pt);
        summary.Text = $"CLIENTE: {customer.SelectedItem}\r\nPROJETO: {project.Description}\r\nMODELO: {GlassProject.ModelName}\r\nSERVIÇO: {service.Text}\r\nMEDIDAS: {project.WidthMm.ToString("0.##", Pt)} × {project.HeightMm.ToString("0.##", Pt)} mm\r\nQUANTIDADE: {project.Quantity}\r\nÁREA POR UNIDADE: {quote.Area.ToString("0.######", Pt)} m²\r\nÁREA TOTAL: {quote.TotalArea.ToString("0.######", Pt)} m²\r\nVALOR POR m²: {MoneyText(price.Value)}\r\nVALOR BASE POR ÁREA: {MoneyText(quote.MaterialValue)}\r\nSERVIÇO ADICIONAL (total): {MoneyText(serviceValue.Value)}\r\nDESCONTO: {MoneyText(discount.Value)}\r\nACRÉSCIMO: {MoneyText(addition.Value)}\r\nTOTAL: {MoneyText(quote.Total)}";
    }
    private static NumericUpDown Money(string name) => new() { Name = name, Dock = DockStyle.Fill,
        DecimalPlaces = 2, Maximum = 1000000000m, ThousandsSeparator = true };
}
