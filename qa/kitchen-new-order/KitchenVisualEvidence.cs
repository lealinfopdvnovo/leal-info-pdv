using LealInfoPDV;
using System.Reflection;

internal static class KitchenVisualEvidence
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static T Field<T>(object source,string name)=>(T)source.GetType().GetField(name,Private)!.GetValue(source)!;
    private static IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Descendants(c)));
    internal static void Render(KitchenForm form,Action<bool,string> check,Action<Func<bool>> pump,string evidence)
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Synthetic visual fixture only");
        // These products/orders are inserted only after preservation assertions, in the disposable CI profile.
        var seeds=ReadyKitchenCatalog.Load();
        ReadyKitchenCatalog.Import(seeds.Select((p,i)=>(p.Code,Price:10m+i)).ToDictionary(x=>x.Code,x=>x.Price));
        var main=Field<KitchenCatalogView>(form,"mainCatalog");
        var load=main.ReloadAsync();pump(()=>load.IsCompleted);load.GetAwaiter().GetResult();
        using(var select=new System.Windows.Forms.Timer{Interval=100})
        {
            select.Tick+=(_,_)=>{
                var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="KitchenPdvSelection");
                if(dialog==null)return;select.Stop();
                var choices=dialog.Controls.Find("KitchenMenuChoices",true).OfType<CheckedListBox>().Single();
                for(int i=0;i<choices.Items.Count;i++)choices.SetItemChecked(i,true);
                Descendants(dialog).OfType<Button>().Single(b=>b.Text=="SALVAR CARDÁPIO").PerformClick();
            };
            select.Start();main.Controls.Find("KitchenImportPdv",true).OfType<Button>().Single().PerformClick();
        }
        var store=Field<KitchenStore>(form,"store");store.ChangeTableCount(40,store.Seating().Revision);

        foreach(var pair in new[]{(2,"AGUARDANDO"),(3,"PREPARANDO"),(5,"PRONTO"),(6,"AGUARDANDO"),(7,"PREPARANDO"),(10,"AGUARDANDO")})
        {
            var product=seeds[pair.Item1%seeds.Length];
            var order=store.Submit(store.StoreId(),new KitchenDraft(Guid.NewGuid(),"Mesa "+pair.Item1.ToString("00"),"EVIDÊNCIA VISUAL — DADOS SINTÉTICOS","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem(product.Name,1,"")}));
            if(pair.Item2 is "PREPARANDO" or "PRONTO")order=store.ChangeStatus(order.Id,order.Revision,"PREPARANDO","QA",Guid.NewGuid());
            if(pair.Item2=="PRONTO")store.ChangeStatus(order.Id,order.Revision,"PRONTO","QA",Guid.NewGuid());
        }
        var prior=store.Seating().Reservations.SingleOrDefault(r=>r.Number==40);
        store.ReserveTable(40,prior?.Revision??0,true,"EVIDÊNCIA SINTÉTICA","QA");
        var refresh=(Task)typeof(KitchenForm).GetMethod("RefreshOrders",Private)!.Invoke(form,null)!;
        pump(()=>refresh.IsCompleted);refresh.GetAwaiter().GetResult();
        pump(()=>main.Controls.Find("CatalogReload",true).OfType<Button>().Single().Enabled);
        Application.DoEvents();
        RenderClient(form,new Size(1680,940),Path.Combine(evidence,"cozinha-referencia-real-windows.png"));
        check(true,"native Windows client controls rendered at reference viewport without changing functional desktop tests");
        var tables=Field<KitchenTablesView>(form,"tables");
        tables.Controls.Find("MoreKitchenTables",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
        var popup=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="AllKitchenTables");Application.DoEvents();
        RenderClient(popup,new Size(1100,620),Path.Combine(evidence,"mesas-referencia-real-windows.png"));
        popup.Close();
        check(true,"real Windows reference screenshots generated from synthetic products, orders and persisted reservations");
    }
    // Render the same live child controls offscreen at a wide client viewport.
    // This is a control rendering check, not a claim that the CI desktop has this physical resolution.
    private static void RenderClient(Form source,Size size,string path)
    {
        var children=source.Controls.Cast<Control>().ToArray();
        using var host=new Panel{Size=size,Font=source.Font,BackColor=KitchenPremiumStyle.Background};
        source.SuspendLayout();host.SuspendLayout();
        try
        {
            foreach(var child in children){source.Controls.Remove(child);host.Controls.Add(child);}
            host.ResumeLayout(true);host.CreateControl();host.PerformLayout();
            using var bitmap=new Bitmap(size.Width,size.Height);
            host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,size));bitmap.Save(path);
        }
        finally
        {
            foreach(var child in children){host.Controls.Remove(child);source.Controls.Add(child);}
            source.ResumeLayout(true);
        }
    }

}
