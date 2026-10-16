using LealInfoPDV;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type Theme = typeof(MainForm).Assembly.GetType("LealInfoPDV.PremiumGlobalTheme", true)!;
    private static readonly List<object> Evidence = [];
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void Apply(Control c) => Theme.GetMethod("Apply", Hidden)!.Invoke(null, [c]);
    private static IEnumerable<Control> All(Control c) => new[] { c }.Concat(c.Controls.Cast<Control>().SelectMany(All));
    private static void Capture(Form f, string name)
    {
        Apply(f);
        using var image = new Bitmap(f.Width, f.Height);
        f.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine("premium-evidence", name + ".png"));
    }

    [STAThread]
    private static void Main()
    {
        Check(!File.Exists(Database.DbPath), "Windows runner must have no operational database");
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Theme.GetMethod("Install", Hidden)!.Invoke(null, null);
        Directory.CreateDirectory("premium-evidence");
        Database.Initialize();
        Auth.CreateUser("QA VISUAL", "visual-qa", "isolated-only", "ADMINISTRADOR", "", "");
        Check(Auth.Login("visual-qa", "isolated-only") != null, "Fixture login failed");
        Invariants();
        using var main = new MainForm();
        main.CreateControl();
        Apply(main);
        Capture(main, "principal");
        foreach (var method in new[] { "OpenSales", "OpenProducts", "OpenCustomers", "OpenSuppliers", "OpenFinance", "OpenReports", "OpenHistory", "OpenSettings", "OpenUsers", "OpenServices", "OpenOrders", "OpenQuotes", "OpenKitchen", "OpenDeliveries", "OpenGlassWorkshop" })
            Module(main, method);
        using (var login = new LoginForm()) { login.CreateControl(); Capture(login, "login"); }
        using (var equipment = new EquipmentSettingsForm()) { equipment.CreateControl(); Capture(equipment, "equipamentos"); }
        using (var network = new LealInfoPDV.Network.NetworkSettingsForm()) { network.CreateControl(); Capture(network, "rede"); }
        File.WriteAllText("premium-evidence/results.json", JsonSerializer.Serialize(Evidence, new JsonSerializerOptions { WriteIndented = true }));
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
        Apply(form); Apply(form);
        foreach (var entry in before)
            Check(entry.c.Bounds == entry.Bounds && entry.c.Text == entry.Text && entry.c.TabIndex == entry.TabIndex && entry.c.Enabled == entry.Enabled && entry.c.Visible == entry.Visible, "Theme changed behavior or geometry");
        Check(number.Value == 17.25m && grid.Rows[0].Cells[0].Value!.Equals(17.25m) && grid.Columns[0].DefaultCellStyle.Format == "N2", "Data or formatting changed");
        Check(ReferenceEquals(picture.Image, qr) && ReferenceEquals(form.AcceptButton, button), "Image or keyboard action changed");
        typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
        Check(clicks == 1 && button.DialogResult == DialogResult.OK, "Click action changed");
        foreach (float scale in new[] { 1f, 1.25f, 1.5f })
        {
            using var scaled = new Form { Size = new Size(800, 500) };
            scaled.Controls.Add(new Button { Text = "CONFIRMAR • F12", Bounds = new Rectangle(20, 20, 220, 44) });
            scaled.Scale(new SizeF(scale, scale)); Apply(scaled); Capture(scaled, "escala-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Evidence.Add(new { name = "control-data-actions-images-geometry-idempotence-and-logical-scaling", result = "PASS" });
        Console.WriteLine("PASS control invariants and logical scaling");
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
            try
            {
                Check(form.Text != "", "Unexpected dialog");
                Capture(form, method);
                var grids = All(form).OfType<DataGridView>().ToArray();
                Check(grids.All(g => g.BackgroundColor.R < 50 && g.DefaultCellStyle.ForeColor.GetBrightness() > .6), "Grid contrast failed");
                seen = true;
                Evidence.Add(new { name = method, title = form.Text, controls = All(form).Count(), grids = grids.Length, result = "PASS" });
            }
            catch (Exception ex) { failure = ex; }
            finally { timer.Stop(); form.Close(); }
        };
        timer.Start();
        typeof(MainForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, null);
        // Settings is intentionally modeless; run its real message loop too.
        while (!seen && failure == null && watch.Elapsed < TimeSpan.FromSeconds(20))
        { Application.DoEvents(); Thread.Sleep(10); }
        Check(seen, "Module did not open: " + method);
        if (failure != null) throw failure;
        Console.WriteLine("PASS " + method);
    }
}
