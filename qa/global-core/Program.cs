using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
static class Tests
{
 const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 static readonly Assembly App=typeof(IPetExtension).Assembly;
 static readonly Type Updater=App.GetType("LealInfoPDV.UpdateManager",true)!;
 static readonly Type Pet=App.GetType("LealInfoPDV.PetRuntime",true)!;
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 sealed class Feed(string? body):HttpMessageHandler
 {
  public List<string> Paths=new();
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken t){Paths.Add(req.RequestUri!.AbsolutePath);return Task.FromResult(new HttpResponseMessage(body==null?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new StringContent(body??"")});}
 }
 static string Manifest(string version="10.444",string component="GLOBAL",int api=1,string target="",string? sha=null)=>JsonSerializer.Serialize(new{Version=version,Component=component,PetApiVersion=api,TargetClientCode=target,Sha256=sha??new string('a',64),PackageUrl="https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v"+version+"/UPDATE.zip"});
 static async Task<object?> Select(string client,string? body){using var feed=new Feed(body);using var http=new HttpClient(feed);var method=Updater.GetMethod("LoadManifestAsync",S,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;var task=(Task)method.Invoke(null,new object[]{http,client,"10.443"})!;await task;Check(feed.Paths.Count==1&&feed.Paths[0].EndsWith("/main/version.json"),"only GLOBAL feed consulted for "+client);return task.GetType().GetProperty("Result")!.GetValue(task);}
 static void Policy(string path)=>App.GetType("LealInfoPDV.Licensing.UpdatePayloadPolicy",true)!.GetMethod("Validate",S)!.Invoke(null,new object[]{path});
 static void Zip(string path,params string[] names){using var z=ZipFile.Open(path,ZipArchiveMode.Create);foreach(var n in names){using var w=new StreamWriter(z.CreateEntry(n).Open());w.Write("TEST "+n);}}
 static void Reject(Action action,string text){bool rejected=false;try{action();}catch(TargetInvocationException){rejected=true;}Check(rejected,text);}
 [STAThread]static int Main(string[] args)
 {
  Directory.CreateDirectory("evidence");using var timeout=new System.Threading.Timer(_=>Environment.Exit(91),null,120000,Timeout.Infinite);
  try{
   Check(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","isolated Windows only");
   Check((string)Updater.GetField("DistributionChannel",S)!.GetRawConstantValue()! =="GLOBAL","neutral GLOBAL build has no customer distribution identity");
   foreach(var id in new[]{"001","002","003","004","005"})Check(Select(id,Manifest()).GetAwaiter().GetResult()!=null,"common update available with independent client identity "+id);
   foreach(var body in new[]{Manifest("10.442"),Manifest("10.443"),Manifest(component:"PET"),Manifest(api:2),Manifest(target:"001"),Manifest(sha:"broken"),"{",null})Check(Select("003",body).GetAwaiter().GetResult()==null,"incompatible missing legacy or non-newer GLOBAL manifest refused");
   var root=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"global-core-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string[] common={"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"};var good=Path.Combine(root,"good.zip");Zip(good,common);Policy(good);Check(true,"exact three-file common payload accepted");
   foreach(var extra in new[]{"Extensions/PET/Pet.dll","Extensions/PET/manifest.json","license.protected","customer.db","../LealInfoPDV.dll","Assets/client-logo.png","LealInfoPDV.DLL"}){var bad=Path.Combine(root,Guid.NewGuid()+".zip");Zip(bad,common.Append(extra).ToArray());Reject(()=>Policy(bad),"GLOBAL refuses local or duplicate payload member "+extra);}
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   var petFolder=Path.Combine(root,"Extensions","PET");Directory.CreateDirectory(petFolder);var dll=Path.Combine(petFolder,"PetFixture.dll");File.Copy(args[0],dll);
   var manifestPath=Path.Combine(petFolder,"manifest.json");var sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)));void PetManifest(string identity="003",int api=1,string file="PetFixture.dll",string? hash=null)=>File.WriteAllText(manifestPath,JsonSerializer.Serialize(new{Identity=identity,Version="1.27",ApiVersion=api,Assembly=file,Sha256=hash??sha}));
   var load=Pet.GetMethod("Load",S)!;PetManifest(identity:"002");Reject(()=>load.Invoke(null,new object[]{petFolder,"003"}),"PET of another installation cannot load");PetManifest(api:2);Reject(()=>load.Invoke(null,new object[]{petFolder,"003"}),"unsupported PET API rejected");PetManifest(file:"../PetFixture.dll");Reject(()=>load.Invoke(null,new object[]{petFolder,"003"}),"PET path traversal rejected");PetManifest(hash:new string('0',64));Reject(()=>load.Invoke(null,new object[]{petFolder,"003"}),"tampered PET binary refused");PetManifest();
   var before=Directory.GetFiles(petFolder).ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));load.Invoke(null,new object[]{petFolder,"003"});using var f=new Form{Text="GLOBAL with PET"};f.Show();Application.RaiseIdle(EventArgs.Empty);Application.RaiseIdle(EventArgs.Empty);Check(f.Controls.Find("PetLocalAction",true).Length==1,"real PET extension customizes native form once");
   Check((string?)Pet.GetProperty("CurrentVersion",S)!.GetValue(null)=="1.27","independent local PET version loaded without changing common version");Check(Select("003",Manifest()).GetAwaiter().GetResult()!=null,"new GLOBAL selected while PET 1.27 remains installed");Check((string?)Pet.GetProperty("CurrentVersion",S)!.GetValue(null)=="1.27"&&before.All(p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Key)))==p.Value),"GLOBAL selection changes neither PET version nor bytes");
   using(var shot=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("evidence/global-pet.png");}f.Close();
   File.WriteAllText("evidence/results.json",JsonSerializer.Serialize(new{Success=true,Source=Environment.GetEnvironmentVariable("GITHUB_SHA"),checks,Published=false,RealCustomerWindowsTest=false}));Console.WriteLine("PASS GLOBAL CORE PREPARATION; NO PUBLICATION");Environment.Exit(0);return 0;
  }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("evidence/failure.txt",e.ToString());return 1;}
 }
}
