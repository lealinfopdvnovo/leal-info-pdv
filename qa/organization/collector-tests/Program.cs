using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LealInfo.Inventory;
using LealInfo.Collector;
static class Tests
{
 static readonly List<string> Passed=new List<string>();static string Root,Source,Output;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Passed.Add(name);Console.WriteLine("PASS "+name);}
 static void Refuse(Action a,string name){try{a();}catch(IOException){Check(true,name);return;}catch(UnauthorizedAccessException){Check(true,name);return;}throw new Exception("Accepted unsafe case: "+name);}
 static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static Dictionary<string,string> Snapshot(){return Directory.GetFiles(Source,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>Hash(x));}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string link,string target,IntPtr security);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle handle,uint code,byte[] input,int length,IntPtr output,int outputSize,out int returned,IntPtr overlapped);
 static void Junction(string link,string target){
  Directory.CreateDirectory(link);var sub=System.Text.Encoding.Unicode.GetBytes(@"\??\"+Path.GetFullPath(target));var print=System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(target));
  var data=new byte[16+sub.Length+2+print.Length+2];
  Action<int,ushort> word=(offset,value)=>{var bytes=BitConverter.GetBytes(value);Buffer.BlockCopy(bytes,0,data,offset,2);};
  Buffer.BlockCopy(BitConverter.GetBytes(0xA0000003u),0,data,0,4);word(4,(ushort)(data.Length-8));word(8,0);word(10,(ushort)sub.Length);word(12,(ushort)(sub.Length+2));word(14,(ushort)print.Length);
  Buffer.BlockCopy(sub,0,data,16,sub.Length);Buffer.BlockCopy(print,0,data,16+sub.Length+2,print.Length);
  using(var handle=CreateFile(link,0x40000000,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){int returned;if(handle.IsInvalid||!DeviceIoControl(handle,0x000900A4,data,data.Length,IntPtr.Zero,0,out returned,IntPtr.Zero))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
 }

 sealed class Dialogs:ICollectorDialogs
 {
  public string Folder,Save;public int ChooseCount,SaveCount,Success,Errors;
  public string ChooseFolder(Form f){ChooseCount++;return Folder;} public string ChooseOutput(Form f){SaveCount++;return Save;}public void Result(Form f,string m,bool error){if(error)Errors++;else Success++;}
 }
 static void Pump(Func<bool> until){var end=DateTime.UtcNow.AddSeconds(20);do{Application.DoEvents();Thread.Sleep(10);}while(!until()&&DateTime.UtcNow<end);if(!until())throw new Exception("GUI timeout");}
 static Button Button(Form form,string name){return form.Controls.OfType<Button>().Single(x=>x.Name==name);}
 static void Ui()
 {
  var d=new Dialogs();using(var form=new CollectorForm(d)){
   form.Show();Application.DoEvents();Check(form.Visible&&Button(form,"collect").Enabled,"graphical form and collect button visible");
   Button(form,"collect").PerformClick();Check(d.ChooseCount==1&&d.SaveCount==0&&!form.Busy,"folder dialog cancellation performs no collection or save");
   d.Folder=Source;Button(form,"collect").PerformClick();Pump(()=>!form.Busy);Check(d.SaveCount==1&&d.Success==0&&d.Errors==0,"save dialog cancellation writes no JSON");
   d.Save=Path.Combine(Output,"gui.json");Button(form,"collect").PerformClick();Pump(()=>!form.Busy);Check(File.Exists(d.Save)&&d.Success==1&&d.Errors==0,"GUI saves report outside PDV");
   d.Folder=Path.Combine(Root,"wrong");Button(form,"collect").PerformClick();Pump(()=>!form.Busy);Check(d.Errors==1,"wrong folder has a graphical error");
   d.Folder=Source;d.Save=Path.Combine(Output,"cancelled.json");Button(form,"collect").PerformClick();Button(form,"cancel").PerformClick();Pump(()=>!form.Busy);Check(!File.Exists(d.Save),"active cancellation creates no report");
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.Combine(Environment.GetEnvironmentVariable("COLLECTOR_EVIDENCE"),"collector-gui.png"));}
   form.Close();
  }
 }
 [STAThread]static int Main(string[] args)
 {
  try{
   if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")return 91;
   var principal=new WindowsPrincipal(WindowsIdentity.GetCurrent());Check(!principal.IsInRole(WindowsBuiltInRole.Administrator),"all tests execute with non-administrator restricted token");
   Root=Path.Combine(Path.GetTempPath(),"LealCollectorQA-"+Guid.NewGuid().ToString("N"));Source=Path.Combine(Root,"PDV");Output=Path.Combine(Root,"Output");Directory.CreateDirectory(Source);Directory.CreateDirectory(Output);Directory.CreateDirectory(Path.Combine(Root,"wrong"));
   var self=Assembly.GetExecutingAssembly().Location;File.Copy(self,Path.Combine(Source,"LealInfoPDV.exe"));File.Copy(self,Path.Combine(Source,"LealInfoPDV.dll"));
   foreach(var name in new[]{"LealInfoPDV.deps.json","LealInfoPDV.runtimeconfig.json"})File.WriteAllText(Path.Combine(Source,name),"{}");
   foreach(var name in new[]{"network.license","lealinfo.db","password.key","settings.config","client.pdf"})File.WriteAllText(Path.Combine(Source,name),"PRIVATE-SYNTHETIC-NEVER-REPORT");
   var pet=Path.Combine(Source,"Extensions","PET");Directory.CreateDirectory(pet);File.Copy(self,Path.Combine(pet,"Pet.dll"));File.WriteAllText(Path.Combine(pet,"manifest.json"),"{\"PrivateMarker\":\"NEVER-COPY-CONTENT\"}");
   var baseline=Snapshot();var report=InstallationInventory.Collect(Source);var json=InstallationInventory.Serialize(report);
   Check(report.Files.Count==6&&report.Missing.Count==0,"correct PDV directory and allowed PET files collected");
   Check(report.Files.All(x=>!Path.IsPathRooted(x.Name)&&!x.Name.Contains(".."))&&!json.Contains(Root)&&!json.Contains("PRIVATE-SYNTHETIC")&&!json.Contains("NEVER-COPY-CONTENT")&&!json.Contains("network.license")&&!json.Contains("client.pdf"),"report contains only relative public metadata and authorized hashes");
   Check(report.Files.All(x=>x.Sha256==Hash(Path.Combine(Source,x.Name.Replace('/',Path.DirectorySeparatorChar)))),"all report SHA-256 values match originals");
   Refuse(()=>InstallationInventory.Collect(Path.Combine(Root,"wrong")),"incorrect folder rejected");
   File.Delete(Path.Combine(Source,"LealInfoPDV.runtimeconfig.json"));Check(InstallationInventory.Collect(Source).Missing.Contains("LealInfoPDV.runtimeconfig.json"),"missing required files explicitly recorded");File.WriteAllText(Path.Combine(Source,"LealInfoPDV.runtimeconfig.json"),"{}");
   using(var reader=File.OpenRead(Path.Combine(Source,"LealInfoPDV.dll")))Check(InstallationInventory.Collect(Source).Files.Count==6,"files in read-only use remain readable");
   using(var writer=new FileStream(Path.Combine(Source,"LealInfoPDV.dll"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite))Refuse(()=>InstallationInventory.Collect(Source),"files open for modification rejected instead of torn reads");
   using(var guard=new PathGuard(Source)){
    PathGuard.Info info;using(var held=guard.OpenRead(Path.Combine(Source,"LealInfoPDV.dll"),out info))Refuse(()=>File.WriteAllText(Path.Combine(Source,"LealInfoPDV.dll"),"changed"),"attempt to modify file during protected read is denied");
    File.WriteAllText(Path.Combine(Source,"live-database.dat"),"database may keep writing");File.Delete(Path.Combine(Source,"live-database.dat"));Check(true,"directory guards do not prevent normal database file creation");
   }
   var denied=Path.Combine(Source,"runtimes");Directory.CreateDirectory(denied);var acl=Directory.GetAccessControl(denied);var changed=Directory.GetAccessControl(denied);var sid=WindowsIdentity.GetCurrent().User;changed.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.ListDirectory,AccessControlType.Deny));Directory.SetAccessControl(denied,changed);
   try{Refuse(()=>InstallationInventory.Collect(Source),"unreadable folder fails safely without elevation");}finally{Directory.SetAccessControl(denied,acl);Directory.Delete(denied);}
   var external=Path.Combine(Root,"External");Directory.CreateDirectory(external);File.Copy(self,Path.Combine(external,"outside.dll"));
   var link=Path.Combine(Source,"runtimes");Junction(link,external);try{Refuse(()=>InstallationInventory.Collect(Source),"redirected dependency directory refused");}finally{Directory.Delete(link);}
   var alias=Path.Combine(Root,"Alias");Junction(alias,Root);try{Refuse(()=>InstallationInventory.Collect(Path.Combine(alias,"PDV")),"redirected ancestor cannot escape selected folder");}finally{Directory.Delete(alias);}
   var hard=Path.Combine(Source,"outside.dll");if(!CreateHardLink(hard,Path.Combine(external,"outside.dll"),IntPtr.Zero))throw new Exception("Hardlink fixture failed");try{Refuse(()=>InstallationInventory.Collect(Source),"hard-linked file outside selected installation refused");}finally{File.Delete(hard);}
   var cancelled=new CancellationTokenSource();cancelled.Cancel();try{InstallationInventory.Collect(Source,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"collection cancellation creates no report");}
   report=InstallationInventory.Collect(Source);Refuse(()=>InstallationInventory.SaveReport(Source,Path.Combine(Source,"unsafe.json"),report,CancellationToken.None),"saving JSON inside installation rejected");
   var outputAlias=Path.Combine(Output,"Alias");Junction(outputAlias,Source);try{Refuse(()=>InstallationInventory.SaveReport(Source,Path.Combine(outputAlias,"unsafe.json"),report,CancellationToken.None),"redirected save destination into PDV refused");}finally{Directory.Delete(outputAlias);}
   var outputHard=Path.Combine(Output,"existing.json");if(!CreateHardLink(outputHard,Path.Combine(Source,"network.license"),IntPtr.Zero))throw new Exception("Output hardlink fixture failed");try{Refuse(()=>InstallationInventory.SaveReport(Source,outputHard,report,CancellationToken.None),"saving over existing hardlink refused without modifying source");}finally{File.Delete(outputHard);}
   InstallationInventory.SaveReport(Source,Path.Combine(Output,"report.json"),report,CancellationToken.None);Check(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(Output,"report.json")))!=null,"outside JSON saved and parsed");
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Ui();
   Check(baseline.All(x=>File.Exists(x.Key)&&Hash(x.Key)==x.Value)&&Snapshot().Count==baseline.Count,"all original source bytes and file set unchanged");
   var exe=args[0];
   var evidence=Environment.GetEnvironmentVariable("COLLECTOR_EVIDENCE");File.WriteAllText(Path.Combine(evidence,"windows-tests.json"),new JavaScriptSerializer().Serialize(new{Pass=true,Checks=Passed,NonAdministrator=true,SourceUnchanged=true,ExecutableSha256=Hash(exe)}));
   Directory.Delete(Root,true);return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
