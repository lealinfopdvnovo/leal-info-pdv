using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace LealInfoPDV;

public sealed class DeliveryManagementForm : Form
{
    private readonly DataGridView _deliveries = Grid();
    private readonly DataGridView _drivers = Grid();
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };

    public DeliveryManagementForm()
    {
        Text = "LEAL INFO PDV - Motoboy e Entregas";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1180;
        Height = 720;
        MinimumSize = new Size(980, 620);
        BackColor = Color.FromArgb(224, 239, 248);
        Font = new Font("Segoe UI", 10);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var header = new Label
        {
            Text = "CENTRAL DE MOTOBOY E ENTREGAS",
            Dock = DockStyle.Top,
            Height = 72,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(4, 70, 112),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold)
        };

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var deliveryTab = new TabPage("ENTREGAS") { BackColor = BackColor, Padding = new Padding(8) };
        var driverTab = new TabPage("MOTOBOYS") { BackColor = BackColor, Padding = new Padding(8) };
        tabs.TabPages.Add(deliveryTab);
        tabs.TabPages.Add(driverTab);

        deliveryTab.Controls.Add(_deliveries);
        deliveryTab.Controls.Add(DeliveryBar());
        driverTab.Controls.Add(_drivers);
        driverTab.Controls.Add(DriverBar());
        Controls.Add(tabs);
        Controls.Add(header);

        _status.Items.AddRange(new[] { "TODAS", "AGUARDANDO", "EM ROTA", "ENTREGUE", "CANCELADA" });
        _status.SelectedIndex = 0;
        _status.SelectedIndexChanged += (_, _) => LoadDeliveries();
        LoadDrivers();
        LoadDeliveries();
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None
    };

    private static Button Button(string text, Color color, int width = 145)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 42,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(4)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private Control DeliveryBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(6), WrapContents = false };
        var add = Button("NOVA ENTREGA", Color.FromArgb(230, 95, 20));
        var edit = Button("EDITAR", Color.FromArgb(0, 125, 190), 110);
        var dispatch = Button("SAIU PARA ENTREGA", Color.FromArgb(185, 22, 38), 175);
        var delivered = Button("MARCAR ENTREGUE", Color.FromArgb(0, 145, 85), 165);
        var route = Button("ABRIR ROTA", Color.FromArgb(35, 105, 180), 135);
        var cancel = Button("CANCELAR", Color.FromArgb(100, 105, 112), 115);
        var filterLabel = new Label { Text = "Status:", AutoSize = true, Margin = new Padding(16, 13, 4, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        bar.Controls.AddRange(new Control[] { add, edit, dispatch, delivered, route, cancel, filterLabel, _status });
        add.Click += (_, _) => EditDelivery(null);
        edit.Click += (_, _) => { var id = SelectedId(_deliveries); if (id.HasValue) EditDelivery(id); };
        dispatch.Click += (_, _) => SetDeliveryStatus("EM ROTA");
        delivered.Click += (_, _) => SetDeliveryStatus("ENTREGUE");
        cancel.Click += (_, _) => SetDeliveryStatus("CANCELADA");
        route.Click += (_, _) => OpenRoute();
        return bar;
    }

    private Control DriverBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(6) };
        var add = Button("NOVO MOTOBOY", Color.FromArgb(230, 95, 20), 155);
        var edit = Button("EDITAR", Color.FromArgb(0, 125, 190), 120);
        var toggle = Button("ATIVAR / INATIVAR", Color.FromArgb(100, 105, 112), 170);
        bar.Controls.AddRange(new Control[] { add, edit, toggle });
        add.Click += (_, _) => EditDriver(null);
        edit.Click += (_, _) => { var id = SelectedId(_drivers); if (id.HasValue) EditDriver(id); };
        toggle.Click += (_, _) =>
        {
            var id = SelectedId(_drivers); if (!id.HasValue) return;
            Exec("UPDATE delivery_drivers SET active=CASE active WHEN 1 THEN 0 ELSE 1 END WHERE id=$id", ("$id", id.Value));
            LoadDrivers();
        };
        return bar;
    }

    private static long? SelectedId(DataGridView grid)
    {
        if (grid.CurrentRow == null) { MessageBox.Show("Selecione um registro."); return null; }
        return Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
    }

    private void LoadDeliveries()
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT d.id AS ID,d.created_at AS Criada,d.customer_name AS Cliente,d.customer_phone AS Telefone,
                   d.address AS Endereço,COALESCE(m.name,'NÃO DEFINIDO') AS Motoboy,d.status AS Status,
                   printf('R$ %.2f',d.amount) AS Pedido,printf('R$ %.2f',d.delivery_fee) AS Taxa,
                   d.payment AS Pagamento,d.departed_at AS Saída,d.delivered_at AS Entregue
            FROM deliveries d LEFT JOIN delivery_drivers m ON m.id=d.driver_id
            WHERE ($status='TODAS' OR d.status=$status) ORDER BY d.id DESC
            """;
        cmd.Parameters.AddWithValue("$status", _status.SelectedItem?.ToString() ?? "TODAS");
        using var rd = cmd.ExecuteReader();
        var table = new System.Data.DataTable();
        table.Load(rd);
        _deliveries.DataSource = table;
    }

    private void LoadDrivers()
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT id AS ID,name AS Nome,phone AS Telefone,vehicle AS Veículo,plate AS Placa,CASE active WHEN 1 THEN 'ATIVO' ELSE 'INATIVO' END AS Status FROM delivery_drivers ORDER BY name";
        using var rd = cmd.ExecuteReader();
        var table = new System.Data.DataTable();
        table.Load(rd);
        _drivers.DataSource = table;
    }

    private void EditDriver(long? id)
    {
        using var f = Dialog(id.HasValue ? "Editar Motoboy" : "Novo Motoboy", 500, 430);
        var name = Field("Nome");
        var phone = Field("Telefone");
        var vehicle = Field("Veículo");
        var plate = Field("Placa");
        var save = Button("SALVAR MOTOBOY", Color.FromArgb(0, 145, 85), 180);
        AddVertical(f, name.Panel, phone.Panel, vehicle.Panel, plate.Panel, save);
        if (id.HasValue)
        {
            using var cn = Database.Open(); using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT name,phone,vehicle,plate FROM delivery_drivers WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id.Value); using var rd = cmd.ExecuteReader();
            if (rd.Read()) { name.Box.Text=rd.GetString(0);phone.Box.Text=rd.IsDBNull(1)?"":rd.GetString(1);vehicle.Box.Text=rd.IsDBNull(2)?"":rd.GetString(2);plate.Box.Text=rd.IsDBNull(3)?"":rd.GetString(3); }
        }
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Box.Text)) { MessageBox.Show("Informe o nome."); return; }
            if (id.HasValue) Exec("UPDATE delivery_drivers SET name=$n,phone=$p,vehicle=$v,plate=$pl WHERE id=$id",("$n",name.Box.Text),("$p",phone.Box.Text),("$v",vehicle.Box.Text),("$pl",plate.Box.Text),("$id",id.Value));
            else Exec("INSERT INTO delivery_drivers(name,phone,vehicle,plate) VALUES($n,$p,$v,$pl)",("$n",name.Box.Text),("$p",phone.Box.Text),("$v",vehicle.Box.Text),("$pl",plate.Box.Text));
            f.DialogResult=DialogResult.OK;f.Close();
        };
        if (f.ShowDialog(this)==DialogResult.OK) { LoadDrivers(); LoadDeliveries(); }
    }

    private void EditDelivery(long? id)
    {
        using var f = Dialog(id.HasValue ? "Editar Entrega" : "Nova Entrega", 720, 690);
        var customer=Field("Cliente");var phone=Field("Telefone");var address=Field("Endereço completo");
        var reference=Field("Referência");var description=Field("Descrição do pedido");var amount=Field("Valor do pedido");
        var fee=Field("Taxa de entrega");var payment=Field("Pagamento");var notes=Field("Observações");
        var driver = new ComboBox { Dock=DockStyle.Bottom,Height=32,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Text",ValueMember="Value" };
        var driverPanel = Labeled("Motoboy", driver);
        LoadDriverCombo(driver);
        var save=Button("SALVAR ENTREGA",Color.FromArgb(0,145,85),190);
        AddVertical(f,customer.Panel,phone.Panel,address.Panel,reference.Panel,description.Panel,amount.Panel,fee.Panel,payment.Panel,driverPanel,notes.Panel,save);
        if(id.HasValue)
        {
            using var cn=Database.Open();using var cmd=cn.CreateCommand();
            cmd.CommandText="SELECT customer_name,customer_phone,address,reference,order_description,amount,delivery_fee,payment,driver_id,notes FROM deliveries WHERE id=$id";
            cmd.Parameters.AddWithValue("$id",id.Value);using var rd=cmd.ExecuteReader();
            if(rd.Read())
            {
                customer.Box.Text=rd.GetString(0);phone.Box.Text=rd.IsDBNull(1)?"":rd.GetString(1);address.Box.Text=rd.GetString(2);
                reference.Box.Text=rd.IsDBNull(3)?"":rd.GetString(3);description.Box.Text=rd.IsDBNull(4)?"":rd.GetString(4);
                amount.Box.Text=rd.GetDouble(5).ToString("0.00");fee.Box.Text=rd.GetDouble(6).ToString("0.00");
                payment.Box.Text=rd.IsDBNull(7)?"":rd.GetString(7);notes.Box.Text=rd.IsDBNull(9)?"":rd.GetString(9);
                if(!rd.IsDBNull(8)) driver.SelectedValue=rd.GetInt64(8);
            }
        }
        save.Click+=(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(customer.Box.Text)||string.IsNullOrWhiteSpace(address.Box.Text)){MessageBox.Show("Cliente e endereço são obrigatórios.");return;}
            var driverId=driver.SelectedValue is long value?(object)value:DBNull.Value;
            if(id.HasValue) Exec("""UPDATE deliveries SET customer_name=$c,customer_phone=$ph,address=$a,reference=$r,order_description=$d,amount=$v,delivery_fee=$f,payment=$p,driver_id=$m,notes=$n WHERE id=$id""",
                ("$c",customer.Box.Text),("$ph",phone.Box.Text),("$a",address.Box.Text),("$r",reference.Box.Text),("$d",description.Box.Text),("$v",Number(amount.Box.Text)),("$f",Number(fee.Box.Text)),("$p",payment.Box.Text),("$m",driverId),("$n",notes.Box.Text),("$id",id.Value));
            else Exec("""INSERT INTO deliveries(created_at,customer_name,customer_phone,address,reference,order_description,amount,delivery_fee,payment,driver_id,notes,operator) VALUES($dt,$c,$ph,$a,$r,$d,$v,$f,$p,$m,$n,$o)""",
                ("$dt",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),("$c",customer.Box.Text),("$ph",phone.Box.Text),("$a",address.Box.Text),("$r",reference.Box.Text),("$d",description.Box.Text),("$v",Number(amount.Box.Text)),("$f",Number(fee.Box.Text)),("$p",payment.Box.Text),("$m",driverId),("$n",notes.Box.Text),("$o",Auth.OperatorName));
            f.DialogResult=DialogResult.OK;f.Close();
        };
        if(f.ShowDialog(this)==DialogResult.OK)LoadDeliveries();
    }

    private void SetDeliveryStatus(string status)
    {
        var id=SelectedId(_deliveries);if(!id.HasValue)return;
        string extra=status=="EM ROTA"?",departed_at=$date":status=="ENTREGUE"?",delivered_at=$date":"";
        Exec($"UPDATE deliveries SET status=$status{extra} WHERE id=$id",("$status",status),("$date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),("$id",id.Value));
        LoadDeliveries();
    }

    private void OpenRoute()
    {
        var id=SelectedId(_deliveries);if(!id.HasValue)return;
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT address FROM deliveries WHERE id=$id";cmd.Parameters.AddWithValue("$id",id.Value);
        var address=Convert.ToString(cmd.ExecuteScalar())??"";
        var url="https://www.google.com/maps/dir/?api=1&destination="+Uri.EscapeDataString(address)+"&travelmode=driving";
        try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); }
        catch(Exception ex){MessageBox.Show("Não foi possível abrir o Google Maps.\n"+ex.Message);}
    }

    private void LoadDriverCombo(ComboBox combo)
    {
        var list=new List<Choice>{new(null,"NÃO DEFINIDO")};
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT id,name FROM delivery_drivers WHERE active=1 ORDER BY name";using var rd=cmd.ExecuteReader();
        while(rd.Read())list.Add(new Choice(rd.GetInt64(0),rd.GetString(1)));
        combo.DataSource=list;
    }

    private sealed record Choice(long? Value,string Text);
    private sealed record Input(Panel Panel,TextBox Box);
    private static Input Field(string label){var box=new TextBox{Dock=DockStyle.Bottom,Height=31};return new Input(Labeled(label,box),box);}
    private static Panel Labeled(string label,Control input){var p=new Panel{Dock=DockStyle.Top,Height=57,Padding=new Padding(0,2,0,3)};p.Controls.Add(input);p.Controls.Add(new Label{Text=label,Dock=DockStyle.Top,Height=21,Font=new Font("Segoe UI",9,FontStyle.Bold)});return p;}
    private static Form Dialog(string title,int width,int height)=>new(){Text=title,StartPosition=FormStartPosition.CenterParent,Width=width,Height=height,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248),AutoScroll=true};
    private static void AddVertical(Form form,params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(28)};foreach(var c in controls){c.Width=form.ClientSize.Width-80;p.Controls.Add(c);}form.Controls.Add(p);}
    private static double Number(string text)=>double.TryParse(text.Replace(".","").Replace(",","."),NumberStyles.Any,CultureInfo.InvariantCulture,out var v)?v:0;
    private static void Exec(string sql,params (string Name,object Value)[] args){using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText=sql;foreach(var a in args)cmd.Parameters.AddWithValue(a.Name,a.Value??DBNull.Value);cmd.ExecuteNonQuery();}
}
