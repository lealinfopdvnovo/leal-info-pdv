using LealInfoPDV;
using System.Reflection;
using System.Runtime.InteropServices;
internal static class KitchenCorrectionsTests
{
    const BindingFlags P=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,P)!.GetValue(o)!;
    static object? Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,P)!.Invoke(o,args);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string? cls,string title);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    static long Count(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return Convert.ToInt64(q.ExecuteScalar());}
    static void Exec(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;q.ExecuteNonQuery();}
    internal static void Run(KitchenForm form,Action<bool,string> check,Action<Func<bool>> pump)
    {
        Exec("INSERT INTO products(id,name,price,stock,kitchen_preparation,track_stock) VALUES(7001,'QA LOJA',15,50,0,1),(7002,'QA COZINHA',10,50,1,1),(7003,'QA SEM ESTOQUE',2,0,1,0)");
        var products=KitchenCatalogView.ReadProducts();check(!products.Any(p=>p.Id==7001)&&products.Any(p=>p.Id==7002),"kitchen excludes store products and preserves kitchen products");
        Database.Initialize();check(Count("SELECT stock FROM products WHERE id=7001")==50,"additive migration repeat preserves existing stock");
        // Mandatory new product classification: a real save click without a choice must not insert anything.
        typeof(ProductRegistrationForm).GetMethod("EnsureSchema",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
        using(var registration=(Form)Activator.CreateInstance(typeof(ProductRegistrationForm),BindingFlags.NonPublic|BindingFlags.Instance,null,new object?[]{null},null)!)
        {
            registration.Show(form);var fields=Field<Dictionary<string,Control>>(registration,"_fields");((TextBox)fields["name"]).Text="QA REQUIRED CHOICE";
            var choice=(ComboBox)fields["kitchen"];check(choice.SelectedIndex==-1,"new product requires explicit kitchen yes/no selection");
            using var timer=new System.Windows.Forms.Timer{Interval=40};timer.Tick+=(_,_)=>{var window=FindWindow(null,"Classificação obrigatória");if(window!=IntPtr.Zero)PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);};timer.Start();Call(registration,"SaveProduct");timer.Stop();
            check(Count("SELECT COUNT(*) FROM products WHERE name='QA REQUIRED CHOICE'")==0,"saving without kitchen choice alerts and inserts no product");choice.SelectedIndex=1;
            timer.Tick+=(_,_)=>{var window=FindWindow(null,"LEAL INFO PDV");if(window!=IntPtr.Zero)PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);};timer.Start();Call(registration,"SaveProduct");timer.Stop();
            check(Count("SELECT COUNT(*) FROM products WHERE name='QA REQUIRED CHOICE' AND kitchen_preparation=0 AND kitchen_classified=1")==1,"explicit NO saves product as store and records confirmed classification");
            choice.SelectedIndex=0;timer.Start();Call(registration,"SaveProduct");timer.Stop();
            check(Count("SELECT COUNT(*) FROM products WHERE name='QA REQUIRED CHOICE' AND kitchen_preparation=1 AND kitchen_classified=1")==1,"explicit YES saves kitchen classification");registration.Close();
        }
        var store=Field<KitchenStore>(form,"store");string identity=store.StoreId();
        KitchenOrder Add(string mesa,params KitchenItem[] items)=>store.Submit(identity,new KitchenDraft(Guid.NewGuid(),mesa,"","QA LAYOUT",DateTimeOffset.UtcNow,items));
        var first=Add("Mesa 99",new KitchenItem("QA COZINHA",2,"",7002));var second=Add("Mesa 99",new KitchenItem("QA SEM ESTOQUE",1,"",7003));
        var bill=store.Quote(first.Id);check(bill.Orders.Length==2&&bill.Total==22,"payment covers all active orders of the selected table with rounded prices");
        long sales=Count("SELECT COUNT(*) FROM sales"),cash=Count("SELECT COUNT(*) FROM cash_movements");
        check(Count("SELECT COUNT(*) FROM sales")==sales,"opening quote and cancelling payment write no sale");
        bool invalid=false;try{store.Receive(bill,new[]{new KitchenPayment("Dinheiro",21)},"QA LAYOUT");}catch(InvalidOperationException){invalid=true;}check(invalid&&Count("SELECT COUNT(*) FROM sales")==sales,"wrong payment total changes no sale or stock");
        long sale=store.Receive(bill,new[]{new KitchenPayment("Dinheiro",10),new KitchenPayment("Cartão",12)},"QA LAYOUT");
        check(Count("SELECT COUNT(*) FROM sales")==sales+1&&Count("SELECT COUNT(*) FROM cash_movements")==cash+2,"payment writes one sale and correct split cash movements");
        check(Count("SELECT stock FROM products WHERE id=7002")==48&&Count("SELECT stock FROM products WHERE id=7003")==0,"stock decreases once only when stock control is enabled");
        check(store.Get(first.Id).Status=="ENTREGUE"&&store.Get(second.Id).Status=="ENTREGUE","successful payment closes all charged orders and releases table");
        check(store.Receive(bill,new[]{new KitchenPayment("Dinheiro",10),new KitchenPayment("Cartão",12)},"QA LAYOUT")==sale&&Count("SELECT COUNT(*) FROM sales")==sales+1&&Count("SELECT stock FROM products WHERE id=7002")==48,"retry after committed payment returns same sale without double charge");
        var stale=Add("Mesa 98",new KitchenItem("QA COZINHA",1,"",7002));var old=store.Quote(stale.Id);Add("Mesa 98",new KitchenItem("QA COZINHA",1,"",7002));
        invalid=false;try{store.Receive(old,new[]{new KitchenPayment("PIX",old.Total)},"QA LAYOUT");}catch(InvalidOperationException){invalid=true;}check(invalid&&Count("SELECT COUNT(*) FROM sales")==sales+1,"new order during payment invalidates quote atomically");
        var noStock=Add("Mesa 97",new KitchenItem("QA COZINHA",100,"",7002));var unavailable=store.Quote(noStock.Id);invalid=false;try{store.Receive(unavailable,new[]{new KitchenPayment("Dinheiro",unavailable.Total)},"QA LAYOUT");}catch(InvalidOperationException){invalid=true;}check(invalid&&Count("SELECT COUNT(*) FROM sales")==sales+1&&Count("SELECT stock FROM products WHERE id=7002")==48,"insufficient stock rolls back full payment");
        var manual=Add("Mesa 95",new KitchenItem("ITEM DIGITADO",2.5m,"SEM CEBOLA"));var manualBill=store.Quote(manual.Id);
        invalid=false;try{store.Receive(manualBill,new[]{new KitchenPayment("Dinheiro",1)},"QA LAYOUT");}catch(InvalidOperationException){invalid=true;}
        check(invalid&&manualBill.Items.Single().UnitPrice==0,"manual order requires explicit unit price before payment");
        manualBill=manualBill with{Items=manualBill.Items.Select(i=>i with{UnitPrice=12.34m}).ToArray()};
        long manualSale=store.Receive(manualBill,new[]{new KitchenPayment("Dinheiro",30.85m)},"QA LAYOUT");
        check(Count("SELECT COUNT(*) FROM sale_items WHERE sale_id="+manualSale+" AND product_id IS NULL AND qty=2.5 AND unit_price=12.34 AND total=30.85")==1,"manual item payment stores entered price and fractional quantity without a fake product");
        check(Count("SELECT stock FROM products WHERE id=7002")==48,"manual item payment does not change unrelated stock");
        // Timers must retain the explicitly selected order even when a newer order arrives.
        var draft=Field<DataGridView>(form,"items");draft.Rows.Clear();Call(form,"SetEntryEnabled",false);
        var task=(Task)Call(form,"RefreshOrders")!;pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();
        typeof(KitchenForm).GetField("dashboardDraft",P)!.SetValue(form,false);Call(form,"SelectSavedOrder",stale.Id);
        var panel=Field<KitchenOrderPanel>(form,"dashboardOrder");var since=Field<DateTimeOffset?>(panel,"started");
        Add("Mesa 96",new KitchenItem("QA COZINHA",1,"",7002));
        Field<System.Windows.Forms.Timer>(form,"dashboardClock").Stop();
        Exec("UPDATE kitchen_orders_v1 SET received_utc='"+DateTimeOffset.UtcNow.AddMinutes(-3).ToString("O")+"' WHERE id='"+stale.Id.ToString("D")+"'");
        task=(Task)Call(form,"RefreshOrders")!;pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();since=Field<DateTimeOffset?>(panel,"started");
        var card=Field<FlowLayoutPanel>(form,"orderCards").Controls.Cast<Control>().Single(c=>Equals(c.Tag,stale.Id));
        check(card.Controls.Find("ActiveOrderClock",true).Single().Text.StartsWith("03:"),"rebuilt active card immediately shows original elapsed time, never a temporary 00:00");
        Field<System.Windows.Forms.Timer>(form,"dashboardClock").Start();
        int resets=0;var elapsed=panel.Controls.Find("DashboardOrderElapsed",true).Single();elapsed.TextChanged+=(_,_)=>{if(Field<DateTimeOffset?>(panel,"started")!=since)resets++;};
        for(int i=0;i<3;i++){task=(Task)Call(form,"RefreshOrders")!;pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
        check(resets==0&&Field<DateTimeOffset?>(panel,"started")==since,"poll refresh and incoming order never reset selected order clock");
        check(panel.Controls.Find("KitchenReceivePayment",true).Single().Visible,"payment button is present on main order panel");
        // Imports preserve existing shop products and classify only new ready-catalog products.
        using(var import=new ReadyKitchenImportForm())
        {
            import.Show(form);var grid=import.Controls.Find("ReadyKitchenProducts",true).OfType<DataGridView>().Single();
            // Use the real event path for select-all; duplicates already imported by DashboardTests are harmless.
            var select=import.Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<Button>().Single(b=>b.Text=="SELECIONAR TODOS");select.PerformClick();
            foreach(DataGridViewRow row in grid.Rows)row.Cells["Price"].Value="12,50";
            import.Controls.Find("ImportReadyKitchen",true).OfType<Button>().Single().PerformClick();pump(()=>import.IsDisposed||!import.Visible);
            check(import.DialogResult==DialogResult.OK,"actual select-all and import button completes with OK after releasing busy guard");
        }
        check(Count("SELECT COUNT(*) FROM products WHERE barcode LIKE 'LEALMIX%' AND kitchen_preparation=1")==18&&Count("SELECT stock FROM products WHERE id=7001")==50,"ready kitchen import preserves existing store catalog and stock");
        bool sawConfiguration=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=30}){
            timer.Tick+=(_,_)=>{var config=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="KitchenConfiguration");if(config==null)return;timer.Stop();sawConfiguration=true;config.Controls.OfType<Button>().Single(b=>b.Text.StartsWith("QUANTIDADE")).PerformClick();};
            timer.Start();form.Controls.Find("KitchenHeaderAction_2",true).OfType<Button>().Single().PerformClick();timer.Stop();
        }
        var all=Application.OpenForms.Cast<Form>().SingleOrDefault(f=>f.Name=="AllKitchenTables");check(sawConfiguration&&all is {Visible:true,Enabled:true},"configuration opens usable table count and reservation window");all?.Close();
        bool sawPrinter=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=30}){
            timer.Tick+=(_,_)=>{var config=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="KitchenPrinterConfiguration");if(config==null)return;timer.Stop();var paper=config.Controls.Find("ComandaPaper",true).OfType<ComboBox>().Single();sawPrinter=paper.Items.Count==3&&paper.Items[0]!.ToString()!.Contains("80")&&paper.Items[1]!.ToString()!.Contains("58")&&paper.Items[2]!.ToString()!.Contains("A4");config.Close();};
            timer.Start();bool configured=KitchenComanda.Configure(form);timer.Stop();check(!configured&&sawPrinter,"printer chooser exposes thermal 80mm, thermal 58mm and nonthermal A4 and permits cancel");
        }
        foreach(int width in new[]{228,315}){
            using var page=new Bitmap(width,250);using var graphics=Graphics.FromImage(page);string text=string.Join("\n",Enumerable.Repeat("2,5 × PRODUTO COM NOME EXTENSO — SEM CEBOLA",40));int position=0,pages=0;
            while(position<text.Length&&pages++<100){int next=KitchenComanda.DrawPage(graphics,new Rectangle(12,12,width-24,220),text,position,9);if(next<=position)break;position=next;}
            check(position==text.Length,$"thermal comanda paginates all items within {width} hundredths-inch width");
        }
        // Restore empty UI draft for subsequent double-click tests; historical fixtures stay auditable.
        typeof(KitchenForm).GetField("dashboardDraft",P)!.SetValue(form,true);Call(form,"SetEntryEnabled",true);Call(form,"SyncDashboard");
    }
}
