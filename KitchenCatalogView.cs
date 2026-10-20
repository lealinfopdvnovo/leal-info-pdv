using LealInfoPDV.Network;
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
    private HashSet<long>? menuIds;
    private string? menuPath;
    private bool menuLoaded;
    internal event Action<KitchenCatalogProduct>? ProductChosen;
    internal event Action? CatalogChanged;
    internal KitchenCatalogView()
    {
        Name = "KitchenCatalog"; Dock = DockStyle.Fill; Font = new Font("Segoe UI",10); Padding = new Padding(10,0,4,10);
        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=9 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int h in new[]{76,40,40,42,40,46,46,46})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.Controls.Add(new Label { Text="CARDÁPIO\nProdutos cadastrados no PDV", Dock=DockStyle.Fill, BackColor=Color.FromArgb(9,29,49), ForeColor=Color.White, Padding=new Padding(12), Font=new Font("Segoe UI",12,FontStyle.Bold), TextAlign=ContentAlignment.MiddleLeft },0,0);
        root.Controls.Add(search,0,1);root.Controls.Add(category,0,2);root.Controls.Add(reload,0,3);root.Controls.Add(status,0,4);
        var ready=new Button{Name="ReadyKitchenOpen",Text="IMPORTAR CATÁLOGO PRONTO",Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(9,29,49),ForeColor=Color.White};
        ready.Click+=async(_,_)=>
        {
            if(!Auth.IsAdmin){status.Text="Importação disponível para ADMINISTRADOR.";return;}
            if(LealInfoPDV.Network.NetworkConfiguration.Current.Mode=="terminal"){status.Text="Importe o catálogo no computador servidor.";return;}
            using var dialog=new ReadyKitchenImportForm();
            if(dialog.ShowDialog(FindForm())==DialogResult.OK) { await ReloadAsync(); CatalogChanged?.Invoke(); }
        };
        var register = new Button { Name = "KitchenAddProduct", Text = "+ ADICIONAR PRODUTO", Dock = DockStyle.Fill };
        register.Click += async (_, _) =>
        {
            if (!Auth.CanManageCatalog) { status.Text = "Sem permissão para cadastrar produtos."; return; }
            register.Enabled = false;
            try
            {
                var before = ReadProducts().Select(p => p.Id).ToHashSet();
                ProductRegistrationForm.Show(FindForm()!, null); await ReloadAsync();
                if (menuIds != null && menuPath != null)
                {
                    menuIds.UnionWith(products.Where(p => !before.Contains(p.Id)).Select(p => p.Id));
                    ProtectedFile.Write(menuPath, menuIds.OrderBy(id => id).ToArray()); Render();
                }
                CatalogChanged?.Invoke();
            }
            catch (Exception e) { status.Text = e.Message; }
            finally { if (!register.IsDisposed) register.Enabled = true; }
        };
        var select = new Button { Name = "KitchenImportPdv", Text = "IMPORTAR PRODUTOS DO PDV", Dock = DockStyle.Fill };
        select.Click += (_, _) => SelectMenu();
        root.Controls.Add(ready,0,5);root.Controls.Add(register,0,6);root.Controls.Add(select,0,7);root.Controls.Add(cards,0,8);Controls.Add(root);
        KitchenPremiumStyle.Apply(this);
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
        menuLoaded = false;
        loading=true;reload.Enabled=false;status.Text="Carregando produtos...";
        try
        {
            var data=await Task.Run(() => ReadProducts());if(IsDisposed)return;
            products=data;
            if (!menuLoaded)
            {
                string identity = await Task.Run(() => new KitchenStore().StoreId());
                string endpoint = LealInfoPDV.Network.NetworkConfiguration.Current.Mode == "terminal"
                    ? LealInfoPDV.Network.NetworkConfiguration.Current.CertificateHash : Database.DbPath;
                string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(endpoint + ":" + identity)));
                menuPath = Path.Combine(Database.AppFolder, "KitchenMenu", key + ".json");
                menuIds = null;
                if (File.Exists(menuPath)) menuIds = ProtectedFile.Read<long[]>(menuPath).ToHashSet();
                menuLoaded = true;
            }
            string previous=Convert.ToString(category.SelectedItem)??"";
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
        var found=products.Where(p=>(menuIds == null || menuIds.Contains(p.Id)) && (selected==""||p.Category.Equals(selected,StringComparison.OrdinalIgnoreCase))&&(term==""||Match(p.Name)||Match(p.Barcode)||Match(p.Category))).ToArray();
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
    private void SelectMenu()
    {
        if (!Auth.CanManageCatalog) { status.Text = "Sem permissão para selecionar o cardápio."; return; }
        if (loading || !menuLoaded || menuPath == null) { status.Text = "Aguarde o carregamento dos produtos."; return; }
        using var dialog = new Form { Name = "KitchenPdvSelection", Text = "PRODUTOS DO PDV — CARDÁPIO", Size = new Size(620, 620), MinimumSize = new Size(500, 400), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false };
        var choices = new CheckedListBox { Name = "KitchenMenuChoices", Dock = DockStyle.Fill, CheckOnClick = true };
        foreach (var product in products) choices.Items.Add(product.Name + " • " + product.Barcode + " • " + product.Price.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")), menuIds == null || menuIds.Contains(product.Id));
        var save = new Button { Text = "SALVAR CARDÁPIO", Dock = DockStyle.Bottom, Height = 48 };
        var hint = new Label { Text = "Selecione os produtos já cadastrados. Preços, fotos e estoque são preservados.", Dock = DockStyle.Top, Height = 52, Padding = new Padding(10) };
        dialog.Controls.Add(choices); dialog.Controls.Add(save); dialog.Controls.Add(hint);
        save.Click += (_, _) =>
        {
            try
            {
                var ids = choices.CheckedIndices.Cast<int>().Select(i => products[i].Id).ToHashSet();
                Directory.CreateDirectory(Path.GetDirectoryName(menuPath)!);
                ProtectedFile.Write(menuPath, ids.OrderBy(id => id).ToArray()); menuIds = ids;
                dialog.DialogResult = DialogResult.OK;
            }
            catch (Exception e) { MessageBox.Show(dialog, e.Message, "Cardápio"); }
        };
        KitchenPremiumStyle.Apply(dialog);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) { Render(); CatalogChanged?.Invoke(); }
    }
    private Control Card(KitchenCatalogProduct product)
    {
        var card=new TableLayoutPanel{Width=156,Height=214,ColumnCount=1,RowCount=4,Padding=new Padding(8),BackColor=Color.White,Margin=new Padding(4)};
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
        KitchenPremiumStyle.Apply(card);
        return card;
    }
}
