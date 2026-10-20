using LealInfoPDV;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

static class Program
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new();
    static void Check(bool value, string text) { if (!value) throw new Exception(text); checks.Add(text); Console.WriteLine("PASS " + text); }
    static T Field<T>(KitchenForm form, string name) => (T)typeof(KitchenForm).GetField(name, P)!.GetValue(form)!;
    static void Pump(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready() && watch.ElapsedMilliseconds < 15000) { Application.DoEvents(); Thread.Sleep(10); }
        Check(ready(), "UI operation completed within timeout");
    }
    static string Snapshot()
    {
        using var db = Database.Open(); var names = new List<string>();
        using(var q=db.CreateCommand()) { q.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'kitchen_%' ORDER BY name"; using var r=q.ExecuteReader(); while(r.Read())names.Add(r.GetString(0)); }
        var rows = new List<object>();
        foreach(var name in names)
        {
            using var q=db.CreateCommand(); q.CommandText="SELECT * FROM \"" + name.Replace("\"","<invalid>") + "\" ORDER BY rowid";
            using var r=q.ExecuteReader(); var table=new List<string[]>();
            while(r.Read())table.Add(Enumerable.Range(0,r.FieldCount).Select(i=>r.IsDBNull(i)?"<null>":Convert.ToString(r.GetValue(i),System.Globalization.CultureInfo.InvariantCulture)!).ToArray());
            rows.Add(new{name,table});
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));
    }
    [STAThread] static int Main()
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" || !OperatingSystem.IsWindows())throw new Exception("Disposable Windows runner only");
        using var watchdog=new System.Threading.Timer(_=>{Console.Error.WriteLine("FAIL UI timeout");Environment.Exit(93);},null,60000,Timeout.Infinite);
        string evidence=Path.GetFullPath("new-order-evidence");Directory.CreateDirectory(evidence);
        try
        {
            Check(!File.Exists(Database.DbPath),"empty disposable profile");
            Database.Initialize();Auth.CreateUser("QA NOVO PEDIDO","neworderqa","QA-isolated-123!","ADMINISTRADOR","qa@example.invalid","");
            Auth.Login("neworderqa","QA-isolated-123!");
            string before=Snapshot();Application.EnableVisualStyles();
            using var central=new KitchenForm();central.Show();
            var fresh=Field<Button>(central,"fresh");var send=Field<Button>(central,"send");
            var grid=Field<DataGridView>(central,"items");var message=Field<Label>(central,"message");
            Pump(()=>message.Text.Contains("Cozinha conectada") && fresh.Enabled && Field<SemaphoreSlim>(central,"gate").CurrentCount==1);
            Field<System.Windows.Forms.Timer>(central,"retry").Stop();
            Check(!Application.OpenForms.Cast<Form>().Any(f=>f.Name=="KitchenOrderEntry"),"entry window opens only on NOVO PEDIDO click");
            Form Open()
            {
                fresh.PerformClick();Application.DoEvents();
                var window=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="KitchenOrderEntry");
                Check(window.Visible && window.Owner==central && !window.Modal && central.Visible && central.Enabled,"real floating window opens over the enabled kitchen central");
                Check(grid.FindForm()==window && send.FindForm()==window,"items and send belong to the floating window");
                Pump(()=>grid.IsCurrentCellInEditMode && grid.ContainsFocus);
                return window;
            }
            var firstWindow=Open();
            Check(grid.Rows.Cast<DataGridViewRow>().Count(r=>!r.IsNewRow)==1 && Convert.ToString(grid.Rows[0].Cells[1].Value)=="1","new floating draft starts with quantity 1");
            var reference=Field<TextBox>(central,"reference");var notes=Field<TextBox>(central,"notes");
            reference.Text="RASCUNHO";notes.Text="PRESERVAR";
            ((TextBox)grid.EditingControl!).Text="ITEM EM EDICAO";grid.EndEdit();
            fresh.PerformClick();Application.DoEvents();
            Check(Application.OpenForms.Cast<Form>().Count(f=>f.Name=="KitchenOrderEntry")==1 && reference.Text=="RASCUNHO","repeated click activates the same window without deleting draft");
            firstWindow.Close();Application.DoEvents();
            Check(!firstWindow.Visible && !firstWindow.IsDisposed,"close hides draft window without losing data");
            var reopened=Open();
            Check(ReferenceEquals(firstWindow,reopened) && reference.Text=="RASCUNHO" && notes.Text=="PRESERVAR" && Convert.ToString(grid.Rows[0].Cells[0].Value)=="ITEM EM EDICAO","reopen restores unsent draft");
            var store=new KitchenStore();int count=store.List().Count;
            reference.Text="QA PRIMEIRO";notes.Text="SEM CEBOLA";
            grid.EndEdit();grid.Rows[0].Cells[1].Value="0";send.PerformClick();
            Pump(()=>fresh.Enabled && Field<SemaphoreSlim>(central,"gate").CurrentCount==1);
            Check(reopened.Visible && store.List().Count==count && send.Enabled,"invalid quantity retains floating draft without persisting order");
            grid.Rows[0].Cells[1].Value="1,5";
            send.PerformClick();
            Pump(()=>fresh.Enabled && Field<SemaphoreSlim>(central,"gate").CurrentCount==1 && !reopened.Visible && store.List().Count==count+1);
            var first=store.List().Single(o=>o.Reference=="QA PRIMEIRO");
            Check(store.Get(first.Id).Items.Single().Quantity==1.5m && store.Get(first.Id).Notes=="SEM CEBOLA","floating send persists decimal quantity and notes exactly once");
            send.PerformClick();Application.DoEvents();Check(store.List().Count==count+1,"hidden sent draft cannot be duplicated");
            var secondWindow=Open();
            Check(reference.Text=="" && notes.Text=="" && Convert.ToString(grid.Rows[0].Cells[0].Value)=="","next order gets a clean floating draft");
            ((TextBox)grid.EditingControl!).Text="SEGUNDO ITEM";grid.EndEdit();reference.Text="QA SEGUNDO";send.PerformClick();
            Pump(()=>fresh.Enabled && Field<SemaphoreSlim>(central,"gate").CurrentCount==1 && !secondWindow.Visible && store.List().Count==count+2);
            Check(store.Get(first.Id).Items.Single().Description=="ITEM EM EDICAO","new floating order preserves previously submitted order");
            var finalWindow=Open();var outbox=Field<KitchenOutbox>(central,"outbox");var id=Field<string>(central,"storeId");
            var pending=new KitchenDraft(Guid.NewGuid(),"QA PENDENTE","PRESERVAR","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("PENDENTE",1,"")});outbox.Queue(id,pending);
            var files=Directory.GetFiles(Database.AppFolder,"*.pending",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
            finalWindow.Close();Open();
            Check(outbox.Pending().Any(x=>x.Draft.Id==pending.Id) && files.All(x=>File.Exists(x.Key)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x.Key)))==x.Value),"floating close and reopen preserve encrypted pending bytes");
            using(var image=new Bitmap(finalWindow.Width,finalWindow.Height)){finalWindow.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(evidence,"novo-pedido-flutuante.png"));}
            Check(Snapshot()==before,"business data, users and settings unchanged");
            Check(UpdateManager.CurrentVersion=="10.418","candidate version 10.418");
            central.Close();Application.DoEvents();Check(finalWindow.IsDisposed,"closing central disposes its floating window");
            File.WriteAllText(Path.Combine(evidence,"fixed.json"),JsonSerializer.Serialize(new{success=true,checks},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(evidence,"failure.txt"),e.ToString());Console.Error.WriteLine(e);return 1;}
    }
}
