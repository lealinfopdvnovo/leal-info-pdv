using LealInfoPDV;
using System.Reflection;
using System.Diagnostics;
static class Program {
 const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 static readonly Assembly A=typeof(MainForm).Assembly;
 static object Make(string name,params object?[] args)=>Activator.CreateInstance(A.GetType("LealInfoPDV."+name)!,I,null,args,null)!;
 static string Id(object x)=>(string)x.GetType().GetProperty("DeviceId",I)!.GetValue(x)!;
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static void Main(string[] args){Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","disposable Windows identity fixture");Database.Initialize();var identity=Make("AngraSyncTransport",new object?[]{null});var id=Id(identity);
 if(args.Length>0){Check(id==args[0],"same DPAPI protected private identity reopened in NEW Windows process");using var reopened=(IDisposable)Make("AngraSyncService",null,false);var store=reopened.GetType().GetProperty("Store",I)!.GetValue(reopened)!;Check((string)store.GetType().GetProperty("Tenant",I)!.GetValue(store)!=="test-0","protected authorized-connection configuration restored without network");return;}
 Check(File.Exists(Path.Combine(Database.AppFolder,"angra-sync.identity")),"private identity exists in original PDV profile");Check(Id(Make("AngraSyncTransport",new object?[]{null}))==id,"same profile reuses identity instead of creating device");
 using(var service=(IDisposable)Make("AngraSyncService",null,false)){var type=service.GetType();type.GetField("tenant",I)!.SetValue(service,"test-0");type.GetField("enabled",I)!.SetValue(service,true);type.GetMethod("SaveConfig",I)!.Invoke(service,null);}
 var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};psi.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);psi.ArgumentList.Add(id);using var child=Process.Start(psi)!;string output=child.StandardOutput.ReadToEnd(),error=child.StandardError.ReadToEnd();Check(child.WaitForExit(30000)&&child.ExitCode==0,"protected identity/configuration survived real process restart");Console.Write(output);Console.Write(error);
 File.WriteAllText(Path.Combine(Database.AppFolder,"angra-sync.identity"),"corrupt test fixture");var result=A.GetType("LealInfoPDV.AngraSyncService")!.GetMethod("TryStart",S)!.Invoke(null,null);Check(result==null,"integration identity failure is isolated from normal PDV startup");
 }
}
