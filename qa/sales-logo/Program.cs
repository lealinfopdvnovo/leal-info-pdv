using LealInfoPDV;
using LealInfoPDV.Network;
using System.Reflection;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Security.Cryptography;

static class Program
{
    const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
    const BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Assembly A = typeof(MainForm).Assembly;
    static readonly Type Branding = A.GetType("LealInfoPDV.Licensing.CompanyBranding", true)!;
    static readonly string Custom = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LEAL INFO CONECTADO", "PDV", "tela_principal.png");
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    static void Pump(int ms) { var t = Stopwatch.StartNew(); while (t.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Exec(string sql) { using var c = Database.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
    static Image? Load(string file) => (Image?)Branding.GetMethod("LoadSalesLogo", S)!.Invoke(null, new object[] { file });
    static void SetLogo(string path) => Branding.GetMethod("SetLogo", S)!.Invoke(null, new object[] { path });
    static int Subscribers() => ((Delegate?)Branding.GetField("Changed", S)!.GetValue(null))?.GetInvocationList().Length ?? 0;
    static void Fixture(string path, Color color, string caption)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var b = new Bitmap(320, 180); using var g = Graphics.FromImage(b);
        g.Clear(color); using var font = new Font("Segoe UI", 26, FontStyle.Bold); g.DrawString(caption, font, Brushes.White, 15, 65);
        b.Save(path, ImageFormat.Png);
    }
    static void IsFixture(Image? image, Color color, string why)
    {
        Check(image != null && image.Size == new Size(320, 180), why + " / proporção");
        using var b = new Bitmap(image!); Check(b.GetPixel(5, 5).ToArgb() == color.ToArgb(), why + " / origem");
    }
    static void IsSame(Image? actual, Image expected, string why)
    {
        Check(actual != null && actual.Size == expected.Size, why + " / tamanho");
        using var a = new Bitmap(actual!); using var b = new Bitmap(expected);
        for (int y = 0; y < b.Height; y += Math.Max(1, b.Height / 20))
            for (int x = 0; x < b.Width; x += Math.Max(1, b.Width / 20))
                Check(a.GetPixel(x, y).ToArgb() == b.GetPixel(x, y).ToArgb(), why + " / conteúdo");
    }
    static void Sources(string logo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Custom)!);
        Exec("DELETE FROM settings WHERE key='company_logo_base64'"); File.Delete(Custom);
        foreach (var name in new[] { "sales_brand_header.png", "sales_brand_product.png" })
        {
            using var actual = Load(name); using var expected = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", name));
            IsSame(actual, expected, "Reserva padrão ausente");
        }
        Fixture(Custom, Color.DarkGoldenrod, "CLIENTE QA");
        using (var h = Load("sales_brand_header.png")) using (var p = Load("sales_brand_product.png"))
        { IsFixture(h, Color.DarkGoldenrod, "Imagem principal não aproveitada no topo"); IsFixture(p, Color.DarkGoldenrod, "Imagem principal não aproveitada na vitrine"); }
        SetLogo(logo);
        using (var h = Load("sales_brand_header.png")) IsFixture(h, Color.RoyalBlue, "Logotipo cadastrado não tem prioridade");
        Exec("UPDATE settings SET value='corrompido' WHERE key='company_logo_base64'");
        using (var h = Load("sales_brand_header.png")) IsFixture(h, Color.DarkGoldenrod, "Logo inválido não usa principal");
        File.WriteAllText(Custom, "imagem inválida");
        using (var h = Load("sales_brand_header.png")) using (var expected = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "sales_brand_header.png"))) IsSame(h, expected, "Imagem inválida não usa reserva");
        Exec("DELETE FROM settings WHERE key='company_logo_base64'"); Fixture(Custom, Color.DarkGoldenrod, "CLIENTE QA");
        Console.WriteLine("PASS logo cadastrado > imagem principal existente > reserva; arquivos inválidos contidos; imagens sem bloqueio");
    }
    static void Capture(Form f, string name)
    {
        using var b = new Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save("logo-evidencias/" + name + ".png", ImageFormat.Png);
    }
    static void AddProduct(Form sales, string code)
    {
        var search = All(sales).OfType<TextBox>().Single(x => !x.ReadOnly && x.Parent is not NumericUpDown);
        search.Text = code; bool quantity = false;
        using var confirm = new System.Windows.Forms.Timer { Interval = 60 };
        confirm.Tick += (_, _) =>
        {
            var f = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Quantidade do Produto");
            if (f == null) return; confirm.Stop(); quantity = true; f.DialogResult = DialogResult.OK; f.Close();
        };
        confirm.Start(); typeof(Control).GetMethod("OnKeyDown", I)!.Invoke(search, new object[] { new KeyEventArgs(Keys.Enter) });
        Check(quantity, "Quantidade não abriu pelo fluxo real de venda");
    }
    static void Sales(MainForm main, Action<Form> inspect)
    {
        Exception? failure = null; bool seen = false; Form? closed = null; int before = Subscribers();
        using var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            var sales = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text.Contains("TELA DE VENDA")); if (sales == null || seen) return;
            seen = true; timer.Stop(); closed = sales;
            try { inspect(sales); } catch (Exception e) { failure = e; } finally { sales.Close(); }
        };
        timer.Start(); typeof(MainForm).GetMethod("OpenSales", I)!.Invoke(main, null);
        Check(seen && failure == null, "Tela real falhou: " + failure); Check(closed!.IsDisposed && Subscribers() == before, "Tela fechada deixou imagem/evento preso");
    }
    static void Forms(string flavor, string logo, string nextLogo, string photo)
    {
        Exec("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1'),('company_name','CLIENTE FICTICIO QA'),('company_city_state','Angra dos Reis / RJ')");
        Auth.CreateUser("QA LOGO", "qa_logo", "Senha_Ficticia_123", "ADMINISTRADOR", "qa@example.invalid", "");
        Check(Auth.Login("qa_logo", "Senha_Ficticia_123") != null, "Login falhou");
        A.GetType("LealInfoPDV.Licensing.InstallationLicense", true)!.GetProperty("Current", S)!.SetValue(null, new LicenseTerms("QA", 2, "unico", null, "", 1, "QA", "standard", "TESTE"));
        using (var c = Database.Open()) using (var q = c.CreateCommand())
        { q.CommandText = "INSERT INTO products(barcode,name,price,stock,active,photo_path) VALUES('QA-FOTO','Produto com foto QA',10,100,1,@path),('QA-SEM-FOTO','Produto sem foto QA',5,100,1,'')"; q.Parameters.AddWithValue("@path", photo); q.ExecuteNonQuery(); }
        using var main = new MainForm(); main.Show(); Pump(100);
        ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer", I)!.GetValue(main)!).Stop(); typeof(MainForm).GetField("automaticBackupCompleted", I)!.SetValue(main, true);
        using (var image = (Image?)typeof(MainForm).GetMethod("LoadMainScreenImage", I)!.Invoke(main, null)) IsFixture(image, Color.DarkGoldenrod, "Imagem principal alterada");
        Sales(main, sales =>
        {
            var header = All(sales).OfType<PictureBox>().Single(x => x.Name == "sales_company_logo");
            var product = All(sales).OfType<PictureBox>().Single(x => x.Name == "sales_product_picture");
            IsFixture(header.Image, Color.DarkGoldenrod, "Topo real não usou imagem principal"); IsFixture(product.Image, Color.DarkGoldenrod, "Vitrine real não usou imagem principal");
            Check(header.SizeMode == PictureBoxSizeMode.Zoom && product.SizeMode == PictureBoxSizeMode.Zoom, "Logo esticado");
            foreach (var sample in new[] { (1366, 730, 96), (1600, 850, 120), (1920, 1030, 144) })
            {
                sales.MaximumSize = new Size(4000, 2400); sales.WindowState = FormWindowState.Normal; sales.Bounds = new Rectangle(0, 0, sample.Item1, sample.Item2);
                sales.Tag!.GetType().GetMethod("Apply", I)!.Invoke(sales.Tag, new object?[] { sample.Item3 }); Pump(40);
                foreach (var p in new[] { header, product }) { var origin = sales.PointToClient(p.PointToScreen(Point.Empty)); Check(p.Width > 60 && p.Height > 40 && sales.ClientRectangle.Contains(new Rectangle(origin, p.Size)), "Logo fora da área útil"); }
                Capture(sales, "principal-" + sample.Item1 + "-" + sample.Item3);
            }
            var oldBounds = (header.Bounds, product.Bounds);
            SetLogo(logo); IsFixture(header.Image, Color.RoyalBlue, "Troca de logo não atualizou topo"); IsFixture(product.Image, Color.RoyalBlue, "Troca de logo não atualizou vitrine");
            var mainLogo = (PictureBox)typeof(MainForm).GetField("companyLogoPicture", I)!.GetValue(main)!; IsFixture(mainLogo.Image, Color.RoyalBlue, "Logo principal não sincronizou");
            Check(oldBounds == (header.Bounds, product.Bounds), "Troca de logo mudou estrutura"); Capture(sales, "logo-cadastrado");
            AddProduct(sales, "QA-FOTO"); IsFixture(product.Image, Color.Firebrick, "Foto do produto substituída pelo logo");
            SetLogo(nextLogo); IsFixture(header.Image, Color.ForestGreen, "Topo não atualizou com produto selecionado"); IsFixture(product.Image, Color.Firebrick, "Troca da marca apagou foto do produto"); Capture(sales, "produto-com-foto");
            All(sales).OfType<Button>().Single(x => x.Text == "Limpar").PerformClick(); IsFixture(product.Image, Color.ForestGreen, "Limpar não restaurou marca atual");
            AddProduct(sales, "QA-SEM-FOTO"); Check(product.Image == null && All(sales).OfType<Label>().Any(x => x.Text.Contains("SEM FOTO CADASTRADA")), "Produto sem foto recebeu logo indevidamente");
            SetLogo(logo); IsFixture(header.Image, Color.RoyalBlue, "Topo não atualizou com produto sem foto"); Check(product.Image == null, "Atualização de marca ocultou aviso sem foto"); Capture(sales, "produto-sem-foto");
            All(sales).OfType<Button>().Single(x => x.Text == "Limpar").PerformClick(); IsFixture(product.Image, Color.RoyalBlue, "Limpar não restaurou logo");
        });
        Sales(main, sales =>
        {
            var header = All(sales).OfType<PictureBox>().Single(x => x.Name == "sales_company_logo"); var product = All(sales).OfType<PictureBox>().Single(x => x.Name == "sales_product_picture");
            IsFixture(header.Image, Color.RoyalBlue, "Reabrir perdeu logo do topo"); IsFixture(product.Image, Color.RoyalBlue, "Reabrir perdeu logo da vitrine");
            Exec("DELETE FROM settings WHERE key='company_logo_base64'"); Fixture(Custom, Color.Purple, "NOVA PRINCIPAL QA");
            typeof(Form).GetMethod("OnActivated", I)!.Invoke(sales, new object[] { EventArgs.Empty });
            IsFixture(header.Image, Color.Purple, "Ativar não recarregou imagem principal"); IsFixture(product.Image, Color.Purple, "Ativar não atualizou vitrine");
        });
        SetLogo(nextLogo); Check(Subscribers() == 1, "Evento de venda vazou após reabertura");
        Check((A.GetType("LealInfoPDV.PartsWithdrawal") != null) == (flavor == "002"), "Módulo 002 alterado"); Check((A.GetType("LealInfoPDV.GlassProjectForm") != null) == (flavor is "001" or "003"), "Módulo 003 alterado");
        main.Close(); Console.WriteLine("PASS PDV real: dois logos, principal e cadastro, troca ao vivo, fotos, aviso sem foto, limpar/reabrir, descarte e particularidades " + flavor);
    }
    static void Live()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) }; var updater = A.GetType("LealInfoPDV.UpdateManager", true)!;
        var load = updater.GetMethod("LoadManifestAsync", S, null, new[] { typeof(HttpClient), typeof(string), typeof(string) }, null)!;
        foreach (var id in new[] { "001", "002", "003", "004" })
        {
            var task = (Task)load.Invoke(null, new object[] { http, id, "10.384" })!; task.GetAwaiter().GetResult(); var m = task.GetType().GetProperty("Result")!.GetValue(task)!;
            string Read(string key) => (string)m.GetType().GetProperty(key)!.GetValue(m)!;
            Check(Read("Version") == "10.385" && Read("TargetClientCode") == (id == "004" ? "" : id), "Feed/alvo incorreto " + id);
            var data = http.GetByteArrayAsync(Read("PackageUrl")).GetAwaiter().GetResult(); Check(Convert.ToHexString(SHA256.HashData(data)).Equals(Read("Sha256"), StringComparison.OrdinalIgnoreCase), "SHA remoto incorreto");
            using var b = new MemoryStream(data); using var z = new ZipArchive(b); Check(z.Entries.Select(x => x.FullName).Order().SequenceEqual(new[] { "LealInfoPDV.deps.json", "LealInfoPDV.dll", "LealInfoPDV.exe" }), "Conteúdo remoto inesperado");
            Console.WriteLine("PASS updater REAL " + id + " -> 10.385; alvo/download/SHA/arquivos válidos");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", "Somente runner descartável"); if (args.Contains("--live")) { Live(); return; }
        _ = Task.Run(async () => { await Task.Delay(90000); Console.Error.WriteLine("QA TIMEOUT LOGO"); Environment.Exit(1); });
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); Directory.CreateDirectory("logo-evidencias"); Database.Initialize();
        var dir = Path.Combine(Path.GetTempPath(), "logo-qa-" + Guid.NewGuid()); Directory.CreateDirectory(dir); var logo = Path.Combine(dir, "logo.png"); var next = Path.Combine(dir, "next.png"); var photo = Path.Combine(dir, "produto.png");
        Fixture(logo, Color.RoyalBlue, "LOGO CADASTRADO"); Fixture(next, Color.ForestGreen, "NOVA MARCA"); Fixture(photo, Color.Firebrick, "FOTO PRODUTO");
        Sources(logo); Forms(args.FirstOrDefault() ?? "global", logo, next, photo);
    }
}
