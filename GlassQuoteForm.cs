using System.Globalization;

namespace LealInfoPDV;

// Prévia em memória. Usa o cadastro existente, sem modificar banco ou produtos.
public sealed class GlassQuoteForm : Form
{
    private GlassProject project;
    private bool embedded;
    private readonly ComboBox customer = new() { Name = "QuoteCustomer", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox service = new() { Name = "QuoteService", Dock = DockStyle.Fill, MaxLength = 240 };
    private readonly NumericUpDown price = Money("QuotePrice");
    private readonly NumericUpDown serviceValue = Money("QuoteServiceValue");
    private readonly NumericUpDown discount = Money("QuoteDiscount");
    private readonly NumericUpDown addition = Money("QuoteAddition");
    private readonly TextBox summary = new() { Name = "QuoteSummary", ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, MinimumSize = new Size(100, 270) };
    private readonly Button load = new SalesVisualButton() { Name = "LoadQuoteCustomers", Text = "CARREGAR CLIENTES", AutoSize = true, MinimumSize = new Size(180, 42) };
    private readonly Button registerCustomer = new SalesVisualButton { Name = "RegisterGlassCustomer", Text = "+ CADASTRAR CLIENTE", AutoSize = true, MinimumSize = new Size(220, 42) };
    private readonly Button selectCustomer = new SalesVisualButton { Name = "SelectGlassCustomer", Text = "SELECIONAR CLIENTE", AutoSize = true, MinimumSize = new Size(220, 42) };
    private readonly Label selectedCustomer = new() { Name = "SelectedGlassCustomer", AutoSize = true, Dock = DockStyle.Top };
    private readonly FlowLayoutPanel customerActions = new() { Name = "GlassCustomerActions", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true };
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
        load.Click += (_, _) => LoadCustomers();
        registerCustomer.Click += (_, _) => RegisterCustomer();
        selectCustomer.Click += (_, _) => SelectCustomer();
        customerActions.Controls.AddRange(new Control[] { load, registerCustomer, selectCustomer });
        fields.Controls.Add(customerActions);
        fields.Controls.Add(selectedCustomer);
        Add("DESCRIÇÃO DO SERVIÇO (opcional)", service);
        Add("VALOR POR m² (R$)", price);
        Add("SERVIÇO ADICIONAL — VALOR TOTAL (R$)", serviceValue);
        Add("DESCONTO — VALOR TOTAL (R$)", discount);
        Add("ACRÉSCIMO — VALOR TOTAL (R$)", addition);
        Add("PRÉVIA COMERCIAL", summary);
        fields.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill,
            Text = "Prévia em memória. Salvar, PDF, impressão comercial, WhatsApp e recibo serão conectados em etapa posterior." });
        scroll.Controls.Add(fields);
        var close = new SalesVisualButton { Text = "FECHAR", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close(); CancelButton = close;
        Controls.Add(scroll); Controls.Add(close);
        foreach (var value in new[] { price, serviceValue, discount, addition }) value.ValueChanged += (_, _) => RefreshQuote();
        customer.SelectedIndexChanged += (_, _) => RefreshQuote(); service.TextChanged += (_, _) => RefreshQuote();
        Shown += (_, _) => { var area = Screen.FromControl(this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); };
        RefreshQuote();
    }
    // Move os mesmos controles: não cria outra fonte de dados ou outro orçamento.
    internal void Mount(TableLayoutPanel projects, TableLayoutPanel calculations)
    {
        embedded = true;
        GlassWorkshopForm.AddField(projects, "CLIENTE (cadastro existente)", customer);
        GlassWorkshopForm.AddField(projects, "", customerActions);
        GlassWorkshopForm.AddField(projects, "", selectedCustomer);
        GlassWorkshopForm.AddField(projects, "DESCRIÇÃO DO SERVIÇO (opcional)", service);
        GlassWorkshopForm.AddField(calculations, "VALOR POR m² (R$)", price);
        GlassWorkshopForm.AddField(calculations, "SERVIÇO ADICIONAL — VALOR TOTAL (R$)", serviceValue);
        GlassWorkshopForm.AddField(calculations, "DESCONTO — VALOR TOTAL (R$)", discount);
        GlassWorkshopForm.AddField(calculations, "ACRÉSCIMO — VALOR TOTAL (R$)", addition);
        summary.Dock = DockStyle.Top; summary.Height = 290;
        GlassWorkshopForm.AddField(calculations, "ÁREA / QUANTIDADE / VALORES / TOTAL", summary);
        GlassWorkshopForm.AddField(calculations, "", new Label { AutoSize = true, Text = "Prévia em memória • orçamento comercial/PDF na aba Impressão • recibo pendente de vínculo com pagamento." });
    }
    public void UpdateProject(GlassProject source)
    {
        if (IsDisposed) return;
        project = source; RefreshQuote();
    }
    internal GlassCommercialSnapshot CommercialSnapshot(IReadOnlyList<GlassMaterialLink> materials)
    {
        var value = CurrentQuote();
        if (value.ValidationError != null) throw new InvalidOperationException(value.ValidationError);
        if (customer.SelectedItem is not Customer selected || selected.Id == 0)
            throw new InvalidOperationException("Selecione um cliente cadastrado na aba Projetos.");
        string company = "";
        using (var connection = Database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT value FROM settings WHERE key='company_name'";
            company = Convert.ToString(command.ExecuteScalar()) ?? "";
        }
        return new(value, selected.Id, selected.Name, service.Text.Trim(), company, DateTime.Now, materials.ToArray());
    }
    internal GlassSavedQuote CaptureSaved()
    {
        var c = customer.SelectedItem as Customer;
        return new(c?.Id ?? 0, c?.Id > 0 ? c.Name : "", service.Text, price.Value, serviceValue.Value, discount.Value, addition.Value);
    }
    internal void RestoreSaved(GlassSavedQuote saved)
    {
        LoadCustomers();
        int index = customer.Items.Cast<Customer>().ToList().FindIndex(c => c.Id == saved.CustomerId);
        if(index < 0 && saved.CustomerId > 0)
        { customer.Items.Add(new Customer(saved.CustomerId, saved.CustomerName)); index = customer.Items.Count - 1; }
        customer.SelectedIndex = Math.Max(0,index);
        service.Text=saved.Service; price.Value=saved.Price; serviceValue.Value=saved.ServiceValue;
        discount.Value=saved.Discount; addition.Value=saved.Addition; RefreshQuote();
    }
    private void SelectCustomer() => RegisterCustomer();
    private void LinkCustomer(long id)
    {
        LoadCustomers();
        int index = customer.Items.Cast<Customer>().ToList().FindIndex(c => c.Id == id && c.Id > 0);
        if (index < 1) throw new InvalidOperationException("O cliente escolhido não está mais disponível no cadastro.");
        customer.SelectedIndex = index;
        RefreshQuote();
    }
    private void RegisterCustomer()
    {
        Form? host = registerCustomer.FindForm();
        while (host != null && host is not MainForm) host = host.Owner;
        if (host is not MainForm main)
        {
            MessageBox.Show(registerCustomer.FindForm(), "Abra a Vidraçaria pela tela principal para acessar o cadastro de clientes.", "Cadastro de clientes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { main.OpenCustomersFromGlass(LinkCustomer); }
        finally { LoadCustomers(); }
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
    private GlassQuote CurrentQuote() => new(project, price.Value, serviceValue.Value, discount.Value, addition.Value);
    private void RefreshQuote()
    {
        selectedCustomer.Text = customer.SelectedItem is Customer c && c.Id > 0
            ? $"CLIENTE SELECIONADO: {c.Name} (ID {c.Id})"
            : "NENHUM CLIENTE VINCULADO — use SELECIONAR CLIENTE.";
        var quote = CurrentQuote();
        if (quote.ValidationError != null) { summary.Text = quote.ValidationError + "\r\nTotal indisponível até corrigir os dados."; return; }
        string MoneyText(decimal value) => GlassBrazilianMoney.Format(value);
        summary.Text = $"CLIENTE: {customer.SelectedItem}\r\nPROJETO: {project.Description}\r\nMODELO: {GlassProject.ModelName}\r\nSERVIÇO: {service.Text}\r\nMEDIDAS: {project.WidthMm.ToString("0.##", Pt)} × {project.HeightMm.ToString("0.##", Pt)} mm\r\nQUANTIDADE: {project.Quantity}\r\nÁREA POR UNIDADE: {quote.Area.ToString("0.00####", Pt)} m²\r\nÁREA TOTAL: {quote.TotalArea.ToString("0.00####", Pt)} m²\r\nVALOR POR m²: {MoneyText(price.Value)}\r\nVALOR BASE POR ÁREA: {MoneyText(quote.MaterialValue)}\r\nSERVIÇO ADICIONAL (total): {MoneyText(serviceValue.Value)}\r\nSUBTOTAL (área + serviço): {MoneyText(quote.BaseValue)}\r\nDESCONTO: {MoneyText(discount.Value)}\r\nACRÉSCIMO: {MoneyText(addition.Value)}\r\nTOTAL: {MoneyText(quote.Total)}";
        if (embedded) summary.Text = summary.Text[summary.Text.IndexOf("QUANTIDADE:", StringComparison.Ordinal)..];
    }
    private static NumericUpDown Money(string name) => new GlassMoneyInput { Name = name, Dock = DockStyle.Fill };
}
