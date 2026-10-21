namespace LealInfoPDV;

internal sealed class KitchenTablesView : UserControl
{
    private readonly TableLayoutPanel main = new() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Name = "MainTables" };
    private readonly Button more = new KitchenMoreTablesButton();
    private KitchenOrder[] active = Array.Empty<KitchenOrder>();
    private Form? floating;
    private FlowLayoutPanel? all;
    private int count = 40;
    private KitchenSeatingSnapshot seating = new(40, 1, Array.Empty<KitchenReservation>());
    private NumericUpDown? amount;
    private bool updatingAmount;
    private int selectedNumber;
    private string filter = "TODAS";
    private Label? selectedLabel;
    private FlowLayoutPanel? filters;
    private bool receivedOrders;
    internal long SeatingRevision => seating.Revision;
    internal event Action<int>? TableChosen;
    internal event Action<int>? TableActivated;
    internal event Action<int>? CapacityRequested;
    internal event Action<int, long, bool>? ReservationRequested;
    internal KitchenTablesView()
    {
        Name = "KitchenTables"; Dock = DockStyle.Top; Height = 300;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.Controls.Add(new Label { Text = "MESAS (10)   ● LIVRE   ● OCUPADA   ● PREPARANDO   ● PRONTA", Dock = DockStyle.Fill, Font=new Font("Segoe UI",10,FontStyle.Bold), TextAlign=ContentAlignment.MiddleLeft },0,0);root.SetColumnSpan(root.Controls[0],2);
        for (int i = 0; i < 5; i++) main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 2; i++) main.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.Controls.Add(main,0,1);root.Controls.Add(more,1,1);Controls.Add(root);
        more.Text = "VER MAIS\nMESAS"; more.Click += (_,_) => OpenAll();
        KitchenPremiumStyle.Apply(this);KitchenPremiumStyle.Frame(this);Render();
    }
    internal static int? TableNumber(string reference)
    {
        var match = System.Text.RegularExpressions.Regex.Match(reference.Trim(), @"^Mesa\s+(\d{1,4})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out int n) && n > 0 && n <= 1000 ? n : null;
    }
    internal static string State(IEnumerable<KitchenOrder> orders, int table)
    {
        var statuses = orders.Where(o => TableNumber(o.Reference) == table && o.Status!="CANCELADO"&&!o.Paid&&o.Items.Length>0).Select(o => o.Status).ToArray();
        if (statuses.Length == 0) return "LIVRE";
        if (statuses.Contains("AGUARDANDO")) return "OCUPADA";
        if (statuses.Contains("PREPARANDO")) return "PREPARANDO";
        if(statuses.Contains("PRONTO"))return "PRONTA";
        return "OCUPADA";
    }
    internal void UpdateOrders(IEnumerable<KitchenOrder> orders)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { var snapshot = orders.ToArray(); Invoke((Action)(() => UpdateOrders(snapshot))); return; }
        var next = orders.ToArray();
        if (receivedOrders && active.Select(o => (o.Id, o.Reference, o.Status)).SequenceEqual(next.Select(o => (o.Id, o.Reference, o.Status)))) { Invalidate(true); return; }
        receivedOrders = true; active = next;
        count = Math.Max(count, active.Select(o => TableNumber(o.Reference) ?? 0).DefaultIfEmpty().Max());
        Render();
    }
    internal void UpdateSeating(KitchenSeatingSnapshot next)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { Invoke((Action)(() => UpdateSeating(next))); return; }
        bool changed = seating.Count != next.Count || seating.Revision != next.Revision || !seating.Reservations.SequenceEqual(next.Reservations);
        seating = next; count = Math.Max(next.Count, active.Select(o=>TableNumber(o.Reference)??0).DefaultIfEmpty().Max());
        if (amount is { IsDisposed:false })
        {
            updatingAmount = true; amount.Value = count; amount.Enabled = true; updatingAmount = false;
        }
        if (changed) Render();
    }
    private string CurrentState(int number)
    {
        string state = State(active,number);
        return state == "LIVRE" && seating.Reservations.Any(r=>r.Number==number&&r.Reserved) ? "RESERVADA" : state;
    }
    private Button Table(int number)
    {
        string state = CurrentState(number);
        var age = active.Where(o=>TableNumber(o.Reference)==number&&!o.TableOnly&&o.Status is ("AGUARDANDO" or "PREPARANDO" or "PRONTO")).Select(o=>(DateTimeOffset?)o.ReceivedUtc).OrderBy(x=>x).FirstOrDefault();
        var button = new KitchenTableButton(number,state,age) { Dock=DockStyle.Fill };
        void Select()
        {
            selectedNumber=number;
            if(selectedLabel!=null)selectedLabel.Text=$"Mesa {number:00} • {CurrentState(number)}";
        }
        button.Click += (_,_) => { Select(); if(state!="RESERVADA")TableChosen?.Invoke(number); };
        button.DoubleClick += (_,_) => { Select(); if(CurrentState(number)!="RESERVADA")TableActivated?.Invoke(number); };
        var menu = new ContextMenuStrip();
        var reserve = menu.Items.Add("RESERVAR MESA");
        reserve.Click += (_,_)=>{Select();RequestReservation(true);};
        var release = menu.Items.Add("LIBERAR RESERVA");
        release.Click += (_,_)=>{Select();RequestReservation(false);};
        menu.Opening += (_,_)=>{ reserve.Enabled=CurrentState(number)=="LIVRE";release.Enabled=CurrentState(number)=="RESERVADA"; };
        button.ContextMenuStrip=menu;button.Disposed+=(_,_)=>menu.Dispose();return button;
    }
    private void RequestReservation(bool reserved)
    {
        if(selectedNumber<1){if(selectedLabel!=null)selectedLabel.Text="Selecione uma mesa (botão direito).";return;}
        long revision=seating.Reservations.SingleOrDefault(r=>r.Number==selectedNumber)?.Revision??0;
        ReservationRequested?.Invoke(selectedNumber,revision,reserved);
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
            for (int n = 1; n <= count; n++)
            {
                if(filter!="TODAS"&&CurrentState(n)!=filter)continue;
                var b=Table(n);b.Dock=DockStyle.None;b.Size=new Size(94,96);all.Controls.Add(b);
            }
            UpdateFilters();
            all.ResumeLayout(true); all.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        }
    }
    private void UpdateFilters()
    {
        if(filters==null)return;
        foreach(Button button in filters.Controls)
        {
            string value=(string)button.Tag!;
            int total=value=="TODAS"?count:Enumerable.Range(1,count).Count(n=>CurrentState(n)==value);
            button.Text=(value=="TODAS"?"TODAS":value)+" ("+total+")";
            button.ForeColor=value=="TODAS"?KitchenPremiumStyle.Gold:KitchenTableButton.StateColor(value);
        }
        if(floating!=null)floating.Text="TODAS AS MESAS ("+count+")";
        if(selectedLabel!=null&&selectedNumber>0)selectedLabel.Text=$"Mesa {selectedNumber:00} • {CurrentState(selectedNumber)}";
    }
    private void OpenAll()
    {
        if(floating is { IsDisposed:false }){floating.Activate();return;}
        var area=Screen.FromControl(this).WorkingArea;
        floating=new Form{Name="AllKitchenTables",Text="TODAS AS MESAS",StartPosition=FormStartPosition.CenterParent,Size=new Size(Math.Min(1100,area.Width-24),Math.Min(650,area.Height-24)),MinimumSize=new Size(Math.Min(640,area.Width),Math.Min(420,area.Height)),ShowInTaskbar=false,Font=new Font("Segoe UI",9)};
        var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,Padding=new Padding(10)};
        header.Controls.Add(new Label{Text="TODAS AS MESAS",AutoSize=true,Font=new Font("Segoe UI",16,FontStyle.Bold),Margin=new Padding(4,0,20,0)});
        header.Controls.Add(new Label{Text="Quantidade",AutoSize=true,Margin=new Padding(4,7,4,0)});
        amount=new NumericUpDown{Name="KitchenTableCount",Minimum=10,Maximum=1000,Increment=10,Value=count,Width=80};
        header.Controls.Add(amount);
        filters=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(8,0,8,0),WrapContents=false,AutoScroll=true};
        foreach(string value in new[]{"TODAS","LIVRE","OCUPADA","PREPARANDO","PRONTA","RESERVADA"})
        {
            var b=new Button{Name="KitchenTableFilter_"+value,Tag=value,Width=150,Height=38};
            b.Click+=(_,_)=>{filter=value;Render();};filters.Controls.Add(b);
        }
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,Padding=new Padding(8),WrapContents=false};
        selectedLabel=new Label{Name="SelectedKitchenTable",Text="Botão direito: reservar / liberar",Width=270,Height=38,TextAlign=ContentAlignment.MiddleLeft};
        var reserve=new Button{Name="ReserveKitchenTable",Text="RESERVAR MESA",Width=155,Height=38};
        var release=new Button{Name="ReleaseKitchenReservation",Text="LIBERAR RESERVA",Width=155,Height=38};
        var close=new Button{Text="FECHAR",Width=100,Height=38};close.Click+=(_,_)=>floating?.Close();
        reserve.Click+=(_,_)=>RequestReservation(true);release.Click+=(_,_)=>RequestReservation(false);
        footer.Controls.Add(selectedLabel);footer.Controls.Add(reserve);footer.Controls.Add(release);footer.Controls.Add(close);
        all=new FlowLayoutPanel{Name="AllKitchenTableCards",Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(10)};
        floating.Controls.Add(all);floating.Controls.Add(footer);floating.Controls.Add(filters);floating.Controls.Add(header);
        KitchenPremiumStyle.Apply(floating);KitchenPremiumStyle.Frame(floating);UpdateFilters();
        amount.ValueChanged+=(_,_)=>{if(updatingAmount)return;amount.Enabled=false;CapacityRequested?.Invoke((int)amount.Value);};
        floating.FormClosed+=(_,_)=>{all=null;floating=null;amount=null;filters=null;selectedLabel=null;filter="TODAS";};
        Render();floating.Show(FindForm());
    }
    protected override void Dispose(bool disposing) { if (disposing) floating?.Dispose(); base.Dispose(disposing); }
}
