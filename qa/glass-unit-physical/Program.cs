using LealInfoPDV;
using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
static class Program
{
    const BindingFlags I=BindingFlags.NonPublic|BindingFlags.Instance;
    static int count;
    static void Check(bool b,string text){if(!b)throw new Exception("FAIL "+text);count++;Console.WriteLine("PASS "+text);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name)where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
    static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
    static void Style(Form f){typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(RuntimeHelpers.GetUninitializedObject(typeof(MainForm)),new object[]{f});typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{f,"Futurista Azul",null});}
    static void Capture(Form f,string name){using var b=new Bitmap(f.Width,f.Height);f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save("glass-unit-physical-evidencias/"+name+".png");}
    static void Edit(GlassCutPlanForm cut,Action<Form> action)
    {
        Exception? error=null;bool opened=false;using var timer=new System.Windows.Forms.Timer{Interval=60};var watch=Stopwatch.StartNew();
        timer.Tick+=(_,_)=>{if(watch.ElapsedMilliseconds>15000)throw new Exception("Timeout editor");var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="GlassCutPieceEditor");if(editor==null)return;timer.Stop();opened=true;try{action(editor);}catch(Exception e){error=e;editor.Close();}};
        timer.Start();Find<Button>(cut,"EditCutPiece").PerformClick();Check(opened,"Editor real abriu");if(error!=null)throw error;
    }
    static void Fits(ComboBox combo,string label)
    {int required=TextRenderer.MeasureText("mm",combo.Font).Width+SystemInformation.VerticalScrollBarWidth+4;Check(combo.Width>=required,label+" mm/cm/m inteiros ("+combo.Width+">="+required+")");}
    static GlassStoredProject Save(GlassWorkshopForm f)=>(GlassStoredProject)f.GetType().GetMethod("SaveCurrentProject",I)!.Invoke(f,null)!;
    static void Load(GlassWorkshopForm f,string id)=>f.GetType().GetMethod("LoadSavedProject",I)!.Invoke(f,new object[]{id});
    [STAThread]static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Somente Windows descartavel");CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();Directory.CreateDirectory("glass-unit-physical-evidencias");
        if(args.Length>0 && args[0]=="--reopen")
        {using var reopened=new GlassWorkshopForm(Style);reopened.Show();Load(reopened,args[1]);var data=GlassProjectStore.Load(args[1]).Data;Check(data.Cut!.WidthMm==3000 && data.Cut.HeightMm==2000,"Chapa fisica apos processo independente");Check(data.Cut.Pieces[0].WidthMm==1200 && data.Cut.Pieces[0].HeightMm==1000 && data.Cut.Pieces[0].WidthUnit=="m" && data.Cut.Pieces[0].HeightUnit=="m","Unidades e dimensoes canonicas apos processo independente");var restored=(GlassCutPlanForm)typeof(GlassWorkshopForm).GetField("cutPlan",I)!.GetValue(reopened)!;Check(restored.Result!.UsedArea==1.2m && restored.Result.RejectedQuantity==0,"Packing apos novo processo");reopened.Close();return;}
        using var central=new GlassWorkshopForm(Style);central.Show();Pump();
        using var cut=new GlassCutPlanForm(()=>new(new GlassProject("CENARIO FISICO",1200,1000,1),"QA",Array.Empty<GlassMaterialLink>()),Style);cut.Show(central);Pump();
        var grid=Find<DataGridView>(cut,"CutPieces");var rows=(BindingList<GlassCutPlanForm.PieceRow>)grid.DataSource!;
        if(args.Contains("--baseline"))
        {
            Edit(cut,e=>{
                var unit=Find<ComboBox>(e,"EditPieceWidthUnit");var entry=Find<NumericUpDown>(e,"EditPieceWidthEntry");var canonical=Find<NumericUpDown>(e,"EditPieceWidth");
                Check(unit.Text=="mm" && entry.Value==1200 && canonical.Value==1200 && !canonical.Visible,"Baseline real: 1200 e cinco casas pertencem a mm, nao metros");
                int required=TextRenderer.MeasureText("mm",unit.Font).Width+SystemInformation.VerticalScrollBarWidth+4;
                Console.WriteLine("ROOT seletor baseline largura="+unit.Width+" necessario="+required+" unidade real="+unit.Text);Check(unit.Width<required,"ROOT mm visualmente truncado pelo seletor estreito");Capture(e,"baseline-1200-mm-truncado");
                entry.Text="1250";unit.SelectedItem="m";
                Console.WriteLine("ROOT troca com texto pendente: canonico="+canonical.Value+" exibido="+entry.Value+" unidade="+unit.Text);
                Capture(e,"baseline-texto-pendente");Find<Button>(e,"CancelPieceEdit").PerformClick();
            });
            Console.WriteLine("PASS REPRODUCAO PACOTE REAL 10.394");return;
        }
        Find<NumericUpDown>(cut,"SheetWidth").Value=3000;Find<NumericUpDown>(cut,"SheetHeight").Value=2000;Find<ComboBox>(cut,"SheetWidthUnit").SelectedItem="m";Find<ComboBox>(cut,"SheetHeightUnit").SelectedItem="m";
        Check(Find<NumericUpDown>(cut,"SheetWidthEntry").Value==3 && Find<NumericUpDown>(cut,"SheetHeightEntry").Value==2,"Chapa 3,00 m x 2,00 m");
        Edit(cut,e=>{
            var w=Find<NumericUpDown>(e,"EditPieceWidthEntry");var h=Find<NumericUpDown>(e,"EditPieceHeightEntry");var wu=Find<ComboBox>(e,"EditPieceWidthUnit");var hu=Find<ComboBox>(e,"EditPieceHeightUnit");
            foreach(var size in new[]{new Size(510,660),new Size(450,580),new Size(360,450)}){e.Size=size;Pump();Fits(wu,"Largura "+size);Fits(hu,"Altura "+size);Capture(e,"seletor-"+size.Width);}
            e.Size=new(510,660);
            for(int cycle=0;cycle<100;cycle++)foreach(var u in new[]{"mm","cm","m","mm","m","cm","mm","m"})
            {wu.SelectedItem=u;Check(w.Value==GlassDimensionUnits.Convert(1200,"mm",u) && Find<NumericUpDown>(e,"EditPieceWidth").Value==1200,"Largura exata ciclo "+cycle+" "+u);Check(Find<NumericUpDown>(e,"EditPieceHeight").Value==1000,"Altura independente");}
            hu.SelectedItem="m";wu.SelectedItem="mm";Check(w.Value==1200 && h.Value==1,"1200 mm x 1 m");wu.SelectedItem="m";Check(w.Value==1.2m && h.Value==1,"1,2 m x 1 m");
            // Texto digitado, sem ler Value antes da troca (a leitura validaria e esconderia o defeito).
            wu.SelectedItem="mm";w.Text="1250";wu.SelectedItem="m";Check(w.Value==1.25m && Find<NumericUpDown>(e,"EditPieceWidth").Value==1250,"Texto pendente mm convertido na unidade anterior");
            w.Text="1,23456789";wu.SelectedItem="cm";Check(w.Value==123.456789m && Find<NumericUpDown>(e,"EditPieceWidth").Value==1234.56789m,"Texto pendente m com precisao decimal preservada");
            for(int n=0;n<50;n++){wu.SelectedItem="mm";wu.SelectedItem="m";}Check(Find<NumericUpDown>(e,"EditPieceWidth").Value==1234.56789m,"Precisao sem erro acumulado");
            w.Value=1.2m;hu.SelectedItem="mm";hu.SelectedItem="cm";hu.SelectedItem="m";Check(h.Value==1,"Altura round trip independente");
            Capture(e,"corrigido-1_2m-1m");Find<Button>(e,"ConfirmPieceEdit").PerformClick();
        });
        Check(rows[0].Width=="1,2" && rows[0].Height=="1" && rows[0].WidthUnit=="m" && rows[0].HeightUnit=="m","Confirmar editor preserva par numero/unidade");
        Check(cut.Recalculate() && cut.Result!.Sheets.Count==1 && cut.Result.RejectedQuantity==0 && cut.Result.UsedArea==1.2m && cut.Result.TotalArea==6m && cut.Result.Utilization==20m && cut.Result.UnusedArea==4.8m,"CENARIO OBRIGATORIO: 1 chapa, peca 1200x1000mm, area 1,2m2, uso20%, sobra4,8m2");
        for(int cycle=0;cycle<50;cycle++)foreach(var u in new[]{"mm","cm","m"}){rows[0].WidthUnit=u;rows[0].HeightUnit=u;Check(GlassDimensionUnits.ToMillimetres(decimal.Parse(rows[0].Width),u)==1200 && GlassDimensionUnits.ToMillimetres(decimal.Parse(rows[0].Height),u)==1000,"Grade normaliza sem erro");}
        Find<Button>(cut,"AddCutPiece").PerformClick();Check(rows.Count==2 && rows[1].WidthUnit=="mm" && rows[1].HeightUnit=="mm","Adicionar peca parte de unidade definida");rows[1].Width="500";rows[1].Height="600";rows[1].WidthUnit="cm";rows[1].HeightUnit="m";Check(rows[1].Width=="50" && rows[1].Height=="0,6","Outra peca com cm/m");
        Find<Button>(cut,"AddCutReference").PerformClick();Check(rows[2].Width=="1200" && rows[2].Height=="1000" && rows[2].WidthUnit=="mm" && rows[2].HeightUnit=="mm","Copiar medidas do projeto usa mm canonicos");
        Find<Button>(cut,"CutAllowRotation").PerformClick();Check(cut.Recalculate() && cut.Result!.RotationAllowed && cut.Result.UsedArea==2.7m,"Multiplas pecas e rotacao mantidas");
        var rotated=GlassCutPacking.Calculate(1000,1200,new[]{new GlassCutPiece("ROT","",1200,1000,1,"",true)},true);Check(rotated.Sheets.Single().Pieces.Single().Rotated,"Rotacao 90 graus real");
        using(var page=GlassCutPlanPrint.RenderPage(new(new("QA",1200,1000,1),"QA",Array.Empty<GlassMaterialLink>()),cut.Result!)) {Check(page.Width==794 && page.Height==1123,"Previa Plano de Corte A4");page.Save("glass-unit-physical-evidencias/plano-a4.png");}
        using(var timer=new System.Windows.Forms.Timer{Interval=60}){bool preview=false;timer.Tick+=(_,_)=>{var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text.StartsWith("PRÉVIA A4"));if(f==null)return;preview=true;timer.Stop();f.Close();};timer.Start();Find<Button>(cut,"CutPreview").PerformClick();Check(preview,"Botao de previa A4 real abre");}
        cut.Close();
        typeof(GlassWorkshopForm).GetMethod("EnsureCutPlan",I)!.Invoke(central,null);var projectCut=(GlassCutPlanForm)typeof(GlassWorkshopForm).GetField("cutPlan",I)!.GetValue(central)!;projectCut.Show(central);Pump();
        Find<NumericUpDown>(projectCut,"SheetWidth").Value=3000;Find<NumericUpDown>(projectCut,"SheetHeight").Value=2000;Find<ComboBox>(projectCut,"SheetWidthUnit").SelectedItem="m";Find<ComboBox>(projectCut,"SheetHeightUnit").SelectedItem="m";
        var persistedRows=(BindingList<GlassCutPlanForm.PieceRow>)Find<DataGridView>(projectCut,"CutPieces").DataSource!;persistedRows[0].WidthUnit="m";persistedRows[0].HeightUnit="m";Check(projectCut.Recalculate(),"Packing integrado para salvar");
        var saved=Save(central);Check(saved.Data.Cut!.Pieces[0].WidthMm==1200 && saved.Data.Cut.Pieces[0].HeightMm==1000,"Persistencia existente recebe milimetros");central.Close();
        using(var reopened=new GlassWorkshopForm(Style)){reopened.Show();Load(reopened,saved.Id);Check(JsonSerializer.Serialize(GlassProjectStore.Load(saved.Id))==JsonSerializer.Serialize(saved),"Reabrir nao altera registro ou revisao");reopened.Close();}
        var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};psi.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);psi.ArgumentList.Add("--reopen");psi.ArgumentList.Add(saved.Id);using(var child=Process.Start(psi)!){var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();Check(child.WaitForExit(60000),"Novo processo terminou");Console.Write(stdout.GetAwaiter().GetResult());Console.Write(stderr.GetAwaiter().GetResult());Check(child.ExitCode==0,"Persistencia preservada entre processos");}
        Console.WriteLine("PASS UNIDADES FISICAS: "+count+" verificacoes Windows");
    }
}
