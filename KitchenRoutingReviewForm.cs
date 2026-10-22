namespace LealInfoPDV;

internal sealed class KitchenRoutingReviewForm:Form
{
    internal readonly DataGridView Rows=new(){Name="KitchenRoutingRows",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false};
    private readonly Dictionary<long,int> edited=new();
    internal KitchenRoutingReviewForm()
    {
        Name="KitchenRoutingReview";Text="CLASSIFICAÇÃO DOS PRODUTOS • COZINHA";Width=1000;Height=700;MinimumSize=new Size(780,500);StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(14)};layout.RowStyles.Add(new(SizeType.Absolute,65));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,54));Controls.Add(layout);
        layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Revise cada produto. Sugestões automáticas podem ser corrigidas.\nEscolha o destino ou marque REVISADO para confirmar a sugestão. Preços e estoque não serão alterados.",ForeColor=Color.White,AutoEllipsis=true},0,0);
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Product",HeaderText="PRODUTO",ReadOnly=true,FillWeight=160});Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Category",HeaderText="CATEGORIA",ReadOnly=true});
        Rows.Columns.Add(new DataGridViewComboBoxColumn{Name="Route",HeaderText="DESTINO",DataSource=new[]{KitchenProductRouting.Label(1),KitchenProductRouting.Label(0)},FillWeight=120});
        Rows.Columns.Add(new DataGridViewCheckBoxColumn{Name="Reviewed",HeaderText="REVISADO",FillWeight=55});
        foreach(var product in KitchenProductRouting.List()){int n=Rows.Rows.Add(product.Name,product.Category,KitchenProductRouting.Label(product.Mode),product.Reviewed);Rows.Rows[n].Tag=product;}
        Rows.CurrentCellDirtyStateChanged+=(_,_)=>{if(Rows.IsCurrentCellDirty)Rows.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        Rows.CellValueChanged+=(_,e)=>{if(e.RowIndex<0||Rows.Rows[e.RowIndex].Tag is not KitchenRouteProduct p)return;var row=Rows.Rows[e.RowIndex];if(Rows.Columns[e.ColumnIndex].Name=="Route"){row.Cells["Reviewed"].Value=true;}if(Convert.ToBoolean(row.Cells["Reviewed"].Value))edited[p.Id]=Convert.ToString(row.Cells["Route"].Value)==KitchenProductRouting.Label(1)?1:0;else edited.Remove(p.Id);};
        Rows.DataError+=(_,e)=>{e.ThrowException=false;};layout.Controls.Add(Rows,0,1);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=new Button{Name="KitchenSaveRouting",Text="SALVAR REVISÃO",Width=185,Height=42};var close=new Button{Text="FECHAR",Width=110,Height=42};actions.Controls.AddRange(new Control[]{save,close});layout.Controls.Add(actions,0,2);
        save.Click+=(_,_)=>{try{Rows.EndEdit();KitchenProductRouting.Review(edited);DialogResult=DialogResult.OK;Close();}catch(Exception e){MessageBox.Show(this,e.Message,"Classificação não salva",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};close.Click+=(_,_)=>Close();
        KitchenPremiumStyle.Apply(this);
    }
}
