using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using LealInfoPDV;
using System.Windows.Forms;
static class Tests
{
 static List<string> checks=new();
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS "+message);}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string c,string title);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumChild callback,IntPtr l);
 delegate bool EnumChild(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 [STAThread]static void Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
  string root=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"notifier-qa-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory("evidence");
  var fixtures=new[]{"lealinfo.db","license.dat","network.config","stock.dat","cash.dat"}.ToDictionary(n=>Path.Combine(root,n),n=>Encoding.UTF8.GetBytes("PRESERVED-"+n));foreach(var p in fixtures)File.WriteAllBytes(p.Key,p.Value);
  using(var native=Process.Start(new ProcessStartInfo(args[0]){Arguments=$"--self-test \"{root}\"",UseShellExecute=false})!){Check(native.WaitForExit(45000)&&native.ExitCode==0,"native parser identity version and persisted notification tests");}
  var nativeProof=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"native-results.json")));Check(nativeProof.RootElement.GetProperty("Checks").GetInt32()>=16,"native checks include real HTTPS feed access without customer data");
  string appFolder=Path.Combine(root,"installed");Directory.CreateDirectory(appFolder);string app=Path.Combine(appFolder,"LealInfoPDV.exe");File.Copy(args[1],app);
  var host=typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateNotifierHost",true)!;
  object Prepare()=>host.GetMethod("Prepare",S)!.Invoke(null,new object?[]{app,"001",Path.Combine(root,"agent")})!;
  object install=Prepare();string Prop(string n)=>(string)install.GetType().GetProperty(n)!.GetValue(install)!;
  string agent=Prop("Agent"),config=Prop("Config"),keyName=Prop("StartupKey"),folder=Prop("Folder");
  Check(File.Exists(agent)&&File.Exists(config),"native component extracted and configured without setup");
  Check(File.ReadAllText(config).Contains("Route=CLIENT") && File.ReadAllText(config).Contains("Identity=001")&&File.ReadAllText(config).Contains("Application="+app),"only licensed Loja identity and path configured; no credentials");
  var installedAgain=Prepare();Check((string)installedAgain.GetType().GetProperty("Agent")!.GetValue(installedAgain)! == agent,"repeat preparation reuses immutable hash-qualified binary");
  string reg=@"Software\LealInfoPDV\QA\"+Guid.NewGuid().ToString("N")+@"\Run";
  try{
   using(var k=Registry.CurrentUser.CreateSubKey(reg))k.SetValue("OTHER-APPLICATION","UNCHANGED");
   host.GetMethod("RegisterStartup",S)!.Invoke(null,new object?[]{install,reg});
   using(var k=Registry.CurrentUser.OpenSubKey(reg)){Check((string?)k!.GetValue("OTHER-APPLICATION")=="UNCHANGED","startup registration preserves every unrelated value");Check(((string?)k.GetValue(keyName))?.StartsWith("\""+agent+"\" --config \"")==true,"Windows startup uses quoted native agent without elevation");}
   File.WriteAllText(Path.Combine(folder,"preferences.ini"),"[Notifier]\r\nStartupEnabled=0\r\n",Encoding.Unicode);
   host.GetMethod("RegisterStartup",S)!.Invoke(null,new object?[]{install,reg});
   using(var k=Registry.CurrentUser.OpenSubKey(reg))Check(k!.GetValue(keyName)==null&&(string?)k.GetValue("OTHER-APPLICATION")=="UNCHANGED","user can disable Windows startup without affecting other applications");
  }finally{Registry.CurrentUser.DeleteSubKeyTree(reg,false);}
  long memory=0;
  using(var running=Process.Start(new ProcessStartInfo(agent){Arguments=$"--config \"{config}\"",UseShellExecute=false})!){
   try{
    IntPtr window=IntPtr.Zero;var deadline=DateTime.UtcNow.AddSeconds(5);while(window==IntPtr.Zero&&DateTime.UtcNow<deadline&&!running.HasExited){Thread.Sleep(100);window=FindWindow("LealInfoPDV.UpdateNotifier.v1",keyName);}
    Check(window!=IntPtr.Zero&&!running.HasExited,"real native tray message loop runs while PDV is closed");
    running.Refresh();memory=running.PrivateMemorySize64;Check(memory<64L*1024*1024,"native idle private memory below 64 MiB");
    using(var duplicate=Process.Start(new ProcessStartInfo(agent){Arguments=$"--config \"{config}\"",UseShellExecute=false})!)Check(duplicate.WaitForExit(3000)&&duplicate.ExitCode==0,"second notifier for same installation exits without duplicate icon");
    // Exercise the actual manual command with native HTTPS and a Windows dialog.
    for(int attempt=0;attempt<2;attempt++){
     PostMessage(window,0x0111,(IntPtr)101,IntPtr.Zero);
     IntPtr feedback=IntPtr.Zero;var until=DateTime.UtcNow.AddSeconds(40);
     while(feedback==IntPtr.Zero&&DateTime.UtcNow<until){Thread.Sleep(100);feedback=FindWindow("#32770","Atualizações do LEAL INFO PDV");}
     Check(feedback!=IntPtr.Zero,"manual check displays visible feedback on attempt "+attempt);
     var content=new StringBuilder();EnumChildWindows(feedback,(h,l)=>{var b=new StringBuilder(2048);GetWindowText(h,b,2048);content.Append(b);return true;},IntPtr.Zero);
     Check(content.ToString().Contains("Verificação concluída"),"real HTTPS check confirms current version instead of silently failing");
     PostMessage(feedback,0x0111,(IntPtr)1,IntPtr.Zero);Thread.Sleep(300);
    }
    PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);Check(running.WaitForExit(5000)&&running.ExitCode==0,"tray notifier exits cleanly without touching PDV");
   }finally{if(!running.HasExited)running.Kill();}
  }
  byte[] original=File.ReadAllBytes(agent);File.WriteAllBytes(agent,new byte[]{1,2,3});bool blocked=false;try{Prepare();}catch(TargetInvocationException e){blocked=e.InnerException is InvalidDataException;}Check(blocked&&File.ReadAllBytes(agent).SequenceEqual(new byte[]{1,2,3}),"corrupt native component is refused and never executed or silently overwritten");File.WriteAllBytes(agent,original);
  Application.EnableVisualStyles();using(var owner=new Form()){
   owner.Show();bool hidden=false;var toast=typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateNotificationForm",true)!;
   Form? dialog=null;dialog=(Form)Activator.CreateInstance(toast,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{owner,"10.448",(Func<Task>)(()=>{hidden=!dialog!.Visible;return Task.CompletedTask;})},null)!;
   using(dialog){dialog.Show(owner);Application.DoEvents();foreach(var panel in dialog.Controls.OfType<Control>())Click(panel);Check(hidden,"update notification hides before transactional updater checks open windows");}
   owner.Close();
  }
  LoginExitTests.Run();
  foreach(var p in fixtures)Check(File.ReadAllBytes(p.Key).SequenceEqual(p.Value),"customer file untouched "+Path.GetFileName(p.Key));
  File.Copy(Path.Combine(root,"native-results.json"),"evidence/native-results.json",true);
  File.WriteAllText("evidence/notifier-results.json",JsonSerializer.Serialize(new{Success=true,Checks=checks,NativePrivateMemoryBytes=memory,NativeSizeBytes=original.Length,Published=false,InstalledOnCustomer=false,RealWindowsHomologated=false}));
 }
 static void Click(Control c){foreach(Control child in c.Controls.Cast<Control>().ToArray()){if(child is Button b&&b.Text=="Atualizar agora"){b.PerformClick();Application.DoEvents();return;}Click(child);}}
}
