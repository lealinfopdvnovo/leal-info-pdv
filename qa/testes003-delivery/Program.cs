using System.Reflection;using System.Security.Cryptography;using System.Text.Json;using LealInfoPDV;
class Checks{
 [STAThread]static int Main(){try{
 if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
 var a=typeof(MainForm).Assembly;var t=a.GetType("LealInfoPDV.Testes003Delivery")!;var method=t.GetMethod("Prepare",BindingFlags.NonPublic|BindingFlags.Static)!;
 var app=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"TestesDelivery-"+Guid.NewGuid());Directory.CreateDirectory(app);Directory.CreateDirectory(Path.Combine(app,".pdv-update/previous"));
 foreach(var n in new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"}){File.Copy(Path.Combine("../previous003",n),Path.Combine(app,".pdv-update/previous",n));File.Copy(Path.Combine("publish",n),Path.Combine(app,n));}
 File.WriteAllText(Path.Combine(app,"license.dat"),"PRESERVED");
 foreach(var id in new[]{"001","002","004"})if(method.Invoke(null,new object[]{app,id})!=null)throw new Exception("Cross-client extraction");
 var before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(app,"LealInfoPDV.dll"))));
 var exe=(string)method.Invoke(null,new object[]{app,"003"})!;if(!File.Exists(exe))throw new Exception("Agent not delivered");
 var expected=File.ReadAllBytes("../agent/LEAL_INFO_PDV_Testes_003.exe");if(!SHA256.HashData(File.ReadAllBytes(exe)).SequenceEqual(SHA256.HashData(expected)))throw new Exception("Approved binary changed");
 if(before!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(app,"LealInfoPDV.dll"))))||File.ReadAllText(Path.Combine(app,"license.dat"))!="PRESERVED")throw new Exception("Operational files changed during extraction");
 // Repeat extraction after upgrading from the installed 10.458, preserving the pinned recovery archive.
 var recoveryRoot=Directory.GetParent(Path.GetDirectoryName(exe)!)!.FullName;
 var recoveryZip=Path.Combine(recoveryRoot,"previous-003-10.457.zip");
 var archiveHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(recoveryZip)));
 foreach(var n in new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"})File.Copy(Path.Combine("../installed003",n),Path.Combine(app,".pdv-update/previous",n),true);
 if((string)method.Invoke(null,new object[]{app,"003"})! != exe || archiveHash!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(recoveryZip))))throw new Exception("458 transition lost recovery backup");
 // The already approved independent EXE is launched with the PDV closed; no restoration or simulated error.
 using(var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=false})){System.Threading.Thread.Sleep(1500);if(p!.HasExited)throw new Exception("Independent agent failed to open");p.Kill();p.WaitForExit();}
 var u=a.GetType("LealInfoPDV.UpdateManager")!;if((string)u.GetField("CurrentVersion")!.GetRawConstantValue()! != "10.457")throw new Exception("GLOBAL changed");
 Directory.CreateDirectory("evidence");File.WriteAllText("evidence/testes003-delivery.json",JsonSerializer.Serialize(new{Success=true,Client="003",GlobalVersion="10.457",DeliveryVersion="10.459",ApprovedAgentUnchanged=true,IndependentProcessOpenedWithPDVClosed=true,Previous003BackupPreserved=true,OtherClientsUntouched=true,NoFaultInjected=true,RealComputerInstalled=false}));return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
