using LealInfoPDV;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.ComponentModel;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type Theme = typeof(MainForm).Assembly.GetType("LealInfoPDV.PremiumGlobalTheme", true)!;
    private static readonly List<object> Evidence = [];
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static readonly Type Workspace = typeof(MainForm).Assembly.GetType("LealInfoPDV.InternalWorkspace", true)!;
    private static void Apply(Control c) { if(c is Form f) Workspace.GetMethod("Apply", Hidden)!.Invoke(null, [f]); }
    private static void ThemeOnly(Control c) => Theme.GetMethod("Apply", Hidden)!.Invoke(null, [c]);
    private static IEnumerable<Control> All(Control c) => new[] { c }.Concat(c.Controls.Cast<Control>().SelectMany(All));
    private static void Capture(Form f, string name)
    {
        Apply(f);
        using var image = new Bitmap(f.Width, f.Height);
        f.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine("internal-evidence", name + ".png"));
    }

    private static void Preview(Form form, string name)
    {
        if (form is LoginForm)
        {
            form.Show();
            var animation = Stopwatch.StartNew();
            while (animation.Elapsed < TimeSpan.FromSeconds(3) || form.Opacity < 1 && animation.Elapsed < TimeSpan.FromSeconds(10)) { Application.DoEvents(); Thread.Sleep(10); }
            Check(form.Opacity == 1, "Login animation did not finish");
            Capture(form, name);
            // DrawToBitmap reverses sibling composition for the translucent login
            // layers. Preserve a real desktop capture as the visual reference too.
            using (var desktop = new Bitmap(form.Width, form.Height))
            using (var graphics = Graphics.FromImage(desktop))
            {
                graphics.CopyFromScreen(form.Location, Point.Empty, desktop.Size);
                desktop.Save(Path.Combine("internal-evidence", name + "-desktop.png"));
            }
            form.Hide(); return;
        }
        // Only suppress startup side effects in this disposable presentation fixture:
        // company onboarding, speech and automatic network update checks are not visual tests.
        var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        var key = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(f => f.Name.Contains("Shown", StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
        var shown = events[key];
        events.RemoveHandler(key, shown);
        try
        {
            form.Show(); Application.DoEvents();
            Check(form.Visible && All(form).Any(c => c.Visible && c != form), "Preview has no visible content: " + name);
            Capture(form, name);
            form.Hide();
        }
        finally { events.AddHandler(key, shown); }
    }

    [STAThread]
    private static void Main()
    {
        Check(!File.Exists(Database.DbPath), "Windows runner must have no operational database");
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Workspace.GetMethod("Install", Hidden)!.Invoke(null, null);
        Directory.CreateDirectory("internal-evidence");
        Database.Initialize();
        using (var connection = Database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO products(name,price,stock) VALUES('PRODUTO QA VISUAL',17.25,12); INSERT INTO customers(name) VALUES('CLIENTE QA'); INSERT INTO suppliers(name) VALUES('FORNECEDOR QA');";
            command.ExecuteNonQuery();
        }
        Auth.CreateUser("QA VISUAL", "visual-qa", "isolated-only", "ADMINISTRADOR", "", "");
        Check(Auth.Login("visual-qa", "isolated-only") != null, "Fixture login failed");
        Invariants();
        EditorInvariant();
        LargeList();
        using var main = NewMain();
        main.CreateControl();
        Apply(main);
        Preview(main, "principal");
        foreach (var method in new[] { "ProductNew", "SelectProductFromCatalog", "OpenSales", "OpenProducts", "OpenCustomers", "OpenSuppliers", "OpenFinance", "OpenReports", "OpenHistory", "OpenSettings", "OpenUsers", "OpenServices", "OpenOrders", "OpenQuotes", "OpenKitchen", "OpenDeliveries", "OpenGlassWorkshop" })
            Module(main, method);
        using (var login = new LoginForm()) { Preview(login, "login"); }
        using (var equipment = new EquipmentSettingsForm()) { Preview(equipment, "equipamentos"); }
        using (var network = new LealInfoPDV.Network.NetworkSettingsForm()) { Preview(network, "rede"); }
        File.WriteAllText("internal-evidence/results.json", JsonSerializer.Serialize(Evidence, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS global-premium-ui Windows; screenshots and invariant evidence saved");
    }

    private static void Invariants()
    {
        using var form = new Form { Size = new Size(850, 620) };
        var text = new TextBox { Text = "Preço / licença / configuração", Bounds = new Rectangle(20, 20, 300, 30), TabIndex = 3 };
        var number = new NumericUpDown { DecimalPlaces = 2, Value = 17.25m, Location = new Point(20, 70) };
        var button = new Button { Text = "Salvar", Bounds = new Rectangle(20, 120, 120, 36), DialogResult = DialogResult.OK };
        var grid = new DataGridView { Bounds = new Rectangle(20, 180, 600, 150), AllowUserToAddRows = false };
        grid.Columns.Add("value", "Preço"); grid.Columns[0].DefaultCellStyle.Format = "N2"; grid.Rows.Add(17.25m);
        using var qr = new Bitmap(48, 48);
        var picture = new PictureBox { Image = qr };
        form.Controls.AddRange([text, number, button, grid, picture]);
        form.AcceptButton = button;
        var before = All(form).Select(c => (c, c.Bounds, c.Text, c.TabIndex, c.Enabled, c.Visible)).ToArray();
        int clicks = 0; button.Click += (_, _) => clicks++;
        ThemeOnly(form); ThemeOnly(form);
        foreach (var entry in before)
            Check(entry.c.Bounds == entry.Bounds && entry.c.Text == entry.Text && entry.c.TabIndex == entry.TabIndex && entry.c.Enabled == entry.Enabled && entry.c.Visible == entry.Visible, "Theme changed behavior or geometry");
        Check(number.Value == 17.25m && grid.Rows[0].Cells[0].Value!.Equals(17.25m) && grid.Columns[0].DefaultCellStyle.Format == "N2", "Data or formatting changed");
        Check(ReferenceEquals(picture.Image, qr) && ReferenceEquals(form.AcceptButton, button), "Image or keyboard action changed");
        typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
        Check(clicks == 1 && button.DialogResult == DialogResult.OK, "Click action changed");
        using (var dynamicForm = new Form())
        {
            dynamicForm.Show(); ThemeOnly(dynamicForm);
            var added = new TextBox { Text = "DYNAMIC VALUE", TabIndex = 7 };
            dynamicForm.Controls.Add(added);
            Application.DoEvents();
            Check(added.BackColor.R < 50 && added.ForeColor.GetBrightness() > .6 && added.Text == "DYNAMIC VALUE" && added.TabIndex == 7,
                "Dynamically added fields lost theme or behavior");
            dynamicForm.Hide();
        }
        foreach (float scale in new[] { 1f, 1.25f, 1.5f })
        {
            using var scaled = new Form { Size = new Size(800, 500) };
            scaled.Controls.Add(new Button { Text = "CONFIRMAR • F12", Bounds = new Rectangle(20, 20, 220, 44) });
            scaled.Scale(new SizeF(scale, scale)); Apply(scaled); Capture(scaled, "escala-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Evidence.Add(new { name = "control-data-actions-images-geometry-idempotence-and-logical-scaling", result = "PASS" });
        Console.WriteLine("PASS control invariants and logical scaling");
    }

    private static MainForm NewMain()
    {
        var main=new MainForm();
        // The isolated UI fixture has no activated retail license. Its periodic
        // activation refresh is outside this presentation-only test.
        ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!).Stop();
        return main;
    }
    private static void AssertActions(Form f)
    {
        foreach(var button in All(f).OfType<Button>().Where(b=>b.Visible)) {
            bool scrollable=false;for(Control? parent=button.Parent;parent!=null&&parent!=f;parent=parent.Parent)if(parent is ScrollableControl sc && sc.AutoScroll)scrollable=true;
            if(scrollable)continue;
            var box=f.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
            Check(button.Parent!.ClientRectangle.Contains(button.Bounds),"Action clipped by its container: "+button.Text);
            Check(f.ClientRectangle.Contains(box),"Action clipped: "+f.Text+" / "+button.Text+" / "+box);
        }
    }
    private static void EditorInvariant()
    {
        using var owner=NewMain();
        using var f=(Form)typeof(MainForm).GetMethod("Editor",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,["CLIENTE",new[]{"Nome","CPF/CNPJ","Telefone","E-mail","Endereço"}])!;
        var fields=(List<TextBox>)f.Tag!;
        for(int i=0;i<fields.Count;i++)fields[i].Text="UNCHANGED-"+i;
        var before=fields.ToArray();
        Apply(f);f.Show();Application.DoEvents();AssertActions(f);
        Check(before.SequenceEqual((List<TextBox>)f.Tag!) && fields.Select((x,i)=>x.Text=="UNCHANGED-"+i).All(x=>x),"Editor controls or values replaced");
        Capture(f,"editor-cliente");
        var save=All(f).OfType<Button>().Single(b=>b.Text=="SALVAR");save.PerformClick();Check(f.DialogResult==DialogResult.OK,"Original save handler lost");
        Evidence.Add(new{name="editor-original-fields-tag-values-save-handler",result="PASS"});
    }

    private static void LargeList()
    {
        using var f=new Form{Text="QA LISTA",Size=new Size(1000,700)};
        var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false};
        grid.Columns.Add("ID","ID");grid.Columns.Add("Nome","Nome");f.Controls.Add(grid);f.Show();Apply(f);
        var watch=Stopwatch.StartNew();
        for(int i=0;i<2000;i++)grid.Rows.Add(i,"QA-"+i);
        Application.DoEvents();
        var search=All(f).OfType<TextBox>().Single(t=>t.Name=="InternalListSearch");
        search.Text="QA-1999";Check(grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)==1,"Large-list filter failed");
        search.Clear();Check(grid.Rows.Count==2000 && grid.Rows.Cast<DataGridViewRow>().All(r=>r.Visible),"Large-list refresh lost data");
        Check(watch.Elapsed<TimeSpan.FromSeconds(15),"Large-list refresh stalled");
        Evidence.Add(new{name="large-list-2000-refresh-search",elapsedMs=watch.ElapsedMilliseconds,result="PASS"});f.Close();
    }

    private static void Module(MainForm main, string method)
    {
        Exception? failure = null;
        bool seen = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 800 };
        var watch = Stopwatch.StartNew();
        timer.Tick += (_, _) =>
        {
            var form = Application.OpenForms.Cast<Form>().LastOrDefault(f => f != main);
            if (form == null) { if (watch.Elapsed.TotalSeconds > 20) throw new Exception("No module: " + method); return; }
            timer.Stop();
            try
            {
                Check(form.Text != "", "Unexpected dialog");
                Capture(form, method);
                Check(Screen.FromControl(form).WorkingArea.Contains(form.RectangleToScreen(form.ClientRectangle)),"Window client area outside work area: "+method);
                if(method=="ProductNew")AssertActions(form);
                if(method=="SelectProductFromCatalog")Check(All(form).OfType<TextBox>().Any(t=>t.Name!="InternalListSearch") && !All(form).Any(c=>c.Name=="InternalWorkspaceRoot"),"Original F5 catalog filters were replaced");
                var root = All(form).FirstOrDefault(c=>c.Name=="InternalWorkspaceRoot");
                if(root!=null) {
                    AssertActions(form);
                    var input=All(form).OfType<TextBox>().FirstOrDefault(c=>c.Name=="InternalListSearch");
                    if(input!=null) {
                        var listing=All(form).OfType<DataGridView>().Single();
                        var original=listing.Rows.Cast<DataGridViewRow>().Select(r=>r.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value)).ToArray()).ToArray();
                        input.Text="NONEXISTENT-QA-SEARCH";Check(listing.Rows.Cast<DataGridViewRow>().All(r=>!r.Visible),"Search failed");
                        input.Clear();Check(listing.Rows.Count==original.Length && listing.Rows.Cast<DataGridViewRow>().All(r=>r.Visible),"Clearing search lost rows");
                        Check(original.SelectMany(x=>x).SequenceEqual(listing.Rows.Cast<DataGridViewRow>().SelectMany(r=>r.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value)))),"Search changed data");
                        foreach(var size in new[]{new Size(1024,640),new Size(1366,768),new Size(1600,900),new Size(1920,1080)}) {
                            form.Size=size;form.PerformLayout();Application.DoEvents();AssertActions(form);
                            Capture(form,method+"-"+size.Width);
                        }
                    }
                }
                var grids = All(form).OfType<DataGridView>().ToArray();
                Check(grids.All(g => g.BackgroundColor.R < 50 && g.DefaultCellStyle.ForeColor.GetBrightness() > .6), "Grid contrast failed");
                seen = true;
                Evidence.Add(new { name = method, title = form.Text, controls = All(form).Count(), grids = grids.Length, result = "PASS" });
            }
            catch (Exception ex) { failure = ex; }
            finally { timer.Stop(); form.Close(); }
        };
        timer.Start();
        if(method=="ProductNew") typeof(MainForm).Assembly.GetType("LealInfoPDV.ProductRegistrationForm",true)!.GetMethod("Show",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[main,null]);
        else typeof(MainForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, null);
        // Settings is intentionally modeless; run its real message loop too.
        while (!seen && failure == null && watch.Elapsed < TimeSpan.FromSeconds(20))
        { Application.DoEvents(); Thread.Sleep(10); }
        Check(seen, "Module did not open: " + method);
        if (failure != null) throw failure;
        Console.WriteLine("PASS " + method);
    }
}
