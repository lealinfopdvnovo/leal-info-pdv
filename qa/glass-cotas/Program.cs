using LealInfoPDV;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

static class Program
{
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    static T Find<T>(Control c, string name) where T : Control => All(c).OfType<T>().Single(x => x.Name == name);
    static int checks;
    static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }
    static void Pump() { Application.DoEvents(); Thread.Sleep(25); Application.DoEvents(); }
    static void Mouse(Control c, string method, int x, int y, int delta = 0) => c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(c, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, delta) });
    static byte[] Pixels(Control c) { using var b = new Bitmap(c.Width, c.Height); c.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); using var s = new MemoryStream(); b.Save(s, ImageFormat.Png); return SHA256.HashData(s.ToArray()); }
    static void Save(Control c, string name) { using var b = new Bitmap(c.Width, c.Height); c.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save("glass-cotas-evidencias/" + name + ".png", ImageFormat.Png); }
    static void Desktop(Form form, string name)
    {
        form.Activate(); form.BringToFront(); Pump();
        try { using var b = new Bitmap(form.ClientSize.Width, form.ClientSize.Height); using var g = Graphics.FromImage(b); g.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, b.Size); b.Save("glass-cotas-evidencias/" + name + "-desktop.png", ImageFormat.Png); }
        catch (System.ComponentModel.Win32Exception) { Console.WriteLine("CAPTURA DESKTOP indisponível; renderização nativa preservada."); }
    }
    static void Style(Form f)
    {
        typeof(MainForm).GetMethod("ApplyFloatingTheme", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(RuntimeHelpers.GetUninitializedObject(typeof(MainForm)), new object[] { f });
        typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object?[] { f, "Futurista Azul", null });
    }
    [STAThread] static void Main()
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", "Windows descartável exigido");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        Directory.CreateDirectory("glass-cotas-evidencias"); Database.Initialize();
        using (var db = Database.Open()) { using var q = db.CreateCommand(); q.CommandText = "INSERT INTO customers(name) VALUES('CLIENTE FICTICIO CORTE')"; q.ExecuteNonQuery(); }
        CompareClean3D();
        using var central = new GlassWorkshopForm(Style); central.Show(); Pump();
        var tabs = Find<TabControl>(central, "GlassSections");
        Check(tabs.TabCount == 7, "Sete abas preservadas");
        Find<Button>(central, "LoadQuoteCustomers").PerformClick();
        var customer = Find<ComboBox>(central, "QuoteCustomer"); customer.SelectedIndex = Enumerable.Range(0, customer.Items.Count).Single(i => customer.Items[i]!.ToString() == "CLIENTE FICTICIO CORTE");
        tabs.SelectedIndex = 5; Pump();
        var view = Find<GlassBox3DView>(central, "Drawing3D"); var toggle = Find<Button>(central, "Toggle3DMeasurements");
        Check(toggle.Visible && !view.ShowTechnicalDimensions, "3D limpo como padrão");
        toggle.PerformClick(); Pump(); Pixels(view);
        Check(view.ShowTechnicalDimensions && toggle.Text == "OCULTAR MEDIDAS", "Mostrar medidas");
        Check(view.TechnicalLayout!.Width.Text.Contains("1200") && view.TechnicalLayout.Height.Text.Contains("1000"), "Cotas da fonte original");
        Find<NumericUpDown>(central, "WidthMm").Value = 1800; Pump(); Pixels(view);
        Check(view.Project!.WidthMm == 1800 && view.TechnicalLayout!.Width.Text.Contains("1800"), "Cota atualiza com projeto");
        Find<NumericUpDown>(central, "WidthMm").Value = 1200;
        toggle.PerformClick(); Check(!view.ShowTechnicalDimensions, "Ocultar restaura modo limpo");
        Mouse(view, "OnMouseDown", 10, 10); Mouse(view, "OnMouseMove", 120, 55); Mouse(view, "OnMouseUp", 120, 55); Mouse(view, "OnMouseWheel", 0, 0, 120);
        Check(view.YawDegrees != -25 && view.ZoomFactor > 1, "Rotação e zoom preservados");
        Find<Button>(central, "Reset3D").PerformClick(); Check(view.YawDegrees == -25 && view.PitchDegrees == 18 && view.ZoomFactor == 1, "Reset preservado");
        tabs.SelectedIndex = 4; Pump(); Check(Find<GlassDrawingView>(central, "Drawing").Project!.WidthMm == 1200, "2D preservado");
        TestCutModal(central);
        TestSizes(central);
        central.Close(); Console.WriteLine($"PASS CORTE/COTAS WINDOWS: {checks} verificações; plano editável, A4, cotas, 3D limpo idêntico e dados originais preservados.");
    }
    static void CompareClean3D()
    {
        using var font = new Font("Segoe UI", 10);
        foreach (var dimensions in new[] { (1200m, 1000m), (1800m, 1000m), (1000m, 1500m) })
        foreach (var delta in new[] { (0, 0), (160, 80), (360, -80), (540, 100), (720, -120) })
        {
            using var current = new GlassBox3DView { Size = new Size(720, 440), Dock = DockStyle.None, Font = font, Project = new GlassProject("QA", dimensions.Item1, dimensions.Item2, 1) };
            using var baseline = new BaselineGlassBox3DView { Size = current.Size, Dock = DockStyle.None, Font = font, Project = current.Project };
            foreach (var c in new Control[] { current, baseline }) { Mouse(c, "OnMouseDown", 0, 0); Mouse(c, "OnMouseMove", delta.Item1, delta.Item2); Mouse(c, "OnMouseUp", delta.Item1, delta.Item2); }
            Check(Pixels(current).SequenceEqual(Pixels(baseline)), "Pixels do 3D limpo iguais à 10.388 " + dimensions + delta);
            current.ShowTechnicalDimensions = true; Pixels(current);
            Check(!current.IsUnavailable && current.TechnicalLayout != null, "Overlay sem falha");
            var layout = current.TechnicalLayout!;
            Check(current.ClientRectangle.Contains(layout.Width.Label) && current.ClientRectangle.Contains(layout.Height.Label) && current.ClientRectangle.Contains(layout.OrientationBounds), "Legendas dentro do visor");
            Check(!layout.Width.Label.IntersectsWith(layout.Height.Label) && !layout.Width.Label.IntersectsWith(layout.OrientationBounds) && !layout.Height.Label.IntersectsWith(layout.OrientationBounds), "Cotas sem sobreposição entre textos");
            Check(layout.Width.Text.Contains(dimensions.Item1.ToString("0.##")) && layout.Height.Text.Contains(dimensions.Item2.ToString("0.##")), "Medidas reais, sem cotas internas inventadas");
            if (delta == (0, 0) && dimensions == (1200m, 1000m)) Check(layout.Width.DrawLine && layout.Height.DrawLine, "Duas linhas de cota na perspectiva inicial");
            Check(layout.Orientation.Contains("interno/externo não definidos"), "Orientação ilustrativa explicitada");
            if (delta.Item1 == 360) Check(layout.Orientation.Contains("FACE B"), "Face posterior identificada");
            Save(current, $"3d-{dimensions.Item1}-{dimensions.Item2}-{delta.Item1}");
            current.ShowTechnicalDimensions = false; Check(Pixels(current).SequenceEqual(Pixels(baseline)), "Ocultar reproduz pixels aprovados");
            foreach (var c in new Control[] { current, baseline }) Mouse(c, "OnMouseWheel", 0, 0, 240);
            Check(Pixels(current).SequenceEqual(Pixels(baseline)), "Zoom limpo idêntico");
            current.ShowTechnicalDimensions = true; Pixels(current); Check(!current.IsUnavailable && current.TechnicalLayout != null, "Overlay no zoom");
        }
        using var tiny = new GlassBox3DView { Size = new Size(500, 100), Dock = DockStyle.None, Font = font, Project = new GlassProject("PEQUENO", 1200, 1000, 1), ShowTechnicalDimensions = true };
        using var tinyBaseline = new BaselineGlassBox3DView { Size = tiny.Size, Dock = DockStyle.None, Font = font, Project = tiny.Project };
        Check(!tiny.CanShowTechnicalDimensions && Pixels(tiny).SequenceEqual(Pixels(tinyBaseline)) && tiny.TechnicalLayout == null && !tiny.IsUnavailable, "Visor pequeno preserva 3D/dimensões originais sem sobrepor novas cotas");
    }
    static void TestCutModal(GlassWorkshopForm central)
    {
        Exception? error = null; GlassCutPlanForm? remembered = null; bool opened = false;
        var tabs = Find<TabControl>(central, "GlassSections"); tabs.SelectedIndex = 1;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            var cut = Application.OpenForms.OfType<GlassCutPlanForm>().FirstOrDefault(); if (cut == null) return; timer.Stop();
            try
            {
                opened = true; remembered = cut; Check(cut.Modal && cut.Owner == central, "Plano integrado à Central em modal própria");
                var grid = Find<DataGridView>(cut, "CutPieces"); var rows = (BindingList<GlassCutPlanForm.PieceRow>)grid.DataSource!;
                Check(grid.Columns.Cast<DataGridViewColumn>().All(c => c is DataGridViewTextBoxColumn or DataGridViewCheckBoxColumn or DataGridViewComboBoxColumn), "Tipos da grade corretos");
                Check(rows.Count == 1 && rows[0].Width == "1200" && rows[0].Height == "1000", "Referência inicial do projeto sem folgas inventadas");
                Check(Find<NumericUpDown>(cut, "SheetWidth").Value == 0 && Find<NumericUpDown>(cut, "SheetHeight").Value == 0, "Chapa não hardcoded");
                Find<NumericUpDown>(cut, "SheetWidth").Value = 2500; Find<NumericUpDown>(cut, "SheetHeight").Value = 1800;
                Find<Button>(cut, "AddCutPiece").PerformClick(); Find<Button>(cut, "AddCutPiece").PerformClick();
                rows[1].Width = "600"; rows[1].Height = "500"; rows[2].Width = "500"; rows[2].Height = "400";
                Check(cut.Recalculate() && cut.Result!.Sheets.Sum(s => s.Pieces.Count) == 3, "Adicionar e distribuir três peças");
                string first = rows[0].Width, second = rows[1].Width;
                grid.CurrentCell = grid.Rows[2].Cells["Width"]; grid.BeginEdit(true); ((TextBox)grid.EditingControl!).Text = "450"; grid.EndEdit(); Pump();
                Check(rows[2].Width == "450" && rows[0].Width == first && rows[1].Width == second, "Editar somente PEÇA 03 na grade real");
                Check(cut.Result == null && !Find<Button>(cut, "CutPreview").Enabled, "Edição invalida resultado anterior");
                rows[2].Quantity = "2"; Check(cut.Recalculate() && cut.Result!.Sheets.Sum(s => s.Pieces.Count) == 4, "Recalcular quantidade");
                Save(cut, "plano-multiplas-pecas"); Desktop(cut, "plano-multiplas-pecas");
                using (var a4 = GlassCutPlanPrint.RenderPage(new(new("REFERÊNCIA 1200 × 1000", 1200, 1000, 1), "CLIENTE FICTICIO CORTE", Array.Empty<GlassMaterialLink>()), cut.Result!)) a4.Save("glass-cotas-evidencias/plano-varias-pecas-a4.png", ImageFormat.Png);
                Check(Find<NumericUpDown>(central, "WidthMm").Value == 1200 && Find<NumericUpDown>(central, "HeightMm").Value == 1000 && Find<NumericUpDown>(central, "Quantity").Value == 1, "Plano não altera o projeto");
                Find<NumericUpDown>(central, "WidthMm").Value = 1800; Check(rows[0].Width == "1200", "Alterar projeto não apaga edição operacional do plano");
                Find<Button>(cut, "AddCutReference").PerformClick(); Check(rows.Last().Width == "1800", "Importação explícita usa fonte atual");
                grid.CurrentCell = grid.Rows[rows.Count - 1].Cells["Id"]; Find<Button>(cut, "RemoveCutPiece").PerformClick(); Check(rows.Count == 3, "Remoção individual");
                rows[2].Width = "abc"; Check(!cut.Recalculate() && cut.Result == null, "Texto numérico inválido bloqueado sem erro de cast");
                rows[2].Width = "3000"; Check(cut.Recalculate() && cut.Result!.RejectedQuantity == 2, "Peça maior identificada");
                rows.Clear(); Find<Button>(cut, "AddCutPiece").PerformClick(); rows[0].Width = "60"; rows[0].Height = "100";
                Find<NumericUpDown>(cut, "SheetWidth").Value = 100; Find<NumericUpDown>(cut, "SheetHeight").Value = 60;
                Check(cut.Recalculate() && cut.Result!.RejectedQuantity == 1, "Rotação global desligada");
                Find<Button>(cut, "CutAllowRotation").PerformClick(); Check(cut.Recalculate() && cut.Result!.Sheets.Single().Pieces.Single().Rotated, "Rotação ligada explicitamente");
                rows[0].Rotation = false; Check(cut.Recalculate() && cut.Result!.RejectedQuantity == 1, "Rotação individual bloqueada"); rows[0].Rotation = true;
                rows[0].Material = "MATERIAL A"; Find<Button>(cut, "AddCutPiece").PerformClick(); rows[1].Width = "60"; rows[1].Height = "100"; rows[1].Material = "MATERIAL B";
                Check(cut.Recalculate() && cut.Result!.Sheets.Count == 2, "Materiais isolados em chapas distintas");
                var drawing = Find<GlassCutDrawingView>(cut, "CutDrawing"); Find<Button>(cut, "CutNext").PerformClick(); Check(drawing.SheetIndex == 1, "Navegar próxima chapa"); Find<Button>(cut, "CutPrevious").PerformClick(); Check(drawing.SheetIndex == 0, "Navegar chapa anterior");
                var result = cut.Result!; var context = new GlassCutContext(new("FICTICIO", 1800, 1000, 1), "CLIENTE FICTICIO CORTE", Array.Empty<GlassMaterialLink>());
                Check(GlassCutPlanPrint.PageCount(result) >= 3, "Impressão inclui chapas e lista de peças");
                for (int i = 0; i < GlassCutPlanPrint.PageCount(result); i++) { using var page = GlassCutPlanPrint.RenderPage(context, result, i); Check(page.Width == 1588 && page.Height == 2246, "A4 do plano"); page.Save($"glass-cotas-evidencias/plano-a4-{i}.png", ImageFormat.Png); }
                bool preview = false; using var nested = new System.Windows.Forms.Timer { Interval = 50 };
                nested.Tick += (_, _) => { var print = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "PRÉVIA A4 — PLANO DE CORTE"); if (print == null) return; nested.Stop(); preview = true; Check(print.Modal && print.Owner == cut && Find<PictureBox>(print, "CutPrintPage").Image != null, "Prévia A4 real"); print.Close(); };
                nested.Start(); Find<Button>(cut, "CutPreview").PerformClick(); Check(preview, "Botão prévia funcional");
                Save(cut, "plano-modal"); Desktop(cut, "plano-modal");
            }
            catch (Exception ex) { error = ex; }
            finally { cut.Close(); }
        };
        timer.Start(); Find<Button>(central, "OpenCutPlan").PerformClick(); if (error != null) throw error; Check(opened, "Abrir plano pelo fluxo real");
        bool reopened = false; using var reopen = new System.Windows.Forms.Timer { Interval = 50 };
        reopen.Tick += (_, _) => { var cut = Application.OpenForms.OfType<GlassCutPlanForm>().FirstOrDefault(); if (cut == null) return; reopen.Stop(); reopened = true; Check(ReferenceEquals(cut, remembered) && Find<NumericUpDown>(cut, "SheetWidth").Value == 100, "Edições preservadas ao reabrir na mesma sessão"); cut.Close(); };
        reopen.Start(); Find<Button>(central, "OpenCutPlan").PerformClick(); Check(reopened, "Reabertura segura");
    }
    static void TestSizes(GlassWorkshopForm central)
    {
        foreach (var size in new[] { new Size(1366, 768), new Size(1600, 900), new Size(1920, 1080), new Size(640, 440) })
        foreach (var factor in new[] { 1f, 1.25f, 1.5f })
        {
            using var cut = new GlassCutPlanForm(() => new GlassCutContext(new("RESOLUÇÃO QA", 1200, 1000, 1), "FICTICIO", Array.Empty<GlassMaterialLink>()), Style);
            cut.Show(central); cut.Scale(new SizeF(factor, factor)); cut.Size = new Size(Math.Min(cut.Width, size.Width), Math.Min(cut.Height, size.Height - (size.Width == 640 ? 0 : 60))); Pump();
            Check(All(cut).OfType<Button>().All(b => b.GetType().Name == "SalesVisualButton"), "Componentes reais do tema preservados");
            foreach (var c in All(cut).Where(c => c.Visible && c is NumericUpDown or ComboBox or Button or DataGridView))
            {
                Check(c.Width > 35 && c.Height >= 20, "Controle acessível " + c.Name);
                var parent = c.Parent; while (parent != null && parent is not Panel { AutoScroll: true }) parent = parent.Parent;
                if (parent is Panel scroll) { scroll.ScrollControlIntoView(c); Pump(); Check(scroll.RectangleToScreen(scroll.ClientRectangle).IntersectsWith(c.RectangleToScreen(c.ClientRectangle)), "Rolagem alcança " + c.Name); }
                if (c is Button b) Check(TextRenderer.MeasureText(b.Text, b.Font).Width <= b.Width - 4, "Texto do botão completo " + c.Name);
            }
            Save(cut, $"plano-{size.Width}-{factor:0.00}");
            if (size.Width == 640 && factor == 1) Desktop(cut, "plano-pequeno");
            cut.Close();
        }
    }
}
