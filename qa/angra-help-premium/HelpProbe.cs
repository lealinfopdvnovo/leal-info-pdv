using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;
static class Probe {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr info);
 [DllImport("user32.dll")]static extern int GetGuiResources(IntPtr process,int kind);
 static Assembly Host=>typeof(MainForm).Assembly;
 static object? Call(string type,string method,params object?[] args)=>Host.GetType("LealInfoPDV."+type,true)!.GetMethod(method,F)!.Invoke(null,args);
 static void Require(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
 static Rectangle Rect(JsonElement e)=>new(e.GetProperty("X").GetInt32(),e.GetProperty("Y").GetInt32(),e.GetProperty("Width").GetInt32(),e.GetProperty("Height").GetInt32());
 static void Capture(string output,string name,Rectangle screen){using var image=new Bitmap(screen.Width,screen.Height);using(var g=Graphics.FromImage(image))g.CopyFromScreen(screen.Location,Point.Empty,screen.Size);image.Save(Path.Combine(output,name+".png"));}
 [STAThread]static int Main(string[] args){try{
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Call("QaIsolation","Initialize");
  if(args[0]=="offer"){
   var d=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("TRANSITION_FEED")!)).RootElement;
   var release=new LealInfo.Components.Release(LealInfo.Components.ComponentKind.PET,"1.10","005",d.GetProperty("Sha256").GetString()!,d.GetProperty("PackageUrl").GetString()!,"10.468","10.469",1);
   Check(LealInfo.Components.IndependentUpdater.Plan(new("10.468","1.9","005","10.468","10.469",1),null,release).Available.Count==1,"existing updater offers PET 1.10 only for QA005 / current 1.9");
   var stage=LealInfo.Components.IndependentUpdater.StageAsync(release,"005",Path.Combine(Path.GetTempPath(),"help-stage-"+Guid.NewGuid()),(_,_)=>Task.FromResult(File.ReadAllBytes(Environment.GetEnvironmentVariable("TRANSITION_PACKAGE")!))).GetAwaiter().GetResult();
   Check(stage.Hashes.Count==2,"existing integrity and PET compatibility validation passes");return 0;
  }
  var serial=Database.DeviceSerial();using var rsa=RSA.Create(2048);var terms=new LicenseTerms(serial,2,"unico",null,"",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),"standard","005",true,0);
  var raw=JsonSerializer.SerializeToUtf8Bytes(terms);var signed=JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(raw),Convert.ToBase64String(rsa.SignData(raw,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));Call("Licensing.InstallationLicense","AcceptServer",signed,serial,rsa.ExportSubjectPublicKeyInfoPem());
  Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");Check(Auth.Login("qa","qa-test-123!")!=null,"QA login remains functional");
  var pet=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,"Extensions/PET/Pet.dll"));var extension=(IPetExtension)Activator.CreateInstance(pet.GetExportedTypes().Single(t=>!t.IsAbstract&&typeof(IPetExtension).IsAssignableFrom(t)))!;
  TryLargerTestDesktop();
  var output=Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!;var screen=Screen.PrimaryScreen!.Bounds;var proofs=new List<JsonElement>();
  for(int scenario=0;scenario<3;scenario++){
   using var main=new MainForm();const BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic;
   ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",instance)!.GetValue(main)!).Stop();typeof(MainForm).GetField("automaticBackupCompleted",instance)!.SetValue(main,true);
   // Existing native UI fixture isolation: suppress unrelated onboarding, backup and update popups.
   var handlers=(EventHandlerList)typeof(Component).GetProperty("Events",instance)!.GetValue(main)!;var shownKey=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;handlers.RemoveHandler(shownKey,handlers[shownKey]);
   main.WindowState=FormWindowState.Normal;main.MaximumSize=new Size(4000,2400);main.Bounds=new Rectangle(screen.Width>=1366?12:-scenario*145,28+scenario*5,Math.Max(980,Math.Min(1400,screen.Width-40)),Math.Min(820,screen.Height-70));
   var menu=Walk(main).OfType<MenuStrip>().First();float scale=1+scenario*.25f;menu.Font=new Font(menu.Font.FontFamily,menu.Font.Size*scale,menu.Font.Style);
   extension.Customize(main);extension.Customize(main);extension.Customize(main);
   var coach=AssemblyLoadContext.Default.Assemblies.Single(a=>a.GetName().Name=="AngraHelpPremium").GetType("AngraHelpPremium.Coach",true)!;
   string Evidence()=>(string)coach.GetMethod("Evidence")!.Invoke(null,new object[]{main})!;
   var help=(ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(i=>i.Text.Replace("&","").Equals("Ajuda",StringComparison.OrdinalIgnoreCase));int clicks=0;help.DropDownOpened+=(_,_)=>clicks++;
   bool moved=false,clicked=false,done=false;int focusSamples=0;bool[] captures=new bool[4];Exception? failure=null;var timeout=Stopwatch.StartNew();int handlesBefore=GetGuiResources(Process.GetCurrentProcess().Handle,0);
   using var monitor=new System.Windows.Forms.Timer{Interval=30};DateTime? finished=null;
   monitor.Tick+=(_,_)=>{
    try{
     if(timeout.Elapsed.TotalSeconds>12)throw new Exception("Help animation did not complete");
     using var doc=JsonDocument.Parse(Evidence());var d=doc.RootElement;if(!d.TryGetProperty("Started",out var started)||!started.GetBoolean()){handlesBefore=GetGuiResources(Process.GetCurrentProcess().Handle,0);return;}
     double seconds=d.GetProperty("Elapsed").GetDouble();bool ended=d.GetProperty("Finished").GetBoolean();
     if(!ended){
      var overlay=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="AngraHelpCoach");long style=GetWindowLongPtr(overlay.Handle,-20).ToInt64();
      Require((style&0x080800A0)==0x080800A0,"native overlay remains layered, click-through, nonactivating and absent from taskbar");
      if(!clicked){Require(GetForegroundWindow()==main.Handle,"animation retains main-window keyboard focus");focusSamples++;}
      if(seconds>.35&&!moved){main.Location=new Point(main.Left+9,main.Top+7);main.Width=main.Width-18;moved=true;return;}
      if(seconds>.55){var expected=menu.RectangleToScreen(help.Bounds);Require(Rect(d.GetProperty("Target"))==expected,"animation follows the live AJUDA menu after move/resize/font scaling");}
      double[] at={.45,1.05,1.75,2.75};string[] names={"circle","arrow","bubble","fade"};
      for(int i=0;i<at.Length;i++)if(seconds>=at[i]&&!captures[i]){Capture(output,$"help-{scenario}-{names[i]}",screen);captures[i]=true;}
      if(seconds>1.9&&!clicked){var target=menu.RectangleToScreen(help.Bounds);SetCursorPos(target.Left+target.Width/2,target.Top+target.Height/2);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);clicked=true;return;}
      if(clicked&&seconds>2.08&&help.DropDown.Visible)help.HideDropDown();
     }else{
      finished??=DateTime.UtcNow;
      if((DateTime.UtcNow-finished.Value).TotalMilliseconds<450)return;
      monitor.Stop();
      Check(d.GetProperty("Error").ValueKind==JsonValueKind.Null,"animation completed without visual-helper errors");
      Check(d.GetProperty("Plays").GetInt32()==1&&d.GetProperty("Frames").GetInt32()>=30,"exactly one animation per MainForm login session");
      Check(d.GetProperty("ClosedAt").GetDouble()>=2.95&&d.GetProperty("ClosedAt").GetDouble()<3.35,"three-second duration and cleanup");
      Check(d.GetProperty("CircleCompletedAt").GetDouble()>=.8&&d.GetProperty("CircleCompletedAt").GetDouble()<1.05&&d.GetProperty("ArrowStartedAt").GetDouble()<1.05,"red drawing completes and blue arrow begins at 0.8 seconds");
      Check(d.GetProperty("BubbleStartedAt").GetDouble()>=1.3&&d.GetProperty("BubbleStartedAt").GetDouble()<1.55&&d.GetProperty("FadeStartedAt").GetDouble()>=2.5,"bubble and fade follow 1.3 and 2.5 second phases");
      Check(captures.All(v=>v)&&focusSamples>5,"all four animation phases captured on Windows");
      Check(clicks>0,"real mouse click reaches the underlying AJUDA menu during animation");
      Check(!Application.OpenForms.Cast<Form>().Any(f=>f.Name=="AngraHelpCoach"),"overlay removed after animation");
      var layout=Walk(main).Select(c=>(c,c.Bounds,c.Font,c.Visible,c.Enabled)).ToArray();extension.Customize(main);extension.Customize(main);
      Check(layout.All(x=>x.c.Bounds==x.Bounds&&x.c.Font==x.Font&&x.c.Visible==x.Visible&&x.c.Enabled==x.Enabled),"all buttons and controls retain geometry, visibility, font and enabled state");
      using var again=JsonDocument.Parse(Evidence());Check(again.RootElement.GetProperty("Plays").GetInt32()==1&&again.RootElement.GetProperty("Finished").GetBoolean(),"re-customization cannot repeat animation in the same session");
      int handlesAfter=GetGuiResources(Process.GetCurrentProcess().Handle,0);
      File.WriteAllText(Path.Combine(output,$"gdi-{scenario}.json"),JsonSerializer.Serialize(new{Before=handlesBefore,After=handlesAfter,Delta=handlesAfter-handlesBefore,MainUiPaintedBeforeBaseline=true}));
      Check(handlesAfter-handlesBefore<20,"temporary GDI resources released");
      proofs.Add(d.Clone());File.WriteAllText(Path.Combine(output,$"help-session-{scenario}.json"),d.GetRawText());done=true;main.Close();
     }
    }catch(Exception ex){failure=ex;monitor.Stop();main.Close();}
   };
   monitor.Start();main.Show();main.Activate();Application.Run(new TestContext(main));if(failure!=null)throw failure;Check(done,"main login session "+scenario+" completed at menu font scale "+scale);
  }
  File.WriteAllText(Path.Combine(output,"help-proof.json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.10",Sessions=proofs,NativeWindowsForms=true,MenuFontScales=new[]{100,125,150},DesktopBounds=Screen.PrimaryScreen!.Bounds,WindowsDpiSettingChanged=false,RealMouseClickThrough=true,EachNewLoginPlayedOnce=true,CustomerMachineTested=false}));return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct DisplayMode {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;
  public ushort Spec,Driver,Size,Extra;public uint Fields;
  public int X,Y;public uint Orientation,FixedOutput;
  public short Color,Duplex,YResolution,TTOption,Collate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Form;
  public ushort LogPixels;public uint Bits,Width,Height,Flags,Frequency,ICMMethod,ICMIntent,MediaType,Dither,Reserved1,Reserved2,PanningWidth,PanningHeight;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplaySettings(string? device,int mode,ref DisplayMode settings);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int ChangeDisplaySettings(ref DisplayMode settings,uint flags);
 static void TryLargerTestDesktop(){
  var mode=new DisplayMode{Size=(ushort)Marshal.SizeOf<DisplayMode>()};DisplayMode? selected=null;
  for(int i=0;i<300&&EnumDisplaySettings(null,i,ref mode);i++)if(mode.Width>=1366&&mode.Width<=1920&&mode.Height>=768&&mode.Height<=1080&&(!selected.HasValue||mode.Width>selected.Value.Width))selected=mode;
  if(selected.HasValue){var candidate=selected.Value;int result=ChangeDisplaySettings(ref candidate,0);Console.WriteLine($"Test desktop mode {candidate.Width}x{candidate.Height}: {result}");Application.DoEvents();}
 }
 sealed class TestContext:ApplicationContext{public TestContext(Form form):base(form){}}
}
