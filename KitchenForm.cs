using LealInfoPDV.Network;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

// Stage 1 entry and consultation only. Sending a kitchen order never finalizes a sale.
internal sealed class KitchenForm : Form
{
    private Form? entryWindow;
    private Form? tableWindow;
    private KitchenOrderPanel? floatingTableOrder;
    private readonly KitchenOrderPanel dashboardOrder=new();
    private KitchenOrder[] visibleOrders=Array.Empty<KitchenOrder>();
    private bool dashboardDraft=true;
    private DateTimeOffset? draftStarted;
    private readonly System.Windows.Forms.Timer dashboardClock=new(){Interval=1000};
    private readonly KitchenCatalogView mainCatalog = new();
    private readonly KitchenTablesView tables = new();
    private readonly FlowLayoutPanel orderCards = new() { Name="KitchenOrderCards",Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=false };
    private readonly KitchenPremiumHeader premiumHeader = new();
    private readonly KitchenCatalogView catalog = new();
    private long? selectedProductId;
    private string selectedProductName = "";
    private readonly TextBox itemDescription = new() { Name = "ItemDescription", Dock = DockStyle.Fill, PlaceholderText = "Nome do produto ou prato" };
    private readonly NumericUpDown itemQuantity = new() { Name = "ItemQuantity", Dock = DockStyle.Fill, DecimalPlaces = 3, Minimum = 0.001m, Maximum = 999999m, Value = 1 };
    private readonly TextBox itemNotes = new() { Name = "ItemNotes", Dock = DockStyle.Fill, PlaceholderText = "Ex.: sem cebola, bem passado" };
    private readonly Button addItem = new() { Name = "AddItem", Text = "ADICIONAR AO PEDIDO" };
    private readonly Label itemSummary = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private int editingRow = -1;
    private static readonly Color Navy = Color.FromArgb(9, 29, 49);
    private static readonly Color Blue = Color.FromArgb(0, 133, 190);
    private static readonly Color Ink = Color.FromArgb(28, 48, 68);
    private readonly KitchenStore store = new();
    private readonly KitchenOutbox outbox;
    private readonly string bindingPath;
    private string? storeId;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly System.Windows.Forms.Timer retry = new() { Interval = 5000 };
    private readonly TextBox reference = new() { PlaceholderText = "Referência do pedido (opcional)", Dock = DockStyle.Top };
    private readonly TextBox notes = new() { PlaceholderText = "Observações gerais", Dock = DockStyle.Top };
    private readonly DataGridView items = new() { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };
    private readonly DataGridView orders = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly TextBox detail = new() { Dock = DockStyle.Bottom, Height = 100, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label message = new() { Dock = DockStyle.Bottom, Height = 45, AutoEllipsis = true };
    private readonly Button send = new() { Text = "ENVIAR PEDIDO À COZINHA", AutoSize = true };
    private readonly Label entryHint = new() { Text = "Clique em NOVO PEDIDO para iniciar e preencha os itens abaixo.", Dock = DockStyle.Top, Height = 36, AutoEllipsis = true };
    private readonly Button fresh = new() { Text = "NOVO PEDIDO", AutoSize = true, CausesValidation = false };
    internal KitchenForm()
    {
        Text = "COZINHA PREMIUM — LEAL INFO PDV PRO"; Name = "KitchenForm";
        StartPosition = FormStartPosition.CenterParent;
        var desktop = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(1080, desktop.Width), Math.Min(720, desktop.Height));
        Size = new Size(Math.Max(MinimumSize.Width, Math.Min(1360, desktop.Width - 24)), Math.Max(MinimumSize.Height, Math.Min(800, desktop.Height - 24)));
        var config = NetworkConfiguration.Current;
        string endpoint = config.Mode == "terminal" ? "network:" + config.CertificateHash + ":" + config.PairingSecret : "local:" + Database.DbPath;
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
        string folder = Path.Combine(Database.AppFolder, "KitchenOutbox", key); outbox = new(folder); bindingPath = Path.Combine(folder, "binding");
        if (File.Exists(bindingPath)) storeId = ProtectedFile.Read<string>(bindingPath);
        items.Columns.Add("Description", "Item"); items.Columns.Add("Quantity", "Quantidade"); items.Columns.Add("Notes", "Observações do item");
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(241, 246, 250); ForeColor = Ink;
        StyleGrid(orders); StyleGrid(items);
        items.ReadOnly = true; items.AllowUserToAddRows = false;
        items.Columns[0].FillWeight = 44; items.Columns[1].FillWeight = 16; items.Columns[2].FillWeight = 40;
        items.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        items.RowsAdded += (_, _) => UpdateItemSummary(); items.RowsRemoved += (_, _) => UpdateItemSummary();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true }; buttons.Controls.Add(fresh);
        foreach (string status in new[] { "PREPARANDO", "PRONTO", "ENTREGUE", "CANCELADO" })
        {
            var button = new Button { Text = status, AutoSize = true }; buttons.Controls.Add(button);
            button.Click += async (_,_) => await AdvanceOrder(status);
        }
        var refresh = new Button { Text = "ATUALIZAR / TENTAR PENDÊNCIAS", AutoSize = true }; buttons.Controls.Add(refresh);
        refresh.Click += async (_, _) => await RefreshAndRetry();
        buttons.Padding = new Padding(12); buttons.BackColor = Color.White;
        foreach (Button button in buttons.Controls) StyleButton(button, button == fresh);
        detail.Visible=false; detail.BorderStyle = BorderStyle.None; detail.BackColor = Color.FromArgb(230, 239, 246); detail.ForeColor = Ink;
        message.Padding = new Padding(14, 4, 14, 4); message.ForeColor = Ink;
        var workspace = new Panel { Dock = DockStyle.Fill,AutoScroll=true };
        var right=new TableLayoutPanel{Name="KitchenPremiumWorkspace",Dock=DockStyle.Top,Height=700,ColumnCount=1,RowCount=5,Padding=new Padding(4)};
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        void LayoutWorkspace()
        {
            if(right.RowStyles.Count<5)return;
            float tableHeight=Math.Max(tables.MinimumSize.Height,workspace.Height<740?226:Math.Clamp((workspace.Height-190)*0.56f,226,365));
            right.RowStyles[0].Height=tableHeight;
            int minimum=(int)Math.Ceiling(tableHeight+right.RowStyles[2].Height+right.RowStyles[4].Height)+dashboardOrder.MinimumSize.Height+right.Padding.Vertical+32;
            right.Height=Math.Max(minimum,workspace.ClientSize.Height);
        }
        workspace.SizeChanged+=(_,_)=>LayoutWorkspace();
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,226));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,150));right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        tables.Dock=DockStyle.Fill;tables.MinimumSize=new Size(0,226);right.Controls.Add(tables,0,0);right.Controls.Add(dashboardOrder,0,1);
        var history=new Panel{Name="KitchenActiveOrders",Dock=DockStyle.Fill,Padding=new Padding(4,4,4,2)};
        history.Controls.Add(orderCards);history.Controls.Add(new Label{Text="PEDIDOS EM ANDAMENTO",Dock=DockStyle.Top,Height=25,Font=new Font("Segoe UI",10,FontStyle.Bold)});
        right.Controls.Add(history,0,2);buttons.Dock=DockStyle.Fill;buttons.Padding=new Padding(3);buttons.Margin=Padding.Empty;right.Controls.Add(buttons,0,3);buttons.Visible=false;dashboardOrder.AttachNewOrder(fresh);
        message.Dock=DockStyle.Fill;message.Padding=new Padding(4);right.Controls.Add(message,0,4);
        orders.Visible=false;detail.Visible=false;workspace.Controls.Add(orders);workspace.Controls.Add(detail);workspace.Controls.Add(right);
        dashboardOrder.PaymentRequested+=async()=>await ReceiveSelected();
        dashboardOrder.SubmitRequested+=async()=>await SubmitDraft();dashboardOrder.ReadyRequested+=async()=>await AdvanceOrder("PRONTO");
        dashboardOrder.StatusRequested+=async status=>await AdvanceOrder(status);dashboardOrder.PrintRequested+=PrintCurrentOrder;dashboardOrder.EditRequested+=EditDashboardItem;dashboardOrder.RemoveRequested+=RemoveDashboardItem;dashboardOrder.ClearRequested+=()=>ClearDashboardDraft();
        dashboardClock.Tick+=(_,_)=>{foreach(var label in orderCards.Controls.Cast<Control>().SelectMany(c=>c.Controls.OfType<Label>()).Where(l=>l.Tag is DateTimeOffset)){var age=DateTimeOffset.UtcNow-(DateTimeOffset)label.Tag!;label.Text=$"{(int)Math.Max(0,age.TotalMinutes):00}:{Math.Max(0,age.Seconds):00}";}};dashboardClock.Start();
        var dashboard = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dashboard.Controls.Add(mainCatalog, 0, 0); dashboard.Controls.Add(workspace, 1, 0); Controls.Add(dashboard);
        tables.CapacityRequested += async number => await Run(async () =>
        {
            if(!Auth.HasPermission("sales"))throw new InvalidOperationException("Sem permissão para configurar mesas.");
            try { await Task.Run(()=>store.ChangeTableCount(number,tables.SeatingRevision)); }
            finally { var saved=await Task.Run(store.Seating);tables.UpdateSeating(saved); }
        });
        tables.ReservationRequested += async (number,revision,reserved) => await Run(async () =>
        {
            if(!Auth.HasPermission("sales"))throw new InvalidOperationException("Sem permissão para reservar mesas.");
            try { await Task.Run(()=>store.ReserveTable(number,revision,reserved,"",Auth.OperatorName)); }
            finally { await RefreshOrders(); }
        });
        tables.TableChosen += SelectDashboardTable;
        tables.TableActivated += OpenTableWindow;
        mainCatalog.ProductChosen += AddDashboardProduct;
        mainCatalog.CatalogChanged += async () => await catalog.ReloadAsync();
        catalog.CatalogChanged += async () => await mainCatalog.ReloadAsync();
        premiumHeader.ImportRequested+=()=>mainCatalog.Controls.Find("KitchenImportPdv",true).OfType<Button>().Single().PerformClick();
        premiumHeader.RefreshRequested+=async()=>await RefreshAndRetry();
        premiumHeader.SettingsRequested+=()=>{
            using var configuration=new Form{Name="KitchenConfiguration",Text="CONFIGURAÇÃO DA COZINHA",Size=new Size(520,300),StartPosition=FormStartPosition.CenterParent};
            var seating=new Button{Text="QUANTIDADE DE MESAS E RESERVAS",Dock=DockStyle.Top,Height=60};
            seating.Click+=(_,_)=>tables.Controls.Find("MoreKitchenTables",true).OfType<Button>().Single().PerformClick();
            var printer=new Button{Text="IMPRESSORA DA COMANDA • TÉRMICA / A4",Dock=DockStyle.Top,Height=60};printer.Click+=(_,_)=>KitchenComanda.Configure(configuration);
            configuration.Controls.Add(new Label{Text="Configure as mesas e a impressão da comanda. Pagamento: selecione a mesa e clique em RECEBER / PAGAMENTO.",Dock=DockStyle.Fill,Padding=new Padding(16)});configuration.Controls.Add(printer);configuration.Controls.Add(seating);KitchenPremiumStyle.Apply(configuration);configuration.ShowDialog(this);
        };
        premiumHeader.ExitRequested+=()=>Close();
        Controls.Add(premiumHeader);
        orderCards.SizeChanged+=(_,_)=>{foreach(Control card in orderCards.Controls)card.Width=178;};
        orders.SelectionChanged += (_, _) =>
        {
            if(rebindingOrders)return;
            if (orders.CurrentRow?.DataBoundItem is OrderRow row) {detail.Text = row.Details;selectedOrderId=row.Id;}
            SyncDashboard();
        };
        itemDescription.TextChanged += (_, _) =>
        {
            if (!string.Equals(itemDescription.Text, selectedProductName, StringComparison.Ordinal)) selectedProductId = null;
        };
        catalog.ProductChosen += ChooseProduct;
        fresh.Click += (_, _) => OpenEntryWindow();
        send.Click += async (_,_)=>await SubmitDraft();
        Shown += async (_, _) => { FitToDesktop(); await RefreshAndRetry(); await mainCatalog.ReloadAsync(); if (!IsDisposed) retry.Start(); };
        retry.Tick += async (_, _) => await RefreshAndRetry();
        KitchenPremiumStyle.Apply(this);
    }
    private void FitToDesktop()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(1080, area.Width), Math.Min(720, area.Height));
        Size = new Size(Math.Clamp(Width, MinimumSize.Width, area.Width), Math.Clamp(Height, MinimumSize.Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width), Math.Clamp(Top, area.Top, area.Bottom - Height));
    }
    private void ChooseProduct(KitchenCatalogProduct product)
    {
        if (!send.Enabled) return;
        if (!string.IsNullOrWhiteSpace(itemDescription.Text) || editingRow >= 0)
        {
            message.Text = "Adicione ou salve o item em edição antes de escolher outro produto."; return;
        }
        selectedProductName = product.Name; itemDescription.Text = product.Name; selectedProductId = product.Id;
        AddDraftItem();
    }
    private void OpenEntryWindow()
    {
        dashboardDraft=true;SyncDashboard();
        if (entryWindow is { IsDisposed: false, Visible: true } && send.Enabled)
        {
            entryWindow.Activate();
            return;
        }
        bool create = entryWindow == null || entryWindow.IsDisposed;
        if (create)
        {
            entryWindow = new Form
            {
                Name = "KitchenOrderEntry", Text = "NOVO PEDIDO — COZINHA",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(Math.Min(1280, Screen.FromControl(this).WorkingArea.Width - 40), Math.Min(700, Screen.FromControl(this).WorkingArea.Height - 30)), MinimumSize = new Size(960, 640),
                ShowInTaskbar = false, Font = Font
            };
            var close = new Button { Text = "FECHAR", AutoSize = true, CausesValidation = false };
            close.Click += (_, _) => entryWindow.Hide();
            BuildEntryLayout(close);
            entryWindow.FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; entryWindow.Hide(); }
            };
        }
        if (!send.Enabled)
        {
            items.CancelEdit(); reference.Clear(); notes.Clear(); items.Rows.Clear();
            SetEntryEnabled(true);
            ResetItemFields(); UpdateItemSummary();
        }
        entryHint.Text = "Preencha os itens, quantidades e observações e clique em ENVIAR PEDIDO À COZINHA.";
        if (!entryWindow!.Visible) entryWindow.Show(this);
        entryWindow.Activate();
        _ = catalog.ReloadAsync();
        entryWindow.BeginInvoke((Action)(() =>
        {
            if (entryWindow is { IsDisposed: false, Visible: true } && send.Enabled)
            {
                itemDescription.Focus();
            }
        }));
    }
    private void BuildEntryLayout(Button close)
    {
        StyleButton(send, true); StyleButton(close, false);
        var shell = new TableLayoutPanel { Name="KitchenEntryShell", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(18, 0, 18, 10) };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for(int i=0;i<3;i++)shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize)); shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(Header("NOVO PEDIDO", "Monte o pedido e envie diretamente para a cozinha.", 80), 0, 0);
        var info = new TableLayoutPanel { AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 10, 0, 10) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        info.RowStyles.Add(new RowStyle(SizeType.AutoSize)); info.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        info.Controls.Add(FieldLabel("REFERÊNCIA DO PEDIDO"), 0, 0); info.Controls.Add(FieldLabel("OBSERVAÇÕES GERAIS"), 1, 0);
        reference.Dock = notes.Dock = DockStyle.Fill;
        reference.Margin = notes.Margin = new Padding(0, 2, 12, 2); reference.BorderStyle = notes.BorderStyle = BorderStyle.FixedSingle;
        info.Controls.Add(reference, 0, 1); info.Controls.Add(notes, 1, 1); shell.Controls.Add(info, 0, 1);
        var compose = new TableLayoutPanel { Name="KitchenEntryCompose", AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Padding = new Padding(12), BackColor = Color.White };
        compose.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); compose.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); compose.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        for(int i=0;i<3;i++)compose.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        compose.Controls.Add(FieldLabel("ITEM / PRODUTO"), 0, 0); compose.Controls.Add(FieldLabel("QUANTIDADE"), 1, 0); compose.Controls.Add(FieldLabel("OBSERVAÇÃO DO ITEM"), 2, 0);
        itemDescription.Margin = itemQuantity.Margin = itemNotes.Margin = new Padding(0, 2, 10, 2);
        itemDescription.BorderStyle = itemNotes.BorderStyle = BorderStyle.FixedSingle;
        compose.Controls.Add(itemDescription, 0, 1); compose.Controls.Add(itemQuantity, 1, 1); compose.Controls.Add(itemNotes, 2, 1);
        StyleButton(addItem, true); addItem.AutoSize=true; addItem.Dock = DockStyle.Fill; addItem.Margin = new Padding(0, 5, 10, 0);
        addItem.Click += (_, _) => AddDraftItem();
        var remove = new Button { Name = "RemoveItem", AutoSize=true, Text = "REMOVER SELECIONADO", Dock = DockStyle.Fill, CausesValidation = false };
        StyleButton(remove, false); remove.Margin = new Padding(0, 5, 0, 0);
        remove.Click += (_, _) =>
        {
            if (!send.Enabled || items.CurrentRow == null) return;
            items.Rows.RemoveAt(items.CurrentRow.Index); ResetItemFields(); UpdateItemSummary();
        };
        compose.Controls.Add(addItem, 0, 2); compose.Controls.Add(remove, 1, 2); compose.SetColumnSpan(remove, 2); shell.Controls.Add(compose, 0, 2);
        var review = new Panel { Name="KitchenEntryReview", MinimumSize=new Size(0,100), Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12), Margin = new Padding(0, 12, 0, 0) };
        review.Controls.Add(items);
        review.Controls.Add(new Label { Text = "ITENS DO PEDIDO   •   Duplo clique para editar", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Ink });
        items.CellDoubleClick += (_, e) =>
        {
            if (!send.Enabled || e.RowIndex < 0) return;
            editingRow = e.RowIndex; var row = items.Rows[editingRow];
            selectedProductName = Convert.ToString(row.Cells[0].Value) ?? "";
            itemDescription.Text = selectedProductName; selectedProductId = row.Tag as long?;
            itemQuantity.Value = KitchenDraft.Quantity(Convert.ToString(row.Cells[1].Value) ?? "1");
            itemNotes.Text = Convert.ToString(row.Cells[2].Value); addItem.Text = "SALVAR ALTERAÇÃO"; itemDescription.Focus();
        };
        shell.Controls.Add(review, 0, 3);
        var payEntry=new Button{Name="KitchenEntryPayment",Text="RECEBER / PAGAMENTO (ENVIAR PEDIDO PRIMEIRO)",Dock=DockStyle.Bottom,Height=42};
        payEntry.Click+=async(_,_)=>{if(DraftItems().Length>0&&send.Enabled)await SubmitDraft();if(!dashboardDraft)await ReceiveSelected();};
        review.Controls.Add(payEntry);
        var actions = new TableLayoutPanel { Name="KitchenEntryActions", AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 10, 0, 4) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        itemSummary.AutoSize=true; itemSummary.TextAlign=ContentAlignment.MiddleCenter; itemSummary.Font = new Font("Segoe UI", 11, FontStyle.Bold); itemSummary.ForeColor = Ink;
        close.Name="CloseKitchenOrder"; send.Name="SendKitchenOrder"; close.AutoSize=send.AutoSize=true;
        close.Dock = send.Dock = DockStyle.Fill; close.Margin = new Padding(0, 0, 10, 0); send.Margin = Padding.Empty;
        actions.Controls.Add(itemSummary, 0, 0); actions.Controls.Add(close, 1, 0); actions.Controls.Add(send, 2, 0); shell.Controls.Add(actions, 0, 4);
        var status = new Label { Name="KitchenEntryStatus", AutoSize=true, TextAlign=ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, AutoEllipsis = false, Text = message.Text, ForeColor = Color.FromArgb(78, 98, 116), Font = new Font("Segoe UI", 9) };
        message.TextChanged += (_, _) => { if (!status.IsDisposed) status.Text = message.Text; };
        shell.Controls.Add(status, 0, 5);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.Controls.Add(catalog, 0, 0); layout.Controls.Add(shell, 1, 0);
        entryWindow!.Controls.Add(layout); KitchenPremiumStyle.Apply(entryWindow); UpdateItemSummary();
    }
    private void SetEntryEnabled(bool value)
    {
        send.Enabled = value; items.ReadOnly = true; reference.ReadOnly = notes.ReadOnly = !value;
        itemDescription.ReadOnly = itemNotes.ReadOnly = !value; itemQuantity.Enabled = addItem.Enabled = value;
    }
    private void ResetItemFields()
    {
        selectedProductId = null; selectedProductName = ""; editingRow = -1; itemDescription.Clear(); itemNotes.Clear(); itemQuantity.Value = 1; addItem.Text = "ADICIONAR AO PEDIDO";
    }
    private void AddDraftItem()
    {
        if (!send.Enabled) return;
        if (string.IsNullOrWhiteSpace(itemDescription.Text))
        {
            message.Text = "Informe o nome do item para adicionar ao pedido."; itemDescription.Focus(); return;
        }
        object[] values = { itemDescription.Text.Trim(), itemQuantity.Value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), itemNotes.Text.Trim() };
        int rowIndex = editingRow >= 0 && editingRow < items.Rows.Count ? editingRow : items.Rows.Add();
        items.Rows[rowIndex].SetValues(values); items.Rows[rowIndex].Tag = selectedProductId;
        ResetItemFields(); UpdateItemSummary(); itemDescription.Focus();
    }
    private void UpdateItemSummary()
    {
        int count = items.Rows.Count;
        itemSummary.Text = count == 0 ? "Adicione o primeiro item" : count == 1 ? "1 item no pedido" : $"{count} itens no pedido";
        SyncDashboard();
    }
    private static Label FieldLabel(string text) => new KitchenCenteredLabel { Text = text, AutoSize=true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Ink, TextAlign = ContentAlignment.MiddleLeft };
    private static Panel Header(string title, string subtitle, int height)
    {
        var panel = new TableLayoutPanel { Name="KitchenEntryHeading", Dock = DockStyle.Fill, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, ColumnCount=1, RowCount=2, MinimumSize=new Size(0,height), BackColor = Navy, Padding = new Padding(16, 10, 16, 8), Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Name="KitchenEntryTitle", Text=title, AutoSize=true, Dock=DockStyle.Fill, ForeColor=KitchenPremiumStyle.Gold, Font=new Font("Segoe UI",20,FontStyle.Bold), TextAlign=ContentAlignment.MiddleCenter },0,0);
        panel.Controls.Add(new Label { Text=subtitle, AutoSize=true, Dock=DockStyle.Fill, ForeColor=Color.FromArgb(178,211,230), Font=new Font("Segoe UI",10), TextAlign=ContentAlignment.MiddleCenter },0,1);
        return panel;
    }
    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(197, 213, 226);
        button.BackColor = primary ? Blue : Color.White; button.ForeColor = primary ? Color.White : Ink;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold); button.Padding = new Padding(10, 5, 10, 5);
        button.Cursor = Cursors.Hand; button.MinimumSize = new Size(0, 42); button.UseVisualStyleBackColor = false;
    }
    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 239, 246); grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        grid.ColumnHeadersHeight = 34; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10); grid.DefaultCellStyle.ForeColor = Ink;
        grid.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(216, 239, 249); grid.DefaultCellStyle.SelectionForeColor = Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 252);
        grid.RowTemplate.Height = 40; grid.AllowUserToResizeRows = false; grid.AllowUserToResizeColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false; grid.GridColor = Color.FromArgb(232, 239, 245);
    }
    private async Task Run(Func<Task> action)
    {
        if (!await gate.WaitAsync(0)) return;
        fresh.Enabled = false;
        try { await action(); }
        catch (Exception e) { if (!IsDisposed) message.Text = e.Message + " • Pendências preservadas; a tentativa será repetida ao reconectar."; }
        finally { gate.Release(); if (!IsDisposed) { fresh.Enabled = true; SyncDashboard(); } }
    }
    private Task AdvanceOrder(string status)=>Run(async()=>
    {
                if (!Auth.HasPermission("sales")) throw new InvalidOperationException("Sem permissão de operação.");
                if(dashboardDraft)throw new InvalidOperationException("Selecione um pedido enviado para alterar seu estado.");
                if (orders.CurrentRow?.DataBoundItem is not OrderRow selected) throw new InvalidOperationException("Selecione um pedido.");
                if (status == "CANCELADO" && MessageBox.Show(this, "Cancelar somente este pedido da cozinha? Isso não cancela vendas nem estorna pagamentos.", "Cozinha", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                string actor = Auth.OperatorName;
                await Task.Run(() =>
                {
                    long revision=selected.Revision;
                    if(status=="PRONTO"&&selected.Status=="AGUARDANDO")revision=store.ChangeStatus(selected.Id,revision,"PREPARANDO",actor,Guid.NewGuid()).Revision;
                    store.ChangeStatus(selected.Id,revision,status,actor,Guid.NewGuid());
                });
                await RefreshOrders();
    });
    private Task SubmitDraft()=>Run(async()=>
    {
            if (storeId == null) throw new InvalidOperationException("Conecte uma vez à cozinha preparada no servidor antes de enviar.");
            if (!Auth.CanSell) throw new InvalidOperationException("Entre no PDV antes de enviar.");
            if (!string.IsNullOrWhiteSpace(itemDescription.Text) || editingRow >= 0) throw new InvalidOperationException("Adicione ou salve o item em edição antes de enviar o pedido.");
            items.EndEdit();
            var rows = items.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new KitchenItem(Convert.ToString(r.Cells[0].Value) ?? "", KitchenDraft.Quantity(Convert.ToString(r.Cells[1].Value) ?? ""), Convert.ToString(r.Cells[2].Value) ?? "", r.Tag as long?)).ToArray();
            var draft = new KitchenDraft(Guid.NewGuid(), reference.Text.Trim(), notes.Text.Trim(), Auth.OperatorName, DateTimeOffset.UtcNow, rows);
            outbox.Queue(storeId, draft); SetEntryEnabled(false);
            message.Text = "Pedido guardado na fila local. Aguardando confirmação do servidor.";
            await FlushAndRefresh();
            if (!outbox.Pending().Any(p => p.Draft.Id == draft.Id)) { entryWindow?.Hide(); dashboardDraft=false;SelectSavedOrder(draft.Id); }
    });
    private KitchenItem[] DraftItems()=>items.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow&&!string.IsNullOrWhiteSpace(Convert.ToString(r.Cells[0].Value))).Select(r=>new KitchenItem(Convert.ToString(r.Cells[0].Value)??"",KitchenDraft.Quantity(Convert.ToString(r.Cells[1].Value)??"1"),Convert.ToString(r.Cells[2].Value)??"",r.Tag as long?)).ToArray();
    private KitchenOrder? SelectedOrder()=>!dashboardDraft&&selectedOrderId.HasValue?visibleOrders.SingleOrDefault(o=>o.Id==selectedOrderId.Value):null;
    private Guid? selectedOrderId;
    private bool rebindingOrders;
    private void SyncDashboard()
    {
        if(IsDisposed||dashboardOrder.IsDisposed)return;
        var selected=SelectedOrder();
        dashboardOrder.Display(selected?.Reference??reference.Text,selected?.Items??DraftItems(),selected!=null,selected?.ReceivedUtc??draftStarted,send.Enabled&&gate.CurrentCount==1,selected?.Status??"");
        if (tableWindow is { IsDisposed: false } && floatingTableOrder is { IsDisposed: false })
        {
            tableWindow.Text="PEDIDO — "+(selected?.Reference??reference.Text).ToUpperInvariant();
            floatingTableOrder.Display(selected?.Reference??reference.Text,selected?.Items??DraftItems(),selected!=null,selected?.ReceivedUtc??draftStarted,send.Enabled&&gate.CurrentCount==1,selected?.Status??"");
        }
    }
    private void SelectSavedOrder(Guid id)
    {
        selectedOrderId=id;
        foreach(DataGridViewRow row in orders.Rows)if(row.DataBoundItem is OrderRow order&&order.Id==id){orders.CurrentCell=row.Cells.Cast<DataGridViewCell>().First(c=>c.Visible);row.Selected=true;break;}
        SyncDashboard();
    }
    private void SelectDashboardTable(int number)
    {
        string target=$"Mesa {number:00}";
        if(send.Enabled&&items.Rows.Count>0&&!reference.Text.Equals(target,StringComparison.OrdinalIgnoreCase)){message.Text="Conclua o rascunho da "+reference.Text+" antes de mudar de mesa.";return;}
        var saved=visibleOrders.FirstOrDefault(o=>KitchenTablesView.TableNumber(o.Reference)==number&&o.Status is not ("ENTREGUE" or "CANCELADO"));
        if(saved!=null&&!(send.Enabled&&items.Rows.Count>0)){dashboardDraft=false;SelectSavedOrder(saved.Id);}
        else {dashboardDraft=true;reference.Text=target;if(!send.Enabled&&outbox.Pending().Count==0){items.Rows.Clear();SetEntryEnabled(true);ResetItemFields();}SyncDashboard();}
    }
    private async Task ReceiveSelected()
    {
        if(dashboardDraft){message.Text="Envie o pedido para a cozinha antes de receber o pagamento.";return;}
        var selected=SelectedOrder();if(selected==null)return;
        await Run(async()=>{
            var main=Application.OpenForms.OfType<MainForm>().FirstOrDefault()??throw new InvalidOperationException("Abra a cozinha pelo PDV para receber o pagamento.");
            bool received;
            try {received=main.ReceiveKitchenOrder(tableWindow is {Visible:true}?tableWindow:this,store,selected.Id);}
            catch(Exception e){MessageBox.Show(tableWindow is {Visible:true}?tableWindow:this,e.Message,"Pagamento não concluído",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            if(received){
                message.Text="Pagamento registrado. Mesa liberada.";tableWindow?.Close();await RefreshOrders();dashboardDraft=true;items.Rows.Clear();reference.Text="";draftStarted=null;SetEntryEnabled(true);SyncDashboard();
            }
        });
    }
    private void OpenTableWindow(int number)
    {
        if (!fresh.Enabled) return;
        SelectDashboardTable(number);
        string target=$"Mesa {number:00}";
        if(KitchenTablesView.TableNumber(SelectedOrder()?.Reference??reference.Text)!=number) return;
        if(SelectedOrder()==null) { OpenEntryForTable(number); return; }
        if(tableWindow is { IsDisposed:false }) { SyncDashboard();tableWindow.Show();tableWindow.Activate();return; }
        var area=Screen.FromControl(this).WorkingArea;
        tableWindow=new Form{Name="KitchenTableOrder",Text="PEDIDO — "+target.ToUpperInvariant(),StartPosition=FormStartPosition.CenterParent,Size=new Size(Math.Min(1000,area.Width-24),Math.Min(540,area.Height-24)),MinimumSize=new Size(Math.Min(700,area.Width-24),Math.Min(380,area.Height-24)),ShowInTaskbar=false,Font=Font};
        floatingTableOrder=new KitchenOrderPanel();
        floatingTableOrder.PaymentRequested+=async()=>await ReceiveSelected();
        floatingTableOrder.SubmitRequested+=async()=>await SubmitDraft();
        floatingTableOrder.ReadyRequested+=async()=>await AdvanceOrder("PRONTO");
        floatingTableOrder.StatusRequested+=async status=>await AdvanceOrder(status);
        floatingTableOrder.PrintRequested+=PrintCurrentOrder;
        floatingTableOrder.EditRequested+=EditDashboardItem;
        floatingTableOrder.RemoveRequested+=RemoveDashboardItem;
        floatingTableOrder.ClearRequested+=()=>ClearDashboardDraft();
        var add=new Button{Name="TableAddItems",Text="ADICIONAR ITENS",AutoSize=true,Font=new Font("Segoe UI",8,FontStyle.Bold)};
        add.Click+=(_,_)=> { int? table=KitchenTablesView.TableNumber(SelectedOrder()?.Reference??reference.Text);if(table.HasValue)OpenEntryForTable(table.Value); };
        floatingTableOrder.AttachNewOrder(add);
        tableWindow.Controls.Add(floatingTableOrder);
        var hint=new Label{Text="ADICIONAR ITENS abre o editor. Botão direito nos itens enviados: preparar, pronto, entregue ou cancelar o pedido.",Dock=DockStyle.Bottom,Height=44,Padding=new Padding(10),AutoEllipsis=true};
        tableWindow.Controls.Add(hint);
        KitchenPremiumStyle.Apply(tableWindow);KitchenPremiumStyle.Frame(tableWindow);
        tableWindow.FormClosed+=(_,_)=>{floatingTableOrder=null;tableWindow=null;};
        SyncDashboard();tableWindow.Show(this);
    }
    private void OpenEntryForTable(int number)
    {
        if(!fresh.Enabled)return;
        if(outbox.Pending().Count!=0){message.Text="Aguarde a confirmação do pedido preservado na fila.";return;}
        string target=$"Mesa {number:00}";
        if(send.Enabled&&items.Rows.Count>0&&KitchenTablesView.TableNumber(reference.Text)!=number){message.Text="Conclua o rascunho da "+reference.Text+" antes de mudar de mesa.";return;}
        OpenEntryWindow();
        reference.Text=target;
        if(entryWindow is { IsDisposed:false })entryWindow.Text="PEDIDO — "+target.ToUpperInvariant();
        SyncDashboard();
    }
    private void AddDashboardProduct(KitchenCatalogProduct product)
    {
        if(!send.Enabled)
        {
            if(outbox.Pending().Count!=0){message.Text="Aguarde a confirmação do pedido preservado na fila.";return;}
            items.Rows.Clear();SetEntryEnabled(true);ResetItemFields();
        }
        var selected=SelectedOrder();if(selected!=null)reference.Text=selected.Reference;
        dashboardDraft=true;draftStarted??=DateTimeOffset.UtcNow;ChooseProduct(product);SyncDashboard();
    }
    private void EditDashboardItem(int index)
    {
        if(!dashboardDraft||!send.Enabled||index<0||index>=items.Rows.Count)return;
        OpenEntryWindow();editingRow=index;var row=items.Rows[index];selectedProductName=Convert.ToString(row.Cells[0].Value)??"";itemDescription.Text=selectedProductName;selectedProductId=row.Tag as long?;
        itemQuantity.Value=KitchenDraft.Quantity(Convert.ToString(row.Cells[1].Value)??"1");itemNotes.Text=Convert.ToString(row.Cells[2].Value);addItem.Text="SALVAR ALTERAÇÃO";itemDescription.Focus();
    }
    private void RemoveDashboardItem(int index){if(!dashboardDraft||!send.Enabled||index<0||index>=items.Rows.Count)return;items.Rows.RemoveAt(index);ResetItemFields();UpdateItemSummary();}
    private void ClearDashboardDraft()
    {
        if(!dashboardDraft||!send.Enabled||items.Rows.Count==0)return;
        if(MessageBox.Show(this,"Limpar somente os itens deste rascunho?","Cozinha",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        items.Rows.Clear();notes.Clear();ResetItemFields();draftStarted=null;UpdateItemSummary();
    }
    private void PrintCurrentOrder()
    {
        var selected=SelectedOrder();var rows=selected?.Items??DraftItems();if(rows.Length==0)return;
        try{KitchenComanda.Preview(this,KitchenComanda.Text(selected?.Reference??reference.Text,rows,selected?.Notes??notes.Text));}
        catch(Exception e){MessageBox.Show(this,e.Message,"Imprimir comanda",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private Task RefreshAndRetry() => Run(async () =>
    {
        string actual = await Task.Run(() => { string id=store.EnsureReady();store.EnsureSeatingReady();return id; });
        if (storeId != null && actual != storeId && outbox.Pending().Count != 0) throw new InvalidOperationException("Base alterada. Pendências anteriores não serão enviadas a outro banco.");
        storeId = actual; ProtectedFile.Write(bindingPath, actual); await FlushAndRefresh();
    });
    private async Task FlushAndRefresh()
    {
        int sent = await Task.Run(() => outbox.Flush(store)); await RefreshOrders();
        if (!IsDisposed) message.Text = $"Cozinha conectada • {sent} confirmação(ões) nesta tentativa • {outbox.Pending().Count} pendente(s). Enviar pedido não registra venda, pagamento ou baixa de estoque.";
    }
    private sealed record OrderRow(Guid Id, string Referencia, string Status, long Revision, string Operador, string Recebido, string Details);
    private async Task RefreshOrders()
    {
        var result = await Task.Run(() => (Recent: store.List(), Active: store.ListActive(), Seating: store.Seating())); if (IsDisposed) return;
        void ApplyResult()
        {
        var list = result.Active.Concat(result.Recent).DistinctBy(o=>o.Id).OrderByDescending(o=>o.ReceivedUtc).ThenBy(o=>o.Id).ToArray(); tables.UpdateOrders(result.Active);tables.UpdateSeating(result.Seating);visibleOrders=list;
        var selected = selectedOrderId;
        rebindingOrders=true;
        try {
        orders.DataSource = list.Select(o => new OrderRow(o.Id, o.Reference, o.Status, o.Revision, o.Operator, o.ReceivedUtc.ToLocalTime().ToString("dd/MM HH:mm:ss"),
            o.Notes + Environment.NewLine + string.Join(Environment.NewLine, o.Items.Select(i => $"{i.Quantity:0.###} × {i.Description} — {i.Notes}")))).ToArray();
        UpdateOrderCards();
        foreach (string hidden in new[] { "Id", "Revision", "Details" }) if (orders.Columns[hidden] is { } column) column.Visible = false;
        foreach (DataGridViewRow row in orders.Rows) if ((row.DataBoundItem as OrderRow)?.Id == selected) { row.Selected = true; orders.CurrentCell = row.Cells.Cast<DataGridViewCell>().First(c => c.Visible); break; }
        } finally {rebindingOrders=false;}
        SyncDashboard();

        }
        if(InvokeRequired)Invoke((Action)ApplyResult);else ApplyResult();
    }
    private void UpdateOrderCards()
    {
        if(IsDisposed)return;
        if(InvokeRequired){Invoke((Action)UpdateOrderCards);return;}
        var selected=(orders.CurrentRow?.DataBoundItem as OrderRow)?.Id;
        foreach(Control card in orderCards.Controls.Cast<Control>().ToArray()){orderCards.Controls.Remove(card);card.Dispose();}
        var rows=orders.Rows.Cast<DataGridViewRow>().Select(r=>(Row:r,Value:r.DataBoundItem as OrderRow)).Where(r=>r.Value!=null&&r.Value.Status is not ("ENTREGUE" or "CANCELADO")).ToArray();
        premiumHeader.UpdateCounts(rows.Count(r=>r.Value!.Status=="AGUARDANDO"),rows.Count(r=>r.Value!.Status=="PREPARANDO"),rows.Count(r=>r.Value!.Status=="PRONTO"));
        foreach(var row in rows)
        {
            var value=row.Value!;
            var card=new Panel{Width=178,Height=88,Padding=new Padding(10),Margin=new Padding(8,5,8,5),Cursor=Cursors.Hand,Tag=value.Id};
            var title=new Label{Text=$"#{value.Id.ToString("N")[..4].ToUpperInvariant()}   {value.Referencia}",Bounds=new Rectangle(8,5,162,22),Font=new Font("Segoe UI",10,FontStyle.Bold)};
            var model=visibleOrders.Single(o=>o.Id==value.Id);
            var text=new Label{Text=value.Status+"   •   "+model.Items.Length+" itens",Bounds=new Rectangle(8,28,162,18),AutoEllipsis=true,Font=new Font("Segoe UI",8)};
            var age=DateTimeOffset.UtcNow-model.ReceivedUtc;
            var ageLabel=new Label{Tag=model.ReceivedUtc,Text=$"{(int)Math.Max(0,age.TotalMinutes):00}:{Math.Max(0,age.Seconds):00}",Name="ActiveOrderClock",Bounds=new Rectangle(8,48,162,18),TextAlign=ContentAlignment.MiddleRight,Font=new Font("Segoe UI",9,FontStyle.Bold)};card.Controls.Add(ageLabel);
            card.Controls.Add(text);card.Controls.Add(title);KitchenPremiumStyle.Apply(card);var accent=KitchenTableButton.StateColor(value.Status=="PRONTO"?"PRONTA":value.Status=="AGUARDANDO"?"OCUPADA":value.Status);title.ForeColor=accent;ageLabel.ForeColor=accent;card.Paint+=(_,e)=>{KitchenLighting.Surface(e.Graphics,new Rectangle(4,4,card.Width-9,card.Height-9),accent,false);KitchenLighting.Activity(e.Graphics,card.ClientRectangle,accent);};
            void Select(){if(send.Enabled&&items.Rows.Count>0){message.Text="Conclua o rascunho antes de selecionar outro pedido.";return;}dashboardDraft=false;orders.CurrentCell=row.Row.Cells.Cast<DataGridViewCell>().First(c=>c.Visible);row.Row.Selected=true;detail.Text=value.Details;message.Text="Pedido selecionado: "+value.Referencia+" • "+value.Status;SyncDashboard();}
            void Details(){using var dialog=new Form{Text="PEDIDO • "+value.Referencia,Size=new Size(680,480),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false};dialog.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=value.Referencia+Environment.NewLine+value.Status+Environment.NewLine+value.Details});KitchenPremiumStyle.Apply(dialog);dialog.ShowDialog(this);}
            card.DoubleClick+=(_,_)=>Details();text.DoubleClick+=(_,_)=>Details();title.DoubleClick+=(_,_)=>Details();
            card.Click+=(_,_)=>Select();title.Click+=(_,_)=>Select();text.Click+=(_,_)=>Select();orderCards.Controls.Add(card);
        }
        if(rows.Length==0)orderCards.Controls.Add(new Label{Text="Nenhum pedido em andamento. Selecione uma mesa para iniciar.",ForeColor=Color.Silver,Width=Math.Max(160,orderCards.ClientSize.Width-28),Height=64,TextAlign=ContentAlignment.MiddleCenter});
    }
    protected override void Dispose(bool disposing) { if (disposing) { retry.Dispose(); dashboardClock.Dispose(); dashboardOrder.Dispose(); tableWindow?.Dispose(); entryWindow?.Dispose(); catalog.Dispose(); mainCatalog.Dispose(); tables.Dispose(); } base.Dispose(disposing); }
}


