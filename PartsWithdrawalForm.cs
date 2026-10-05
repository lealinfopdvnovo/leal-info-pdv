namespace LealInfoPDV;
internal sealed class PartsWithdrawalForm : Form
{
    private readonly Action<Form> _theme;
    private readonly DataGridView _history=Grid();
    internal PartsWithdrawalForm(Action<Form> theme)
    {
        _theme=theme;Text="RETIRADA DE PEÇAS";StartPosition=FormStartPosition.CenterParent;
        Width=1180;Height=720;MinimumSize=new Size(800,500);AutoScaleMode=AutoScaleMode.Dpi;
        Controls.Add(_history);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=70,Padding=new Padding(12)};
        bar.Controls.Add(Button("NOVA RETIRADA","newWithdrawal",()=>NewWithdrawal()));
        bar.Controls.Add(Button("ATUALIZAR","refresh",Reload));bar.Controls.Add(Button("FECHAR","close",Close));Controls.Add(bar);
        var header=new Label{Text="RETIRADA DE PEÇAS",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(22,0,0,0),Font=new Font("Segoe UI",18,FontStyle.Bold),BackColor=Color.FromArgb(4,70,112),ForeColor=Color.White};Controls.Add(header);
        Reload();_theme(this);
    }
    private void Reload()=>Bind(_history,PartsWithdrawal.History());
    private void NewWithdrawal()
    {
        using var f=new Form{Text="NOVA RETIRADA",Width=680,Height=490,MinimumSize=new Size(550,440),StartPosition=FormStartPosition.CenterParent,KeyPreview=true,AutoScaleMode=AutoScaleMode.Dpi};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2,RowCount=6};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var clock=new Label{Name="withdrawalTime",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
        var description=new TextBox{Name="description",Dock=DockStyle.Fill,Multiline=true};
        var person=new TextBox{Name="collectedBy",Dock=DockStyle.Fill};
        var customer=new TextBox{Name="customer",Dock=DockStyle.Fill,ReadOnly=true,Text="RETIRADA AVULSA"};long? customerId=null;
        void Row(int row,string title,Control input,int height){layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));layout.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);layout.Controls.Add(input,1,row);}
        Row(0,"DATA / HORA",clock,38);Row(1,"DESCRIÇÃO",description,85);Row(2,"RETIRADO POR",person,48);Row(3,"CLIENTE (opcional)",customer,48);
        void Search()
        {
            var selection=SelectCustomer(f);if(selection==null)return;
            customerId=selection.Value.Id;customer.Text=selection.Value.Name;
        }
        var searchBar=new FlowLayoutPanel{Dock=DockStyle.Fill};searchBar.Controls.Add(Button("[F8] BUSCAR CLIENTE","findCustomer",Search,210));searchBar.Controls.Add(Button("SEM CLIENTE","clearCustomer",()=>{customerId=null;customer.Text="RETIRADA AVULSA";},150));
        Row(4,"",searchBar,65);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};
        var save=Button("SALVAR","saveWithdrawal",()=>{});
        save.Click+=(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(description.Text)||string.IsNullOrWhiteSpace(person.Text)){MessageBox.Show(f,"Informe a descrição e o nome de quem retirou.","Nova retirada",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            var when=DateTime.Now;clock.Text=$"{when:dd/MM/yyyy}   {when:HH:mm:ss}";
            if(!ConfirmWithdrawal(f,customer.Text,person.Text,description.Text,when))return;
            save.Enabled=false;
            try{PartsWithdrawal.Register(description.Text,customerId,person.Text,when);f.DialogResult=DialogResult.OK;f.Close();}
            catch(Exception ex){save.Enabled=true;MessageBox.Show(f,ex.Message,"Retirada não registrada",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        };
        buttons.Controls.Add(save);buttons.Controls.Add(Button("CANCELAR","cancelWithdrawal",f.Close));Row(5,"",buttons,60);f.Controls.Add(layout);
        f.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.F8){e.SuppressKeyPress=true;Search();}};
        using var timer=new System.Windows.Forms.Timer{Interval=1000};void Tick()=>clock.Text=$"{DateTime.Now:dd/MM/yyyy}   {DateTime.Now:HH:mm:ss}";timer.Tick+=(_,_)=>Tick();Tick();timer.Start();
        _theme(f);if(f.ShowDialog(this)==DialogResult.OK)Reload();
    }
    private (long Id,string Name)? SelectCustomer(Form owner)
    {
        using var f=new Form{Text="BUSCAR CLIENTE CADASTRADO",Width=880,Height=580,MinimumSize=new Size(650,420),StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.Dpi};
        var grid=Grid();var search=new TextBox{Name="customerSearch",Dock=DockStyle.Top,PlaceholderText="Nome, CPF/CNPJ, telefone ou ID"};
        void Refresh()=>Bind(grid,PartsWithdrawal.FindCustomers(search.Text));search.TextChanged+=(_,_)=>Refresh();Refresh();
        (long Id,string Name)? selected=null;
        void Select(){if(grid.CurrentRow==null)return;selected=(Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value),Convert.ToString(grid.CurrentRow.Cells["Nome"].Value)??"");f.DialogResult=DialogResult.OK;f.Close();}
        grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Select();};
        var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=65,Padding=new Padding(10)};var select=Button("SELECIONAR","selectCustomer",Select);bar.Controls.Add(select);bar.Controls.Add(Button("CANCELAR","cancelCustomer",f.Close));f.Controls.Add(grid);f.Controls.Add(search);f.Controls.Add(bar);f.AcceptButton=select;_theme(f);f.ShowDialog(owner);return selected;
    }
    private bool ConfirmWithdrawal(Form owner,string customer,string person,string description,DateTime when)
    {
        using var f=new Form{Text="CONFIRMAR RETIRADA?",Width=650,Height=430,MinimumSize=new Size(500,350),StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.Dpi};
        f.Controls.Add(new TextBox{ReadOnly=true,Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=$"Cliente: {customer}\r\n\r\nRetirado por: {person.Trim()}\r\n\r\nDescrição: {description.Trim()}\r\n\r\nData: {when:dd/MM/yyyy}\r\nHora: {when:HH:mm:ss}"});
        var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=70,Padding=new Padding(12)};bar.Controls.Add(Button("CONFIRMAR","confirmWithdrawal",()=>{f.DialogResult=DialogResult.OK;f.Close();}));bar.Controls.Add(Button("CANCELAR","cancelConfirmation",f.Close));f.Controls.Add(bar);_theme(f);return f.ShowDialog(owner)==DialogResult.OK;
    }
    private static void Bind(DataGridView grid,System.Data.DataTable data)
    {
        grid.DataSource=null;grid.Columns.Clear();grid.AutoGenerateColumns=false;
        foreach(System.Data.DataColumn column in data.Columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn{Name=column.ColumnName,HeaderText=column.ColumnName,DataPropertyName=column.ColumnName});
        grid.DataSource=data;
    }
    private static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,Name="withdrawalGrid"};
    private static Button Button(string text,string name,Action action,int width=170){var b=new Button{Text=text,Name=name,Width=width,Height=42,BackColor=Color.FromArgb(4,70,112),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(5)};b.Click+=(_,_)=>action();return b;}
}
