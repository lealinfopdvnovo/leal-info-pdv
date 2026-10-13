namespace LealInfoPDV;

internal sealed class GlassSavedProjectBrowser : Form
{
    private readonly DataGridView grid = new() { Name="SavedGlassProjects", Dock=DockStyle.Fill,
        AutoGenerateColumns=false, ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false,
        MultiSelect=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill };
    internal string? SelectedProjectId { get; private set; }
    internal GlassSavedProjectBrowser(Action<Form>? theme)
    {
        Text="ABRIR PROJETO — VIDRAÇARIA";StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(960,580);MinimumSize=new Size(560,360);
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=2,ColumnCount=1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        foreach(var (name,title) in new[]{("Description","Projeto"),("Customer","Cliente"),("Model","Modelo"),("CreatedUtc","Criação (UTC)"),("UpdatedUtc","Última alteração (UTC)")})
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name=name,DataPropertyName=name,HeaderText=title,ValueType=typeof(string) });
        layout.Controls.Add(grid,0,0);
        var footer=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true };
        var open=new SalesVisualButton { Name="ConfirmOpenGlassProject",Text="ABRIR",Width=140,Height=42 };
        var delete=new SalesVisualButton { Name="DeleteGlassProject",Text="EXCLUIR PROJETO",Width=210,Height=42 };
        var close=new SalesVisualButton { Text="FECHAR",Width=140,Height=42,DialogResult=DialogResult.Cancel };
        open.Click+=(_,_)=>OpenSelected();grid.CellDoubleClick+=(_,_)=>OpenSelected();
        delete.Click+=(_,_)=>
        {
            if(grid.CurrentRow?.DataBoundItem is not GlassProjectListItem selected)return;
            if(MessageBox.Show(this,"Excluir somente o projeto "+selected.Description+"?","Excluir projeto",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            try { GlassProjectStore.Delete(selected.Id,selected.Revision);Reload(); }
            catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        };
        close.Click+=(_,_)=>Close();CancelButton=close;footer.Controls.AddRange(new Control[]{open,delete,close});
        layout.Controls.Add(footer,0,1);Controls.Add(layout);
        Reload();theme?.Invoke(this);GlassWorkshopVisuals.Apply(this);
        Shown+=(_,_)=>{var area=Screen.FromControl(this).WorkingArea;Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));};
    }
    private void Reload()=>grid.DataSource=GlassProjectStore.List().ToList();
    private void OpenSelected()
    {if(grid.CurrentRow?.DataBoundItem is GlassProjectListItem selected){SelectedProjectId=selected.Id;DialogResult=DialogResult.OK;Close();}}
}
