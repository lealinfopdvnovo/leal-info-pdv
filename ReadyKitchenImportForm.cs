using System.Globalization;
using System.Text.RegularExpressions;

namespace LealInfoPDV;

internal sealed class ReadyKitchenImportForm : Form
{
    private readonly DataGridView grid=new(){Name="ReadyKitchenProducts",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,RowTemplate={Height=76}};
    private readonly Button import=new(){Name="ImportReadyKitchen",Text="IMPORTAR SELECIONADOS",Dock=DockStyle.Right,Width=260,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(0,133,190),ForeColor=Color.White};
    private readonly Label status=new(){Dock=DockStyle.Bottom,Height=44,ForeColor=Color.FromArgb(45,65,85)};
    private readonly ReadyKitchenItem[] entries;
    private bool busy;
    internal ReadyKitchenImportForm()
    {
        Text="CATÁLOGO PRONTO — IMPORTAR PRODUTOS";Name="ReadyKitchenImport";StartPosition=FormStartPosition.CenterParent;
        Size=new Size(1040,700);MinimumSize=new Size(840,560);Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(241,246,250);Padding=new Padding(16);
        entries=ReadyKitchenCatalog.Load();
        grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Selected",HeaderText="Importar",FillWeight=12});
        grid.Columns.Add(new DataGridViewImageColumn{Name="Photo",HeaderText="Foto ilustrativa",ImageLayout=DataGridViewImageCellLayout.Zoom,FillWeight=18,ReadOnly=true});
        grid.Columns.Add("Product","Produto");grid.Columns["Product"].ReadOnly=true;grid.Columns["Product"].FillWeight=40;
        grid.Columns.Add("Category","Categoria");grid.Columns["Category"].ReadOnly=true;grid.Columns["Category"].FillWeight=24;
        grid.Columns.Add("Price","Seu preço (R$)");grid.Columns["Price"].FillWeight=20;grid.Columns["Price"].DefaultCellStyle.NullValue="";
        grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersHeight=38;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(9,29,49);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(241,246,250);grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.GridColor=Color.FromArgb(225,235,242);
        foreach(var item in entries)
        {
            using var input=new MemoryStream(ReadyKitchenCatalog.Photo(item));using var original=Image.FromStream(input);
            var image=new Bitmap(original,new Size(90,66));int index=grid.Rows.Add(false,image,item.Name,item.Category,"");
            grid.Rows[index].Tag=item;
        }
        var heading=new Label{Dock=DockStyle.Top,Height=82,BackColor=Color.FromArgb(9,29,49),ForeColor=Color.White,Padding=new Padding(12),
            Text="CATÁLOGO MISTO • 18 PRODUTOS COM FOTOS\nSelecione os itens e defina os preços. Produtos existentes serão preservados.",Font=new Font("Segoe UI",12,FontStyle.Bold)};
        var actions=new Panel{Dock=DockStyle.Bottom,Height=56,Padding=new Padding(0,8,0,0)};
        var credits=new Button{Text="ORIGEM DAS FOTOS",Dock=DockStyle.Left,Width=170,FlatStyle=FlatStyle.Flat};
        credits.Click+=(_,_)=>ShowCredits();
        var selectAll=new Button{Text="SELECIONAR TODOS",Dock=DockStyle.Left,Width=180,FlatStyle=FlatStyle.Flat};
        selectAll.Click+=(_,_)=>{foreach(DataGridViewRow row in grid.Rows)row.Cells["Selected"].Value=true;};
        var cancel=new Button{Text="FECHAR",Dock=DockStyle.Right,Width=110,DialogResult=DialogResult.Cancel,FlatStyle=FlatStyle.Flat};
        actions.Controls.Add(import);actions.Controls.Add(cancel);actions.Controls.Add(selectAll);actions.Controls.Add(credits);
        Controls.Add(grid);Controls.Add(heading);Controls.Add(status);Controls.Add(actions);
        import.Click+=async(_,_)=>await ImportSelected();
        FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};
    }
    private async Task ImportSelected()
    {
        if(busy)return;
        grid.EndEdit();var selected=new Dictionary<string,decimal>();
        foreach(DataGridViewRow row in grid.Rows)
        {
            if(Convert.ToBoolean(row.Cells["Selected"].Value)!=true)continue;
            string text=(Convert.ToString(row.Cells["Price"].Value)??"").Trim();
            if(!Regex.IsMatch(text,@"^[0-9]+(?:[.,][0-9]{1,2})?$")||!decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var price)||price<=0)
            {status.Text="Informe seu preço positivo para "+row.Cells["Product"].Value+".";grid.CurrentCell=row.Cells["Price"];grid.BeginEdit(true);MessageBox.Show(this,status.Text+"\nPreencha a coluna Seu preço (R$) dos itens selecionados. Nenhum produto foi alterado.","Preço obrigatório",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            selected.Add(((ReadyKitchenItem)row.Tag!).Code,price);
        }
        if(selected.Count==0){status.Text="Selecione os produtos que deseja importar.";return;}
        bool success=false;busy=true;import.Enabled=false;grid.Enabled=false;status.Text="Criando backup de proteção e importando os produtos selecionados...";
        try
        {
            var result=await Task.Run(()=>ReadyKitchenCatalog.Import(selected));
            status.Text=$"{result.Added} produto(s) importado(s). {result.Skipped} já cadastrado(s), preservado(s). Backup criado.";
            success=true;
        }
        catch(Exception e){status.Text=e.Message;MessageBox.Show(this,e.Message,"Importação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{busy=false;import.Enabled=true;grid.Enabled=true;}
        if(success){DialogResult=DialogResult.OK;Close();}
    }
    private void ShowCredits()
    {
        using var dialog=new Form{Text="Origem e licença das fotos ilustrativas",StartPosition=FormStartPosition.CenterParent,Size=new Size(820,560)};
        dialog.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,
            Text=string.Join(Environment.NewLine+Environment.NewLine,entries.Select(e=>e.Name+"\r\n"+e.Author+" — "+e.License+"\r\n"+e.SourceUrl+"\r\n"+e.LicenseUrl+"\r\n"+e.Changes))});
        dialog.ShowDialog(this);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing)foreach(DataGridViewRow row in grid.Rows)(row.Cells["Photo"].Value as Image)?.Dispose();
        base.Dispose(disposing);
    }
}
