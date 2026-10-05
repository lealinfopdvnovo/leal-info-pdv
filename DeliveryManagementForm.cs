using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoPDV;

public sealed class DeliveryManagementForm : Form
{
    private const string TrackingPage = "https://novo-91da7436.web.app/track.html?t=";
    private const string TrackingAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly DataGridView _deliveries = Grid();
    private readonly DataGridView _drivers = Grid();
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private Action<string,string,MessageBoxIcon> _driverMessage = (text,title,icon) => MessageBox.Show(text,title,MessageBoxButtons.OK,icon);

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

        EnsureTrackingSchema();
        _status.Items.AddRange(new[] { "TODAS", "AGUARDANDO", "EM ROTA", "ENTREGUE", "CANCELADA" });
        _status.SelectedIndex = 0;
        _status.SelectedIndexChanged += (_, _) => LoadDeliveries();
        LoadDrivers();
        LoadDeliveries();
    }

    private static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            e.Cancel = false;
        };
        return grid;
    }

    private static void BindTextGrid(DataGridView grid, System.Data.DataTable table)
    {
        grid.DataSource = null;
        grid.Columns.Clear();
        foreach (System.Data.DataColumn column in table.Columns)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = column.ColumnName,
                HeaderText = column.ColumnName,
                DataPropertyName = column.ColumnName,
                ValueType = typeof(string),
                SortMode = DataGridViewColumnSortMode.Automatic
            });
        }
        grid.DataSource = table;
    }

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
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 104, Padding = new Padding(6), WrapContents = true };
        var add = Button("NOVA ENTREGA", Color.FromArgb(230, 95, 20));
        var edit = Button("EDITAR", Color.FromArgb(0, 125, 190), 110);
        var dispatch = Button("SAIU PARA ENTREGA", Color.FromArgb(185, 22, 38), 175);
        var delivered = Button("MARCAR ENTREGUE", Color.FromArgb(0, 145, 85), 165);
        var route = Button("ABRIR ROTA", Color.FromArgb(35, 105, 180), 135);
        var sendDriver = Button("ENVIAR CÓDIGO AO MOTOBOY", Color.FromArgb(96, 62, 150), 190);
        var sendCustomer = Button("ENVIAR LINK AO CLIENTE", Color.FromArgb(0, 145, 85), 180);
        var delete = Button("EXCLUIR PEDIDO", Color.FromArgb(185, 22, 38), 150);
        var cancel = Button("CANCELAR", Color.FromArgb(100, 105, 112), 115);
        var filterLabel = new Label { Text = "Status:", AutoSize = true, Margin = new Padding(16, 13, 4, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        bar.Controls.AddRange(new Control[] { add, edit, dispatch, delivered, route, sendDriver, sendCustomer, cancel, delete, filterLabel, _status });
        add.Click += (_, _) => EditDelivery(null);
        edit.Click += (_, _) => { var id = SelectedId(_deliveries); if (id.HasValue) EditDelivery(id); };
        dispatch.Click += (_, _) => SetDeliveryStatus("EM ROTA");
        delivered.Click += (_, _) => SetDeliveryStatus("ENTREGUE");
        cancel.Click += (_, _) => SetDeliveryStatus("CANCELADA");
        delete.Click += (_, _) => DeleteSelectedDelivery();
        route.Click += (_, _) => OpenRoute();
        sendDriver.Click += async (_, _) => await SendDriverCodeAsync();
        sendCustomer.Click += async (_, _) => await SendCustomerTrackingLinkAsync();
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


    private static void EnsureTrackingSchema()
    {
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="CREATE TABLE IF NOT EXISTS delivery_tracking(delivery_id INTEGER PRIMARY KEY,code TEXT NOT NULL UNIQUE,created_at TEXT NOT NULL,FOREIGN KEY(delivery_id) REFERENCES deliveries(id) ON DELETE CASCADE)";
        cmd.ExecuteNonQuery();
    }

    private static string NewTrackingCode()
    {
        var chars=new char[12];
        for(var i=0;i<chars.Length;i++)chars[i]=TrackingAlphabet[RandomNumberGenerator.GetInt32(TrackingAlphabet.Length)];
        return new string(chars);
    }

    private static string EnsureTrackingCode(long deliveryId)
    {
        using var cn=Database.Open();using var read=cn.CreateCommand();
        read.CommandText="SELECT code FROM delivery_tracking WHERE delivery_id=$id";read.Parameters.AddWithValue("$id",deliveryId);
        var current=Convert.ToString(read.ExecuteScalar());if(!string.IsNullOrWhiteSpace(current))return current;
        for(var attempt=0;attempt<5;attempt++)
        {
            var code=NewTrackingCode();using var add=cn.CreateCommand();
            add.CommandText="INSERT OR IGNORE INTO delivery_tracking(delivery_id,code,created_at) VALUES($id,$code,$created)";
            add.Parameters.AddWithValue("$id",deliveryId);add.Parameters.AddWithValue("$code",code);add.Parameters.AddWithValue("$created",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            add.ExecuteNonQuery();
            using var confirm=cn.CreateCommand();confirm.CommandText="SELECT code FROM delivery_tracking WHERE delivery_id=$id";confirm.Parameters.AddWithValue("$id",deliveryId);
            current=Convert.ToString(confirm.ExecuteScalar());if(!string.IsNullOrWhiteSpace(current))return current;
        }
        throw new InvalidOperationException("Não foi possível gerar o código desta entrega.");
    }

    private static long InsertDeliveryWithTracking(string createdAt,string customer,string phone,string address,string reference,string description,double amount,double fee,string payment,object driverId,string notes,string operatorName)
    {
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="INSERT INTO deliveries(created_at,customer_name,customer_phone,address,reference,order_description,amount,delivery_fee,payment,driver_id,notes,operator) VALUES($dt,$c,$ph,$a,$r,$d,$v,$f,$p,$m,$n,$o)";
        cmd.Parameters.AddWithValue("$dt",createdAt);cmd.Parameters.AddWithValue("$c",customer);cmd.Parameters.AddWithValue("$ph",phone);cmd.Parameters.AddWithValue("$a",address);cmd.Parameters.AddWithValue("$r",reference);cmd.Parameters.AddWithValue("$d",description);cmd.Parameters.AddWithValue("$v",amount);cmd.Parameters.AddWithValue("$f",fee);cmd.Parameters.AddWithValue("$p",payment);cmd.Parameters.AddWithValue("$m",driverId);cmd.Parameters.AddWithValue("$n",notes);cmd.Parameters.AddWithValue("$o",operatorName);
        cmd.ExecuteNonQuery();
        using var idCommand=cn.CreateCommand();idCommand.CommandText="SELECT last_insert_rowid()";var id=Convert.ToInt64(idCommand.ExecuteScalar());
        EnsureTrackingCode(id);return id;
    }

    private sealed record DeliveryShare(string Customer,string CustomerPhone,string Address,string DriverPhone,string Code,long? DriverId);

    private static DeliveryShare? ReadDeliveryShare(long id)
    {
        var code=EnsureTrackingCode(id);using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT d.customer_name,COALESCE(d.customer_phone,''),d.address,COALESCE(m.phone,''),d.driver_id FROM deliveries d LEFT JOIN delivery_drivers m ON m.id=d.driver_id WHERE d.id=$id";
        cmd.Parameters.AddWithValue("$id",id);using var rd=cmd.ExecuteReader();if(!rd.Read())return null;
        return new DeliveryShare(rd.GetString(0),rd.GetString(1),rd.GetString(2),rd.GetString(3),code,rd.IsDBNull(4)?null:rd.GetInt64(4));
    }

    private async Task SendDriverCodeAsync()
    {
        var id=SelectedId(_deliveries);if(!id.HasValue)return;var data=ReadDeliveryShare(id.Value);if(data==null)return;
        if(!data.DriverId.HasValue){_driverMessage("Selecione um motoboy para esta entrega.","Telefone do motoboy",MessageBoxIcon.Information);return;}
        if(string.IsNullOrWhiteSpace(data.DriverPhone)){_driverMessage("O motoboy selecionado não possui telefone cadastrado.","Telefone do motoboy",MessageBoxIcon.Information);return;}
        var message=$"Nova entrega da LEAL INFO PDV\nCliente: {data.Customer}\nEndereço: {data.Address}\nCódigo para iniciar o rastreamento: {data.Code}";
        try
        {
            await FirebaseDeliverySync.PublishAsync(this, data.Code, data.Address);
            OpenWhatsApp(data.DriverPhone,message);
        }
        catch(Exception ex) { _driverMessage(ex.Message,"Envio ao SpeedFood",MessageBoxIcon.Warning); }
    }

    private async Task SendCustomerTrackingLinkAsync()
    {
        var id=SelectedId(_deliveries);if(!id.HasValue)return;var data=ReadDeliveryShare(id.Value);if(data==null)return;
        if(string.IsNullOrWhiteSpace(data.CustomerPhone)){MessageBox.Show("Cadastre o telefone do cliente nesta entrega.","Telefone do cliente",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        try
        {
            var token = await FirebaseDeliverySync.ReadTrackingTokenAsync(this, data.Code);
            if(string.IsNullOrWhiteSpace(token)){MessageBox.Show("O motoboy ainda não iniciou esta entrega no aplicativo. Envie o código ao motoboy e tente novamente.","Rastreamento aguardando",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            var link=TrackingPage+Uri.EscapeDataString(token);var message=$"Olá, {data.Customer}! Acompanhe em tempo real a entrega do seu pedido: {link}";OpenWhatsApp(data.CustomerPhone,message);
        }
        catch(Exception ex){MessageBox.Show("Não foi possível preparar o link agora.\n\n"+ex.Message,"Rastreamento da entrega",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private static string BuildWhatsAppUrl(string phone,string message)
    {
        var digits=new string(phone.Where(char.IsDigit).ToArray());if(digits.Length is 10 or 11)digits="55"+digits;
        return "https://wa.me/"+digits+"?text="+Uri.EscapeDataString(message);
    }

    private static void OpenWhatsApp(string phone,string message)
    {
        var url=BuildWhatsAppUrl(phone,message);
        try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
        catch(Exception ex){MessageBox.Show("Não foi possível abrir o WhatsApp.\n"+ex.Message);}
    }

    private void LoadDeliveries()
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT d.id AS ID,d.created_at AS Criada,COALESCE(t.code,'') AS Código,d.customer_name AS Cliente,d.customer_phone AS Telefone,
                   d.address AS Endereço,COALESCE(m.name,'NÃO DEFINIDO') AS Motoboy,d.status AS Status,
                   printf('R$ %.2f',d.amount) AS Pedido,printf('R$ %.2f',d.delivery_fee) AS Taxa,
                   d.payment AS Pagamento,d.departed_at AS Saída,d.delivered_at AS Entregue
            FROM deliveries d LEFT JOIN delivery_drivers m ON m.id=d.driver_id
            LEFT JOIN delivery_tracking t ON t.delivery_id=d.id
            WHERE ($status='TODAS' OR d.status=$status) ORDER BY d.id DESC
            """;
        cmd.Parameters.AddWithValue("$status", _status.SelectedItem?.ToString() ?? "TODAS");
        using var rd = cmd.ExecuteReader();
        var table = new System.Data.DataTable();
        table.Load(rd);
        BindTextGrid(_deliveries, table);
    }

    private void LoadDrivers()
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT id AS ID,name AS Nome,phone AS Telefone,vehicle AS Veículo,plate AS Placa,CASE active WHEN 1 THEN 'ATIVO' ELSE 'INATIVO' END AS Status FROM delivery_drivers ORDER BY name";
        using var rd = cmd.ExecuteReader();
        var table = new System.Data.DataTable();
        table.Load(rd);
        BindTextGrid(_drivers, table);
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
        var cep=Field("CEP");var street=Field("Rua/Logradouro");var number=Field("Número");
        var complement=Field("Complemento");var district=Field("Bairro");var city=Field("Cidade");var uf=Field("UF");
        cep.Box.MaxLength=9;uf.Box.MaxLength=2;uf.Box.CharacterCasing=CharacterCasing.Upper;
        var reference=Field("Referência");var description=Field("Descrição do pedido");var amount=Field("Valor do pedido");
        var fee=Field("Taxa de entrega");var payment=Field("Pagamento");var notes=Field("Observações");
        var driver = new ComboBox { Dock=DockStyle.Bottom,Height=32,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Text",ValueMember="Value" };
        var driverPanel = Labeled("Motoboy", driver);
        LoadDriverCombo(driver,id);
        var save=Button("SALVAR ENTREGA",Color.FromArgb(0,145,85),190);
        if(id.HasValue)
            AddVertical(f,customer.Panel,phone.Panel,address.Panel,reference.Panel,description.Panel,amount.Panel,fee.Panel,payment.Panel,driverPanel,notes.Panel,save);
        else
            AddVertical(f,customer.Panel,phone.Panel,AddressRow(cep.Panel,street.Panel),AddressRow(number.Panel,complement.Panel),AddressRow(district.Panel,city.Panel,uf.Panel),reference.Panel,description.Panel,amount.Panel,fee.Panel,payment.Panel,driverPanel,notes.Panel,save);
        var cepStatus=new Label{AutoSize=false,Height=22,ForeColor=Color.FromArgb(4,70,112)};
        if(!id.HasValue){cep.Panel.Parent!.Height=79;cepStatus.Dock=DockStyle.Bottom;cep.Panel.Controls.Add(cepStatus);cepStatus.BringToFront();}
        CancellationTokenSource? lookup=null;
        var generation=0;
        string[]? lastAutomatic=null;
        cep.Box.TextChanged+=async (_,_)=>
        {
            var revision=++generation;
            lookup?.Cancel();
            var fields=new[]{street.Box,district.Box,city.Box,uf.Box};
            if(lastAutomatic!=null)
                for(var i=0;i<fields.Length;i++)
                    if(fields[i].Text==lastAutomatic[i])fields[i].Clear();
            lastAutomatic=null;
            var valuesBefore=fields.Select(b=>b.Text).ToArray();
            var digits=DeliveryAddress.NormalizeCep(cep.Box.Text);
            cepStatus.Text="";
            if(digits.Length!=8)return;
            var request=new CancellationTokenSource();lookup=request;
            try
            {
                await Task.Delay(350,request.Token);
                cepStatus.Text="Consultando CEP...";
                var result=await DeliveryAddress.LookupAsync(digits,request.Token);
                if(f.IsDisposed||revision!=generation)return;
                var values=new[]{result.Street,result.District,result.City,result.Uf};
                for(var i=0;i<fields.Length;i++)
                    if(fields[i].Text==valuesBefore[i])fields[i].Text=values[i];
                lastAutomatic=values;
                cepStatus.Text="CEP encontrado";
            }
            catch(OperationCanceledException){}
            catch(Exception)
            {
                if(!f.IsDisposed&&revision==generation)
                    cepStatus.Text="Preencha manualmente";
            }
        };
        f.FormClosed+=(_,_)=>{generation++;lookup?.Cancel();};
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
                if(!rd.IsDBNull(8))
                {
                    var linkedId=rd.GetInt64(8);
                    for(var i=0;i<driver.Items.Count;i++)
                        if(driver.Items[i] is Choice choice&&choice.Value==linkedId){driver.SelectedIndex=i;break;}
                }
            }
        }
        save.Click+=async (_,_)=>
        {
            if(!id.HasValue)
            {
                try{address.Box.Text=DeliveryAddress.Compose(cep.Box.Text,street.Box.Text,number.Box.Text,district.Box.Text,city.Box.Text,uf.Box.Text);}
                catch(ArgumentException ex){MessageBox.Show(f,ex.Message,"Endereço da entrega",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            }
            if(string.IsNullOrWhiteSpace(customer.Box.Text)||string.IsNullOrWhiteSpace(address.Box.Text)){MessageBox.Show("Cliente e endereço são obrigatórios.");return;}
            var driverId=driver.SelectedItem is Choice { Value: long value }?(object)value:DBNull.Value;
            if(id.HasValue) Exec("""UPDATE deliveries SET customer_name=$c,customer_phone=$ph,address=$a,reference=$r,order_description=$d,amount=$v,delivery_fee=$f,payment=$p,driver_id=$m,notes=$n WHERE id=$id""",
                ("$c",customer.Box.Text),("$ph",phone.Box.Text),("$a",address.Box.Text),("$r",reference.Box.Text),("$d",description.Box.Text),("$v",Number(amount.Box.Text)),("$f",Number(fee.Box.Text)),("$p",payment.Box.Text),("$m",driverId),("$n",notes.Box.Text),("$id",id.Value));
            else id = InsertDeliveryWithTracking(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),customer.Box.Text,phone.Box.Text,address.Box.Text,DeliveryAddress.Additional(complement.Box.Text,reference.Box.Text),description.Box.Text,Number(amount.Box.Text),Number(fee.Box.Text),payment.Box.Text,driverId,notes.Box.Text,Auth.OperatorName);
            save.Enabled = false;
            try
            {
                var delivery = ReadDeliveryShare(id!.Value);
                if (delivery != null) await FirebaseDeliverySync.PublishAsync(f, delivery.Code, delivery.Address);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Entrega salva no PDV, mas ainda não enviada ao SpeedFood.\n" + ex.Message + "\nUse Enviar código ao motoboy para tentar novamente.","Envio pendente",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            f.DialogResult=DialogResult.OK;f.Close();
        };
        if(f.ShowDialog(this)==DialogResult.OK)LoadDeliveries();
    }

    private void DeleteSelectedDelivery()
    {
        var id = SelectedId(_deliveries);
        if (!id.HasValue) return;
        var code = Convert.ToString(_deliveries.CurrentRow!.Cells["Código"].Value) ?? "";
        if (MessageBox.Show(this, "Deseja realmente excluir este pedido?", "Excluir pedido",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            DeleteLocalDelivery(id.Value, code);
            LoadDeliveries();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível excluir este pedido.\n" + ex.Message,
                "Excluir pedido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Match both immutable identifiers within one transaction; never contacts Firebase.
    internal static void DeleteLocalDelivery(long id, string code)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Selecione um pedido com código válido.");
        using var cn = Database.Open();
        using var transaction = cn.BeginTransaction();
        using var delete = cn.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM deliveries WHERE id=$id AND EXISTS(SELECT 1 FROM delivery_tracking WHERE delivery_id=$id AND code=$code)";
        delete.Parameters.AddWithValue("$id", id);
        delete.Parameters.AddWithValue("$code", code);
        if (delete.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("O pedido selecionado mudou ou já foi excluído. Atualize a lista.");
        using var tracking = cn.CreateCommand();
        tracking.Transaction = transaction;
        tracking.CommandText = "DELETE FROM delivery_tracking WHERE delivery_id=$id AND code=$code";
        tracking.Parameters.AddWithValue("$id", id);
        tracking.Parameters.AddWithValue("$code", code);
        tracking.ExecuteNonQuery();
        transaction.Commit();
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
        var destination=Convert.ToString(cmd.ExecuteScalar())??"";

        using var company=cn.CreateCommand();
        company.CommandText="""
            SELECT
                COALESCE((SELECT value FROM settings WHERE key='company_address'),''),
                COALESCE((SELECT value FROM settings WHERE key='company_city_state'),'')
            """;
        using var companyReader=company.ExecuteReader();
        string origin="";
        if(companyReader.Read())
            origin=string.Join(", ",new[]{companyReader.GetString(0),companyReader.GetString(1)}
                .Where(x=>!string.IsNullOrWhiteSpace(x)));

        if(string.IsNullOrWhiteSpace(origin))
        {
            MessageBox.Show(
                "O endereço da empresa ainda não foi informado.\n\nCadastre-o em Configurações > Dados da Empresa.",
                "Endereço de origem",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var url="https://www.google.com/maps/dir/?api=1&origin="+Uri.EscapeDataString(origin)
            +"&destination="+Uri.EscapeDataString(destination)+"&travelmode=driving";
        try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); }
        catch(Exception ex){MessageBox.Show("Não foi possível abrir o Google Maps.\n"+ex.Message);}
    }

    private void LoadDriverCombo(ComboBox combo,long? deliveryId)
    {
        var list=new List<Choice>{new(null,"NÃO DEFINIDO")};
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT id,name FROM delivery_drivers WHERE active=1 OR id=(SELECT driver_id FROM deliveries WHERE id=$delivery) ORDER BY name";
        cmd.Parameters.AddWithValue("$delivery",(object?)deliveryId??DBNull.Value);using var rd=cmd.ExecuteReader();
        while(rd.Read())list.Add(new Choice(rd.GetInt64(0),rd.GetString(1)));
        combo.Items.AddRange(list.Cast<object>().ToArray());
        combo.SelectedIndex=0;
    }

    private static Panel AddressRow(params Panel[] fields)
    {
        var row=new TableLayoutPanel{Height=fields.Max(p=>p.Height),ColumnCount=fields.Length,RowCount=1,Margin=new Padding(0,3,0,3)};
        for(var i=0;i<fields.Length;i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/fields.Length));
            fields[i].Dock=DockStyle.Fill;fields[i].Margin=new Padding(0,0,i==fields.Length-1?0:10,0);
            row.Controls.Add(fields[i],i,0);
        }
        return row;
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
