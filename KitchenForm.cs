using LealInfoPDV.Network;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

// Stage 1 entry and consultation only. Sending a kitchen order never finalizes a sale.
internal sealed class KitchenForm : Form
{
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
    private readonly Button fresh = new() { Text = "NOVO PEDIDO", AutoSize = true };
    internal KitchenForm()
    {
        Text = "COZINHA — PEDIDOS / ETAPA 1"; Name = "KitchenForm";
        StartPosition = FormStartPosition.CenterParent; Size = new Size(1080, 720); MinimumSize = new Size(720, 520);
        var config = NetworkConfiguration.Current;
        string endpoint = config.Mode == "terminal" ? "network:" + config.CertificateHash + ":" + config.PairingSecret : "local:" + Database.DbPath;
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
        string folder = Path.Combine(Database.AppFolder, "KitchenOutbox", key); outbox = new(folder); bindingPath = Path.Combine(folder, "binding");
        if (File.Exists(bindingPath)) storeId = ProtectedFile.Read<string>(bindingPath);
        var tabs = new TabControl { Dock = DockStyle.Fill }; var entry = new TabPage("NOVO PEDIDO"); var received = new TabPage("PEDIDOS REGISTRADOS");
        tabs.TabPages.AddRange(new[] { entry, received });
        items.Columns.Add("Description", "Item"); items.Columns.Add("Quantity", "Quantidade"); items.Columns.Add("Notes", "Observações do item");
        var entryButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true }; entryButtons.Controls.AddRange(new Control[] { fresh, send });
        entry.Controls.Add(items); entry.Controls.Add(notes); entry.Controls.Add(reference); entry.Controls.Add(entryHint); entry.Controls.Add(entryButtons);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        foreach (string status in new[] { "PREPARANDO", "PRONTO", "ENTREGUE", "CANCELADO" })
        {
            var button = new Button { Text = status, AutoSize = true }; buttons.Controls.Add(button);
            button.Click += async (_, _) => await Run(async () =>
            {
                if (!Auth.HasPermission("sales")) throw new InvalidOperationException("Sem permissão de operação.");
                if (orders.CurrentRow?.DataBoundItem is not OrderRow selected) throw new InvalidOperationException("Selecione um pedido.");
                if (status == "CANCELADO" && MessageBox.Show(this, "Cancelar somente este pedido da cozinha? Isso não cancela vendas nem estorna pagamentos.", "Cozinha", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                string actor = Auth.OperatorName;
                await Task.Run(() => store.ChangeStatus(selected.Id, selected.Revision, status, actor, Guid.NewGuid()));
                await RefreshOrders();
            });
        }
        var refresh = new Button { Text = "ATUALIZAR / TENTAR PENDÊNCIAS", AutoSize = true }; buttons.Controls.Add(refresh);
        refresh.Click += async (_, _) => await RefreshAndRetry();
        received.Controls.Add(orders); received.Controls.Add(detail); received.Controls.Add(buttons);
        Controls.Add(tabs); Controls.Add(message);
        orders.SelectionChanged += (_, _) =>
        {
            if (orders.CurrentRow?.DataBoundItem is OrderRow row)
                detail.Text = row.Details;
        };
        fresh.Click += async (_, _) => await Run(() =>
        {
            tabs.SelectedTab = entry;
            items.CancelEdit();
            reference.Clear(); notes.Clear(); items.Rows.Clear();
            SetEntryEnabled(true);
            int row = items.Rows.Add("", "1", "");
            items.CurrentCell = items.Rows[row].Cells[0];
            items.Focus(); items.BeginEdit(true);
            entryHint.Text = "Novo pedido iniciado. Digite o item e a quantidade; depois clique em ENVIAR PEDIDO À COZINHA.";
            return Task.CompletedTask;
        });
        send.Click += async (_, _) => await Run(async () =>
        {
            if (storeId == null) throw new InvalidOperationException("Conecte uma vez à cozinha preparada no servidor antes de enviar.");
            if (!Auth.CanSell) throw new InvalidOperationException("Entre no PDV antes de enviar.");
            items.EndEdit();
            var rows = items.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new KitchenItem(Convert.ToString(r.Cells[0].Value) ?? "", KitchenDraft.Quantity(Convert.ToString(r.Cells[1].Value) ?? ""), Convert.ToString(r.Cells[2].Value) ?? "")).ToArray();
            var draft = new KitchenDraft(Guid.NewGuid(), reference.Text.Trim(), notes.Text.Trim(), Auth.OperatorName, DateTimeOffset.UtcNow, rows);
            outbox.Queue(storeId, draft); SetEntryEnabled(false);
            message.Text = "Pedido guardado na fila local. Aguardando confirmação do servidor.";
            await FlushAndRefresh();
        });
        Shown += async (_, _) => { await RefreshAndRetry(); if (!IsDisposed) retry.Start(); };
        retry.Tick += async (_, _) => await RefreshAndRetry();
    }
    private void SetEntryEnabled(bool value) { send.Enabled = value; items.ReadOnly = !value; reference.ReadOnly = notes.ReadOnly = !value; }
    private async Task Run(Func<Task> action)
    {
        if (!await gate.WaitAsync(0)) return;
        fresh.Enabled = false;
        try { await action(); }
        catch (Exception e) { if (!IsDisposed) message.Text = e.Message + " • Pendências preservadas; a tentativa será repetida ao reconectar."; }
        finally { if (!IsDisposed) fresh.Enabled = true; gate.Release(); }
    }
    private Task RefreshAndRetry() => Run(async () =>
    {
        string actual = await Task.Run(store.EnsureReady);
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
        var list = await Task.Run(store.List); if (IsDisposed) return;
        var selected = (orders.CurrentRow?.DataBoundItem as OrderRow)?.Id;
        orders.DataSource = list.Select(o => new OrderRow(o.Id, o.Reference, o.Status, o.Revision, o.Operator, o.ReceivedUtc.ToLocalTime().ToString("dd/MM HH:mm:ss"),
            o.Notes + Environment.NewLine + string.Join(Environment.NewLine, o.Items.Select(i => $"{i.Quantity:0.###} × {i.Description} — {i.Notes}")))).ToArray();
        foreach (string hidden in new[] { "Id", "Revision", "Details" }) if (orders.Columns[hidden] is { } column) column.Visible = false;
        foreach (DataGridViewRow row in orders.Rows) if ((row.DataBoundItem as OrderRow)?.Id == selected) { row.Selected = true; orders.CurrentCell = row.Cells.Cast<DataGridViewCell>().First(c => c.Visible); break; }
    }
    protected override void Dispose(bool disposing) { if (disposing) retry.Dispose(); base.Dispose(disposing); }
}
