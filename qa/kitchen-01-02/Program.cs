using LealInfoPDV;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
static class CorrectionsTests
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new();
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks.Add(why);Console.WriteLine("PASS "+why);}
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,P)!.GetValue(o)!;
    static object? Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,P)!.Invoke(o,args);
    static void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(12);while(!done()&&DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}if(!done())throw new Exception("Timed out waiting for targeted UI action");}
    static IEnumerable<Control> All(Control c)=>new[]{c}.Concat(c.Controls.Cast<Control>().SelectMany(All));
    static string Rows(KitchenOrderPanel panel)=>string.Join("|",panel.Rows.Rows.Cast<DataGridViewRow>().Select(r=>$"{r.Cells[0].Value}:{r.Cells[1].Value}:{r.Cells[2].Value}"));
    static string Financial(){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT (SELECT COUNT(*) FROM sales)||'|'||(SELECT COUNT(*) FROM sale_payments)||'|'||(SELECT COUNT(*) FROM cash_movements)||'|'||(SELECT group_concat(id||':'||price||':'||stock,';') FROM products ORDER BY id)";return Convert.ToString(q.ExecuteScalar())!;}
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string? cls,string title);
    static void ClickCell(DataGridView grid,string column,bool right=false){
        var cell=grid.GetCellDisplayRectangle(grid.Columns[column].Index,0,true);Check(cell.Width>=24&&cell.Height>=24,"status cell is fully visible with usable click target");
        var point=new IntPtr(((cell.Top+cell.Height/2)<<16)|(cell.Left+cell.Width/2));
        SendMessage(grid.Handle,right?0x0204u:0x0201u,new IntPtr(right?2:1),point);SendMessage(grid.Handle,right?0x0205u:0x0202u,IntPtr.Zero,point);Application.DoEvents();
    }
    static void Capture(Form f,string name){using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("corrections-evidence/"+name+".png");}
    [STAThread]static int Main(){
        if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Disposable Windows CI only");
        Directory.CreateDirectory("corrections-evidence");using var watchdog=new System.Threading.Timer(_=>Environment.Exit(93),null,90000,Timeout.Infinite);
        try{
            Check(!File.Exists(Database.DbPath),"isolated empty Windows profile");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();
            Database.Initialize();Auth.CreateUser("QA 01 02","qa0102","QA-0102-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qa0102","QA-0102-123!");
            using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="INSERT INTO products(id,barcode,name,category,price,cost,stock,active,kitchen_preparation) VALUES(1,'QA0102A','ARROZ NOVO QA','COZINHA',12.34,3,40,1,1),(2,'QA0102B','FEIJAO NOVO QA','COZINHA',23.45,4,50,1,1)";q.ExecuteNonQuery();}
            string money=Financial();var store=new KitchenStore();store.EnsureReady();store.EnsureSeatingReady();
            KitchenOrder Seed(string table,string text)=>store.Submit(store.StoreId(),new KitchenDraft(Guid.NewGuid(),table,"","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem(text,1,"")}));
            var old=Seed("Mesa 01","PRODUTO ANTIGO MESA 01");var other=Seed("Mesa 03","PRODUTO OUTRA MESA 03");
            InternalWorkspace.Install();PremiumGlobalTheme.Install();using var form=new KitchenForm();form.Show();
            var menu=Field<KitchenCatalogView>(form,"mainCatalog");var fresh=Field<Button>(form,"fresh");var panel=Field<KitchenOrderPanel>(form,"dashboardOrder");
            Pump(()=>fresh.Enabled&&menu.Controls.Find("CatalogProduct_1",true).Length==1&&menu.Controls.Find("CatalogReload",true).OfType<Button>().Single().Enabled);Field<System.Windows.Forms.Timer>(form,"retry").Stop();
            Call(form,"OpenTableWindow",1);Application.DoEvents();var floating=Field<KitchenOrderPanel>(form,"floatingTableOrder");
            Check(Rows(panel).Contains(old.Items[0].Description),"fixture selects existing table with old order");
            menu.Controls.Find("CatalogProduct_1",true).OfType<Button>().Single().PerformClick();
            // Edit quantity and note through the existing draft editor.
            Call(form,"EditDashboardItem",0);Field<NumericUpDown>(form,"itemQuantity").Value=2.5m;Field<TextBox>(form,"itemNotes").Text="SEM SAL QA";Field<Button>(form,"addItem").PerformClick();Field<Form>(form,"entryWindow").Hide();
            Check(!panel.Rows.Columns["StatusMenu"].Visible,"draft keeps edit/remove controls and hides status arrow");
            var send=All(panel).OfType<Button>().Single(b=>b.Name=="DashboardSend");Pump(()=>send.Enabled&&fresh.Enabled);send.PerformClick();
            Pump(()=>fresh.Enabled&&store.ListActive().Any(o=>o.Reference=="Mesa 01"&&o.Items[0].ProductId==1));
            var submitted=store.ListActive().Single(o=>o.Reference=="Mesa 01"&&o.Items[0].ProductId==1);
            Check(Field<Guid?>(form,"selectedOrderId")==submitted.Id,"confirmed new order stays selected after hidden grid selection events");
            Check(Rows(panel).Contains("ARROZ NOVO QA")&&!Rows(panel).Contains("PRODUTO ANTIGO")&&!Rows(panel).Contains("OUTRA MESA"),"main table panel updates immediately after send without clicking table again");
            Check(Rows(floating)==Rows(panel),"open floating table panel updates immediately with the same submitted products");
            Check(submitted.Items.Single().Quantity==2.5m&&submitted.Items.Single().Notes=="SEM SAL QA"&&submitted.Status=="AGUARDANDO","sending preserves exact quantity, note and initial status");
            Check(store.Get(old.Id).Items[0].Description==old.Items[0].Description&&store.Get(other.Id).Items[0].Description==other.Items[0].Description&&store.Get(other.Id).Status==other.Status,"older table orders remain unchanged");
            Check(Financial()==money,"submit and panel refresh do not change prices, stock, sales, payments or cash");Capture(form,"01-main-after-send");Capture(Field<Form>(form,"tableWindow"),"01-floating-after-send");
            // A second table submission must not leak items from the first table.
            Call(form,"OpenTableWindow",2);menu.Controls.Find("CatalogProduct_2",true).OfType<Button>().Single().PerformClick();send=All(floating).OfType<Button>().Single(b=>b.Name=="DashboardSend");Pump(()=>send.Enabled&&fresh.Enabled);send.PerformClick();
            Pump(()=>fresh.Enabled&&store.ListActive().Any(o=>o.Reference=="Mesa 02"&&o.Items[0].ProductId==2));var second=store.ListActive().Single(o=>o.Reference=="Mesa 02"&&o.Items[0].ProductId==2);
            Check(Rows(panel).Contains("FEIJAO NOVO QA")&&!Rows(panel).Contains("ARROZ NOVO QA")&&!Rows(panel).Contains("PRODUTO OUTRA"),"sending on another table immediately replaces prior table products");Check(Rows(floating)==Rows(panel),"floating and main views stay synchronized when switching tables and sending");
            Check(panel.Rows.Columns["StatusMenu"].Visible&&floating.Rows.Columns["StatusMenu"].Visible,"each persisted product exposes a small status arrow in both panels");
            // Native left mouse button opens the menu; existing right mouse shortcut uses that same menu.
            ClickCell(floating.Rows,"StatusMenu");var states=floating.Rows.ContextMenuStrip!;Check(states.Visible&&states.Items.Cast<ToolStripItem>().Select(i=>i.Text).SequenceEqual(new[]{"Preparando","Pronto","Entregue","Cancelado"}),"left click on arrow opens all four requested status options");
            states.Close();ClickCell(floating.Rows,"Product",true);Check(states.Visible,"native right click remains a working shortcut to status menu");states.Close();
            void Status(KitchenOrderPanel view,string target){ClickCell(view.Rows,"StatusMenu");var state=view.Rows.ContextMenuStrip!;Check(state.Visible,"arrow menu opens for status "+target);state.Items.Cast<ToolStripItem>().Single(i=>Convert.ToString(i.Tag)==target).PerformClick();state.Close();Pump(()=>fresh.Enabled&&store.Get(second.Id).Status==target);}
            Status(floating,"PREPARANDO");Check(All(panel).Single(c=>c.Name=="DashboardOrderTitle").Text.Contains("PREPARANDO"),"preparing updates main and floating title from persisted status");
            var tables=Field<KitchenTablesView>(form,"tables");Check(tables.Controls.Find("KitchenTable_2",true).Single().Text.Contains("PREPARANDO"),"table indicator updates after preparing from arrow");
            Status(panel,"PRONTO");Check(All(floating).Single(c=>c.Name=="DashboardOrderTitle").Text.Contains("PRONTO"),"ready from main arrow updates floating panel immediately");
            Check(tables.Controls.Find("KitchenTable_2",true).Single().Text.Contains("PRONTA"),"ready indicator refreshes without another table click");Capture(form,"02-main-status-arrow");Capture(Field<Form>(form,"tableWindow"),"02-floating-status-arrow");
            Status(floating,"ENTREGUE");Check(store.History(second.Id).Select(e=>e.Status).SequenceEqual(new[]{"AGUARDANDO","PREPARANDO","PRONTO","ENTREGUE"}),"arrow status changes retain existing revision checked history");
            Call(form,"SelectDashboardTable",1);var selected=Field<Guid?>(form,"selectedOrderId")!.Value;
            using(var yes=new System.Windows.Forms.Timer{Interval=100}){yes.Tick+=(_,_)=>{var box=FindWindow(null,"Cozinha");if(box!=IntPtr.Zero)SendMessage(box,0x0111,new IntPtr(6),IntPtr.Zero);};yes.Start();ClickCell(panel.Rows,"StatusMenu");panel.Rows.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(i=>Convert.ToString(i.Tag)=="CANCELADO").PerformClick();panel.Rows.ContextMenuStrip.Close();Pump(()=>fresh.Enabled&&store.Get(selected).Status=="CANCELADO");yes.Stop();}
            Check(store.Get(selected).Status=="CANCELADO","cancel option retains existing confirmation and updates selected order");
            Check(Financial()==money,"all status shortcuts preserve prices, stock, sales, payments and cash values");
            Check(panel.BackColor==KitchenPremiumStyle.Background&&floating.BackColor==KitchenPremiumStyle.Background&&panel.Rows.DefaultCellStyle.BackColor.GetBrightness()<.2f,"both panels preserve premium dark surfaces and 3D action rendering");
            var missing=Guid.NewGuid();Call(form,"SelectSavedOrder",missing);Check(panel.Rows.Rows.Count==0&&floating.Rows.Rows.Count==0,"missing saved selection clears products instead of leaking stale draft or another table");
            form.Close();Check(form.IsDisposed,"targeted windows close cleanly");
            File.WriteAllText("corrections-evidence/results.json",JsonSerializer.Serialize(new{success=true,channel=Environment.GetEnvironmentVariable("CHANNEL"),source=Environment.GetEnvironmentVariable("APPROVED_SOURCE"),checks,nativeWindows=true,published=false}));Console.WriteLine("PASS ONLY KITCHEN CORRECTIONS 01 AND 02");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("corrections-evidence/failure.txt",e.ToString());return 1;}
    }
}
