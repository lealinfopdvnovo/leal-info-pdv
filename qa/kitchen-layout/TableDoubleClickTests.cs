using LealInfoPDV;
using System.Reflection;
using System.Runtime.InteropServices;
internal static class TableDoubleClickTests
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,P)!.GetValue(o)!;
    static void NativeDoubleClick(Control c)
    {
        var point=new IntPtr((Math.Max(1,c.Height/2)<<16)|(Math.Max(1,c.Width/2)&65535));
        SendMessage(c.Handle,0x0201,new IntPtr(1),point);SendMessage(c.Handle,0x0202,IntPtr.Zero,point);
        SendMessage(c.Handle,0x0203,new IntPtr(1),point);SendMessage(c.Handle,0x0202,IntPtr.Zero,point);
        Application.DoEvents();
    }
    internal static void Run(Action<bool,string> check,Action<Func<bool>> pump)
    {
        using var form=new KitchenForm();form.Show();
        var fresh=Field<Button>(form,"fresh");pump(()=>fresh.Enabled&&Field<Label>(form,"message").Text.Contains("Cozinha conectada"));
        Field<System.Windows.Forms.Timer>(form,"retry").Stop();
        var tables=Field<KitchenTablesView>(form,"tables");
        NativeDoubleClick(tables.Controls.Find("KitchenTable_9",true).Single());
        var entry=Application.OpenForms.Cast<Form>().SingleOrDefault(f=>f.Name=="KitchenOrderEntry"&&f.Owner==form);
        check(entry is { Visible:true,Modal:false },"native double click on free table opens an owned floating item editor");
        var reference=Field<TextBox>(form,"reference");check(reference.Text=="Mesa 09","floating editor binds exactly the double-clicked table");
        InternalWorkspace.Apply(entry!);PremiumGlobalTheme.Apply(entry!);
        check(entry!.BackColor==KitchenPremiumStyle.Background,"table editor preserves approved premium styling");
        Field<TextBox>(form,"itemDescription").Text="ITEM DUPLO CLIQUE";Field<NumericUpDown>(form,"itemQuantity").Value=2;Field<Button>(form,"addItem").PerformClick();
        var items=Field<DataGridView>(form,"items");check(items.Rows.Count==1,"table floating editor adds an item to its own draft");
        entry.Close();NativeDoubleClick(tables.Controls.Find("KitchenTable_9",true).Single());
        check(entry.Visible&&items.Rows.Count==1&&reference.Text=="Mesa 09","repeated double click reuses the editor without clearing the table draft");
        check(Application.OpenForms.Cast<Form>().Count(f=>f.Name=="KitchenOrderEntry"&&f.Owner==form)==1,"table double click never creates duplicate editors");
        NativeDoubleClick(tables.Controls.Find("KitchenTable_4",true).Single());
        check(reference.Text=="Mesa 09"&&items.Rows.Count==1,"another table cannot capture or discard the outstanding draft");
        items.CurrentCell=items.Rows[0].Cells[0];entry.Controls.Find("RemoveItem",true).OfType<Button>().Single().PerformClick();
        check(items.Rows.Count==0,"floating table editor removes a selected unsent item");entry.Close();
        NativeDoubleClick(tables.Controls.Find("KitchenTable_5",true).Single());
        var popup=Application.OpenForms.Cast<Form>().SingleOrDefault(f=>f.Name=="KitchenTableOrder"&&f.Owner==form);
        check(popup is { Visible:true,Modal:false }&&popup.Text.Contains("05"),"native double click on occupied table opens its floating operations window");
        InternalWorkspace.Apply(popup!);PremiumGlobalTheme.Apply(popup!);
        check(popup!.BackColor==KitchenPremiumStyle.Background,"table operations preserve approved premium styling");
        var panel=Field<KitchenOrderPanel>(form,"floatingTableOrder");
        check(panel.Rows.Rows.Count>0&&!panel.Rows.Columns["Remove"].Visible,"sent order is displayed with its existing edit protections");
        panel.Controls.Find("DashboardReady",true).OfType<Button>().Single().PerformClick();
        var store=Field<KitchenStore>(form,"store");pump(()=>fresh.Enabled&&store.ListActive().Any(o=>KitchenTablesView.TableNumber(o.Reference)==5&&o.Status=="PRONTO"));
        check(panel.Controls.Find("DashboardOrderTitle",true).Single().Text.Contains("PRONTO"),"floating ready action uses the existing persisted status flow and refreshes the window");
        popup.Controls.Find("TableAddItems",true).OfType<Button>().Single().PerformClick();
        check(entry.Visible&&reference.Text=="Mesa 05"&&items.Rows.Count==0,"occupied table adds new items in a separate correctly-bound draft");entry.Close();popup.Close();
        tables.Controls.Find("MoreKitchenTables",true).OfType<Button>().Single().PerformClick();
        var all=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="AllKitchenTables"&&f.Owner==form);
        NativeDoubleClick(all.Controls.Find("KitchenTable_11",true).Single());
        check(entry.Visible&&reference.Text=="Mesa 11","native double click also opens tables beyond the first ten");
        entry.Close();all.Close();form.Close();
    }
}
