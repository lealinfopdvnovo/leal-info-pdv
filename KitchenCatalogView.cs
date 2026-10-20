using System.Globalization;

namespace LealInfoPDV;

internal sealed record KitchenCatalogProduct(long Id, string Name, string Category, decimal Price, string PhotoPath, string Barcode);

internal sealed class KitchenCatalogView : UserControl
{
    private readonly TextBox search = new() { Name = "CatalogSearch", Dock = DockStyle.Fill, PlaceholderText = "Buscar produto ou código" };
    private readonly ComboBox category = new() { Name = "CatalogCategory", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly FlowLayoutPanel cards = new() { Name = "CatalogCards", Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, BackColor = Color.FromArgb(241,246,250) };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.FromArgb(65,90,110) };
    private readonly Button reload = new() { Name = "CatalogReload", Text = "ATUALIZAR CARDÁPIO", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
    private KitchenCatalogProduct[] products = Array.Empty<KitchenCatalogProduct>();
    private bool loading;
    private int limit = 60;
    internal event Action<KitchenCatalogProduct>? ProductChosen;
    internal KitchenCatalogView()
    {
        Name = "KitchenCatalog"; Dock = DockStyle.Fill; Font = new Font("Segoe UI",10); Padding = new Padding(10,0,4,10);
        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=6 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int h in new[]{76,40,40,42,40})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.Controls.Add(new Label { Text="CARDÁPIO\nProdutos cadastrados no PDV", Dock=DockStyle.Fill, BackColor=Color.FromArgb(9,29,49), ForeColor=Color.White, Padding=new Padding(12), Font=new Font("Segoe UI",12,FontStyle.Bold), TextAlign=ContentAlignment.MiddleLeft },0,0);
        root.Controls.Add(search,0,1);root.Controls.Add(category,0,2);root.Controls.Add(reload,0,3);root.Controls.Add(status,0,4);root.Controls.Add(cards,0,5);Controls.Add(root);
        category.Items.Add("Todas as categorias");category.SelectedIndex=0;
        search.TextChanged+=(_,_)=>{limit=60;Render();};
        category.SelectedIndexChanged+=(_,_)=>{if(!loading){limit=60;Render();}};
        reload.Click+=async(_,_)=>await ReloadAsync();
    }
    internal static KitchenCatalogProduct[] ReadProducts(Func<LealInfoPDV.Network.PdvConnection>? open = null)
    {
        using var db=(open ?? Database.Open)();using var cmd=db.CreateCommand();
        cmd.CommandText="SELECT id,name,COALESCE(NULLIF(TRIM(category),''),'Sem categoria'),price,COALESCE(photo_path,''),COALESCE(barcode,'') FROM products WHERE active=1 AND id>0 ORDER BY name,id";
        using var r=cmd.ExecuteReader();var result=new List<KitchenCatalogProduct>();
        while(r.Read())result.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),Convert.ToDecimal(r.GetDouble(3)),r.GetString(4),r.GetString(5)));
        return result.ToArray();
    }
    internal async Task ReloadAsync()
    {
        if(loading||IsDisposed)return;
        loading=true;reload.Enabled=false;status.Text="Carregando produtos...";
        try
        {
            var data=await Task.Run(() => ReadProducts());if(IsDisposed)return;
            products=data;string previous=Convert.ToString(category.SelectedItem)??"";
            category.Items.Clear();category.Items.Add("Todas as categorias");
            foreach(var item in products.Select(p=>p.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c=>c))category.Items.Add(item);
            category.SelectedItem=category.Items.Cast<string>().FirstOrDefault(c=>c==previous)??"Todas as categorias";limit=60;
            Render();
        }
        catch(Exception e){if(!IsDisposed)status.Text="Cardápio indisponível. Tente atualizar.";System.Diagnostics.Debug.WriteLine(e.GetType().Name);}
        finally{loading=false;if(!IsDisposed)reload.Enabled=true;}
    }
    private void Render()
    {
        if(IsDisposed)return;
        string term=search.Text.Trim();string selected=category.SelectedIndex>0?Convert.ToString(category.SelectedItem)??"":"";
        var compare=CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        bool Match(string value)=>compare.IndexOf(value,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0;
        var found=products.Where(p=>(selected==""||p.Category.Equals(selected,StringComparison.OrdinalIgnoreCase))&&(term==""||Match(p.Name)||Match(p.Barcode)||Match(p.Category))).ToArray();
        cards.SuspendLayout();
        foreach(Control c in cards.Controls.Cast<Control>().ToArray()){cards.Controls.Remove(c);c.Dispose();}
        foreach(var product in found.Take(limit))cards.Controls.Add(Card(product));
        if(found.Length>limit)
        {
            var more=new Button{Text="MOSTRAR MAIS PRODUTOS",Width=180,Height=42,FlatStyle=FlatStyle.Flat};
            more.Click+=(_,_)=>{limit+=60;Render();};cards.Controls.Add(more);
        }
        status.Text=products.Length==0?"Nenhum produto ativo cadastrado.":found.Length==0?"Nenhum produto encontrado.":$"{Math.Min(limit,found.Length)} de {found.Length} produto(s)";
        cards.ResumeLayout(true);
    }
    private Control Card(KitchenCatalogProduct product)
    {
        var card=new TableLayoutPanel{Width=174,Height=214,ColumnCount=1,RowCount=4,Padding=new Padding(8),BackColor=Color.White,Margin=new Padding(4)};
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int h in new[]{82,44,26,40})card.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        Image? photo=null;
        try{if(File.Exists(product.PhotoPath)){using var original=Image.FromFile(product.PhotoPath);double scale=Math.Min(120d/original.Width,78d/original.Height);photo=new Bitmap(original,new Size(Math.Max(1,(int)(original.Width*scale)),Math.Max(1,(int)(original.Height*scale))));}}
        catch(Exception e)when(e is IOException or ArgumentException or System.Runtime.InteropServices.ExternalException or OutOfMemoryException){ }
        Control visual;
        if(photo!=null)
        {
            var picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Image=photo};
            picture.Disposed+=(_,_)=>picture.Image?.Dispose();visual=picture;
        }
        else visual=new Label{Text="SEM FOTO",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.FromArgb(230,239,246),ForeColor=Color.FromArgb(65,90,110)};
        var title=new Label{Text=product.Name,Dock=DockStyle.Fill,AutoEllipsis=true,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(28,48,68)};
        var price=new Label{Text=product.Price.ToString("C2",CultureInfo.GetCultureInfo("pt-BR")),Dock=DockStyle.Fill,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(0,114,162)};
        var choose=new Button{Name="CatalogProduct_"+product.Id,Text="ADICIONAR",Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(0,133,190),ForeColor=Color.White,Cursor=Cursors.Hand,AccessibleName=product.Name+" "+price.Text,Tag=product};
        choose.FlatAppearance.BorderSize=0;
        void Pick(){ProductChosen?.Invoke(product);}
        choose.Click+=(_,_)=>Pick();visual.Click+=(_,_)=>Pick();title.Click+=(_,_)=>Pick();price.Click+=(_,_)=>Pick();
        card.Controls.Add(visual,0,0);card.Controls.Add(title,0,1);card.Controls.Add(price,0,2);card.Controls.Add(choose,0,3);
        return card;
    }
}
