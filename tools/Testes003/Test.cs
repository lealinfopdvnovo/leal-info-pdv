using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net.Http;
using System.Web.Script.Serialization;
class Test {
 public static int Run(){try{
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated runner only");
  var root=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP"),"tests003-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory("evidence");
  var zip=Path.Combine(root,"baseline.zip");using(var http=new HttpClient())File.WriteAllBytes(zip,http.GetByteArrayAsync(Core.Url).GetAwaiter().GetResult());var hashes=Core.Validate(zip);
  var app=Path.Combine(root,"App003");Directory.CreateDirectory(app);ZipFile.ExtractToDirectory(zip,app);
  foreach(var n in new[]{"customer.db","license.dat","settings.json"})File.WriteAllText(Path.Combine(app,n),"PRESERVED");
  var prepared=Core.Prepare(app).GetAwaiter().GetResult();if(Core.Ready(app)!=prepared)throw new Exception("Readiness mismatch");
  foreach(var n in Core.Names)if(Core.HashFile(Path.Combine(app,n))!=hashes[n])throw new Exception("Preparation altered PDV");
  var worker=Path.Combine(root,"worker.ps1");File.WriteAllBytes(worker,Core.Worker());
  var ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
  foreach(var mode in new[]{"Prepare","Apply"}){
   var args="-NoProfile -ExecutionPolicy Bypass -File \""+worker+"\" -Mode "+mode+" -App \""+app+"\" -Channel 003 -Version 10.457 -Package \""+zip+"\" -Sha256 "+Core.PackageHash;
   using(var p=Process.Start(new ProcessStartInfo(ps,args){UseShellExecute=false})){if(!p.WaitForExit(60000)){p.Kill();throw new Exception("Worker timed out");}if(p.ExitCode!=0)throw new Exception("Worker failed "+mode);}
  }
  foreach(var n in Core.Names)if(Core.HashFile(Path.Combine(app,n))!=hashes[n]||Core.HashFile(Path.Combine(app,".pdv-update","previous",n))!=hashes[n])throw new Exception("Restore or previous bytes differ");
  foreach(var n in new[]{"customer.db","license.dat","settings.json"})if(File.ReadAllText(Path.Combine(app,n))!="PRESERVED")throw new Exception("Customer files changed");
  using(var f=new Window()){
   f.SetVerificationPath(app);f.Show();Application.DoEvents();
   f.ClickVerify();
   if(!f.VerificationBusy || !f.VerificationStatus.StartsWith("VERIFICANDO"))throw new Exception("Click did not show immediate feedback");
   int ticks=0;using(var timer=new System.Windows.Forms.Timer()){timer.Interval=1;timer.Tick+=delegate{ticks++;};timer.Start();
    var deadline=DateTime.UtcNow.AddSeconds(60);
    while(f.VerificationBusy && DateTime.UtcNow<deadline){Application.DoEvents();System.Threading.Thread.Sleep(5);}
   }
   if(f.VerificationBusy || !f.VerificationStatus.StartsWith("CÓPIA ÍNTEGRA") || ticks==0)throw new Exception("Click completion or UI responsiveness failed: "+f.VerificationStatus);
   File.WriteAllText("evidence/verification-click.txt",f.VerificationStatus+" UI ticks="+ticks);
   f.SetVerificationPath(Path.Combine(root,"Missing"));
   f.ClickVerify();var failureDeadline=DateTime.UtcNow.AddSeconds(20);
   while(f.VerificationBusy && DateTime.UtcNow<failureDeadline){Application.DoEvents();System.Threading.Thread.Sleep(5);}
   if(f.VerificationBusy || !f.VerificationStatus.StartsWith("VERIFICAÇÃO NÃO APROVADA"))throw new Exception("Missing backup has no visible failure");
   f.SetVerificationPath(app);f.ClickVerify();var repeatDeadline=DateTime.UtcNow.AddSeconds(60);
   while(f.VerificationBusy && DateTime.UtcNow<repeatDeadline){Application.DoEvents();System.Threading.Thread.Sleep(5);}
   if(f.VerificationBusy || !f.VerificationStatus.StartsWith("CÓPIA ÍNTEGRA"))throw new Exception("Repeat after error failed: "+f.VerificationStatus+" busy="+f.VerificationBusy);
   Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,f.ClientRectangle);image.Save("evidence/Testes003.png");}f.Dispose();}
  File.WriteAllText("evidence/results.json",new JavaScriptSerializer().Serialize(new{Client="003",Program="LEAL INFO PDV Testes",IndependentWindow=true,PDVClosed=true,Published003BaselineVerified=true,PreviousPreserved=true,IntactFixtureRestore=true,CustomerFilesPreserved=true,FaultInjected=false,ExistingUpdaterModified=false,RealComputerInstalled=false,HealthHandoffRealComputerValidated=false}));
  Console.WriteLine("PASS TESTES 003: standalone window; actual approved baseline; closed PDV; verified previous copy; intact fixture restore; no fault injection; customer sentinels unchanged");return 0;
 }catch(Exception e){Directory.CreateDirectory("evidence");File.WriteAllText("evidence/failure.txt",e.ToString());Console.Error.WriteLine(e);return 1;}}
}
