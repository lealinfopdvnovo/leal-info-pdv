using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;
static class Probe
{
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int L,T,R,B;}
 [DllImport("user32.dll")]static extern IntPtr GetTopWindow(IntPtr parent);
 [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr h,uint cmd);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr h,uint attribute,out int value,int size);
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static Assembly Host=>typeof(IPetExtension).Assembly;
 static object? Call(string type,string method,params object?[] args)=>Host.GetType("LealInfoPDV."+type,true)!.GetMethod(method,F)!.Invoke(null,args);
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in Walk(child))yield return x;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,System.Text.StringBuilder text,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,System.Text.StringBuilder text,int count);
 static string? firstForeign;static volatile bool guard,revealing;static IntPtr coverHandle;static int frames,desktopFrames,foreignFrames;
 static bool ForeignAboveLogin(Rectangle screen){
  var points=new[]{new Point(screen.Left+4,screen.Top+4),new Point(screen.Right-5,screen.Top+4),new Point(screen.Left+4,screen.Bottom-5),new Point(screen.Right-5,screen.Bottom-5),new Point(screen.Left+screen.Width/2,screen.Top+screen.Height/2)};
  foreach(var p in points){
   int count=0;for(var h=GetTopWindow(IntPtr.Zero);h!=IntPtr.Zero&&count++<400;h=GetWindow(h,2)){
    if(h==coverHandle||!IsWindowVisible(h))continue;
    if(DwmGetWindowAttribute(h,14,out int cloaked,4)==0&&cloaked!=0)continue;
    if(!GetWindowRect(h,out var r)||p.X<r.L||p.X>=r.R||p.Y<r.T||p.Y>=r.B)continue;
    GetWindowThreadProcessId(h,out var pid);if(pid!=(uint)Environment.ProcessId){var title=new System.Text.StringBuilder(256);var cls=new System.Text.StringBuilder(256);GetWindowText(h,title,256);GetClassName(h,cls,256);firstForeign??=$"pid={pid} hwnd={h} class={cls} title={title} point={p}";return true;}break;
   }
  }return false;
 }
 static void Sample(Rectangle screen,CancellationToken cancellation){
  using var image=new Bitmap(screen.Width,screen.Height);using var g=Graphics.FromImage(image);
  while(!cancellation.IsCancellationRequested){
   if(guard){
    try{
     g.CopyFromScreen(screen.Location,Point.Empty,screen.Size);int purple=0;
     for(int y=4;y<image.Height;y+=24)for(int x=4;x<image.Width;x+=24){var c=image.GetPixel(x,y);if(c.R>25&&c.B>25&&c.G<12&&Math.Abs(c.R-c.B)<8)purple++;}
     Interlocked.Increment(ref frames);if(purple>15&&Interlocked.Increment(ref desktopFrames)==1)image.Save(Path.Combine(Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!,"first-marker.png"));
     if(guard&&revealing&&ForeignAboveLogin(screen)&&guard&&Interlocked.Increment(ref foreignFrames)==1)image.Save(Path.Combine(Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!,"first-foreign.png"));
    }catch{}
   }
   Thread.Sleep(20);
  }
 }
 [STAThread]static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  try{
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Call("QaIsolation","Initialize");
   var output=Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!;
   if(args[0]=="offer"){
    var d=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("TRANSITION_FEED")!)).RootElement;
    var release=new LealInfo.Components.Release(LealInfo.Components.ComponentKind.PET,"1.8","005",d.GetProperty("Sha256").GetString()!,d.GetProperty("PackageUrl").GetString()!,"10.468","10.469",1);
    Check(LealInfo.Components.IndependentUpdater.Plan(new("10.468","1.7","005","10.468","10.469",1),null,release).Available.Count==1,"unchanged updater offers PET 1.8 to 005 / installed 1.7");
    var stage=LealInfo.Components.IndependentUpdater.StageAsync(release,"005",Path.Combine(Path.GetTempPath(),"transition-stage-"+Guid.NewGuid()),(_,_)=>Task.FromResult(File.ReadAllBytes(Environment.GetEnvironmentVariable("TRANSITION_PACKAGE")!))).GetAwaiter().GetResult();
    Check(stage.Hashes.Count==2,"transition package passes existing integrity and compatibility checks");return 0;
   }
   var serial=Database.DeviceSerial();using var rsa=RSA.Create(2048);
   var terms=new LicenseTerms(serial,2,"unico",null,"",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),"standard","005",true,0);
   var raw=JsonSerializer.SerializeToUtf8Bytes(terms);var signed=JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(raw),Convert.ToBase64String(rsa.SignData(raw,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));Call("Licensing.InstallationLicense","AcceptServer",signed,serial,rsa.ExportSubjectPublicKeyInfoPem());
   Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   var screen=Screen.PrimaryScreen!.Bounds;
   using var desktop=new Form{Name="DesktopSentinel",Text="QA DESKTOP SENTINEL",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=screen,BackColor=Color.Magenta,ShowInTaskbar=false};desktop.Show();desktop.Refresh();
   using var cancellation=new CancellationTokenSource();var sampler=Task.Run(()=>Sample(screen,cancellation.Token));
   bool hooked=false,blendSaved=false,loginValidated=false;LoginForm? login=null;DateTime? finishedAt=null;
   using var observer=new System.Windows.Forms.Timer{Interval=16};var deadline=DateTime.UtcNow.AddSeconds(60);
   observer.Tick+=(_,_)=>{
    var movie=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="AngraOpening");
    if(movie!=null&&!hooked){hooked=true;guard=true;}
    var cover=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="AngraOpening");
    if(cover!=null){coverHandle=cover.Handle;if(cover.Opacity<0.999)revealing=true;
     if(!blendSaved&&cover.Opacity>0.25&&cover.Opacity<0.75){using var image=new Bitmap(screen.Width,screen.Height);using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(screen.Location,Point.Empty,screen.Size);image.Save(Path.Combine(output,"direct-fade.png"));blendSaved=true;}
    }
    var path=Path.Combine(QaIsolation.LocalRoot,"transition-result.json");
    if(File.Exists(path)&&login!=null){
     using var doc=JsonDocument.Parse(File.ReadAllText(path));if(doc.RootElement.GetProperty("Pass").GetBoolean()){
      revealing=true;finishedAt??=DateTime.UtcNow;
      if((DateTime.UtcNow-finishedAt.Value).TotalMilliseconds>=250){
       Check(login.Visible&&login.Enabled&&login.Opacity>=0.999&&login.Bounds==screen,"real login opaque and covering the complete monitor at handoff");
       Check(Walk(login).Any(c=>c.Text=="ANGRA")&&Walk(login).Any(c=>c.Text=="VIDROS"),"approved ANGRA identity retained");
       Check(Walk(login).Any(c=>c.Text.StartsWith("LEAL INFO CONECTADO — ACESSO SEGURO",StringComparison.Ordinal)),"real runtime login preserves manufacturer secure-access footer");
       Check(!Walk(login).Any(c=>c is Label&&c.Text is "LEAL INFO" or "CONECTADO"),"no former manufacturer name in upper login title");
       Check(!Application.OpenForms.Cast<Form>().Any(f=>f.Name=="AngraOpening"),"visible opening retired after login readiness");
       Check(Application.OpenForms.Cast<Form>().Where(f=>f.Name=="AngraTransitionOwner").All(f=>f.Opacity==0&&!f.TopMost),"native modal owner retained fully transparent below login");
       using var image=new Bitmap(screen.Width,screen.Height);using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(screen.Location,Point.Empty,screen.Size);image.Save(Path.Combine(output,"real-login.png"));
       loginValidated=true;guard=false;observer.Stop();login.Close();
      }
     }
    }
    if(DateTime.UtcNow>deadline){guard=false;observer.Stop();foreach(var f in Application.OpenForms.Cast<Form>().Where(f=>f!=desktop).ToArray())f.Close();throw new Exception("Transition deadline exceeded");}
   };
   observer.Start();Call("PetRuntime","Initialize","005");
   // Reproduce the slow gap from the owner recording, including a temporarily busy UI thread.
   Thread.Sleep(600);var delay=Stopwatch.StartNew();while(delay.ElapsedMilliseconds<700){Application.DoEvents();Thread.Sleep(10);}
   using(login=new LoginForm()){login.ShowDialog();}
   guard=false;cancellation.Cancel();sampler.GetAwaiter().GetResult();observer.Stop();
   // Re-enter the actual login path without manually calling the PET customization method.
   using(var again=new LoginForm()){
    using var verifyAgain=new System.Windows.Forms.Timer{Interval=1200};
    bool seen=false;
    verifyAgain.Tick+=(_,_)=>{
     verifyAgain.Stop();
     Check(Walk(again).Any(c=>c.Text=="ANGRA")&&Walk(again).Any(c=>c.Text=="VIDROS"),"new login after session exit receives ANGRA identity automatically");
     Check(Walk(again).Any(c=>c.Text.StartsWith("LEAL INFO CONECTADO — ACESSO SEGURO",StringComparison.Ordinal)),"new login retains manufacturer footer automatically");
     using var image=new Bitmap(again.Width,again.Height);again.DrawToBitmap(image,again.ClientRectangle);image.Save(Path.Combine(output,"login-reentry.png"));seen=true;again.Close();
    };
    verifyAgain.Start();again.ShowDialog();Check(seen,"session reentry login tested without direct PET Apply calls");
   }

   Check(!Application.OpenForms.Cast<Form>().Any(f=>f.Name=="AngraTransitionOwner"),"retained modal owner disposed after login closes");
   Check(loginValidated&&blendSaved,"continuous fade to the genuine login completed");
   File.WriteAllText(Path.Combine(output,"coverage-counters.json"),JsonSerializer.Serialize(new{frames,desktopFrames,foreignFrames,firstForeign}));
   Check(frames>=20&&desktopFrames==0&&foreignFrames==0,"no synthetic desktop pixels or foreign desktop/taskbar windows exposed in sampled transition frames");
   using var transition=JsonDocument.Parse(File.ReadAllText(Path.Combine(QaIsolation.LocalRoot,"transition-result.json")));var tr=transition.RootElement;
   Check(tr.GetProperty("LoginPreparedBeforeCoverRetired").GetBoolean()&&tr.GetProperty("LoginPreparedBeforeMediaEnded").GetBoolean()&&tr.GetProperty("OpaqueLoginAtReveal").GetDouble()>=0.999,"login prepared before retiring visual coverage");
   using var media=JsonDocument.Parse(File.ReadAllText(Path.Combine(QaIsolation.LocalRoot,"opening-result.json")));var mr=media.RootElement;
   Check(mr.GetProperty("Played").GetBoolean()&&mr.GetProperty("Ended").GetBoolean()&&mr.GetProperty("LastSeconds").GetDouble()>=9&&mr.GetProperty("VideoHash").GetString()=="a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89","approved complete narration and animation remain byte-identical");
   File.Copy(Path.Combine(QaIsolation.LocalRoot,"transition-result.json"),Path.Combine(output,"transition-result.json"),true);File.Copy(Path.Combine(QaIsolation.LocalRoot,"opening-result.json"),Path.Combine(output,"opening-result.json"),true);
   File.WriteAllText(Path.Combine(output,"visual-coverage.json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.8",SampledFrames=frames,DesktopMarkerFrames=desktopFrames,ForeignWindowFramesDuringReveal=foreignFrames,SimulatedUiBusyMs=600,AdditionalLoginDelayMs=700,FullScreenLogin=true,RealLoginValidated=true,CustomerMachineTested=false}));return 0;
  }catch(Exception ex){
   Console.Error.WriteLine(ex);try{var target=Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!;Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"probe-error.txt"),ex.ToString());foreach(var n in new[]{"opening-result.json","transition-result.json"}){var p=Path.Combine(QaIsolation.LocalRoot,n);if(File.Exists(p))File.Copy(p,Path.Combine(target,n),true);}var s=Screen.PrimaryScreen!.Bounds;using var image=new Bitmap(s.Width,s.Height);using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(s.Location,Point.Empty,s.Size);image.Save(Path.Combine(target,"failed-frame.png"));}catch{}return 1;
  }
 }
}
