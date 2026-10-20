namespace LealInfoPDV;

internal sealed class KitchenTablesView : UserControl
{
    private readonly TableLayoutPanel main = new() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Name = "MainTables" };
    private readonly Button more = new() { Name = "MoreKitchenTables", Text = "VER MAIS MESAS", Dock = DockStyle.Fill };
    private KitchenOrder[] active = Array.Empty<KitchenOrder>();
    private Form? floating;
    private FlowLayoutPanel? all;
    private int count = 40;
    private bool receivedOrders;
    internal event Action<int>? TableChosen;
    internal KitchenTablesView()
    {
        Name = "KitchenTables"; Dock = DockStyle.Top; Height = 248;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        for (int i = 0; i < 5; i++) main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 2; i++) main.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.Controls.Add(new Label { Text = "MESAS  •  Livre  /  Ocupada  /  Preparando  /  Pronta", Dock = DockStyle.Fill }, 0, 0);
        root.Controls.Add(main, 0, 1); root.Controls.Add(more, 0, 2); Controls.Add(root);
        more.Click += (_, _) => OpenAll(); KitchenPremiumStyle.Apply(this); Render();
    }
    internal static int? TableNumber(string reference)
    {
        var match = System.Text.RegularExpressions.Regex.Match(reference.Trim(), @"^Mesa\s+(\d{1,4})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out int n) && n > 0 && n <= 1000 ? n : null;
    }
    internal static string State(IEnumerable<KitchenOrder> orders, int table)
    {
        var statuses = orders.Where(o => TableNumber(o.Reference) == table && o.Status is not ("ENTREGUE" or "CANCELADO")).Select(o => o.Status).ToArray();
        if (statuses.Length == 0) return "LIVRE";
        if (statuses.Contains("AGUARDANDO")) return "OCUPADA";
        if (statuses.Contains("PREPARANDO")) return "PREPARANDO";
        return "PRONTA";
    }
    internal void UpdateOrders(IEnumerable<KitchenOrder> orders)
    {
        var next = orders.ToArray();
        if (receivedOrders && active.Select(o => (o.Id, o.Reference, o.Status)).SequenceEqual(next.Select(o => (o.Id, o.Reference, o.Status)))) return;
        receivedOrders = true; active = next;
        count = Math.Max(count, active.Select(o => TableNumber(o.Reference) ?? 0).DefaultIfEmpty().Max());
        Render();
    }
    private Button Table(int number)
    {
        string state = State(active, number);
        var button = new Button { Name = "KitchenTable_" + number, Text = $"MESA {number:00}\n{state}", Dock = DockStyle.Fill, Margin = new Padding(4), AccessibleName = $"Mesa {number}, {state}" };
        KitchenPremiumStyle.Apply(button);
        button.BackColor = state switch { "LIVRE" => Color.FromArgb(22, 103, 65), "OCUPADA" => Color.FromArgb(160, 53, 42), "PREPARANDO" => Color.FromArgb(148, 102, 15), _ => Color.FromArgb(19, 101, 155) };
        button.Click += (_, _) => TableChosen?.Invoke(number); return button;
    }
    private static void Clear(Control container)
    {
        foreach (Control c in container.Controls.Cast<Control>().ToArray()) { container.Controls.Remove(c); c.Dispose(); }
    }
    private void Render()
    {
        main.SuspendLayout(); Clear(main);
        for (int n = 1; n <= 10; n++) main.Controls.Add(Table(n), (n - 1) % 5, (n - 1) / 5);
        main.ResumeLayout(true);
        if (all is { IsDisposed: false })
        {
            var scroll = all.AutoScrollPosition;
            all.SuspendLayout(); Clear(all);
            for (int n = 1; n <= count; n++) { var b = Table(n); b.Dock = DockStyle.None; b.Size = new Size(142, 76); all.Controls.Add(b); }
            all.ResumeLayout(true); all.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        }
    }
    private void OpenAll()
    {
        if (floating is { IsDisposed: false }) { floating.Activate(); return; }
        floating = new Form { Name = "AllKitchenTables", Text = "TODAS AS MESAS", StartPosition = FormStartPosition.CenterParent, Size = new Size(850, 620), MinimumSize = new Size(520, 400), ShowInTaskbar = false };
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8) };
        var amount = new NumericUpDown { Name = "KitchenTableCount", Minimum = 10, Maximum = 1000, Increment = 10, Value = count, Width = 95 };
        header.Controls.Add(new Label { Text = "Quantidade de mesas", AutoSize = true, Margin = new Padding(6, 7, 12, 0) }); header.Controls.Add(amount);
        all = new FlowLayoutPanel { Name = "AllKitchenTableCards", Dock = DockStyle.Fill, AutoScroll = true };
        floating.Controls.Add(all); floating.Controls.Add(header); KitchenPremiumStyle.Apply(floating);
        amount.ValueChanged += (_, _) =>
        {
            int highest = active.Where(o => o.Status is not ("ENTREGUE" or "CANCELADO")).Select(o => TableNumber(o.Reference) ?? 0).DefaultIfEmpty().Max();
            count = Math.Max((int)amount.Value, highest);
            if (amount.Value != count) amount.Value = count;
            Render();
        };
        floating.FormClosed += (_, _) => { all = null; floating = null; };
        Render(); floating.Show(FindForm());
    }
    protected override void Dispose(bool disposing) { if (disposing) floating?.Dispose(); base.Dispose(disposing); }
}
