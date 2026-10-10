using LealInfoPDV;
using System.Drawing.Imaging;
using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Security.Cryptography;

static class Program
{
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    static T Find<T>(Control f, string name) where T : Control => All(f).OfType<T>().Single(x => x.Name == name);
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    static void Pump() { Application.DoEvents(); Thread.Sleep(40); Application.DoEvents(); }
    static void Escape(Form f) => typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f, new object[] { Keys.Escape });
    static void Capture(Form f, string name) { using var bmp = new Bitmap(f.Width, f.Height); f.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); bmp.Save("glass-evidencias/" + name + ".png", ImageFormat.Png); }
    sealed class Feed(string id) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            bool global = r.RequestUri!.AbsolutePath.EndsWith("/main/version.json");
            string version = !global && id == "003" ? "10.381" : "10.379";
            string target = global ? "" : id;
            if (!global && id == "004") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            { Version = version, TargetClientCode = target, Sha256 = new string('a', 64), PackageUrl = $"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/{(global ? "v" : "client-" + id + "-v")}{version}/UPDATE.zip" })) });
        }
    }
    static async Task Selection(bool live)
    {
        var load = typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateManager")!.GetMethod("LoadManifestAsync", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(HttpClient), typeof(string), typeof(string) }, null)!;
        foreach (string id in new[] { "001", "002", "003", "004" })
        {
            using var http = live ? new HttpClient() : new HttpClient(new Feed(id));
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-QA-Vidracaria/10.381");
            var task = (Task)load.Invoke(null, new object[] { http, id, "10.378" })!; await task;
            var m = task.GetType().GetProperty("Result")!.GetValue(task)!;
            string Get(string key) => (string)m.GetType().GetProperty(key)!.GetValue(m)!;
            Check(Get("Version") == (id == "003" ? "10.381" : "10.379"), (live ? "REAL " : "MOCK ") + "versao/canal " + id);
            Check(Get("TargetClientCode") == (id == "004" ? "" : id), "Alvo exclusivo " + id);
            if (live && id == "003")
            {
                var bytes = await http.GetByteArrayAsync(Get("PackageUrl"));
                Check(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == Get("Sha256"), "Hash remoto 003");
            }
        }
    }
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory("glass-evidencias");
        if (args.Contains("--live")) { Selection(true).GetAwaiter().GetResult(); return; }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        Selection(false).GetAwaiter().GetResult();
        Check(new GlassProject("", 0, 1000, 1).ValidationError != null, "Zero bloqueado");
        Check(new GlassProject("", -1, 1000, 1).ValidationError != null, "Negativo bloqueado");
        Check(new GlassProject("", 1200, 1000, 0).ValidationError != null, "Quantidade zero bloqueada");
        Check(GlassWindowRenderer.Millimeters(1200) == "1200 mm", "Cota largura mm");
        Check(GlassWindowRenderer.Millimeters(1000) == "1000 mm", "Cota altura mm");
        foreach (var size in new[] { new Size(1366, 768), new Size(1600, 900), new Size(1920, 1080) })
        foreach (float factor in new[] { 1f, 1.25f, 1.5f })
        {
            using var f = new GlassProjectForm(); f.Show(); f.Scale(new SizeF(factor, factor));
            f.Size = new Size(Math.Min(f.Width, size.Width), Math.Min(f.Height, size.Height - 60)); Pump();
            var w = Find<NumericUpDown>(f, "WidthMm"); var h = Find<NumericUpDown>(f, "HeightMm");
            var q = Find<NumericUpDown>(f, "Quantity"); var draw = Find<GlassDrawingView>(f, "Drawing");
            var view = Find<Button>(f, "Visualize"); var preview = Find<Button>(f, "Preview");
            Check(!preview.Enabled && draw.Project == null, "Nenhuma impressao antes da visualizacao");
            foreach (var measures in new[] { (1200m, 1000m), (1800m, 1200m), (1000m, 1500m) })
            {
                w.Value = measures.Item1; h.Value = measures.Item2; q.Value = 3; view.PerformClick(); Pump();
                Check(draw.Project == new GlassProject("Projeto de teste", measures.Item1, measures.Item2, 3), "Parametros vinculados/quantidade");
                Check(f.RectangleToScreen(f.ClientRectangle).Contains(draw.RectangleToScreen(draw.ClientRectangle)), "Area grafica visivel dentro da janela (DPI)");
                var area = (RectangleF)draw.ClientRectangle; var bounds = GlassWindowRenderer.WindowBounds(area, draw.Project!);
                Check(Math.Abs(bounds.Width / bounds.Height - (float)(measures.Item1 / measures.Item2)) < .001, $"Proporcao {measures} {size} {factor}");
                Check(area.Contains(bounds) && bounds.Width > 0 && bounds.Height > 0, "Desenho dentro da area");
                Check(preview.Enabled && view.Text == "ATUALIZAR DESENHO", "Atualizacao sem reiniciar");
                Capture(f, $"projeto-{size.Width}-{factor:0.00}-{measures.Item1}-{measures.Item2}");
            }
            w.Value = 0; view.PerformClick(); Check(draw.Project == null && !preview.Enabled, "Invalido limpa desenho/bloqueia impressao");
            w.Value = 1200; h.Value = 1000; view.PerformClick();
            Find<TextBox>(f, "Description").Text = "Janela de teste";
            Check(!preview.Enabled, "Dados alterados exigem atualizar antes de imprimir"); view.PerformClick();
            Check(Find<Button>(f, "CloseProject").Bottom <= Find<Button>(f, "CloseProject").Parent!.Height, "FECHAR acessivel");
            var scroll = Find<Panel>(f, "Parameters"); scroll.ScrollControlIntoView(preview); Pump();
            Check(preview.Visible && preview.Parent!.RectangleToScreen(preview.Bounds).IntersectsWith(scroll.RectangleToScreen(scroll.ClientRectangle)), "Rolagem acesso aos botoes");
            Check(!Find<Button>(f, "SaveProject").Enabled, "Salvar desabilitado sem alterar banco");
            if (factor == 1 && size.Width == 1366)
            {
                bool seen = false; Exception? error = null;
                using var timer = new System.Windows.Forms.Timer { Interval = 60 };
                timer.Tick += (_, _) => {
                    var p = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text.StartsWith("PRÉVIA A4")); if (p == null) return;
                    timer.Stop(); seen = true;
                    try { Check(p.Modal && p.Owner == f, "Previa modal pertence projeto");
                        var image = Find<PictureBox>(p, "A4Page").Image!;
                        Check(image.Width == 794 && image.Height == 1123, "Previa proporcao A4"); Capture(p, "previa-A4"); Escape(p);
                    } catch (Exception ex) { error = ex; p.Close(); }
                };
                timer.Start(); preview.PerformClick(); Check(seen, "Previa real abriu"); if (error != null) throw error;
            }
            Escape(f); Check(f.IsDisposed || !f.Visible, "ESC projeto");
        }
        using (var central = new GlassWorkshopForm())
        {
            central.Show(); Pump(); bool seen = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 60 };
            timer.Tick += (_, _) => { var f = Application.OpenForms.OfType<GlassProjectForm>().FirstOrDefault(); if (f == null) return; timer.Stop(); seen = true; Check(f.Owner == central && f.Modal, "Novo Projeto na Central"); Escape(f); };
            timer.Start(); Find<Button>(central, "NewGlassProject").PerformClick(); Check(seen, "Central Projetos Novo Projeto"); Escape(central);
        }
        using (var page = GlassProjectPrint.RenderPage(new GlassProject("Janela homologacao", 1800, 1200, 3)))
        { Check(page.GetPixel(1, 1).ToArgb() == Color.White.ToArgb(), "Papel branco A4"); page.Save("glass-evidencias/A4-1800-1200.png", ImageFormat.Png); }
        Console.WriteLine("PASS modelo, proporcoes, cotas, quantidade, atualizacao, validacoes, A4, ESC, 9 layouts; impressao fisica pendente");
    }
}
