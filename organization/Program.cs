using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
class Checks {
 [STAThread]static int Main(string[] args){
 try {
 if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
 ApplicationConfiguration.Initialize();
 var identity=args[0];var folder=Path.GetFullPath(args[1]);
 string version=identity=="001"?"1.2":"1.1";
 var host=typeof(IPetExtension).Assembly;var before=SHA256.HashData(File.ReadAllBytes(host.Location));
 var rt=host.GetType("LealInfoPDV.PetRuntime")!;
 var load=rt.GetMethod("Load",BindingFlags.Static|BindingFlags.NonPublic)!;
 var file=Path.Combine(folder,"manifest.json");string original=File.ReadAllText(file);
 bool rejected=false;
 try{load.Invoke(null,new object[]{folder,identity=="001"?"002":"001"});}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){rejected=true;}
 if(!rejected)throw new Exception("Cross-client manifest accepted");
 using(var doc=JsonDocument.Parse(original)){
  var m=doc.RootElement;File.WriteAllText(file,JsonSerializer.Serialize(new{Identity=identity,Version=version,ApiVersion=1,Assembly="Pet.dll",Sha256=new string('0',64)}));
 }
 rejected=false;try{load.Invoke(null,new object[]{folder,identity});}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){rejected=true;}
 if(!rejected)throw new Exception("Wrong hash accepted");File.WriteAllText(file,original);
 load.Invoke(null,new object[]{folder,identity});
 if((string)rt.GetProperty("CurrentVersion",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)! != version)throw new Exception("Wrong PET version");
 if(!before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(host.Location))))throw new Exception("GLOBAL bytes changed");
 Directory.CreateDirectory("evidence");File.WriteAllText("evidence/identity-"+identity+".json",JsonSerializer.Serialize(new{Identity=identity,PetVersion=version,SameGlobal=true,CrossIdentityRejected=true,WrongHashRejected=true,ValidPetLoaded=true,GlobalUnchanged=true,Published=false}));
 Console.WriteLine("PASS "+identity+": same GLOBAL, identity, hash, version, real module loading");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
