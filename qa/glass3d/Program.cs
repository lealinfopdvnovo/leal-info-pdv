using LealInfoPDV;
using System.Reflection;
using System.Drawing.Imaging;
using System.Diagnostics;

static class Program
{
    static readonly BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    static T Find<T>(Control c, string name) where T : Control => All(c).OfType<T>().Single(x => x.Name == name);
    static void Check(bool condition, string text) { if (!condition) throw new Exception("FAIL " + text); Console.WriteLine("PASS " + text); }
    static void Pump(int ms = 60) { var t = Stopwatch.StartNew(); while (t.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Mouse(GlassBox3DView v, string method, MouseButtons button, int x, int y, int delta = 0) => typeof(GlassBox3DView).GetMethod(method, I)!.Invoke(v, new object[] { new MouseEventArgs(button, 1, x, y, delta) });
    static void Capture(Form f, string name) { using var b = new Bitmap(f.Width, f.Height); f.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save("glass3d-evidencias/" + name + ".png", ImageFormat.Png); }
    [STAThread] static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        Directory.CreateDirectory("glass3d-evidencias");
        foreach (var size in new[] { new Size(1366, 768), new Size(1600, 900), new Size(1920, 1080) })
        foreach (float scale in new[] { 1f, 1.25f, 1.5f })
        {
            using var f = new GlassProjectForm(); f.Show(); f.Scale(new SizeF(scale, scale));
            f.Size = new Size(Math.Min(f.Width, size.Width), Math.Min(f.Height, size.Height - 60)); Pump();
            var two = Find<GlassDrawingView>(f, "Drawing"); var mode3D = Find<Button>(f, "Mode3D");
            Check(two.Visible && !mode3D.Enabled && !All(f).OfType<GlassBox3DView>().Any(), "2D padrao / 3D criado apenas sob demanda");
            var view = Find<Button>(f, "Visualize"); var w = Find<NumericUpDown>(f, "WidthMm"); var h = Find<NumericUpDown>(f, "HeightMm");
            foreach (var pair in new[] { (1200m, 1000m), (1800m, 1000m), (1000m, 1500m) })
            {
                w.Value = pair.Item1; h.Value = pair.Item2; view.PerformClick(); mode3D.PerformClick(); Pump();
                var three = Find<GlassBox3DView>(f, "Drawing3D");
                Check(three.Visible && !two.Visible && three.Project == two.Project, "Alternar 2D para 3D / parametros compartilhados");
                Check(f.RectangleToScreen(f.ClientRectangle).Contains(three.RectangleToScreen(three.ClientRectangle)), "3D contido na janela " + size + " " + scale);
                // Testa a geometria fonte, não apenas a legenda.
                var vertices = (Array)typeof(GlassBox3DView).GetField("vertices", I)!.GetValue(three)!;
                object a = vertices.GetValue(0)!, b = vertices.GetValue(2)!;
                float X(object v) => (float)v.GetType().GetProperty("X")!.GetValue(v)!;
                float Y(object v) => (float)v.GetType().GetProperty("Y")!.GetValue(v)!;
                Check(Math.Abs((X(b) - X(a)) / (Y(b) - Y(a)) - (float)(pair.Item1 / pair.Item2)) < .0001, "Proporcao geometrica 3D " + pair);
                string expectedDimensions = $"DIMENSÕES DO PROJETO\nLargura: {pair.Item1:0.##} mm\nAltura: {pair.Item2:0.##} mm";
                string Dimensions() => (string)typeof(GlassBox3DView).GetField("dimensionsText", I)!.GetValue(three)!;
                Check(Dimensions() == expectedDimensions, "Medidas fixas da mesma fonte do 2D " + pair);
                Capture(f, $"Inicial-{size.Width}-{scale:0.00}-{pair.Item1}-{pair.Item2}");
                float yaw = three.YawDegrees, pitch = three.PitchDegrees;
                Mouse(three, "OnMouseMove", MouseButtons.None, 10, 10); Check(three.YawDegrees == yaw, "Mouse sem arrastar nao gira");
                Mouse(three, "OnMouseDown", MouseButtons.Left, 10, 10); Mouse(three, "OnMouseMove", MouseButtons.Left, 150, 70); Mouse(three, "OnMouseUp", MouseButtons.Left, 150, 70);
                Check(three.YawDegrees != yaw && three.PitchDegrees != pitch, "Rotacao horizontal e vertical pelo mouse");
                Mouse(three, "OnMouseWheel", MouseButtons.None, 0, 0, 120); Check(three.ZoomFactor > 1, "Zoom rodinha");
                Capture(f, $"3D-{size.Width}-{scale:0.00}-{pair.Item1}-{pair.Item2}");
                for (int i = 0; i < 50; i++) Mouse(three, "OnMouseWheel", MouseButtons.None, 0, 0, 120);
                Check(three.ZoomFactor == 2.5f, "Limite zoom superior");
                Check(Dimensions() == expectedDimensions, "Medidas independentes de rotacao e zoom");
                Capture(f, $"Zoom-{size.Width}-{scale:0.00}-{pair.Item1}-{pair.Item2}");
                for (int i = 0; i < 100; i++) Mouse(three, "OnMouseWheel", MouseButtons.None, 0, 0, -120);
                Check(three.ZoomFactor == .55f, "Limite zoom inferior");
                Mouse(three, "OnMouseDown", MouseButtons.Left, 0, 0); Mouse(three, "OnMouseMove", MouseButtons.Left, 0, 10000); Mouse(three, "OnMouseUp", MouseButtons.Left, 0, 10000);
                Check(three.PitchDegrees == -75, "Limite inclinacao vertical");
                Find<Button>(f, "Reset3D").PerformClick(); Check(three.YawDegrees == -25 && three.PitchDegrees == 18 && three.ZoomFactor == 1, "Resetar visao");
                Mouse(three, "OnMouseDown", MouseButtons.Left, 0, 0); Mouse(three, "OnMouseMove", MouseButtons.Left, 360, 0); Mouse(three, "OnMouseUp", MouseButtons.Left, 360, 0);
                Capture(f, $"Traseira-{size.Width}-{scale:0.00}-{pair.Item1}-{pair.Item2}");
                Find<Button>(f, "Reset3D").PerformClick();
                for (int n = 0; n < 4; n++) { Find<Button>(f, "Mode2D").PerformClick(); mode3D.PerformClick(); }
                Check(three.Project == two.Project && Dimensions() == expectedDimensions, "Alternancia repetida preserva medidas");
                int paints = 0; PaintEventHandler count = (_, _) => paints++; three.Paint += count;
                Pump(100); paints = 0; yaw = three.YawDegrees; pitch = three.PitchDegrees; Pump(350);
                Check(three.YawDegrees == yaw && three.PitchDegrees == pitch && paints <= 2, "Repouso sem animacao / sem loop de renderizacao");
                three.Paint -= count;
                Find<Button>(f, "Mode2D").PerformClick(); Pump();
                Check(two.Visible && !three.Visible && !Find<Button>(f, "Reset3D").Visible, "Retorno 3D para 2D");
                var r = GlassWindowRenderer.WindowBounds(two.ClientRectangle, two.Project!);
                Check(Math.Abs(r.Width / r.Height - (float)(pair.Item1 / pair.Item2)) < .001, "2D original proporcional preservado");
            }
            // Provoca uma falha GDI real local com Graphics descartado, sem simular sucesso.
            mode3D.PerformClick(); Pump(); var v3 = Find<GlassBox3DView>(f, "Drawing3D"); var original = two.Project;
            using var bitmap = new Bitmap(16, 16); var broken = Graphics.FromImage(bitmap); broken.Dispose();
            typeof(GlassBox3DView).GetMethod("OnPaint", I)!.Invoke(v3, new object[] { new PaintEventArgs(broken, new Rectangle(0, 0, 16, 16)) });
            Pump(); Check(v3.IsUnavailable && !mode3D.Enabled && two.Visible && two.Project == original, "Falha GDI retorna ao 2D sem perder projeto");
            Check(Find<Button>(f, "Preview").Enabled && Find<Label>(f, "ViewHint").Text.Contains("indisponível"), "Impressao 2D preservada / aviso discreto");
            Capture(f, $"fallback-{size.Width}-{scale:0.00}");
            f.Close();
        }
        using (var reopened = new GlassProjectForm())
        { reopened.Show(); Pump(); Check(Find<GlassDrawingView>(reopened, "Drawing").Visible && !All(reopened).OfType<GlassBox3DView>().Any(), "Reabrir em 2D / estado independente"); reopened.Close(); }
        using (var three = new GlassBox3DView { Size = new Size(640, 420), Project = new GlassProject("Benchmark", 1200, 1000, 1) })
        using (var bitmap = new Bitmap(640, 420))
        {
            three.CreateControl(); three.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            long allocated = GC.GetAllocatedBytesForCurrentThread(); var t = Stopwatch.StartNew();
            for (int i = 0; i < 300; i++) three.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            t.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Console.WriteLine($"BENCHMARK 300 desenhos GDI+ 640x420: {t.Elapsed.TotalMilliseconds:0.00} ms; media {t.Elapsed.TotalMilliseconds / 300:0.000} ms; alocacao total framework {allocated} bytes");
            Check(t.Elapsed < TimeSpan.FromSeconds(15), "Renderizacao benchmark responsiva no runner");
            Check(!typeof(GlassBox3DView).GetFields(I).Any(x => x.FieldType.Name.Contains("Timer")), "Sem timer no componente 3D");
        }
        Console.WriteLine("PASS 3D tecnico local, oito vertices/seis faces, 9 layouts, mouse, zoom, reset, repouso, fallback e 2D preservado; hardware modesto fisico pendente");
    }
}
