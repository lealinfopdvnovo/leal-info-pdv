using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfoPDV;
static class BootstrapProbe
{
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Assembly App=typeof(IPetExtension).Assembly;
 static readonly List<string> Checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Checks.Add(text);Console.WriteLine("PASS "+text);}
 static bool Install(string folder,string id,Action<string>? point=null)=>(bool)App.GetType("LealInfoPDV.PetBootstrap",true)!.GetMethod("EnsurePresent",Flags)!.Invoke(null,new object?[]{folder,id,point})!;
 static void Reject(Action action,string text){bool rejected=false;try{action();}catch(TargetInvocationException){rejected=true;}Check(rejected,text);}
 static string State(){
  using var db=Database.Open();var tables=new List<string>();
  using(var command=db.CreateCommand()){command.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";using var reader=command.ExecuteReader();while(reader.Read())tables.Add(reader.GetString(0));}
  var data=new Dictionary<string,List<object?[]>>();
  foreach(var table in tables){using var command=db.CreateCommand();command.CommandText="SELECT * FROM \""+table.Replace("\"","\"\"")+"\" ORDER BY rowid";using var reader=command.ExecuteReader();var rows=new List<object?[]>();while(reader.Read()){var values=new object?[reader.FieldCount];for(int i=0;i<values.Length;i++)values[i]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(values);}data[table]=rows;}
  return JsonSerializer.Serialize(data);
 }

 [STAThread]static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=4)return 91;
  string identity=args[0],version=args[1],module=Path.GetFullPath(args[2]),exe=Path.GetFullPath(args[3]);
  try{
   Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   Check(Auth.Login("qa","qa-test-123!")!=null,"QA credentials valid before migration");
   using(var db=Database.Open())using(var q=db.CreateCommand()){
    q.CommandText="INSERT OR REPLACE INTO products(id,name,price,stock,active) VALUES(979,'MIGRATION QA STOCK',12.5,77,1);INSERT OR REPLACE INTO customers(id,name) VALUES(979,'MIGRATION QA CUSTOMER')";q.ExecuteNonQuery();
   }
   App.GetType("LealInfoPDV.CashFlow",true)!.GetMethod("Register",Flags)!.Invoke(null,new object[]{"ENTRADA","MIGRATION QA CASH "+identity,100m});
   var config=Path.Combine(Database.AppFolder,"migration-organization.config");
   File.WriteAllText(config,"QA-CONFIGURATION-PRESERVE-"+identity);
   var configBytes=File.ReadAllBytes(config);var before=State();
   byte[] dll=File.ReadAllBytes(module);
   byte[] capsule;
   using(var memory=new MemoryStream()){
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){
     using(var output=zip.CreateEntry("Pet.dll").Open())output.Write(dll);
     using(var output=zip.CreateEntry("manifest.json").Open())JsonSerializer.Serialize(output,new{Identity=identity,Version=version,ApiVersion=1,Assembly="Pet.dll",Sha256=Convert.ToHexString(SHA256.HashData(dll))});
    }capsule=memory.ToArray();
   }
   byte[] trailer=new byte[56];BinaryPrimitives.WriteInt64LittleEndian(trailer,capsule.Length);SHA256.HashData(capsule).CopyTo(trailer,8);Encoding.ASCII.GetBytes("LEAL-PET-BOOT-01").CopyTo(trailer,40);
   byte[] host=File.ReadAllBytes(exe).Concat(capsule).Concat(trailer).ToArray();
   var payload=Path.Combine("pair-fixtures",identity);Directory.CreateDirectory(payload);
   File.WriteAllBytes(Path.Combine(payload,"LealInfoPDV.exe"),host);
   foreach(var name in new[]{"LealInfoPDV.dll","LealInfoPDV.deps.json"})File.Copy(Path.Combine(Path.GetDirectoryName(exe)!,name),Path.Combine(payload,name),true);
   using(var zip=ZipFile.Open(Path.Combine(payload,"transition.zip"),ZipArchiveMode.Create))
    foreach(var name in new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"})zip.CreateEntryFromFile(Path.Combine(payload,name),name,CompressionLevel.Optimal);
   string Fixture(string kind){
    var folder=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"bootstrap-"+identity+"-"+kind+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder,"QA-ISOLATED"),"disposable fixture");
    File.WriteAllBytes(Path.Combine(folder,"LealInfoPDV.exe"),host);return folder;
   }
   string wrong=Fixture("wrong");Reject(()=>Install(wrong,identity=="003"?"001":"003"),"capsule identity mismatch refused");
   Check(!Directory.Exists(Path.Combine(wrong,"Extensions")),"identity rejection creates no profile");
   string corrupt=Fixture("corrupt");var altered=host.ToArray();altered[^60]^=1;File.WriteAllBytes(Path.Combine(corrupt,"LealInfoPDV.exe"),altered);Reject(()=>Install(corrupt,identity),"corrupt capsule refused");
   string orphan=Fixture("orphan");var orphanPet=Path.Combine(orphan,"Extensions","PET");Directory.CreateDirectory(orphanPet);File.WriteAllText(Path.Combine(orphanPet,"preserve.txt"),"KEEP");Reject(()=>Install(orphan,identity),"incomplete existing PET never overwritten");Check(File.ReadAllText(Path.Combine(orphanPet,"preserve.txt"))=="KEEP","orphan bytes retained");
   string interrupted=Fixture("interrupted");Reject(()=>Install(interrupted,identity,_=>throw new InvalidOperationException("QA interrupted before activation")),"interruption before activation fails closed");
   Check(!Directory.Exists(Path.Combine(interrupted,"Extensions","PET")),"no partial active PET after interrupted staging");
   Check(Install(interrupted,identity),"retry completes first atomic migration");
   string good=Fixture("success");Check(Install(good,identity),"actual profile "+identity+" installed independently from three-file transition capsule");
   var pet=Path.Combine(good,"Extensions","PET");var saved=Directory.GetFiles(pet).ToDictionary(p=>p,p=>File.ReadAllBytes(p));
   Check(!Install(good,identity)&&saved.All(p=>File.ReadAllBytes(p.Key).SequenceEqual(p.Value)),"repeat bootstrap preserves installed PET byte for byte");
   Check(File.ReadAllBytes(Path.Combine(pet,"Pet.dll")).SequenceEqual(dll),"installed module exactly matches compiled approved profile");
   var backups=Directory.GetDirectories(Database.BackupFolder,"MIGRACAO_PET_*");
   Check(backups.Length>0,"pre-migration backup exists");
   foreach(var backup in backups){
    var index=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(backup,"integrity.json")))!;
    Check(index.All(p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(backup,p.Key))))==p.Value),"backup integrity verified");
    RestoreEngine.Validate(Path.Combine(backup,"lealinfo.db"));
   }
   App.GetType("LealInfoPDV.PetRuntime",true)!.GetMethod("Load",Flags)!.Invoke(null,new object[]{pet,identity});
   Check((string?)App.GetType("LealInfoPDV.PetRuntime",true)!.GetProperty("CurrentVersion",Flags)!.GetValue(null)==version,"migrated profile loads through common API");
   Check(State()==before,"all populated database rows including stock and cash preserved");
   Check(File.ReadAllBytes(config).SequenceEqual(configBytes),"configuration preserved byte for byte");
   Directory.CreateDirectory("pair-evidence");
   File.WriteAllText("pair-evidence/bootstrap-"+identity+".json",JsonSerializer.Serialize(new{Pass=true,Identity=identity,Version=version,Checks,DatabaseStockAndCashPreserved=true,ActualProfileSource=true,LegacyInstalledWorkerExercised=false,OperationalCustomer=false,Published=false}));
   return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
