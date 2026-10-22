using System.Globalization;
namespace LealInfoPDV;
internal sealed class KitchenPartialPaymentForm:Form
{
    private readonly KitchenBill account;
    private readonly DataGridView grid=new(){Name="KitchenPartialProducts",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false,BackgroundColor=KitchenPremiumStyle.Background};
    private readonly Label summary=new(){Name="KitchenPartialSummary",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=KitchenPremiumStyle.Gold,Font=new Font("Segoe UI",11,FontStyle.Bold)};
    private readonly Label warning=new(){Name="KitchenPartialWarning",Dock=DockStyle.Top,Height=52,Padding=new Padding(12),ForeColor=Color.Gold};
    private bool changing;
    internal KitchenBill? Selection {get;private set;}
    internal KitchenPartialPaymentForm(KitchenBill bill){
        account=bill;Name="KitchenPartialPayment";Text="RECEBER PAGAMENTO — "+bill.Reference.ToUpperInvariant();StartPosition=FormStartPosition.CenterParent;Size=new Size(1100,650);MinimumSize=new Size(900,520);ShowInTaskbar=false;
        grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Pay",HeaderText="PAGAR",FillWeight=10});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Product",HeaderText="PRODUTO",FillWeight=40,ReadOnly=true});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="State",HeaderText="SITUAÇÃO",FillWeight=24,ReadOnly=true});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Available",HeaderText="QTD. PENDENTE",FillWeight=19,ReadOnly=true});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Quantity",HeaderText="QTD. A PAGAR",FillWeight=19});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Price",HeaderText="UNIT. R$",FillWeight=19});
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Total",HeaderText="PAGAR R$",FillWeight=19,ReadOnly=true});
        foreach(var item in bill.Items){int n=grid.Rows.Add(false,item.Description,item.Payable?"DISPONÍVEL":bill.Orders.Single(o=>o.Id==item.OrderId).Status,item.Quantity,0m,item.UnitPrice,0m);var row=grid.Rows[n];row.Tag=item;
            row.Cells["Pay"].ReadOnly=row.Cells["Quantity"].ReadOnly=!item.Payable;row.Cells["Price"].ReadOnly=!item.Payable||item.ProductId.HasValue||item.UnitPrice>0;if(!item.Payable)row.DefaultCellStyle.ForeColor=Color.Gray;
        }
        foreach(string name in new[]{"Price","Total"}){grid.Columns[name].DefaultCellStyle.Format="N2";grid.Columns[name].DefaultCellStyle.FormatProvider=CultureInfo.GetCultureInfo("pt-BR");}
        foreach(string name in new[]{"Available","Quantity"}){grid.Columns[name].DefaultCellStyle.Format="0.###";grid.Columns[name].DefaultCellStyle.FormatProvider=CultureInfo.GetCultureInfo("pt-BR");}
        grid.RowTemplate.Height=36;grid.ColumnHeadersHeight=44;grid.DefaultCellStyle.BackColor=KitchenPremiumStyle.Surface;grid.DefaultCellStyle.ForeColor=Color.White;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(13,21,29);
        grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        grid.CellValueChanged+=(_,e)=>{if(changing||e.RowIndex<0)return;changing=true;try{var row=grid.Rows[e.RowIndex];var item=(KitchenChargeItem)row.Tag!;bool selected=Convert.ToBoolean(row.Cells["Pay"].Value);if(e.ColumnIndex==grid.Columns["Pay"].Index)row.Cells["Quantity"].Value=selected?item.Quantity:0m;else if(e.ColumnIndex==grid.Columns["Quantity"].Index)row.Cells["Pay"].Value=Decimal(row.Cells["Quantity"].Value)>0;}catch(Exception validationError)when(validationError is FormatException or OverflowException){warning.Text="Informe uma quantidade numérica válida.";}finally{changing=false;}RefreshSummary();};
        grid.DataError+=(_,e)=>{e.ThrowException=false;warning.Text="Informe valores numéricos válidos.";};
        var partial=new Button{Name="KitchenConfirmPartial",Text="PAGAR SELECIONADOS",Dock=DockStyle.Fill};var full=new Button{Name="KitchenConfirmFull",Text="PAGAR TODA A CONTA",Dock=DockStyle.Fill,Enabled=bill.Items.All(i=>i.Payable)};var close=new Button{Name="KitchenCancelPayment",Text="VOLTAR",Dock=DockStyle.Fill};
        partial.Click+=(_,_)=>Confirm(false);full.Click+=(_,_)=>Confirm(true);close.Click+=(_,_)=>Close();
        var footer=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=64,ColumnCount=4,RowCount=1,Padding=new Padding(10)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int width in new[]{205,205,100})footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));footer.Controls.Add(summary,0,0);footer.Controls.Add(partial,1,0);footer.Controls.Add(full,2,0);footer.Controls.Add(close,3,0);
        warning.Text=full.Enabled?"Marque os produtos e informe as quantidades. O restante permanece na mesa.":"Pagamento total bloqueado: há produtos aguardando preparo, preparando ou prontos para entrega. Selecione somente consumo direto ou produtos entregues.";
        Controls.Add(grid);Controls.Add(warning);Controls.Add(footer);KitchenPremiumStyle.Apply(this);KitchenPremiumStyle.Frame(this);RefreshSummary();
    }
    private static decimal Decimal(object? value)=>value is string s?decimal.Parse(s,s.Contains(',')?CultureInfo.GetCultureInfo("pt-BR"):CultureInfo.InvariantCulture):Convert.ToDecimal(value,CultureInfo.InvariantCulture);
    private KitchenBill Choose(bool full){
        grid.EndEdit();var quantities=new List<KitchenPayQuantity>();var priced=account.Items.ToArray();
        foreach(DataGridViewRow row in grid.Rows){var item=(KitchenChargeItem)row.Tag!;if(!full&&!Convert.ToBoolean(row.Cells["Pay"].Value))continue;if(!item.Payable)throw new InvalidOperationException("Produto ainda não foi entregue.");decimal qty=full?item.Quantity:Decimal(row.Cells["Quantity"].Value);decimal price=Decimal(row.Cells["Price"].Value);if(price<=0||price>1_000_000||decimal.Round(price,2)!=price)throw new InvalidOperationException("Informe preço unitário positivo para os itens avulsos.");int index=Array.FindIndex(priced,i=>i.OrderId==item.OrderId&&i.Position==item.Position);priced[index]=item with{UnitPrice=price,TotalOverride=item.UnitPrice>0?item.TotalOverride:null};quantities.Add(new(item.OrderId,item.Position,qty));}
        return KitchenStore.SelectItems(account with{Items=priced,AccountFingerprint=account.Fingerprint},quantities,full);
    }
    private void RefreshSummary(){if(changing)return;changing=true;try{var bill=Choose(false);summary.Text="RECEBER: "+bill.Total.ToString("C2",CultureInfo.GetCultureInfo("pt-BR"));foreach(DataGridViewRow row in grid.Rows){var i=(KitchenChargeItem)row.Tag!;row.Cells["Total"].Value=bill.Items.FirstOrDefault(x=>x.OrderId==i.OrderId&&x.Position==i.Position)?.Total??0m;}}catch(Exception e)when(e is FormatException or OverflowException or InvalidOperationException){summary.Text="Selecione produtos e quantidades.";}finally{changing=false;}}
    private void Confirm(bool full){try{Selection=Choose(full);if(Selection.Total==0&&Selection.Items.Any(i=>account.Items.Single(x=>x.OrderId==i.OrderId&&x.Position==i.Position).Total!=0))throw new InvalidOperationException("Aumente a quantidade para gerar um valor de cobrança válido.");DialogResult=DialogResult.OK;}catch(Exception e)when(e is FormatException or OverflowException or InvalidOperationException){warning.Text=e.Message;}}
}
