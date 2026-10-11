using LealInfoPDV;
using LealInfoPDV.Network;
using System.Reflection;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;

static class Program
{
    const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
    const BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Assembly A = typeof(MainForm).Assembly;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    static void Pump(int ms) { var t = Stopwatch.StartNew(); while (t.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Exec(string sql) { using var c = Database.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
    static string Setting(string key) { using var c = Database.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT value FROM settings WHERE key=@k"; q.Parameters.AddWithValue("@k", key); return Convert.ToString(q.ExecuteScalar()) ?? ""; }
    static void Components()
    {
        var good = new DeliveryAddress.CepAddress("Rua QA", "Bairro QA", "Cidade QA", "RJ");
        using var f = new Form { Width = 650, Height = 250 };
        var cep = new TextBox(); var address = new TextBox { Text = "Anterior, 123" }; var city = new TextBox { Text = "Anterior / RJ" };
        int calls = 0; var tasks = new Dictionary<string, TaskCompletionSource<DeliveryAddress.CepAddress>>();
        Task<DeliveryAddress.CepAddress> Lookup(string digits, CancellationToken ct)
        {
            calls++;
            if (digits == "99999999") throw new ArgumentException("CEP inexistente");
            if (digits == "88888888") throw new HttpRequestException("Sem internet");
            if (digits.StartsWith("111") || digits.StartsWith("222") || digits.StartsWith("333"))
            { var t = new TaskCompletionSource<DeliveryAddress.CepAddress>(); tasks[digits] = t; return t.Task; }
            return Task.FromResult(good);
        }
        var binding = CompanyCepLookup.Create(f, cep, address, city, Lookup); var panel = binding.Panel; panel.Dock = DockStyle.Top; panel.Height = 64; f.Controls.Add(panel); f.Show(); Pump(50);
        var search = All(panel).OfType<Button>().Single();
        cep.Text = "12345"; search.PerformClick(); Pump(600); Check(calls == 0 && address.Text == "Anterior, 123", "CEP inválido consultado/apagou endereço");
        cep.Text = "01001000"; Pump(650); Check(calls == 1 && cep.Text == "01001-000" && address.Text == "Rua QA, Bairro QA" && city.Text == "Cidade QA / RJ", "Auto preenchimento falhou");
        address.Text += ", 42, sala 2"; cep.Focus(); search.Focus(); Pump(650); Check(calls == 1 && address.Text.EndsWith("sala 2"), "Leave duplicou lookup e apagou complemento");
        cep.Text = "01001001";
        var flush = binding.CompleteAsync(); Pump(50);
        Check(flush.IsCompletedSuccessfully && cep.Text == "01001-001", "Salvar antes do debounce não finaliza consulta");
        var before = address.Text;
        cep.Text = "99999999"; Pump(650); Check(address.Text == before && city.Text == "Cidade QA / RJ", "CEP inexistente apagou dados");
        cep.Text = "88888888"; Pump(650); Check(address.Text == before, "Erro de rede apagou dados");
        cep.Text = "11111111"; Pump(650); cep.Text = "22222222"; Pump(650);
        tasks["22222222"].SetResult(new("Rua B", "Bairro B", "Cidade B", "SP")); Pump(50);
        tasks["11111111"].SetResult(good); Pump(50); Check(address.Text == "Rua B, Bairro B" && city.Text == "Cidade B / SP", "Resposta atrasada aplicou CEP anterior");
        cep.Text = "33333333"; Pump(650); address.Text = "Edição manual, 789"; tasks["33333333"].SetResult(good); Pump(50); Check(address.Text == "Edição manual, 789", "Consulta apagou edição manual durante requisição");
        cep.Text = "11111112"; Pump(650); f.Close(); panel.Dispose(); tasks["11111112"].SetResult(good); Pump(50); Check(address.Text == "Edição manual, 789", "Consulta alterou formulário fechado");
        Console.WriteLine("PASS consulta automática, máscara, CEP inválido/inexistente, offline, corrida de respostas, edição manual e fechamento");
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string caption);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h, int id);
    delegate bool WindowCallback(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static readonly HashSet<string> SeenWindows = new();
    static void DismissMessage()
    {
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var pid); if (pid != Environment.ProcessId) return true;
            var title = new System.Text.StringBuilder(256); var cls = new System.Text.StringBuilder(256);
            GetWindowText(h, title, 256); GetClassName(h, cls, 256);
            var name = title.ToString(); var type = cls.ToString();
            lock (SeenWindows) { if (SeenWindows.Add(name + type)) Console.WriteLine("QA WINDOW " + name + " [" + type + "]"); }
            if (type == "#32770" && name == "LEAL INFO PDV")
            {
                Console.WriteLine("QA confirmar mensagem nativa");
                PostMessage(h, 0x111, (IntPtr)1, GetDlgItem(h, 1));
                PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero);
            }
            return true;
        }, IntPtr.Zero);
    }
    static void CheckOrder(Form f)
    {
        var cep = All(f).OfType<TextBox>().Single(x => x.Name == "company_cep");
        var table = (TableLayoutPanel)cep.Parent!.Parent!;
        int row = table.GetRow(cep.Parent!);
        Check(All(table).OfType<Label>().Any(x => x.Text == "CEP"), "Legenda CEP ausente");
        if (table.ColumnCount == 2)
        {
            Check(table.GetControlFromPosition(0, row - 1)!.Text == "Telefone / WhatsApp", "CEP não está abaixo do telefone");
            Check(table.GetControlFromPosition(0, row + 1)!.Text == "Endereço", "CEP não está acima do endereço");
        }
        else
        {
            Check(table.GetControlFromPosition(0, row - 3)!.Text == "Telefone / WhatsApp", "Ordem inicial telefone incorreta");
            Check(table.GetControlFromPosition(0, row + 1)!.Text == "Endereço", "Ordem inicial endereço incorreta");
        }
        Check(!cep.ReadOnly && table.AutoScroll, "CEP bloqueado ou sem rolagem");
        Check(cep.Width >= 90 && cep.Height >= 20, "CEP cortado");
    }
    static void Forms(string flavor)
    {
        Database.Initialize();
        Exec("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1'),('company_name','EMPRESA FICTICIA QA CEP'),('company_phone','21999999999'),('company_address','Endereco salvo, 42'),('company_city_state','Cidade salva / RJ'),('company_cep','23934005')");
        Auth.CreateUser("QA FICTICIO", "qa_cep", "Senha_Ficticia_123", "ADMINISTRADOR", "qa@example.invalid", "");
        Check(Auth.Login("qa_cep", "Senha_Ficticia_123") != null, "Login falhou");
        A.GetType("LealInfoPDV.Licensing.InstallationLicense", true)!.GetProperty("Current", S)!.SetValue(null, new LicenseTerms("QA", 2, "unico", null, "", 1, "QA", "standard", "TESTE"));
        using var main = new MainForm(); main.Show(); Pump(150);
        ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer", I)!.GetValue(main)!).Stop();
        typeof(MainForm).GetField("automaticBackupCompleted", I)!.SetValue(main, true);
        Directory.CreateDirectory("cep-evidencias");
        var open = typeof(MainForm).GetMethod("OpenSettings", I)!;
        Console.WriteLine("OPEN central configurações"); open.Invoke(main, null); Pump(150);
        var config = Application.OpenForms.Cast<Form>().Single(x => x.Text == "Configurações do Sistema"); CheckOrder(config);
        var boxes = All(config).OfType<TextBox>().ToArray(); var cep = boxes.Single(x => x.Name == "company_cep");
        Check(cep.Text == "23934005" && boxes.Any(x => x.Text == "Endereco salvo, 42"), "Reabrir apagou valores anteriores");
        foreach (var size in new[] { new Size(900, 680), new Size(900, 760), new Size(1150, 900) })
        {
            config.Size = size; config.PerformLayout(); Pump(70); CheckOrder(config);
            using var bmp = new Bitmap(config.Width, config.Height); config.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); bmp.Save($"cep-evidencias/config-{size.Height}.png", ImageFormat.Png);
        }
        cep.Text = "23934-005"; boxes.Single(x => x.Text == "Endereco salvo, 42").Text = "Rua QA, 99, Bairro QA";
        boxes.Single(x => x.Text == "Cidade salva / RJ").Text = "São Paulo / SP";
        using var dismiss = new System.Threading.Timer(_ => DismissMessage(), null, 100, 100);
        Console.WriteLine("SAVE central configurações"); All(config).OfType<Button>().Single(x => x.Text == "SALVAR DADOS DA EMPRESA").PerformClick(); Console.WriteLine("SAVED central configurações");
        config.Close(); config.Dispose(); dismiss.Change(Timeout.Infinite, Timeout.Infinite);
        Check(Setting("company_cep") == "23934005" && Setting("company_address") == "Rua QA, 99, Bairro QA" && Setting("company_city_state") == "São Paulo / SP", "Persistência central falhou");
        open.Invoke(main, null); Pump(100); config = Application.OpenForms.Cast<Form>().Single(x => x.Text == "Configurações do Sistema");
        Check(All(config).OfType<TextBox>().Single(x => x.Name == "company_cep").Text == "23934005", "CEP não recarregou"); config.Close(); config.Dispose();
        bool seen = false; Exception? fail = null;
        using var inspect = new System.Windows.Forms.Timer { Interval = 80 };
        inspect.Tick += (_, _) =>
        {
            var f = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Dados da Empresa"); if (f == null || seen) return; seen = true; inspect.Stop();
            try { CheckOrder(f); All(f).OfType<TextBox>().Single(x => x.Name == "company_cep").Text = "23934-005";
                using var bmp = new Bitmap(f.Width, f.Height); f.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); bmp.Save("cep-evidencias/cadastro-inicial.png", ImageFormat.Png);
                All(f).OfType<Button>().Single(x => x.Text == "SALVAR").PerformClick(); }
            catch (Exception e) { fail = e; f.Close(); }
        };
        Console.WriteLine("OPEN cadastro inicial"); inspect.Start(); typeof(MainForm).GetMethod("ShowCompanyRegistration", I)!.Invoke(main, new object[] { false });
        Check(seen && fail == null, "Cadastro inicial falhou: " + fail); Check(Setting("company_cep") == "23934005", "CEP inicial não persistiu");
        Check((A.GetType("LealInfoPDV.PartsWithdrawal") != null) == (flavor == "002"), "Módulos 002 alterados");
        Check((A.GetType("LealInfoPDV.GlassProjectForm") != null) == (flavor == "003"), "Módulo vidraçaria 003 alterado");
        main.Close(); Console.WriteLine("PASS formulários reais, posição CEP, endereço editável, persistência central/inicial, rolagem e particularidades " + flavor);
    }
    static void Live()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        var updater = A.GetType("LealInfoPDV.UpdateManager", true)!;
        var load = updater.GetMethod("LoadManifestAsync", S, null, new[] { typeof(HttpClient), typeof(string), typeof(string) }, null)!;
        foreach (var id in new[] { "001", "002", "003", "004" })
        {
            var task = (Task)load.Invoke(null, new object[] { http, id, "10.379" })!; task.GetAwaiter().GetResult(); var m = task.GetType().GetProperty("Result")!.GetValue(task)!;
            string Read(string key) => (string)m.GetType().GetProperty(key)!.GetValue(m)!;
            Check(Read("Version") == "10.384" && Read("TargetClientCode") == (id == "004" ? "" : id), "Feed/alvo incorreto " + id);
            var data = http.GetByteArrayAsync(Read("PackageUrl")).GetAwaiter().GetResult();
            Check(Convert.ToHexString(SHA256.HashData(data)).Equals(Read("Sha256"), StringComparison.OrdinalIgnoreCase), "SHA remoto incorreto");
            using var b = new MemoryStream(data); using var z = new ZipArchive(b);
            Check(z.Entries.Select(x => x.FullName).Order().SequenceEqual(new[] { "LealInfoPDV.deps.json", "LealInfoPDV.dll", "LealInfoPDV.exe" }), "Conteúdo remoto inesperado");
            Console.WriteLine("PASS updater REAL " + id + " -> 10.384; alvo/download/SHA/arquivos válidos");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", "Somente runner descartável");
        if (args.Contains("--live")) { Live(); return; }
        _ = Task.Run(async () => { await Task.Delay(90000); Console.Error.WriteLine("QA TIMEOUT: teste excedeu 90 segundos"); Environment.Exit(1); });
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles();
        Console.WriteLine("START componentes CEP"); Components(); Console.WriteLine("START formulários reais"); Forms(args.FirstOrDefault() ?? "global"); Console.WriteLine("START consulta ViaCEP real fora da thread UI");
        var result = Task.Run(() => DeliveryAddress.LookupAsync("01001000", CancellationToken.None)).GetAwaiter().GetResult();
        Check(result.City.Length > 0 && result.Uf.Length == 2, "ViaCEP real inválido");
        Console.WriteLine("PASS ViaCEP real: " + result.City + " / " + result.Uf);
    }
}
