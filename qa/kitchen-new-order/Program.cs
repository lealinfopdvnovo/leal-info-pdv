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
    [STAThread] static int Main(string[] args)
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" || !OperatingSystem.IsWindows())throw new Exception("Disposable Windows runner only");
        bool baseline=args.Contains("--baseline");
        string evidence=Path.GetFullPath("new-order-evidence"); Directory.CreateDirectory(evidence);
        try
        {
            if(baseline)
            {
                Check(!File.Exists(Database.DbPath),"empty disposable profile");
                Database.Initialize();
                Auth.CreateUser("QA NOVO PEDIDO","neworderqa","QA-isolated-123!","ADMINISTRADOR","qa@example.invalid","");
            }
            Auth.Login("neworderqa","QA-isolated-123!");
            string before=Snapshot();
            Application.EnableVisualStyles();
            using var form=new KitchenForm(); form.Show();
            var fresh=Field<Button>(form,"fresh"); var send=Field<Button>(form,"send");
            var grid=Field<DataGridView>(form,"items"); var message=Field<Label>(form,"message");
            Pump(()=>message.Text.Contains("Cozinha conectada") && fresh.Enabled);
            Field<System.Windows.Forms.Timer>(form,"retry").Stop();
            int DataRows()=>grid.Rows.Cast<DataGridViewRow>().Count(r=>!r.IsNewRow);
            if(baseline)
            {
                string previous=message.Text;
                fresh.PerformClick(); Application.DoEvents();
                Check(DataRows()==0 && message.Text==previous,"REPRODUCED 10.416 R2: blank NOVO PEDIDO click has no visible response");
            }
            else
            {
                var store=new KitchenStore(); int count=store.List().Count;
                var reference=Field<TextBox>(form,"reference"); var notes=Field<TextBox>(form,"notes");
                var hint=Field<Label>(form,"entryHint");
                void Fresh()
                {
                    fresh.PerformClick(); Application.DoEvents();
                    Check(DataRows()==1 && Convert.ToString(grid.Rows[0].Cells[1].Value)=="1","new order has exactly one editable item with quantity 1");
                    Console.WriteLine($"FOCUS cell={grid.CurrentCell?.ColumnIndex}/{grid.CurrentCell?.RowIndex} editing={grid.IsCurrentCellInEditMode} focus={grid.ContainsFocus} enabled={grid.Enabled} readonly={grid.ReadOnly} active={form.ActiveControl?.GetType().Name} editEnabled={grid.EditingControl?.Enabled}");
                    try { Pump(() => grid.CurrentCell?.ColumnIndex==0 && grid.IsCurrentCellInEditMode && grid.ContainsFocus); }
                    catch { Console.WriteLine($"FOCUS FAILED cell={grid.CurrentCell?.ColumnIndex}/{grid.CurrentCell?.RowIndex} editing={grid.IsCurrentCellInEditMode} focus={grid.ContainsFocus} enabled={grid.Enabled} readonly={grid.ReadOnly} active={form.ActiveControl?.GetType().Name} editEnabled={grid.EditingControl?.Enabled} message={message.Text}"); throw; }
                    Check(true,"cursor is editing the item field");
                    Check(hint.Text.StartsWith("Novo pedido iniciado") && hint.Visible,"persistent visible acknowledgement");
                    Check(send.Enabled && !grid.ReadOnly && !reference.ReadOnly && !notes.ReadOnly,"entry enabled");
                }
                Fresh(); Fresh();
                reference.Text="DESCARTAR RASCUNHO"; notes.Text="APENAS RASCUNHO";
                ((TextBox)grid.EditingControl!).Text="EDICAO AINDA NAO CONFIRMADA";
                Fresh();
                Check(reference.Text=="" && notes.Text=="" && Convert.ToString(grid.Rows[0].Cells[0].Value)=="","active cell edit and draft fields cleared");
                ((TextBox)grid.EditingControl!).Text="ITEM QA UM"; grid.EndEdit(); reference.Text="QA PRIMEIRO";
                send.PerformClick();
                Check(!fresh.Enabled,"new order disabled while real send is in progress");
                Pump(()=>fresh.Enabled && !send.Enabled && store.List().Count==count+1);
                send.PerformClick(); Application.DoEvents();
                Check(store.List().Count==count+1,"sent draft cannot be duplicated");
                var first=store.List().Single(o=>o.Reference=="QA PRIMEIRO");
                Fresh();
                Check(store.Get(first.Id).Items.Single().Description=="ITEM QA UM","new order preserves registered order and items");
                ((TextBox)grid.EditingControl!).Text="ITEM QA DOIS"; grid.EndEdit(); reference.Text="QA SEGUNDO";
                send.PerformClick(); Pump(()=>fresh.Enabled && !send.Enabled && store.List().Count==count+2);
                Check(store.List().Count==count+2,"second order sent after new order click");
                Fresh();
                var outbox=Field<KitchenOutbox>(form,"outbox"); var id=Field<string>(form,"storeId");
                var pending=new KitchenDraft(Guid.NewGuid(),"QA PENDENTE","PRESERVAR","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("PENDENTE",1,"")});
                outbox.Queue(id,pending);
                var files=Directory.GetFiles(Database.AppFolder,"*.pending",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
                Fresh();
                Check(outbox.Pending().Any(x=>x.Draft.Id==pending.Id) && files.All(x=>File.Exists(x.Key) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x.Key)))==x.Value),"durable pending bytes preserved by new order");
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(evidence,"novo-pedido.png"));}
                Check(UpdateManager.CurrentVersion=="10.417","candidate version 10.417");
            }
            Check(Snapshot()==before,"business data, users and settings preserved");
            form.Close();
            File.WriteAllText(Path.Combine(evidence,baseline?"baseline.json":"fixed.json"),JsonSerializer.Serialize(new{success=true,checks},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(evidence,"failure.txt"),e.ToString());Console.Error.WriteLine(e);return 1;}
    }
}
