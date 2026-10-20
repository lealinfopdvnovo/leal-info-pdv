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
            Database.Initialize();Auth.CreateUser("QA PEDIDO","neworderqa","QA-isolated-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("neworderqa","QA-isolated-123!");
            string before=Snapshot();Application.EnableVisualStyles();using var central=new KitchenForm();central.Show();
            var fresh=Field<Button>(central,"fresh");var send=Field<Button>(central,"send");var grid=Field<DataGridView>(central,"items");var message=Field<Label>(central,"message");
            Pump(()=>message.Text.Contains("Cozinha conectada")&&fresh.Enabled&&Field<SemaphoreSlim>(central,"gate").CurrentCount==1);
            Field<System.Windows.Forms.Timer>(central,"retry").Stop();
            fresh.PerformClick();Application.DoEvents();var window=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="KitchenOrderEntry");
            var name=Field<TextBox>(central,"itemDescription");var qty=Field<NumericUpDown>(central,"itemQuantity");var itemNotes=Field<TextBox>(central,"itemNotes");var add=Field<Button>(central,"addItem");
            var reference=Field<TextBox>(central,"reference");var notes=Field<TextBox>(central,"notes");
            Pump(()=>name.Focused);
            Check(window.Owner==central&&!window.Modal&&central.Enabled,"floating professional form keeps central available");
            Check(grid.ReadOnly&&!grid.AllowUserToAddRows&&!grid.RowHeadersVisible&&grid.Rows.Count==0,"review list has no spreadsheet entry or row selector");
            Check(add.FlatStyle==FlatStyle.Flat&&send.FlatStyle==FlatStyle.Flat&&send.BackColor!=SystemColors.Control,"styled primary actions");
            void Geometry()
            {
                Application.DoEvents();
                foreach(Control c in new Control[]{name,qty,itemNotes,add,send,reference,notes,grid})
                {
                    var bounds=window.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                    Check(c.Visible&&bounds.Width>40&&bounds.Height>15&&window.ClientRectangle.Contains(bounds),"visible control without clipping: "+c.Name);
                }
                Check(grid.Height>=65,"review area remains usable");
            }
            Geometry();var saved=window.Size;window.Size=new Size(820,640);Geometry();window.Size=saved;Application.DoEvents();
            add.PerformClick();Check(grid.Rows.Count==0&&message.Text.Contains("Informe o nome"),"empty item rejected without adding a blank row");
            name.Text="PRATO QA";qty.Value=1.5m;itemNotes.Text="SEM CEBOLA";add.PerformClick();
            Check(grid.Rows.Count==1&&KitchenDraft.Quantity(Convert.ToString(grid.Rows[0].Cells[1].Value)!)==1.5m,"form fields add item with exact decimal quantity");
            typeof(DataGridView).GetMethod("OnCellDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(grid,new object[]{new DataGridViewCellEventArgs(0,0)});
            Check(name.Text=="PRATO QA"&&qty.Value==1.5m,"double click loads item into form for editing");
            name.Text="PRATO QA EDITADO";qty.Value=2.5m;add.PerformClick();Check(grid.Rows.Count==1&&Convert.ToString(grid.Rows[0].Cells[0].Value)=="PRATO QA EDITADO","edit replaces item without duplication");
            name.Text="REMOVER QA";add.PerformClick();grid.CurrentCell=grid.Rows[1].Cells[0];
            window.Controls.Find("RemoveItem",true).OfType<Button>().Single().PerformClick();Check(grid.Rows.Count==1,"remove selected draft item");
            reference.Text="QA PRIMEIRO";notes.Text="OBSERVACAO GERAL";name.Text="RASCUNHO EM CAMPO";
            window.Close();Application.DoEvents();fresh.PerformClick();Application.DoEvents();
            Check(window.Visible&&name.Text=="RASCUNHO EM CAMPO"&&grid.Rows.Count==1,"reopen preserves pending item field and assembled draft");
            var store=new KitchenStore();int count=store.List().Count;send.PerformClick();Application.DoEvents();
            Check(store.List().Count==count&&window.Visible&&message.Text.Contains("em edição"),"unsaved item blocks sending without silent omission");
            name.Clear();send.PerformClick();
            Pump(()=>fresh.Enabled&&Field<SemaphoreSlim>(central,"gate").CurrentCount==1&&!window.Visible&&store.List().Count==count+1);
            var first=store.List().Single(o=>o.Reference=="QA PRIMEIRO");
            Check(store.Get(first.Id).Items.Single().Description=="PRATO QA EDITADO"&&store.Get(first.Id).Items.Single().Quantity==2.5m&&store.Get(first.Id).Items.Single().Notes=="SEM CEBOLA","real send preserves edited item quantity and notes");
            send.PerformClick();Application.DoEvents();Check(store.List().Count==count+1,"sent draft not duplicated");
            fresh.PerformClick();Application.DoEvents();Check(window.Visible&&grid.Rows.Count==0&&reference.Text==""&&notes.Text==""&&qty.Value==1,"next order starts clean");
            name.Text="SEGUNDO PEDIDO";add.PerformClick();reference.Text="QA SEGUNDO";send.PerformClick();
            Pump(()=>fresh.Enabled&&Field<SemaphoreSlim>(central,"gate").CurrentCount==1&&!window.Visible&&store.List().Count==count+2);
            fresh.PerformClick();Application.DoEvents();
            var outbox=Field<KitchenOutbox>(central,"outbox");var id=Field<string>(central,"storeId");var pending=new KitchenDraft(Guid.NewGuid(),"QA PENDENTE","PRESERVAR","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("PENDENTE",1,"")});outbox.Queue(id,pending);
            var files=Directory.GetFiles(Database.AppFolder,"*.pending",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
            window.Close();fresh.PerformClick();Application.DoEvents();Check(files.All(x=>File.Exists(x.Key)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x.Key)))==x.Value),"pending encrypted records unchanged");
            name.Text="Hambúrguer artesanal";qty.Value=2;itemNotes.Text="Sem cebola";add.PerformClick();reference.Text="Mesa 05";notes.Text="Servir juntos";
            using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(evidence,"pedido-profissional.png"));}
            Check(Snapshot()==before,"business data, users and settings preserved");Check(UpdateManager.CurrentVersion=="10.419","version 10.419");
            central.Close();Check(window.IsDisposed,"floating window disposed with central");
            File.WriteAllText(Path.Combine(evidence,"fixed.json"),JsonSerializer.Serialize(new{success=true,checks},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(evidence,"failure.txt"),e.ToString());Console.Error.WriteLine(e);return 1;}
    }
}
