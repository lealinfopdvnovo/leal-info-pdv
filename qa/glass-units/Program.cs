using LealInfoPDV;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Drawing.Imaging;

static class Program
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); count++; Console.WriteLine("PASS " + name); }
    static IEnumerable<Control> All(Control p) => p.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c)));
    static T Find<T>(Control p, string name) where T : Control => All(p).OfType<T>().Single(c => c.Name == name);
    static void Pump() { Application.DoEvents(); Thread.Sleep(30); Application.DoEvents(); }
    static void Style(Form f)
    {
        typeof(MainForm).GetMethod("ApplyFloatingTheme", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(RuntimeHelpers.GetUninitializedObject(typeof(MainForm)), new object[] { f });
        typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object?[] { f, "Futurista Azul", null });
    }
    [STAThread] static void Main()
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", "Runner Windows descartável");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        Directory.CreateDirectory("glass-units-evidencias"); Database.Initialize();
        foreach (var size in new[] {1000m, 3000m, 1200m, 800m, 1900m, 0.01m, 1000000m})
        foreach (var unit in GlassDimensionUnits.Symbols)
        {
            decimal value = GlassDimensionUnits.Convert(size,"mm",unit);
            Check(GlassDimensionUnits.ToMillimetres(value,unit)==size,"Conversão exata " + size + " " + unit);
            for(int i=0;i<100;i++) { value=GlassDimensionUnits.Convert(value,unit,"mm"); value=GlassDimensionUnits.Convert(value,"mm",unit); }
            Check(GlassDimensionUnits.ToMillimetres(value,unit)==size,"100 trocas sem acumular erro");
        }
        Check(GlassDimensionUnits.ConvertText("1,90","m","mm")=="1900","Cultura pt-BR");
        var reference=GlassCutPacking.Calculate(3000,2000,new[]{new GlassCutPiece("A","",800,1900,2,""),new GlassCutPiece("B","",1200,1000,1,"")},true);
        foreach(var unit in GlassDimensionUnits.Symbols)
        {
            var equivalent=GlassCutPacking.Calculate(GlassDimensionUnits.ToMillimetres(GlassDimensionUnits.Convert(3000,"mm",unit),unit),2000,new[]{new GlassCutPiece("A","",GlassDimensionUnits.ToMillimetres(80,"cm"),GlassDimensionUnits.ToMillimetres(1.9m,"m"),2,""),new GlassCutPiece("B","",1200,GlassDimensionUnits.ToMillimetres(1,"m"),1,"")},true);
            Check(reference.TotalArea == equivalent.TotalArea && reference.UsedArea == equivalent.UsedArea && reference.UnusedArea == equivalent.UnusedArea && reference.Utilization == equivalent.Utilization && reference.Waste == equivalent.Waste && reference.Sheets.Count == equivalent.Sheets.Count &&
                reference.Sheets.SelectMany(s=>s.Pieces.Select(p=>(s.Number,s.Material,p.Piece.Id,p.Instance,p.X,p.Y,p.WidthMm,p.HeightMm,p.Rotated))).SequenceEqual(equivalent.Sheets.SelectMany(s=>s.Pieces.Select(p=>(s.Number,s.Material,p.Piece.Id,p.Instance,p.X,p.Y,p.WidthMm,p.HeightMm,p.Rotated)))),"Packing e aproveitamento equivalentes " + unit);
        }
        using var central = new GlassWorkshopForm(Style); central.Show(); Pump();
        var tabs=Find<TabControl>(central,"GlassSections"); tabs.SelectedIndex=2;
        var wu=Find<ComboBox>(central,"WidthMmUnit"); var hu=Find<ComboBox>(central,"HeightMmUnit");
        wu.SelectedItem="cm"; Check(Find<NumericUpDown>(central,"WidthMmEntry").Value==120,"1200 mm → 120 cm");
        wu.SelectedItem="m"; Check(Find<NumericUpDown>(central,"WidthMmEntry").Value==1.2m,"120 cm → 1,2 m");
        Check(Find<NumericUpDown>(central,"WidthMm").Value==1200,"Fonte canônica preservada");
        wu.SelectedItem="mm"; hu.SelectedItem="m"; Find<NumericUpDown>(central,"HeightMmEntry").Value=1;
        tabs.SelectedIndex=5; Pump();
        var view=Find<GlassBox3DView>(central,"Drawing3D"); Check(view.Project!.WidthMm==1200 && view.Project.HeightMm==1000,"3D recebe mistura mm/m normalizada");
        tabs.SelectedIndex=2; wu.SelectedItem="cm"; Find<NumericUpDown>(central,"WidthMmEntry").Value=80; Find<NumericUpDown>(central,"HeightMmEntry").Value=1.9m;
        tabs.SelectedIndex=4; Pump(); Check(Find<GlassDrawingView>(central,"Drawing").Project is {WidthMm:800, HeightMm:1900},"2D recebe 80 cm × 1,90 m");
        foreach(var scale in new[]{1f,1.25f,1.5f}) foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
        {
            central.Size=size; central.Font=new Font("Segoe UI",10*scale); tabs.SelectedIndex=2; Pump();
            foreach(var name in new[]{"WidthMmEntry","HeightMmEntry","WidthMmUnit","HeightMmUnit"})
            {
                var c=Find<Control>(central,name); Check(c.Visible && c.Width>30 && c.Height>12,"Campo unidade visível " + name);
            }
            using var image=new Bitmap(central.Width,central.Height); central.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save($"glass-units-evidencias/medidas-{size.Width}-{scale}.png",ImageFormat.Png);
        }
        TestCut(central); central.Close(); Console.WriteLine($"PASS UNIDADES/EDIÇÃO: {count} verificações Windows");
    }
    static void TestCut(Form owner)
    {
        using var cut=new GlassCutPlanForm(()=>new(new GlassProject("TESTE",1200,1000,1),"CLIENTE FICTICIO",Array.Empty<GlassMaterialLink>()),Style); cut.Show(owner); Pump();
        var grid=Find<DataGridView>(cut,"CutPieces"); var rows=(BindingList<GlassCutPlanForm.PieceRow>)grid.DataSource!;
        Find<NumericUpDown>(cut,"SheetWidth").Value=3000; Find<NumericUpDown>(cut,"SheetHeight").Value=2000;
        Find<ComboBox>(cut,"SheetWidthUnit").SelectedItem="m"; Find<ComboBox>(cut,"SheetHeightUnit").SelectedItem="cm";
        Check(Find<NumericUpDown>(cut,"SheetWidthEntry").Value==3 && Find<NumericUpDown>(cut,"SheetHeightEntry").Value==200,"Chapa m/cm individual");
        rows[0].WidthUnit="cm"; rows[0].HeightUnit="m"; Check(rows[0].Width=="120" && rows[0].Height=="1","Peça cm/m preserva dimensão");
        Check(cut.Recalculate() && cut.Result!.UsedArea==1.2m,"Packing normaliza peça e chapa");
        Find<Button>(cut,"AddCutPiece").PerformClick(); rows[1].Width="800";rows[1].Height="500";
        string untouched=System.Text.Json.JsonSerializer.Serialize(rows[0]); grid.CurrentCell=grid.Rows[1].Cells["Id"];
        Exception? error=null; bool edited=false;
        using var timer=new System.Windows.Forms.Timer{Interval=50};
        timer.Tick+=(_,_)=>
        {
            var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="GlassCutPieceEditor");if(editor==null)return;timer.Stop();
            try
            {
                Find<ComboBox>(editor,"EditPieceWidthUnit").SelectedItem="cm";
                Find<NumericUpDown>(editor,"EditPieceWidthEntry").Value=60;
                Find<ComboBox>(editor,"EditPieceHeightUnit").SelectedItem="m";
                Find<NumericUpDown>(editor,"EditPieceHeightEntry").Value=0.7m;
                Find<TextBox>(editor,"EditPieceDescription").Text="SOMENTE PEÇA 02";
                using var image=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("glass-units-evidencias/editar-peca.png",ImageFormat.Png);
                Find<Button>(editor,"ConfirmPieceEdit").PerformClick();edited=true;
            }catch(Exception e){error=e;editor.Close();}
        };
        Check(Find<Button>(cut,"EditCutPiece").Visible,"Botão EDITAR PEÇA visível acima da grade");
        timer.Start(); Find<Button>(cut,"EditCutPiece").PerformClick();if(error!=null)throw error;
        Check(edited && rows[1].Width=="60" && rows[1].WidthUnit=="cm" && rows[1].Height=="0,7" && rows[1].HeightUnit=="m","Editor confirma unidades individuais");
        Check(System.Text.Json.JsonSerializer.Serialize(rows[0])==untouched,"Outra peça intacta");
        grid.CurrentCell=grid.Rows[1].Cells["Width"];grid.BeginEdit(true);((TextBox)grid.EditingControl!).Text="65";grid.EndEdit();Pump();
        Check(rows[1].Width=="65" && cut.Recalculate(),"Edição inline existente e recálculo preservados");
        Check(cut.Result!.UsedArea==1.655m,"Área normalizada após edição");
        string beforeCancel=System.Text.Json.JsonSerializer.Serialize(rows);
        using var cancelTimer=new System.Windows.Forms.Timer{Interval=50};
        cancelTimer.Tick+=(_,_)=>{
            var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="GlassCutPieceEditor");if(editor==null)return;cancelTimer.Stop();
            Find<TextBox>(editor,"EditPieceDescription").Text="NÃO CONFIRMADO";Find<Button>(editor,"CancelPieceEdit").PerformClick();
        };
        cancelTimer.Start();Find<Button>(cut,"EditCutPiece").PerformClick();
        Check(System.Text.Json.JsonSerializer.Serialize(rows)==beforeCancel,"Cancelar não modifica nenhuma peça");
        Find<Button>(cut,"RemoveCutPiece").PerformClick();Check(rows.Count==1,"Remoção preservada");
        using(var image=GlassCutPlanPrint.RenderPage(new(new("TESTE",1200,1000,1),"TESTE",Array.Empty<GlassMaterialLink>()),cut.Recalculate()?cut.Result!:throw new Exception("Plano")))image.Save("glass-units-evidencias/plano-a4.png",ImageFormat.Png);
        cut.Close();
    }
}
