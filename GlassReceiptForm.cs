namespace LealInfoPDV;
internal sealed class GlassReceiptForm : Form
{
    private readonly GlassStoredProject project;
    private readonly DataGridView grid=new(){Name="GlassReceiptGrid",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoGenerateColumns=false};
    internal GlassReceiptForm(GlassStoredProject saved,Action<Form>? theme)
    {
        project=saved;Name="GlassReceipts";Text="RECIBOS — VIDRAÇARIA";ClientSize=new(1020,620);MinimumSize=new(600,420);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;ShowInTaskbar=false;
        foreach(var field in new[]{("MovementId","LANÇAMENTO ID"),("ReceivedAt","RECEBIDO EM"),("Amount","VALOR RECEBIDO"),("Description","REFERÊNCIA")})grid.Columns.Add(new DataGridViewTextBoxColumn{Name=field.Item1,DataPropertyName=field.Item1,HeaderText=field.Item2,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});
        grid.Columns["ReceivedAt"].DefaultCellStyle.Format="dd/MM/yyyy HH:mm:ss";grid.Columns["Amount"].DefaultCellStyle.Format="C2";grid.Columns["Amount"].DefaultCellStyle.FormatProvider=GlassBrazilianMoney.Culture;
        var header=new Label{Name="GlassReceiptStatus",Dock=DockStyle.Top,AutoSize=true,Padding=new(12),Text=$"PROJETO: {saved.Data.Description}\r\nEscolha uma ENTRADA real. Saídas não são recibos. Emitir/reimprimir não registra recebimento."};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new(10)};
        var view=new SalesVisualButton{Name="PreviewGlassReceipt",Text="VISUALIZAR / PDF / REIMPRIMIR",AutoSize=true,MinimumSize=new(285,42)};
        view.Click+=(_,_)=>{try{if(grid.CurrentRow?.DataBoundItem is not GlassReceiptListItem row)return;GlassLinkedDocument.Preview(this,()=>GlassLinkedDocument.Receipt(GlassDocumentStore.Receipt(project,row.MovementId)),theme);}catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
        var refresh=new SalesVisualButton{Name="RefreshGlassReceipts",Text="ATUALIZAR",AutoSize=true,MinimumSize=new(140,42)};
        void Reload(){var list=GlassDocumentStore.Receipts(saved);grid.DataSource=list.ToList();view.Enabled=list.Count>0;header.Text=$"PROJETO: {saved.Data.Description}\r\n"+(list.Count==0?"Nenhum recebimento real vinculado. Não há recibo a emitir.":$"{list.Count} recebimento(s) legítimo(s). Escolha uma ENTRADA. Saídas não são recibos; reimprimir não registra pagamento.");}
        refresh.Click+=(_,_)=>{try{Reload();}catch(Exception e){MessageBox.Show(this,e.Message,Text);}};
        var close=new SalesVisualButton{Text="FECHAR",AutoSize=true,MinimumSize=new(110,42),DialogResult=DialogResult.Cancel};close.Click+=(_,_)=>Close();CancelButton=close;actions.Controls.AddRange(new Control[]{view,refresh,close});Controls.Add(grid);Controls.Add(header);Controls.Add(actions);grid.BringToFront();theme?.Invoke(this);Reload();
    }
}
