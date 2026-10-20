using LealInfoPDV;
using LealInfoPDV.Network;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr FindWindow(string className, string title);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetLastActivePopup(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetDlgItem(IntPtr window, int id);
    static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new();
    static void Check(bool value, string text) { if (!value) throw new Exception(text); checks.Add(text); Console.WriteLine("PASS " + text); }
    static T Field<T>(KitchenForm form, string name) => (T)typeof(KitchenForm).GetField(name, P)!.GetValue(form)!;
    static void Pump(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready() && watch.ElapsedMilliseconds < 15000) { Application.DoEvents(); Thread.Sleep(10); }
        if (!ready())
        {
            foreach (var form in Application.OpenForms.Cast<Form>())
            {
                Console.Error.WriteLine($"DIAGNOSTIC FORM {form.Name} Visible={form.Visible} Enabled={form.Enabled}");
                if (form is KitchenForm kitchen)
                {
                    Console.Error.WriteLine("DIAGNOSTIC MESSAGE " + Field<Label>(kitchen, "message").Text);
                    Console.Error.WriteLine($"DIAGNOSTIC fresh={Field<Button>(kitchen, "fresh").Enabled} send={Field<Button>(kitchen, "send").Enabled} gate={Field<SemaphoreSlim>(kitchen, "gate").CurrentCount}");
                    Console.Error.WriteLine("DIAGNOSTIC PENDING " + Field<KitchenOutbox>(kitchen, "outbox").Pending().Count);
                }
            }
        }
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
        using var watchdog=new System.Threading.Timer(_=>{Console.Error.WriteLine("FAIL UI timeout");Environment.Exit(93);},null,90000,Timeout.Infinite);
        string evidence=Path.GetFullPath("new-order-evidence");Directory.CreateDirectory(evidence);
        try
        {
            Check(!File.Exists(Database.DbPath),"empty disposable profile");
            Database.Initialize();Auth.CreateUser("QA PEDIDO","neworderqa","QA-isolated-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("neworderqa","QA-isolated-123!");
            string photo=Path.Combine(Path.GetTempPath(),"catalog-qa-"+Guid.NewGuid()+".png");
            using(var fixture=new Bitmap(64,64)){using var g=Graphics.FromImage(fixture);g.Clear(Color.OrangeRed);fixture.Save(photo);}
            string photoHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(photo)));
            using(var db=Database.Open())using(var q=db.CreateCommand())
            {
                q.CommandText="INSERT INTO products(id,barcode,name,category,price,stock,active,photo_path) VALUES(1,'CARD001','Lanche catálogo','LANCHES',12.50,10,1,$photo),(2,'CARD002','Suco natural','BEBIDAS',7.25,20,1,''),(3,'INATIVO','Produto inativo','LANCHES',9,10,0,''),(4,'CARD004','Lanche catálogo','LANCHES',15,10,1,'missing-photo.png')";
                q.Parameters.AddWithValue("$photo",photo);q.ExecuteNonQuery();
            }
            string before=Snapshot();Application.EnableVisualStyles();using var central=new KitchenForm();central.Show();
            var fresh=Field<Button>(central,"fresh");var send=Field<Button>(central,"send");var grid=Field<DataGridView>(central,"items");var message=Field<Label>(central,"message");
            Pump(()=>message.Text.Contains("Cozinha conectada")&&fresh.Enabled&&Field<SemaphoreSlim>(central,"gate").CurrentCount==1);
            Field<System.Windows.Forms.Timer>(central,"retry").Stop();
            var tables = Field<KitchenTablesView>(central, "tables");
            SeatingTests.Run(Field<KitchenStore>(central,"store"),Check);tables.UpdateSeating(new KitchenStore().Seating());
            Check(Snapshot()==before,"seating migration and reservation lifecycle preserve all business data");
            var mainMenu = Field<KitchenCatalogView>(central, "mainCatalog");
            Check(Screen.FromControl(central).WorkingArea.Contains(central.Bounds), "premium dashboard initially fits available Windows desktop");
            Check(tables.Controls.Find("MainTables", true).Single().Controls.Count == 10, "premium main dashboard shows exactly ten tables");
            Check(mainMenu.Parent != null && mainMenu.Left < tables.Parent!.Left + tables.Left + mainMenu.Width, "product catalog is attached to left dashboard");
            tables.Controls.Find("MoreKitchenTables", true).OfType<Button>().Single().PerformClick();
            Application.DoEvents();
            var allTables = Application.OpenForms.Cast<Form>().Single(f => f.Name == "AllKitchenTables");
            Check(allTables.Owner == central && !allTables.Modal, "more tables is a floating owned window");
            var amount = allTables.Controls.Find("KitchenTableCount", true).OfType<NumericUpDown>().Single();
            var allCards = allTables.Controls.Find("AllKitchenTableCards", true).Single();
            foreach (int total in new[] { 20, 30, 40, 80 })
            {
                amount.Value = total; Pump(()=>allCards.Controls.Count==total&&fresh.Enabled&&Field<SemaphoreSlim>(central,"gate").CurrentCount==1);
                Check(allCards.Controls.Count == total && new KitchenStore().Seating().Count==total, "floating table capacity persists " + total);
            }
            var tableStore = new KitchenStore();
            string kitchenId = Field<string>(central, "storeId");
            var mesa = tableStore.Submit(kitchenId, new KitchenDraft(Guid.NewGuid(), "Mesa 40", "", "QA", DateTimeOffset.UtcNow, new[] { new KitchenItem("MESA QA", 1, "") }));
            Check(KitchenTablesView.TableNumber("Mesa 040") == 40 && KitchenTablesView.TableNumber("ORCAMENTO 40") == null, "only explicit table references are linked");
            foreach (string state in new[] { "OCUPADA", "PREPARANDO", "PRONTA", "LIVRE" })
            {
                tables.UpdateOrders(tableStore.ListActive());
                var table40 = allCards.Controls.Find("KitchenTable_40", true).OfType<Button>().Single();
                Check(table40.Text.Contains(state), "table status follows durable order: " + state);
                string? next = state switch { "OCUPADA" => "PREPARANDO", "PREPARANDO" => "PRONTO", "PRONTA" => "ENTREGUE", _ => null };
                if (next != null) mesa = tableStore.ChangeStatus(mesa.Id, mesa.Revision, next, "QA", Guid.NewGuid());
            }
            allCards.Controls.Find("KitchenTable_40", true).OfType<Button>().Single().PerformClick();
            Application.DoEvents();
            Check(Field<TextBox>(central, "reference").Text == "Mesa 40", "table click opens floating order bound to selected table");
            Field<TextBox>(central, "reference").Clear();
            allCards.Controls.Find("KitchenTable_10",true).OfType<Button>().Single().ContextMenuStrip!.Items[0].PerformClick();
            Pump(()=>new KitchenStore().Seating().Reservations.Any(r=>r.Number==10&&r.Reserved)&&fresh.Enabled);
            Check(allCards.Controls.Find("KitchenTable_10",true).OfType<Button>().Single().Text.Contains("RESERVADA"),"reserved table appears with its actual reservation state");
            allTables.Controls.Find("KitchenTableFilter_RESERVADA",true).OfType<Button>().Single().PerformClick();
            Check(allCards.Controls.Count==1,"reservation filter uses persisted table state");
            allCards.Controls.Find("KitchenTable_10",true).OfType<Button>().Single().ContextMenuStrip!.Items[1].PerformClick();
            Pump(()=>allCards.Controls.Count==0&&fresh.Enabled);
            allTables.Controls.Find("KitchenTableFilter_TODAS",true).OfType<Button>().Single().PerformClick();
            Check(allCards.Controls.Count==80,"all table filter restores full capacity");
            using(var popupImage=new Bitmap(allTables.Width,allTables.Height)){allTables.DrawToBitmap(popupImage,new Rectangle(Point.Empty,popupImage.Size));popupImage.Save(Path.Combine(evidence,"mesas-premium.png"));}
            allTables.Close(); Application.DoEvents();
            Check(Snapshot() == before, "table UI and status transitions preserve business data");
            using (var screen = new Bitmap(central.Width, central.Height)) { central.DrawToBitmap(screen, new Rectangle(Point.Empty, screen.Size)); screen.Save(Path.Combine(evidence, "cozinha-premium-preparacao.png")); }
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
            using(var probe=new TcpListener(IPAddress.Loopback,0))
            {
                probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
                var config=NetworkConfiguration.NewServer(port);
                using var key=RSA.Create(2048);var device=new DeviceKey(key.ExportPkcs8PrivateKeyPem(),key.ExportSubjectPublicKeyInfoPem());
                using var server=new NetworkDatabaseServer(config);server.Start();
                var terminal=new NetworkConfiguration{Mode="terminal",Host="127.0.0.1",Port=port,PairingSecret=config.PairingSecret,CertificateHash=server.CertificateHash};
                var remote=KitchenCatalogView.ReadProducts(()=>new PdvConnection(new NetworkDatabaseClient(terminal,"CATALOG-QA-TERMINAL",device)));
                Check(remote.Length==3&&remote.Single(p=>p.Id==1).Price==12.5m,"real authenticated TLS terminal reads same active catalog and prices");
                var remoteStore=new KitchenStore(()=>new PdvConnection(new NetworkDatabaseClient(terminal,"CATALOG-QA-TERMINAL",device)),false);
                remoteStore.EnsureSeatingReady();
                var remoteReservation=remoteStore.ReserveTable(28,0,true,"TERMINAL QA","QA");
                Check(new KitchenStore().Seating().Reservations.Single(r=>r.Number==28).Reserved,"real TLS terminal shares reservation with server");
                remoteStore.ReserveTable(28,remoteReservation.Reservations.Single(r=>r.Number==28).Revision,false,"","QA");
            }
            var catalog=Field<KitchenCatalogView>(central,"catalog");
            var catalogSearch=catalog.Controls.Find("CatalogSearch",true).OfType<TextBox>().Single();
            var catalogCategory=catalog.Controls.Find("CatalogCategory",true).OfType<ComboBox>().Single();
            var catalogCards=catalog.Controls.Find("CatalogCards",true).OfType<FlowLayoutPanel>().Single();
            Pump(()=>catalog.Controls.Find("CatalogReload",true).OfType<Button>().Single().Enabled&&catalog.Controls.Find("CatalogProduct_1",true).Length==1);
            Check(catalog.Controls.Find("CatalogProduct_3",true).Length==0&&catalog.Controls.Find("CatalogProduct_0",true).Length==0,"only active registered products appear in menu");
            Check(catalogCards.Controls.Count==3&&Descendants(catalogCards).OfType<Label>().Any(l=>l.Text.Contains("R$")&&l.Text.EndsWith("12,50")),"cards display registered prices in BRL and distinguish same-name products");
            Check(catalogCards.Controls.Cast<Control>().SelectMany(x=>x.Controls.OfType<PictureBox>()).Any(p=>p.Image!=null),"registered product photo displayed");
            catalogCategory.SelectedItem="BEBIDAS";Application.DoEvents();
            Check(catalog.Controls.Find("CatalogProduct_2",true).Length==1&&catalog.Controls.Find("CatalogProduct_1",true).Length==0,"category filter follows existing product categories");
            catalogCategory.SelectedIndex=0;catalogSearch.Text="LANCHE CATALOGO";Application.DoEvents();
            Check(catalogCards.Controls.Count==2,"accent-insensitive product search");
            catalogSearch.Text="CARD001";Application.DoEvents();
            Check(catalogCards.Controls.Count==1,"barcode search identifies exact product");
            qty.Value=2.5m;itemNotes.Text="SEM GELO";
            catalog.Controls.Find("CatalogProduct_1",true).OfType<Button>().Single().PerformClick();
            Check(grid.Rows.Count==1&&Equals(grid.Rows[0].Tag,1L)&&KitchenDraft.Quantity(Convert.ToString(grid.Rows[0].Cells[1].Value)!)==2.5m,"card click adds selected registered product and quantity");
            reference.Text="QA CARDAPIO";send.PerformClick();
            var catalogStore=new KitchenStore();
            Pump(()=>fresh.Enabled&&Field<SemaphoreSlim>(central,"gate").CurrentCount==1&&!window.Visible&&catalogStore.List().Any(o=>o.Reference=="QA CARDAPIO"));
            var catalogOrder=catalogStore.List().Single(o=>o.Reference=="QA CARDAPIO");
            Check(catalogStore.Get(catalogOrder.Id).Items.Single().ProductId==1&&catalogStore.Get(catalogOrder.Id).Items.Single().Notes=="SEM GELO","real order persists original product ID and kitchen note");
            Check(Snapshot()==before,"catalog selection and kitchen send do not change products prices stock or sales");
            fresh.PerformClick();Application.DoEvents();catalogSearch.Clear();catalogCategory.SelectedIndex=0;
            Pump(()=>catalog.Controls.Find("CatalogReload",true).OfType<Button>().Single().Enabled);
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
            Check(Snapshot()==before,"business data, users and settings preserved");Check(UpdateManager.CurrentVersion=="10.421","version 10.421");
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(photo)))==photoHash,"catalog photo remains unchanged and unlocked");// Verify the new menu selector against the real modal dialog.
            catalogSearch.Clear(); catalogCategory.SelectedIndex = 0;
            Pump(() => catalog.Controls.Find("CatalogReload", true).OfType<Button>().Single().Enabled && mainMenu.Controls.Find("CatalogReload", true).OfType<Button>().Single().Enabled);
            Exception? modalError = null;
            using (var chooseMenu = new System.Windows.Forms.Timer { Interval = 100 })
            {
                chooseMenu.Tick += (_, _) =>
                {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "KitchenPdvSelection");
                    if (dialog == null) return;
                    chooseMenu.Stop();
                    try
                    {
                        var choices = dialog.Controls.Find("KitchenMenuChoices", true).OfType<CheckedListBox>().Single();
                        for (int i = 0; i < choices.Items.Count; i++) choices.SetItemChecked(i, Convert.ToString(choices.Items[i])!.Contains("CARD001"));
                        Descendants(dialog).OfType<Button>().Single(b => b.Text == "SALVAR CARDÁPIO").PerformClick();
                    }
                    catch (Exception e) { modalError = e; dialog.Close(); }
                };
                chooseMenu.Start(); catalog.Controls.Find("KitchenImportPdv", true).OfType<Button>().Single().PerformClick();
            }
            Check(modalError == null, "existing product selection dialog works");
            Pump(() => mainMenu.Controls.Find("CatalogReload", true).OfType<Button>().Single().Enabled && mainMenu.Controls.Find("CatalogProduct_2", true).Length == 0);
            Check(catalog.Controls.Find("CatalogProduct_1", true).Length == 1 && catalog.Controls.Find("CatalogProduct_2", true).Length == 0, "menu selector filters actual existing product IDs");
            Check(Snapshot() == before, "selecting menu products does not mutate any business table");
            // Open the actual registration form from the new kitchen button and save via its real action.
            using (var acceptSave = new System.Windows.Forms.Timer { Interval = 50 })
            using (var populate = new System.Windows.Forms.Timer { Interval = 100 })
            {
                acceptSave.Tick += (_, _) =>
                {
                    var box = FindWindow("#32770", "LEAL INFO PDV");
                    var owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f is ProductRegistrationForm);
                    if (box == IntPtr.Zero && owner != null) { var popup = GetLastActivePopup(owner.Handle); if (popup != owner.Handle) box = popup; }
                    if (box != IntPtr.Zero)
                    {
                        GetWindowThreadProcessId(box, out uint pid);
                        Console.WriteLine($"REGISTRATION confirmation pid={pid} current={Environment.ProcessId}");
                        if (pid == (uint)Environment.ProcessId)
                        {
                            var ok = GetDlgItem(box, 1);
                            if (ok != IntPtr.Zero) PostMessage(ok, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                            PostMessage(box, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                };
                populate.Tick += (_, _) =>
                {
                    var productForm = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f is ProductRegistrationForm);
                    if (productForm == null) return;
                    populate.Stop();
                    Console.WriteLine("REGISTRATION existing form opened");
                    try
                    {
                        if (!productForm.Modal || productForm.Owner != central) throw new Exception("Existing modal registration owner differs");
                        var fields = (Dictionary<string, Control>)typeof(ProductRegistrationForm).GetField("_fields", P)!.GetValue(productForm)!;
                        fields["name"].Text = "Produto novo premium QA"; fields["barcode"].Text = "PREMIUM-QA-NEW"; fields["price"].Text = "2350"; fields["stock"].Text = "0";
                        typeof(ProductRegistrationForm).GetField("_photoPath", P)!.SetValue(productForm, photo);
                        acceptSave.Start();
                        Console.WriteLine("REGISTRATION invoking real save action");
                        Descendants(productForm).OfType<Button>().Single(b => b.Text == "SALVAR • F3").PerformClick();
                        Console.WriteLine("REGISTRATION save action returned");
                        acceptSave.Stop(); productForm.Close();
                    }
                    catch (Exception e) { Console.Error.WriteLine("REGISTRATION " + e); modalError = e; productForm.Close(); }
                };
                populate.Start(); Console.WriteLine("REGISTRATION clicking kitchen add product"); mainMenu.Controls.Find("KitchenAddProduct", true).OfType<Button>().Single().PerformClick();
                Pump(() => mainMenu.Controls.Find("KitchenAddProduct", true).OfType<Button>().Single().Enabled);
            }
            Check(modalError == null, "kitchen button opens and saves through existing registration form");
            var added = KitchenCatalogView.ReadProducts().Single(p => p.Barcode == "PREMIUM-QA-NEW");
            Pump(() => mainMenu.Controls.Find("CatalogProduct_" + added.Id, true).Length == 1 && catalog.Controls.Find("CatalogProduct_" + added.Id, true).Length == 1);
            Check(added.Price == 23.5m && File.Exists(added.PhotoPath), "saved registered price and photo appear automatically in both kitchen catalogs");
            Check(mainMenu.Controls.Find("CatalogProduct_2", true).Length == 0, "adding a product preserves existing menu selection");
            using (var db = Database.Open()) using (var verify = db.CreateCommand())
            {
                verify.CommandText = "SELECT COUNT(*) FROM products WHERE (id=1 AND price=12.5 AND stock=10) OR (id=2 AND price=7.25 AND stock=20) OR (id=4 AND price=15 AND stock=10)";
                Check(Convert.ToInt32(verify.ExecuteScalar()) == 3, "new product registration preserves existing prices and stock");
            }
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(photo))) == photoHash, "registration preserves original photo bytes"); File.Delete(photo);
            KitchenVisualEvidence.Render(central,Check,Pump,evidence);
            central.Close();Check(window.IsDisposed,"floating window disposed with central");
            File.WriteAllText(Path.Combine(evidence,"fixed.json"),JsonSerializer.Serialize(new{success=true,checks},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(evidence,"failure.txt"),e.ToString());Console.Error.WriteLine(e);return 1;}
    }
}
