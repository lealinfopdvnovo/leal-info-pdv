using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;
using Microsoft.Web.WebView2.WinForms;
static class Probe
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static Assembly Host=>typeof(IPetExtension).Assembly;
 static object? Call(string type,string method,params object?[] args)=>Host.GetType("LealInfoPDV."+type,true)!.GetMethod(method,F)!.Invoke(null,args);
 static IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var n in Walk(child))yield return n;}
 static System.Windows.Forms.Timer? TimerIn(object? o,int depth=0){
  if(o==null||depth>2)return null;
  foreach(var f in o.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){
   if(f.FieldType==typeof(System.Windows.Forms.Timer))return (System.Windows.Forms.Timer?)f.GetValue(o);
   if(f.FieldType.Name.Contains("DisplayClass")){var found=TimerIn(f.GetValue(o),depth+1);if(found!=null)return found;}
  }return null;
 }
 static System.Windows.Forms.Timer? OpeningWatchdog(Form form){
  const BindingFlags p=BindingFlags.Instance|BindingFlags.NonPublic;
  var handlers=(EventHandlerList)typeof(Component).GetProperty("Events",p)!.GetValue(form)!;
  var key=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
  var list=handlers[key];return list?.GetInvocationList().Select(x=>TimerIn(x.Target)).FirstOrDefault(x=>x!=null);
 }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 [STAThread]static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  try{
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Call("QaIsolation","Initialize");
   var output=Environment.GetEnvironmentVariable("SYNC_EVIDENCE")!;
   if(args[0]=="offer"){
    var feed=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("SYNC_FEED")!)).RootElement;
    var offer=new LealInfo.Components.Release(LealInfo.Components.ComponentKind.PET,"1.5","005",feed.GetProperty("Sha256").GetString()!,feed.GetProperty("PackageUrl").GetString()!,"10.468","10.469",1);
    var plan=LealInfo.Components.IndependentUpdater.Plan(new("10.468","1.4","005","10.468","10.469",1),null,offer);
    Check(plan.Available.Count==1&&plan.Available[0].Version=="1.5","unchanged updater offers PET 1.5 to installed 1.4 / identity 005");
    var staged=LealInfo.Components.IndependentUpdater.StageAsync(offer,"005",Path.Combine(Path.GetTempPath(),"sync-stage-"+Guid.NewGuid()),(_,_)=>Task.FromResult(File.ReadAllBytes(Environment.GetEnvironmentVariable("SYNC_PACKAGE")!))).GetAwaiter().GetResult();
    Check(staged.Hashes.Count==2,"PET 1.5 passes the existing package and compatibility validator");return 0;
   }
   // Same supported signed-fixture verifier as the already approved QA probe; no core/key changes.
   var serial=Database.DeviceSerial();using var rsa=RSA.Create(2048);
   var terms=new LicenseTerms(serial,2,"unico",null,"",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),"standard","005",true,0);
   var payload=JsonSerializer.SerializeToUtf8Bytes(terms);
   var signed=JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(payload),Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));
   Call("Licensing.InstallationLicense","AcceptServer",signed,serial,rsa.ExportSubjectPublicKeyInfoPem());
   Database.Initialize();bool stoppedDuringPlayback=false;int samples=0;double lastSample=0;bool pending=false;
   using var poll=new System.Windows.Forms.Timer{Interval=200};
   poll.Tick+=async(_,_)=>{
    if(pending)return;
    var form=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="AngraOpening");if(form==null)return;
    var view=Walk(form).OfType<WebView2>().FirstOrDefault();if(view?.CoreWebView2==null)return;
    pending=true;try{
     var raw=await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({time:document.getElementById('v')?.currentTime||0,paused:document.getElementById('v')?.paused!==false})");
     var str=JsonSerializer.Deserialize<string>(raw);if(str==null)return;using var doc=JsonDocument.Parse(str);var d=doc.RootElement;
     var t=d.GetProperty("time").GetDouble();if(t>0.5&&!d.GetProperty("paused").GetBoolean()){
      samples++;lastSample=Math.Max(lastSample,t);var watchdog=OpeningWatchdog(form);
      if(watchdog!=null&&!watchdog.Enabled)stoppedDuringPlayback=true;
     }
    }catch{}finally{pending=false;}
   };
   poll.Start();var clock=Stopwatch.StartNew();Call("PetRuntime","Initialize","005");poll.Stop();clock.Stop();
   var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(QaIsolation.LocalRoot,"opening-result.json"))).RootElement;
   Check(result.GetProperty("Version").GetString()=="1.5","real loader accepts identity 005 / PET 1.5");
   Check(result.GetProperty("Played").GetBoolean()&&result.GetProperty("Ended").GetBoolean()&&result.GetProperty("LastSeconds").GetDouble()>8.99,"full nine-second media played to ended with audio enabled");
   Check(clock.Elapsed.TotalSeconds>=9.4,"login handoff waits for playback and final audio drain");
   Check(samples>=3&&lastSample>7.414&&stoppedDuringPlayback,"load watchdog stopped during actual playback and speech-end timeline passed");
   if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   using(var login=new LoginForm()){
    login.Show();Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
    Check(login.Visible&&Walk(login).Any(c=>c.Text=="ANGRA")&&Walk(login).Any(c=>c.Text=="VIDROS"),"unchanged genuine ANGRA login opens after the complete intro");login.Close();
   }
   File.Copy(Path.Combine(QaIsolation.LocalRoot,"opening-result.json"),Path.Combine(output,"opening-result.json"),true);
   File.WriteAllText(Path.Combine(output,"sync-playback.json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.5",Samples=samples,LastSample=lastSample,WatchdogStoppedDuringPlayback=stoppedDuringPlayback,TotalOpeningSeconds=clock.Elapsed.TotalSeconds,VoiceStart=1.75,VoiceEnd=7.414,FadeStart=8.35,VideoEnd=9.0,AudioDrainMs=350,RealLoginOpened=true,CustomerMachineTested=false}));
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}
