using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
static class StartupProbe
{
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Assembly App=typeof(IPetExtension).Assembly;
 static void Ready(string type)=>App.GetType("LealInfoPDV."+type,true)!.GetMethod("ReportReady",Flags)!.Invoke(null,null);
 static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS "+text);}
 [STAThread]static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" || args.Length!=2 || args[0]!="--update-transaction")return 91;
  var common=Path.Combine(AppContext.BaseDirectory,".pdv-update");
  var pet=Path.Combine(AppContext.BaseDirectory,".pdv-pet-update");
  var fixture=Environment.GetEnvironmentVariable("PET_FIXTURE")!;
  try{
   Directory.CreateDirectory(common);Directory.CreateDirectory(pet);
   Ready("TransactionalUpdate");Ready("PetTransactionalUpdate");
   Check(!File.Exists(Path.Combine(common,"health.json"))&&!File.Exists(Path.Combine(pet,"health.json")),"foreign or absent journal does not interrupt startup");
   var petFolder=Path.Combine(AppContext.BaseDirectory,"fixture-pet");Directory.CreateDirectory(petFolder);
   File.Copy(fixture,Path.Combine(petFolder,"PetFixture.dll"),true);
   File.WriteAllText(Path.Combine(petFolder,"manifest.json"),JsonSerializer.Serialize(new{Identity="003",Version="1.27",ApiVersion=1,Assembly="PetFixture.dll",Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture)))}));
   App.GetType("LealInfoPDV.PetRuntime",true)!.GetMethod("Load",Flags)!.Invoke(null,new object[]{petFolder,"003"});
   File.WriteAllText(Path.Combine(pet,"journal.json"),JsonSerializer.Serialize(new{Phase="AwaitingHealth",Nonce=args[1]}));
   Ready("TransactionalUpdate");Ready("PetTransactionalUpdate");
   using(var health=JsonDocument.Parse(File.ReadAllText(Path.Combine(pet,"health.json"))))
    Check(health.RootElement.GetProperty("Version").GetString()=="1.27"&&!File.Exists(Path.Combine(common,"health.json")),"PET confirms its loaded version without creating GLOBAL health");
   File.Delete(Path.Combine(pet,"journal.json"));File.Delete(Path.Combine(pet,"health.json"));
   File.WriteAllText(Path.Combine(common,"journal.json"),JsonSerializer.Serialize(new{Phase="AwaitingHealth",Nonce=args[1]}));
   Ready("TransactionalUpdate");Ready("PetTransactionalUpdate");
   var version=(string)App.GetType("LealInfoPDV.UpdateManager",true)!.GetField("CurrentVersion",Flags)!.GetRawConstantValue()!;
   using(var health=JsonDocument.Parse(File.ReadAllText(Path.Combine(common,"health.json"))))
    Check(health.RootElement.GetProperty("Version").GetString()==version&&!File.Exists(Path.Combine(pet,"health.json")),"GLOBAL confirms common version independently of PET journal");
   Directory.CreateDirectory("pair-evidence");
   File.WriteAllText("pair-evidence/startup-contract.json",JsonSerializer.Serialize(new{Pass=true,Checks=3,GlobalVersion=version,PetVersion="1.27",Published=false}));
   return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  finally{Directory.Delete(common,true);Directory.Delete(pet,true);}
 }
}
