using LealInfoPDV;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

static class LayoutTests
{
    const BindingFlags P=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<string> checks=new();
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,P)!.GetValue(o)!;
    static IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
    static void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(15);while(!done()&&DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}Check(done(),"asynchronous UI completed");}
    static void RenderClient(Form form,Size size,string path,Action<Panel>? verify=null)
    {
        var children=form.Controls.Cast<Control>().ToArray();
        using var host=new Panel{Size=size,Font=form.Font,BackColor=KitchenPremiumStyle.Background};
        form.SuspendLayout();host.SuspendLayout();
        try
        {
            foreach(var child in children){form.Controls.Remove(child);host.Controls.Add(child);}
            host.ResumeLayout(true);host.CreateControl();host.PerformLayout();
            using var image=new Bitmap(size.Width,size.Height);
            host.DrawToBitmap(image,new Rectangle(Point.Empty,size));image.Save(path);verify?.Invoke(host);
        }
        finally
        {
            foreach(var child in children){host.Controls.Remove(child);form.Controls.Add(child);}
            form.ResumeLayout(true);
        }
    }
    [STAThread] static int Main()
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!OperatingSystem.IsWindows())throw new Exception("Disposable Windows runner only");
        using var watchdog=new System.Threading.Timer(_=>{Console.Error.WriteLine("FAIL layout timeout");Environment.Exit(93);},null,90000,Timeout.Infinite);
        Directory.CreateDirectory("layout-evidence");
        try
        {
            Check(!File.Exists(Database.DbPath),"empty disposable profile");
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
            Database.Initialize();Auth.CreateUser("QA LAYOUT","layoutqa","QA-layout-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("layoutqa","QA-layout-123!");
            using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="INSERT INTO products(id,barcode,name,category,price,stock,active,photo_path,kitchen_preparation) VALUES(1,'LAYOUT1','Produto com nome extenso','CONDIMENTOS',7.99,10,1,'',1),(2,'LAYOUT2','Produto sem categoria','',.50,20,1,'',1)";q.ExecuteNonQuery();}
            InternalWorkspace.Install();PremiumGlobalTheme.Install();
            using var form=new KitchenForm();form.Show();
            var fresh=Field<Button>(form,"fresh");
            Pump(()=>Field<Label>(form,"message").Text.Contains("Cozinha conectada")&&fresh.Enabled);
            Field<System.Windows.Forms.Timer>(form,"retry").Stop();
            var catalog=Field<KitchenCatalogView>(form,"mainCatalog");
            Pump(()=>catalog.Controls.Find("CatalogProduct_1",true).Length==1);
            // Exercise the production shell that the original kitchen tests did not install.
            InternalWorkspace.Apply(form);PremiumGlobalTheme.Apply(form);Application.DoEvents();
            Check(form.BackColor==KitchenPremiumStyle.Background,"production idle shell preserves kitchen black premium palette");
            Check(Children(form).OfType<TableLayoutPanel>().All(c=>c.BackColor==KitchenPremiumStyle.Background),"production shell preserves kitchen panel colors");
            // Explicit software scaling checks complement native rendering; these are not physical-monitor DPI claims.
            foreach(float scale in new[]{1f,1.25f,1.5f})
            {
                if(scale>1)form.Scale(new SizeF(scale/(scale==1.25f?1f:1.25f),scale/(scale==1.25f?1f:1.25f)));
                foreach(var viewport in new[]{new Size(1366,720),new Size(1600,850),new Size(1909,970)})
                {
                    form.MinimumSize=Size.Empty;form.ClientSize=viewport;form.PerformLayout();Application.DoEvents();
                    var orderPanel=Field<KitchenOrderPanel>(form,"dashboardOrder");
                    Check(orderPanel.Rows.ClientSize.Height>=orderPanel.Rows.ColumnHeadersHeight+34,$"order item area retains a full row at {viewport} scale {scale}");
                    var header=Field<KitchenPremiumHeader>(form,"premiumHeader");
                    foreach(var action in Children(header).OfType<Button>())
                    {
                        var actionBounds=header.RectangleToClient(action.RectangleToScreen(action.ClientRectangle));
                        Check(header.ClientRectangle.Contains(actionBounds),$"header action fully visible {action.Text} at {viewport} scale {scale}");
                    }
                    var tables=Field<KitchenTablesView>(form,"tables");
                    Check(tables.Controls.Find("MainTables",true).Single().Controls.Count==10,$"ten tables retained at {viewport} scale {scale}");
                    foreach(var b in Children(catalog).OfType<Button>().Where(b=>b.Name is "KitchenAddProduct" or "KitchenImportPdv" or "ReadyKitchenOpen" or "CatalogReload"))
                        Check(b.Parent!.ClientRectangle.Contains(b.Bounds),$"catalog action fully inside toolbar {b.Name} at {viewport} scale {scale}");
                    foreach(var card in catalog.Controls.Find("CatalogCards",true).Single().Controls.OfType<TableLayoutPanel>())
                    {
                        var plus=Children(card).OfType<Button>().Single();
                        var rect=card.RectangleToClient(plus.RectangleToScreen(plus.ClientRectangle));
                        Console.WriteLine($"GEOMETRY card={card.ClientRectangle} plus={rect} info={plus.Parent!.Bounds}");
                        Check(rect.Height>=20&&card.ClientRectangle.Contains(rect),$"product add action remains visible at {viewport} scale {scale}");
                    }
                    RenderClient(form,viewport,$"layout-evidence/cozinha-{viewport.Width}-{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}.png");
                }
            }
            // Actual native desktop repaint: compare after a resize cycle against a full clean paint.
            form.MinimumSize=Size.Empty;form.ClientSize=new Size(900,640);
            form.Location=Screen.FromControl(form).WorkingArea.Location+new Size(8,8);form.Refresh();Application.DoEvents();
            var brand=form.Controls.Find("KitchenHeaderBrand",true).Single();
            byte[] CaptureBrand(string filename)
            {
                using var picture=new Bitmap(brand.Width,brand.Height);
                using(var g=Graphics.FromImage(picture))g.CopyFromScreen(brand.PointToScreen(Point.Empty),Point.Empty,picture.Size);
                picture.Save(filename);using var stream=new MemoryStream();picture.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return stream.ToArray();
            }
            var cleanBrand=CaptureBrand("layout-evidence/cabecalho-limpo-nativo.png");
            form.ClientSize=new Size(760,640);Application.DoEvents();form.ClientSize=new Size(900,640);Application.DoEvents();
            var resizedBrand=CaptureBrand("layout-evidence/cabecalho-redimensionado-nativo.png");
            Check(SHA256.HashData(cleanBrand).SequenceEqual(SHA256.HashData(resizedBrand)),"native resize repaint leaves exactly one centered brand with no stale glyphs");
            fresh.PerformClick();Application.DoEvents();
            var entry=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="KitchenOrderEntry");
            InternalWorkspace.Apply(entry);PremiumGlobalTheme.Apply(entry);
            Check(entry.Visible&&entry.Owner==form&&!entry.Modal,"NOVO PEDIDO opens owned floating window after production theme pass");
            Check(entry.BackColor==KitchenPremiumStyle.Background,"floating order keeps black premium palette");
            Check(Screen.FromControl(entry).WorkingArea.Contains(entry.Bounds),"floating order remains inside the real Windows desktop");
            var entryCatalog=Field<KitchenCatalogView>(form,"catalog");
            Pump(()=>entryCatalog.Controls.Find("CatalogProduct_1",true).Length==1);
            var draft=Field<TextBox>(form,"reference");draft.Text="QA CENTRALIZACAO";
            Field<TextBox>(form,"itemDescription").Text="ITEM PRESERVADO";Field<Button>(form,"addItem").PerformClick();
            var textControls=Children(entry).Where(c=>c is Label or Button or TextBoxBase or NumericUpDown or ComboBox).ToArray();
            var fonts=textControls.ToDictionary(c=>c,c=>c.Font);
            foreach(float textScale in new[]{1f,1.25f,1.5f})
            {
                foreach(var c in textControls)if(!c.IsDisposed){var original=fonts[c];c.Font=new Font(original.FontFamily,original.SizeInPoints*textScale,original.Style);}
                foreach(var viewport in new[]{new Size(1366,720),new Size(1909,970)})
                {
                    RenderClient(entry,viewport,$"layout-evidence/pedido-{viewport.Width}-texto-{textScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}.png",host=>
                    {
                        foreach(var c in Children(host).Where(c=>c.Name is "KitchenEntryTitle" or "CloseKitchenOrder" or "SendKitchenOrder"))
                        {
                            var bounds=host.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                            Check(host.ClientRectangle.Contains(bounds),$"floating order title/actions fit complete client: {c.Name} {viewport} text {textScale}");
                            if(c is Label label)Check(label.Height>=TextRenderer.MeasureText(label.Text,label.Font).Height,$"complete title glyph height at text scale {textScale}");
                        }
                        var review=host.Controls.Find("KitchenEntryReview",true).Single();
                        Check(review.Height>=100,$"order review remains visible at {viewport} text {textScale}");
                        foreach(var action in Children(host).OfType<Button>().Where(b=>b.Name is "AddItem" or "RemoveItem"))
                        {
                            var bounds=host.RectangleToClient(action.RectangleToScreen(action.ClientRectangle));
                            Check(host.ClientRectangle.Contains(bounds)&&action.Height<140,$"item action fits without consuming review area: {action.Name} at {viewport} text {textScale}");
                        }
                        var actions=host.Controls.Find("KitchenEntryActions",true).Single();
                        foreach(var action in actions.Controls.Cast<Control>())Check(actions.ClientRectangle.Contains(action.Bounds),$"order footer child fits its own row {action.Name} text {textScale}");
                        var title=host.Controls.Find("KitchenEntryTitle",true).OfType<Label>().Single();
                        Check(title.TextAlign==ContentAlignment.MiddleCenter,"NOVO PEDIDO heading centered");
                    });
                }
            }
            foreach(var c in textControls)if(!c.IsDisposed)c.Font=fonts[c];
            Check(draft.Text=="QA CENTRALIZACAO"&&Field<DataGridView>(form,"items").Rows.Count==1,"text and resize checks preserve the assembled draft");
            foreach(var rail in new[]{catalog,Field<KitchenCatalogView>(form,"catalog")})
            {
                var buttons=rail.Controls.Find("KitchenCategoryRail",true).Single().Controls.OfType<Button>().ToArray();
                Check(buttons.Any(b=>b.Text=="CONDIMENTOS")&&buttons.Any(b=>b.Text=="SEM\nCATEGORIA"),"category labels retain complete words and intentional lines");
                Check(Children(rail).OfType<KitchenCenteredLabel>().Any(p=>p.Text.Contains("7,99")),"full price remains available to centered fitted renderer");
            }
            var ready=ReadyKitchenCatalog.Load();Check(ready.Length>=200&&ready.All(p=>p.CommonsFile.StartsWith("File:")&&p.SourceUrl.StartsWith("https://commons.wikimedia.org/wiki/")&&ReadyKitchenCatalog.Photo(p).Length>0),"immutable ready-menu photos and attributions preserved");
            entry.Close();
            form.Controls.Find("MoreKitchenTables",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
            var popup=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="AllKitchenTables");
            InternalWorkspace.Apply(popup);PremiumGlobalTheme.Apply(popup);
            Check(popup.BackColor==KitchenPremiumStyle.Background,"floating tables keep black premium palette");popup.Close();
            using(var ordinary=new Form{Name="UnrelatedForm"}){PremiumGlobalTheme.Apply(ordinary);Check(ordinary.BackColor==PremiumGlobalTheme.Background,"unrelated forms retain their existing shared theme");}
            DashboardTests.Run(form,Check,Pump,"layout-evidence",RenderClient);
            KitchenCorrectionsTests.Run(form,Check,Pump);
            TableDoubleClickTests.Run(Check,Pump);
            using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="SELECT COUNT(*) FROM products WHERE (id=1 AND price=7.99 AND stock=10) OR (id=2 AND price=.50 AND stock=20)";Check(Convert.ToInt32(q.ExecuteScalar())==2,"layout checks preserve product prices and stock");}
            form.Close();form.Dispose();
            Check(form.IsDisposed&&Application.OpenForms.Count==0,"all tested windows close and dispose before successful runner shutdown");
            File.WriteAllText("layout-evidence/results.json",JsonSerializer.Serialize(new{success=true,checks,physicalDpiTest=false,nativeWindows=true}));
            Console.WriteLine("PASS Windows fixture disposed; terminating isolated harness");
            Environment.Exit(0);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("layout-evidence/failure.txt",e.ToString());return 1;}
    }
}


