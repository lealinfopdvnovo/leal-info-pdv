using LealInfoPDV;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;
static class Program
{
 const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static int checks;
 static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
 static T Find<T>(Control f,string name)where T:Control=>All(f).OfType<T>().Single(x=>x.Name==name);
 static void Check(bool b,string name){if(!b)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
 static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
 static void Style(Form f)=>typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(RuntimeHelpers.GetUninitializedObject(typeof(MainForm)),new object[]{f});
 static void Mouse(GlassBox3DView v,string name,int x,int y,int delta=0)=>typeof(GlassBox3DView).GetMethod(name,I)!.Invoke(v,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,delta)});
 static GlassStoredProject Save(GlassWorkshopForm f)=>(GlassStoredProject)typeof(GlassWorkshopForm).GetMethod("SaveCurrentProject",I)!.Invoke(f,null)!;
 static void Load(GlassWorkshopForm f,string id)=>typeof(GlassWorkshopForm).GetMethod("LoadSavedProject",I)!.Invoke(f,new object[]{id});
 static void Capture(GlassWorkshopForm f,string name){using var b=new System.Drawing.Bitmap(f.Width,f.Height);f.DrawToBitmap(b,f.ClientRectangle);b.Save("glass-central-3d-evidencias/"+name+".png");}
 static void Modes(GlassWorkshopForm f,decimal width,decimal height)
 {
  var tabs=Find<TabControl>(f,"GlassSections");tabs.SelectedIndex=4;Pump();var mode3=Find<Button>(f,"Mode3D");var mode2=Find<Button>(f,"Mode2D");
  Check(mode3.Visible && mode3.Enabled && mode2.Visible,"Seletor ORIGINAL2D/3D visivel na Central");Check(mode3.FindForm()==f && f.RectangleToScreen(f.ClientRectangle).Contains(mode3.RectangleToScreen(mode3.ClientRectangle)),"Botao3D pertence a Central e fica contido na tela");
  mode3.PerformClick();Pump();var three=Find<GlassBox3DView>(f,"Drawing3D");var two=Find<GlassDrawingView>(f,"Drawing");Check(tabs.SelectedIndex==5 && three.Visible && !two.Visible && !three.IsUnavailable,"Botao3D abre renderizador original na aba existente");Check(three.Project==two.Project && three.Project!.WidthMm==width && three.Project.HeightMm==height,"2D/3D mesma fonte canonica e dimensoes reais");
  var parent=three.Parent!;var bar=mode3.Parent!;Check(!parent.Bounds.IntersectsWith(bar.Bounds),"Seletor nao encobre superficie3D");Check(f.RectangleToScreen(f.ClientRectangle).Contains(three.RectangleToScreen(three.ClientRectangle)),"Geometria contida na Central");
  string dimensions=(string)typeof(GlassBox3DView).GetField("dimensionsText",I)!.GetValue(three)!;Check(dimensions.Contains(width.ToString("0.##")) && dimensions.Contains(height.ToString("0.##")),"Medidas informadas pelo componente original");
  float yaw=three.YawDegrees;Mouse(three,"OnMouseDown",10,10);Mouse(three,"OnMouseMove",180,90);Mouse(three,"OnMouseUp",180,90);Check(three.YawDegrees!=yaw,"Rotacao por arraste original preservada");Mouse(three,"OnMouseWheel",0,0,120);Check(three.ZoomFactor>1,"Zoom original preservado");Find<Button>(f,"Reset3D").PerformClick();Check(three.YawDegrees==-25 && three.PitchDegrees==18 && three.ZoomFactor==1,"Reset original preservado");
  var measure=Find<Button>(f,"Toggle3DMeasurements");Check(measure.Visible,"Mostrar/ocultar medidas original disponivel");if(measure.Enabled){bool previous=three.ShowTechnicalDimensions;measure.PerformClick();Pump();Check(three.ShowTechnicalDimensions!=previous && three.TechnicalLayout!=null,"Cotas originais ligadas sem alterar geometria");measure.PerformClick();Check(three.ShowTechnicalDimensions==previous,"Cotas originais podem ser ocultadas");}
  Capture(f,"3d-"+f.Width+"-"+f.Height+"-"+width+"x"+height);mode2.PerformClick();Pump();Check(tabs.SelectedIndex==4 && two.Visible && !three.Visible && mode3.Visible,"Botao2D retorna a desenho original e mantem acesso3D");
 }
 [STAThread]static void Main(string[] args)
 {
  Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Windows descartavel");CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();Directory.CreateDirectory("glass-central-3d-evidencias");
  if(args.Contains("--baseline")){using var f=new GlassWorkshopForm(Style);f.Show();Pump();Check(!All(f).OfType<Button>().Any(x=>x.Name=="Mode3D"||x.Name=="Mode2D"),"BASE10.402 REPRODUZ perda do seletor: controles nao montados na Central");Find<TabControl>(f,"GlassSections").SelectedIndex=5;Pump();Check(Find<GlassBox3DView>(f,"Drawing3D").Visible,"BASE10.402 conserva motor3D e acesso automatico pela aba, sem seletor original");f.Close();return;}
  if(args.Length>0){using var reopened=new GlassWorkshopForm(Style);reopened.Show();Load(reopened,args[0]);Pump();Modes(reopened,1800,1500);Check(Find<Button>(reopened,"Mode3D").Visible,"Acesso3D persiste em novo processo/projeto reaberto");reopened.Close();return;}
  foreach(var screen in new[]{new System.Drawing.Size(1366,768),new System.Drawing.Size(1600,900),new System.Drawing.Size(1920,1080)})foreach(float factor in new[]{1f,1.25f,1.5f})
  {
   using var f=new GlassWorkshopForm(Style);f.Show();f.Scale(new System.Drawing.SizeF(factor,factor));f.Size=new(Math.Min(f.Width,screen.Width),Math.Min(f.Height,screen.Height-60));Pump();Modes(f,1200,1000);
   Find<Button>(f,"Mode3D").PerformClick();var three=Find<GlassBox3DView>(f,"Drawing3D");Find<NumericUpDown>(f,"WidthMm").Value=1800;Find<NumericUpDown>(f,"HeightMm").Value=1500;Pump();Check(three.Project!.WidthMm==1800 && three.Project.HeightMm==1500 && three.Visible,"Alteracao legitima atualiza3D ativo imediatamente");Modes(f,1800,1500);
   var saved=Save(f);Find<NumericUpDown>(f,"WidthMm").Value=900;Load(f,saved.Id);Modes(f,1800,1500);Check(Find<TabControl>(f,"GlassSections").TabPages.Count==7,"Central e sete abas homologadas preservadas");f.Close();
  }
  using var seed=new GlassWorkshopForm(Style);seed.Show();Find<NumericUpDown>(seed,"WidthMm").Value=1800;Find<NumericUpDown>(seed,"HeightMm").Value=1500;var project=Save(seed);seed.Close();var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};psi.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);psi.ArgumentList.Add(project.Id);using var child=Process.Start(psi)!;var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();Check(child.WaitForExit(60000),"Novo processo encerra");Console.Write(output.GetAwaiter().GetResult());Console.Write(error.GetAwaiter().GetResult());Check(child.ExitCode==0,"Central fechada/reaberta em NOVO PROCESSO preserva3D projeto/medidas");Console.WriteLine("PASS RESTAURACAO3D CENTRAL "+checks+" verificacoes Windows");
 }
}
