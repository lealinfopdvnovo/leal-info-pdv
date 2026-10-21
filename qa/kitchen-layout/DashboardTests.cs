using LealInfoPDV;
using System.Reflection;
using System.Runtime.InteropServices;
internal static class DashboardTests
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,P)!.GetValue(o)!;
    static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,P)!.Invoke(o,args);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string? cls,string title);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    internal static void Run(KitchenForm form,Action<bool,string> check,Action<Func<bool>> pump,string evidence,Action<Form,Size,string,Action<Panel>?> render)
    {
        var draft=Field<DataGridView>(form,"items");draft.Rows.Clear();Call(form,"SetEntryEnabled",true);Call(form,"ResetItemFields");
        var reference=Field<TextBox>(form,"reference");reference.Clear();typeof(KitchenForm).GetField("dashboardDraft",P)!.SetValue(form,true);
        var menu=Field<KitchenCatalogView>(form,"mainCatalog");var tables=Field<KitchenTablesView>(form,"tables");var panel=Field<KitchenOrderPanel>(form,"dashboardOrder");var store=Field<KitchenStore>(form,"store");var fresh=Field<Button>(form,"fresh");
        tables.Controls.Find("KitchenTable_3",true).OfType<Button>().Single().PerformClick();
        check(panel.Controls.Find("DashboardOrderTitle",true).Single().Text.Contains("03"),"table selection binds the embedded order panel");
        menu.Controls.Find("CatalogProduct_1",true).OfType<Button>().Single().PerformClick();
        check(panel.Rows.Rows.Count==1&&draft.Rows.Count==1&&Field<Form>(form,"entryWindow").Visible==false,"main product adds to embedded draft without opening another window");
        panel.Controls.Find("DashboardSend",true).OfType<Button>().Single().PerformClick();
        pump(()=>fresh.Enabled&&store.ListActive().Any(o=>o.Reference=="Mesa 03"));
        var saved=store.ListActive().Single(o=>o.Reference=="Mesa 03");check(saved.Items.Single().ProductId==1,"embedded send persists the original product ID");
        check(!panel.Rows.Columns["Remove"].Visible,"saved order cannot be edited as an unsent draft");
        panel.Controls.Find("DashboardReady",true).OfType<Button>().Single().PerformClick();pump(()=>fresh.Enabled&&store.Get(saved.Id).Status=="PRONTO");
        check(store.History(saved.Id).Any(e=>e.Status=="PREPARANDO")&&store.History(saved.Id).Any(e=>e.Status=="PRONTO"),"MARCAR PRONTO uses valid revision-checked preparation and ready transitions");
        check(tables.Controls.Find("KitchenTable_3",true).Single().Text.Contains("PRONTA"),"table color follows actual ready order");
        bool previewOpened=false,noPrinter=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=100})
        {
            timer.Tick+=(_,_)=>
            {
                var preview=Application.OpenForms.OfType<PrintPreviewDialog>().FirstOrDefault();if(preview!=null){previewOpened=preview.Document?.DocumentName=="Comanda da cozinha";preview.Close();return;}
                var box=FindWindow(null,"Imprimir comanda");if(box!=IntPtr.Zero){GetWindowThreadProcessId(box,out uint pid);if(pid==(uint)Environment.ProcessId){noPrinter=true;PostMessage(box,0x0010,IntPtr.Zero,IntPtr.Zero);}}
            };
            timer.Start();panel.Controls.Find("DashboardPrint",true).OfType<Button>().Single().PerformClick();timer.Stop();
        }
        check(previewOpened||noPrinter,"real print button opens the preview or reports the runner's missing printer without spooling");
        tables.Controls.Find("KitchenTable_1",true).OfType<Button>().Single().PerformClick();menu.Controls.Find("CatalogProduct_1",true).OfType<Button>().Single().PerformClick();
        typeof(DataGridView).GetMethod("OnCellContentClick",P)!.Invoke(panel.Rows,new object[]{new DataGridViewCellEventArgs(3,0)});
        var entry=Field<Form>(form,"entryWindow");check(entry.Visible,"embedded edit opens the existing floating editor");
        Field<NumericUpDown>(form,"itemQuantity").Value=2.5m;Field<TextBox>(form,"itemNotes").Text="SEM CEBOLA";Field<Button>(form,"addItem").PerformClick();entry.Close();
        check(panel.Rows.Rows.Count==1&&Convert.ToString(panel.Rows.Rows[0].Cells[0].Value)=="2,5"&&Convert.ToString(panel.Rows.Rows[0].Cells[2].Value)=="SEM CEBOLA","quantity and item note are shared between floating editor and embedded panel");
        panel.Controls.Find("DashboardSend",true).OfType<Button>().Single().PerformClick();pump(()=>fresh.Enabled&&store.ListActive().Any(o=>o.Reference=="Mesa 01"));
        var text=KitchenComanda.Text("Mesa 01",store.ListActive().Single(o=>o.Reference=="Mesa 01").Items,"Observação geral QA");
        check(text.Contains("2,5")||text.Contains("2.5"),"comanda keeps exact decimal quantity");check(text.Contains("SEM CEBOLA")&&text.Contains("Observação geral QA"),"comanda preserves item and order notes");
        string large=string.Join("\n\n",Enumerable.Repeat(text,50));int offset=0,pages=0;
        while(offset<large.Length&&pages<30){using var page=new Bitmap(700,900);using var g=Graphics.FromImage(page);g.Clear(Color.White);int next=KitchenComanda.DrawPage(g,new Rectangle(40,40,620,820),large,offset);check(next>offset,"comanda pagination makes progress");offset=next;if(pages++==0)page.Save(Path.Combine(evidence,"comanda-preview-windows.png"));}
        check(offset==large.Length&&pages>1&&pages<30,"production comanda renderer paginates all content without dropping lines");
        // Build the comparison screenshot only in the disposable fixture; no automatic customer catalog import.
        var seeds=ReadyKitchenCatalog.Load();ReadyKitchenCatalog.Import(seeds.Select((p,i)=>(p.Code,Price:10m+i)).ToDictionary(x=>x.Code,x=>x.Price));
        var reload=menu.ReloadAsync();pump(()=>reload.IsCompleted);reload.GetAwaiter().GetResult();
        foreach(var pair in new[]{(2,"AGUARDANDO"),(5,"AGUARDANDO"),(6,"PREPARANDO"),(7,"PREPARANDO"),(8,"PRONTO"),(10,"AGUARDANDO")})
        {
            var order=store.Submit(store.StoreId(),new KitchenDraft(Guid.NewGuid(),$"Mesa {pair.Item1:00}","Dados sintéticos da comparação visual","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem(seeds[pair.Item1%seeds.Length].Name,1,"")}));
            if(pair.Item2!="AGUARDANDO")order=store.ChangeStatus(order.Id,order.Revision,"PREPARANDO","QA",Guid.NewGuid());if(pair.Item2=="PRONTO")store.ChangeStatus(order.Id,order.Revision,"PRONTO","QA",Guid.NewGuid());
        }
        var refresh=(Task)typeof(KitchenForm).GetMethod("RefreshOrders",P)!.Invoke(form,null)!;pump(()=>refresh.IsCompleted);refresh.GetAwaiter().GetResult();
        using var visual=new KitchenForm();visual.Show();
        pump(()=>Field<Label>(visual,"message").Text.Contains("Cozinha conectada")&&Field<Button>(visual,"fresh").Enabled);
        Field<System.Windows.Forms.Timer>(visual,"retry").Stop();
        var visualMenu=Field<KitchenCatalogView>(visual,"mainCatalog");pump(()=>visualMenu.Controls.Find("CatalogProduct_1",true).Length==1);
        Field<KitchenTablesView>(visual,"tables").Controls.Find("KitchenTable_1",true).OfType<Button>().Single().PerformClick();
        foreach(var viewport in new[]{new Size(1672,941),new Size(1920,1000),new Size(1366,720)})
        {
            render(visual,viewport,Path.Combine(evidence,$"referencia-dashboard-{viewport.Width}.png"),host=>
            {
                var order=host.Controls.Find("KitchenDashboardOrder",true).Single();
                foreach(var name in new[]{"DashboardSend","DashboardPrint","DashboardReady"}){var button=host.Controls.Find(name,true).Single();var bounds=order.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));check(order.ClientRectangle.Contains(bounds),$"premium order action visible: {name} at {viewport}");}
                check(host.Controls.Find("MainTables",true).Single().Controls.Count==10,"reference comparison retains ten main tables");
                var orderGrid=host.Controls.Find("KitchenDashboardOrder",true).OfType<KitchenOrderPanel>().Single().Rows;
                check(orderGrid.RowsDefaultCellStyle.BackColor.GetBrightness()<.2f&&orderGrid.AlternatingRowsDefaultCellStyle.BackColor.GetBrightness()<.2f,"order rows have explicit dark surfaces, including alternating rows");
                check(orderGrid.Rows.Count>0&&orderGrid.GetRowDisplayRectangle(0,false).Bottom<=orderGrid.ClientRectangle.Bottom,"selected order shows a complete first item row");
                foreach(var timerLabel in host.Controls.Find("ActiveOrderClock",true)){check(timerLabel.Parent!.ClientRectangle.Contains(timerLabel.Bounds)&&!timerLabel.Parent.Controls.Cast<Control>().Any(c=>c!=timerLabel&&c.Bounds.IntersectsWith(timerLabel.Bounds)),"active order timer has its own unobscured area");}
                var strip=host.Controls.Find("KitchenOrderCards",true).OfType<FlowLayoutPanel>().Single();
                check(strip.Controls.Cast<Control>().Where(c=>c is Panel).All(c=>c.Bottom<=strip.ClientSize.Height),"active order cards and timers are fully visible vertically");
                check(host.Controls.Find("MainTables",true).Single().Controls.Cast<Control>().All(c=>c.Height>=70),"ten main table buttons retain readable height");
                var rail=host.Controls.Find("KitchenCategoryRail",true).OfType<FlowLayoutPanel>().Single();check(!rail.HorizontalScroll.Visible,"category rail does not add a clipping horizontal scrollbar");
            });
        }
        check(Field<FlowLayoutPanel>(form,"orderCards").FlowDirection==FlowDirection.LeftToRight,"active orders use the reference horizontal composition");
    }
}
