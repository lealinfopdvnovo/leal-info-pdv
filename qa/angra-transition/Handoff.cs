using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace AngraTransition;
public static class Handoff
{
 const string MediaHash="a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89";
 [DllImport("dwmapi.dll")]static extern int DwmFlush();
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 sealed class OpeningSplash:Form{
  public OpeningSplash(Rectangle bounds){Name="AngraOpening";Text="ANGRA VIDROS";FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;Bounds=bounds;BackColor=Color.Black;ShowInTaskbar=false;TopMost=true;Opacity=1;}
  protected override bool ShowWithoutActivation=>true;
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
 }
 static readonly System.Windows.Forms.Timer Poll=new(){Interval=16};
 static readonly System.Windows.Forms.Timer LoadWatchdog=new(){Interval=45000};
 static readonly Stopwatch Clock=new();
 static OpeningSplash? opening;static WebView2? view;static LoginForm? login;
 static Rectangle bounds;static bool installed,finished,preparing,played,ended,originalEnabled;
 static double coveredAt=-1,loginAt=-1,readyAt=-1,fadeAt=-1,completedAt=-1,endedAt=-1,last;
 static int readyFrames,fadeFrames;static double minimumOpacity=1;static string? error;static string version="1.6";
 public static void Start(Assembly pet,string approvedPlayerHtml){
  if(installed)return;
  var type=typeof(LoginForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
  var current=type.GetProperty("Current",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null);
  var id=(string?)current?.GetType().GetProperty("ClientCode")?.GetValue(current);
  if(!QaIsolation.Isolated||id!="005")throw new InvalidOperationException("Transição exclusiva ANGRA QA 005");
  version=pet.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="PetVersion").Value!;
  var root=Path.Combine(QaIsolation.LocalRoot,"Opening",MediaHash);Directory.CreateDirectory(root);var file=Path.Combine(root,"abertura.mp4");
  using(var source=pet.GetManifestResourceStream("ANGRA.Opening.mp4")??throw new InvalidDataException("Vídeo aprovado ausente")){
   using var memory=new MemoryStream();source.CopyTo(memory);var bytes=memory.ToArray();
   if(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=MediaHash)throw new InvalidDataException("Vídeo aprovado divergente");
   if(!File.Exists(file)||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant()!=MediaHash)File.WriteAllBytes(file,bytes);
  }
  installed=true;Clock.Start();bounds=Screen.FromPoint(Cursor.Position).Bounds;
  opening=new OpeningSplash(bounds);view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.Black};opening.Controls.Add(view);
  opening.Shown+=async(_,_)=>{
   LoadWatchdog.Start();try{
    var options=new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
    var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(QaIsolation.LocalRoot,"WebView2-Angra"),options);
    await view.EnsureCoreWebView2Async(environment);
    view.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.IsStatusBarEnabled=false;
    view.CoreWebView2.SetVirtualHostNameToFolderMapping("angra-opening.local",root,CoreWebView2HostResourceAccessKind.DenyCors);
    view.CoreWebView2.WebResourceRequested+=(_,e)=>{if(!e.Request.Uri.StartsWith("https://angra-opening.local/",StringComparison.Ordinal))e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),403,"Offline","");};
    view.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
    view.CoreWebView2.WebMessageReceived+=(_,e)=>{
     using var message=JsonDocument.Parse(e.WebMessageAsJson);var d=message.RootElement;var kind=d.GetProperty("type").GetString();
     if(kind=="playing"){played=d.GetProperty("audible").GetBoolean();LoadWatchdog.Stop();}
     if(kind=="time")last=d.GetProperty("seconds").GetDouble();
     if(kind=="ended"){ended=true;endedAt=Clock.Elapsed.TotalSeconds;}
     if(kind=="error"){error="Falha de reprodução";ended=true;endedAt=Clock.Elapsed.TotalSeconds;LoadWatchdog.Stop();}
    };
    // Exact player from approved PET 1.5: no audio/video/timing changes.
    view.NavigateToString(approvedPlayerHtml);
   }catch(Exception ex){error=ex.GetType().Name;ended=true;endedAt=Clock.Elapsed.TotalSeconds;LoadWatchdog.Stop();}
  };
  LoadWatchdog.Tick+=(_,_)=>{error="Tempo limite de carregamento";ended=true;endedAt=Clock.Elapsed.TotalSeconds;LoadWatchdog.Stop();};
  Poll.Tick+=Observe;Poll.Start();opening.Show();opening.Refresh();opening.Update();SetWindowPos(opening.Handle,new IntPtr(-1),0,0,0,0,0x0013);DwmFlush();coveredAt=Clock.Elapsed.TotalSeconds;
  // Return with the SAME opening window still covering the monitor, so Program creates its genuine login behind it.
 }
 static void Observe(object? sender,EventArgs args){
  if(finished||preparing||opening==null)return;
  if(login==null){
   login=Application.OpenForms.OfType<LoginForm>().FirstOrDefault(f=>f.Visible&&!f.IsDisposed);
   if(login==null)return;
   loginAt=Clock.Elapsed.TotalSeconds;originalEnabled=login.Enabled;login.Enabled=false;
   if(login.Owner==opening)login.Owner=null;
   login.StartPosition=FormStartPosition.Manual;login.WindowState=FormWindowState.Normal;login.Bounds=bounds;
   login.FormClosed+=(_,_)=>Finish(false);
  }
  if(login.IsDisposed||!login.Visible){Finish(false);return;}
  if(login.Bounds!=bounds)login.Bounds=bounds;
  if(login.Opacity<0.999||!login.Text.Contains("ANGRA VIDROS",StringComparison.Ordinal)||!login.IsHandleCreated){readyFrames=0;fadeAt=-1;opening.Opacity=1;return;}
  readyFrames++;
  if(readyFrames<6)return;
  if(readyAt<0){login.PerformLayout();login.Refresh();login.Update();DwmFlush();readyAt=Clock.Elapsed.TotalSeconds;}
  // Narration has finished at 7.414 s; align the direct crossfade with the approved video fade at 8.35 s.
  if(last<8.35&&!ended)return;
  if(fadeAt<0){
   preparing=true;try{
    login.Enabled=originalEnabled;login.Refresh();login.Update();login.Activate();SetWindowPos(login.Handle,IntPtr.Zero,0,0,0,0,0x0003);
    SetWindowPos(opening.Handle,new IntPtr(-1),0,0,0,0,0x0013);DwmFlush();fadeAt=Clock.Elapsed.TotalSeconds;
   }finally{preparing=false;}
  }
  if(login.Opacity<0.999){opening.Opacity=1;fadeAt=-1;readyFrames=0;return;}
  minimumOpacity=Math.Min(minimumOpacity,login.Opacity);fadeFrames++;
  double t=Math.Clamp((Clock.Elapsed.TotalSeconds-fadeAt)/0.65,0,1);double smooth=t*t*(3-2*t);opening.Opacity=1-smooth;
  // Keep the player alive through its complete ended event and the original 350 ms audio drain.
  if(t>=1&&ended){login.Refresh();login.Update();DwmFlush();Finish(true);}
 }
 static void Finish(bool success){
  if(finished)return;finished=true;completedAt=Clock.Elapsed.TotalSeconds;Poll.Stop();LoadWatchdog.Stop();Poll.Tick-=Observe;
  var actualLogin=login;if(actualLogin!=null&&!actualLogin.IsDisposed)actualLogin.Enabled=originalEnabled;
  try{view?.CoreWebView2?.Stop();}catch{}
  view?.Dispose();if(opening!=null&&!opening.IsDisposed){opening.Close();opening.Dispose();}
  if(success&&actualLogin!=null&&!actualLogin.IsDisposed)actualLogin.Activate();
  File.WriteAllText(Path.Combine(QaIsolation.LocalRoot,"opening-result.json"),JsonSerializer.Serialize(new{Identity="005",Version=version,Played=played,Ended=ended,LastSeconds=last,Offline=true,VideoHash=MediaHash,Error=error}));
  File.WriteAllText(Path.Combine(QaIsolation.LocalRoot,"transition-result.json"),JsonSerializer.Serialize(new{Identity="005",Version=version,Pass=success&&error==null,CoverStarted=coveredAt,LoginObserved=loginAt,LoginReady=readyAt,MediaEnded=endedAt,FadeStarted=fadeAt,CoverRetired=completedAt,OpaqueLoginAtReveal=minimumOpacity,FadeFrames=fadeFrames,FadeSeconds=0.65,FullMonitorBounds=new{bounds.X,bounds.Y,bounds.Width,bounds.Height},LoginPreparedBeforeCoverRetired=success&&readyAt>=0&&completedAt>readyAt,LoginPreparedBeforeMediaEnded=success&&readyAt>=0&&endedAt>readyAt,MediaChanged=false,Error=error}));
  login=null;opening=null;view=null;Poll.Dispose();LoadWatchdog.Dispose();
 }
}
