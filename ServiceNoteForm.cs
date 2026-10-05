using System.Data;
using System.Globalization;
namespace LealInfoPDV;

internal sealed class ServiceNoteForm : Form
{
    private readonly Action<Form> _theme;
    private readonly DataGridView _history=Grid("noteHistory");
    private readonly TextBox _search=new(){Name="noteSearch",Dock=DockStyle.Top,PlaceholderText="Número, cliente, CPF/CNPJ, telefone, data ou equipamento"};
    internal ServiceNoteForm(Action<Form> theme)
    {
        _theme=theme;Text="NOTA DE SERVIÇO";Width=1180;Height=720;MinimumSize=new Size(800,500);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;
        var header=new Label{Text="HISTÓRICO DE NOTAS DE SERVIÇO",Dock=DockStyle.Top,Height=60,Padding=new Padding(20,0,0,0),TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",17,FontStyle.Bold)};
        var actions=Bar();actions.Controls.Add(Button("NOVA NOTA","newNote",()=>Edit(new())));actions.Controls.Add(Button("ABRIR / EDITAR","openNote",OpenSelected));actions.Controls.Add(Button("VISUALIZAR","previewNote",()=>PrintSelected(false)));actions.Controls.Add(Button("REIMPRIMIR A4","printNote",()=>PrintSelected(true)));actions.Controls.Add(Button("FECHAR","closeNotes",Close));
        Controls.Add(_history);Controls.Add(_search);Controls.Add(header);Controls.Add(actions);_search.TextChanged+=(_,_)=>Reload();_history.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)OpenSelected();};Reload();_theme(this);
    }
    private void Reload(){Bind(_history,ServiceNote.History(_search.Text));_history.Columns["ID"].Visible=false;_history.Columns["VALOR TOTAL"].DefaultCellStyle.Format="C2";_history.Columns["VALOR TOTAL"].DefaultCellStyle.FormatProvider=ServiceNote.Brazilian;}
    private long? Selected()=>_history.CurrentRow==null?null:Convert.ToInt64(_history.CurrentRow.Cells["ID"].Value);
    private void OpenSelected(){if(Selected() is long id)Guard(()=>Edit(ServiceNote.Load(id)));}
    private void PrintSelected(bool print){if(Selected() is long id)Guard(()=>{using var job=new ServiceNotePrint(ServiceNote.Load(id));if(print)job.Print(this);else job.Preview(this);});}
    private void Guard(Action action){try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"Nota de Serviço",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    private void Edit(ServiceNoteRecord note)
    {
        using var f=new Form{Text=note.Id==0?"NOVA NOTA DE SERVIÇO":$"NOTA DE SERVIÇO Nº {note.Id:000000}",Width=1040,Height=760,MinimumSize=new Size(760,560),StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.Dpi,KeyPreview=true};
        var tabs=new TabControl{Dock=DockStyle.Fill,Name="noteTabs"};var data=new TabPage("DADOS DA NOTA");var items=new TabPage("ITENS / SERVIÇOS");var completion=new TabPage("FINALIZAÇÃO");tabs.TabPages.AddRange(new[]{data,items,completion});
        var fields=new Dictionary<string,TextBox>();
        var details=Layout();data.Controls.Add(details);
        var customerLine=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var linked=new Label{Name="linkedCustomer",Text=note.CustomerId.HasValue?$"Cliente vinculado: ID {note.CustomerId}":"Sem cliente vinculado",AutoSize=true,Margin=new Padding(8,14,0,0)};
        void FindCustomer()
        {
            var id=SelectCustomer(f);if(!id.HasValue)return;
            var snapshot=ServiceNote.Customer(id.Value);note.CustomerId=id;
            foreach(var pair in fields)pair.Value.Text=snapshot.GetValueOrDefault(pair.Key)??"";
            linked.Text=$"Cliente vinculado: ID {id}";
        }
        customerLine.Controls.Add(Button("[F11] BUSCAR CLIENTE","findNoteCustomer",()=>Guard(FindCustomer),230));customerLine.Controls.Add(Button("DESVINCULAR","unlinkCustomer",()=>{note.CustomerId=null;linked.Text="Sem cliente vinculado";}));customerLine.Controls.Add(linked);FullRow(details,customerLine,60);
        var labels=new[]{("name","CLIENTE"),("document","CPF/CNPJ"),("phone","TELEFONE"),("zip","CEP"),("address","RUA / ENDEREÇO"),("number","NÚMERO"),("complement","COMPLEMENTO"),("district","BAIRRO"),("city","CIDADE"),("state","UF"),("reference","REFERÊNCIA")};
        foreach(var pair in labels){var box=Box(pair.Item1,note.Detail(pair.Item1));fields[pair.Item1]=box;Row(details,pair.Item2,box);}
        var equipment=Box("equipment",note.Equipment);Row(details,"PRODUTO / EQUIPAMENTO",equipment);
        var orderDate=new DateTimePicker{Name="orderDate",Dock=DockStyle.Fill,Format=DateTimePickerFormat.Short,Value=note.OrderDate};Row(details,"DATA DO PEDIDO",orderDate);
        var payment=new ComboBox{Name="notePayment",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};payment.Items.AddRange(new[]{"DINHEIRO","PIX","CARTÃO","OUTRO"});payment.SelectedItem=payment.Items.Cast<object>().FirstOrDefault(x=>x.ToString()==note.Payment)??"OUTRO";Row(details,"FORMA DE PAGAMENTO",payment);
        var grid=Grid("noteItems");items.Controls.Add(grid);var itemActions=Bar();items.Controls.Add(itemActions);
        var total=new Label{Name="noteTotal",AutoSize=true,Margin=new Padding(12,15,0,0),Font=new Font("Segoe UI",12,FontStyle.Bold)};
        void RefreshItems()
        {
            var table=new DataTable();table.Columns.Add("CÓDIGO");table.Columns.Add("DISCRIMINAÇÃO");table.Columns.Add("QUANTIDADE",typeof(decimal));table.Columns.Add("VALOR UNITÁRIO",typeof(decimal));table.Columns.Add("TOTAL",typeof(decimal));
            foreach(var i in note.Items)table.Rows.Add(i.Code,i.Description,i.Quantity,i.UnitCents/100m,i.TotalCents/100m);
            Bind(grid,table);foreach(var col in new[]{"VALOR UNITÁRIO","TOTAL"}){grid.Columns[col].DefaultCellStyle.Format="C2";grid.Columns[col].DefaultCellStyle.FormatProvider=ServiceNote.Brazilian;}grid.Columns["DISCRIMINAÇÃO"].FillWeight=230;total.Text="TOTAL: "+ServiceNote.Money(note.TotalCents);
        }
        void ChangeItem(bool edit)
        {
            var index=grid.CurrentRow?.Index??-1;if(edit&&index<0)return;
            var value=EditItem(f,edit?note.Items[index]:new());if(value==null)return;
            if(edit)note.Items[index]=value;else note.Items.Add(value);RefreshItems();
        }
        itemActions.Controls.Add(Button("ADICIONAR ITEM","addNoteItem",()=>ChangeItem(false)));itemActions.Controls.Add(Button("EDITAR ITEM","editNoteItem",()=>ChangeItem(true)));itemActions.Controls.Add(Button("REMOVER ITEM","removeNoteItem",()=>{var i=grid.CurrentRow?.Index??-1;if(i>=0){note.Items.RemoveAt(i);RefreshItems();}}));itemActions.Controls.Add(total);RefreshItems();
        var finish=Layout();completion.Controls.Add(finish);
        var status=new ComboBox{Name="noteStatus",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};status.Items.AddRange(new[]{"ABERTA","FINALIZADA"});status.SelectedItem=note.Status;Row(finish,"STATUS",status);
        var completed=new DateTimePicker{Name="completedAt",Dock=DockStyle.Fill,Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm",ShowCheckBox=true,Value=note.CompletedAt??DateTime.Now,Checked=note.CompletedAt.HasValue};Row(finish,"SERVIÇO FINALIZADO EM",completed);
        status.SelectedIndexChanged+=(_,_)=>{if(status.SelectedItem?.ToString()=="FINALIZADA"&&!completed.Checked){completed.Value=DateTime.Now;completed.Checked=true;}};
        var warranty=Box("warranty",note.Warranty);Row(finish,"GARANTIA",warranty);
        var paid=Box("paidAmount","");CashAmountMask.Attach(paid);paid.Text=(note.PaidCents/100m).ToString("N2",ServiceNote.Brazilian);Row(finish,"VALOR PAGO",paid);
        var observations=Box("observations",note.Observations);observations.Multiline=true;observations.ScrollBars=ScrollBars.Vertical;Row(finish,"OBSERVAÇÕES",observations,160);
        var creation=new Label{Name="noteCreation",Text=$"Criação: {(note.CreatedAt.Length==0?"Ao salvar":note.CreatedAt)}  •  Responsável: {(note.Operator.Length==0?Auth.OperatorName:note.Operator)}",Dock=DockStyle.Fill,AutoSize=true};FullRow(finish,creation,50);
        var bottom=Bar(88);var saved=new Label{Name="saveNoteResult",AutoSize=true,Margin=new Padding(10,13,0,0)};
        void Capture()
        {foreach(var pair in fields)note.Customer[pair.Key]=pair.Value.Text.Trim();note.Equipment=equipment.Text;note.OrderDate=orderDate.Value.Date;note.Payment=payment.SelectedItem?.ToString()??"OUTRO";note.Status=status.SelectedItem?.ToString()??"ABERTA";note.CompletedAt=completed.Checked?completed.Value:null;note.Warranty=warranty.Text;note.PaidCents=ServiceNote.ParseMoney(paid.Text);note.Observations=observations.Text;}
        void Save(){Capture();ServiceNote.Save(note);f.Text=$"NOTA DE SERVIÇO Nº {note.Id:000000}";saved.Text=$"Nota {note.Id:000000} salva";creation.Text=$"Criação: {note.CreatedAt}  •  Responsável: {note.Operator}";}
        void Print(bool print)
        {Capture();if(note.Id==0){MessageBox.Show(f,"Salve a nota antes de visualizar ou imprimir.","Nota de Serviço");return;}using var job=new ServiceNotePrint(note);if(print)job.Print(f);else job.Preview(f);}
        bottom.Controls.Add(Button("SALVAR NOTA","saveNote",()=>Guard(Save)));bottom.Controls.Add(Button("VISUALIZAR IMPRESSÃO","previewDraft",()=>Guard(()=>Print(false)),220));bottom.Controls.Add(Button("IMPRIMIR A4","printDraft",()=>Guard(()=>Print(true))));bottom.Controls.Add(Button("FECHAR","closeNoteEditor",f.Close));bottom.Controls.Add(saved);
        f.Controls.Add(tabs);f.Controls.Add(bottom);f.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.F11){e.SuppressKeyPress=true;Guard(FindCustomer);}};_theme(f);f.ShowDialog(this);Reload();
    }
    private ServiceNoteItem? EditItem(Form owner,ServiceNoteItem original)
    {
        using var f=new Form{Text="ITEM / SERVIÇO DA NOTA",Width=650,Height=480,MinimumSize=new Size(520,400),StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.Dpi};var layout=Layout();
        var code=Box("itemCode",original.Code);var description=Box("itemDescription",original.Description);description.Multiline=true;description.ScrollBars=ScrollBars.Vertical;
        var quantity=Box("itemQuantity",original.Quantity.ToString("0.####",ServiceNote.Brazilian));var unit=Box("itemUnit","");CashAmountMask.Attach(unit);unit.Text=(original.UnitCents/100m).ToString("N2",ServiceNote.Brazilian);
        Row(layout,"CÓDIGO",code);Row(layout,"DISCRIMINAÇÃO",description,110);Row(layout,"QUANTIDADE",quantity);Row(layout,"VALOR UNITÁRIO",unit);
        var result=new Label{Name="itemTotal",Dock=DockStyle.Fill};Row(layout,"TOTAL DO ITEM",result);
        decimal Qty(){if(!decimal.TryParse(quantity.Text,NumberStyles.Number,ServiceNote.Brazilian,out var q)||q<=0||q>999999m||decimal.Round(q,4)!=q)throw new ArgumentException("Informe uma quantidade positiva com até 4 casas decimais.");return q;}
        void Total(){try{result.Text=ServiceNote.Money(new ServiceNoteItem{Quantity=Qty(),UnitCents=ServiceNote.ParseMoney(unit.Text)}.TotalCents);}catch{result.Text="Confira os valores";}}
        quantity.TextChanged+=(_,_)=>Total();unit.TextChanged+=(_,_)=>Total();Total();ServiceNoteItem? item=null;
        var bar=Bar();bar.Controls.Add(Button("CONFIRMAR ITEM","confirmNoteItem",()=>{try{if(string.IsNullOrWhiteSpace(description.Text))throw new ArgumentException("Informe a descrição do item.");item=new(){Code=code.Text,Description=description.Text,Quantity=Qty(),UnitCents=ServiceNote.ParseMoney(unit.Text)};f.DialogResult=DialogResult.OK;f.Close();}catch(Exception e){MessageBox.Show(f,e.Message,"Item da nota",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}));bar.Controls.Add(Button("CANCELAR","cancelNoteItem",f.Close));f.Controls.Add(layout);f.Controls.Add(bar);_theme(f);f.ShowDialog(owner);return item;
    }
    private long? SelectCustomer(Form owner)
    {
        using var f=new Form{Text="BUSCAR CLIENTE — NOTA DE SERVIÇO",Width=880,Height=580,MinimumSize=new Size(650,420),StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.Dpi};var grid=Grid("noteCustomerGrid");var search=Box("noteCustomerSearch","");search.Dock=DockStyle.Top;search.PlaceholderText="Nome, CPF/CNPJ, telefone ou ID";
        void Refresh()=>Bind(grid,PartsWithdrawal.FindCustomers(search.Text));search.TextChanged+=(_,_)=>Refresh();Refresh();long? id=null;
        void Select(){if(grid.CurrentRow==null)return;id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);f.DialogResult=DialogResult.OK;f.Close();}
        var bar=Bar();var select=Button("SELECIONAR","selectNoteCustomer",Select);bar.Controls.Add(select);bar.Controls.Add(Button("CANCELAR","cancelNoteCustomer",f.Close));grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Select();};f.Controls.Add(grid);f.Controls.Add(search);f.Controls.Add(bar);f.AcceptButton=select;_theme(f);f.ShowDialog(owner);return id;
    }
    private static TableLayoutPanel Layout(){var p=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=2,Padding=new Padding(20),GrowStyle=TableLayoutPanelGrowStyle.AddRows};p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return p;}
    private static void Row(TableLayoutPanel p,string label,Control field,int height=46){var row=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.Absolute,height));p.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);p.Controls.Add(field,1,row);}
    private static void FullRow(TableLayoutPanel p,Control c,int height){var row=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.Absolute,height));p.Controls.Add(c,0,row);p.SetColumnSpan(c,2);}
    private static TextBox Box(string name,string text)=>new(){Name=name,Text=text,Dock=DockStyle.Fill,Margin=new Padding(5,8,5,8)};
    private static FlowLayoutPanel Bar(int height=72)=>new(){Dock=DockStyle.Bottom,Height=height,Padding=new Padding(10),AutoScroll=true};
    private static Button Button(string text,string name,Action action,int width=170){var b=new Button{Name=name,Text=text,Width=width,Height=42,Margin=new Padding(5),Font=new Font("Segoe UI",10,FontStyle.Bold),BackColor=Color.FromArgb(4,70,112),ForeColor=Color.White};b.Click+=(_,_)=>action();return b;}
    private static DataGridView Grid(string name)=>new(){Name=name,Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false};
    private static void Bind(DataGridView grid,DataTable table){grid.DataSource=null;grid.Columns.Clear();grid.AutoGenerateColumns=false;foreach(DataColumn c in table.Columns)grid.Columns.Add(new DataGridViewTextBoxColumn{Name=c.ColumnName,HeaderText=c.ColumnName,DataPropertyName=c.ColumnName});grid.DataSource=table;}
}
