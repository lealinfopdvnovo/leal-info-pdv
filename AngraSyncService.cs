using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LealInfoPDV.Network;
namespace LealInfoPDV;

internal sealed class AngraSyncService : IDisposable
{
 internal static AngraSyncService? Current{get;private set;}
 internal AngraSyncTransport Transport{get;}
 internal AngraSyncStore? Store{get;private set;}
 private readonly System.Threading.Timer timer;
 private readonly SemaphoreSlim gate=new(1,1);
 private bool enabled,disposed,authorized;
 private int photosPending;
 private string tenant="";
 internal string Status{get;private set;}="PDV NÃO CONECTADO";
 internal string AuthorizationUrl{get;private set;}="";
 private static string ConfigPath=>Path.Combine(Database.AppFolder,"angra-sync.config");
 internal static AngraSyncService? TryStart(){try{return new AngraSyncService();}catch{return null;}}
 internal AngraSyncService(AngraSyncTransport? transport=null,bool startTimer=true)
 {
  Transport=transport??new();Current=this;if(File.Exists(ConfigPath)){try{var config=JsonNode.Parse(ProtectedData.Unprotect(File.ReadAllBytes(ConfigPath),null,DataProtectionScope.CurrentUser))!;tenant=AngraProjectData.Text(config["tenant"]);enabled=config["enabled"]?.GetValue<bool>()==true;if(enabled&&tenant!=""&&NetworkConfiguration.Current.Mode!="terminal")Store=new AngraSyncStore(tenant);}catch{Status="Configuração protegida indisponível. Nenhum vínculo foi recriado.";}}
  timer=new System.Threading.Timer(async _=>await Tick(),null,startTimer?TimeSpan.FromSeconds(2):Timeout.InfiniteTimeSpan,TimeSpan.FromSeconds(30));
 }
 private void SaveConfig(){Directory.CreateDirectory(Database.AppFolder);byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(new JsonObject{["tenant"]=tenant,["enabled"]=enabled}.ToJsonString()),null,DataProtectionScope.CurrentUser);string temporary=ConfigPath+".tmp";using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes);file.Flush(true);}File.Move(temporary,ConfigPath,true);}
 internal async Task Connect()
 {
  if(NetworkConfiguration.Current.Mode=="terminal")throw new InvalidOperationException("Faça a conexão no computador servidor do PDV.");await Transport.Register();enabled=true;SaveConfig();await Tick();
 }
 internal async Task Tick()
 {
  if(disposed||!enabled||NetworkConfiguration.Current.Mode=="terminal"||!await gate.WaitAsync(0))return;
  try{
   {var result=await Transport.Call("/sync/request-pdv",new(){["name"]=CompanyName()});AuthorizationUrl=AngraProjectData.Text(result["authorizationUrl"]);if(result["approved"]?.GetValue<bool>()!=true){Status="AGUARDANDO AUTORIZAÇÃO DO PROPRIETÁRIO";return;}string granted=AngraProjectData.Text(result["tenant"]);if(tenant!=""&&tenant!=granted)throw new InvalidOperationException("Vínculo de outra empresa recusado.");tenant=granted;SaveConfig();}
   await Transport.Call("/sync/enable-workshop",new());authorized=true;Store??=new AngraSyncStore(tenant);string? cursor=null;JsonArray changes=ReadyChanges(Store.Prepare());do{var response=await Transport.Call("/sync/exchange",new(){["changes"]=changes,["cursor"]=cursor});var permission=await Transport.Call("/sync/status",new());if(permission["tenant"]?.ToString()!=tenant||permission["bindingOnly"]?.GetValue<bool>()==true)throw new InvalidOperationException("Sincronização ainda não autorizada.");Store.Apply(response);changes=new();cursor=AngraProjectData.Text(response["cursor"]);if(cursor=="null")cursor="";}while(cursor!=""&&!disposed);
   await Photos();var links=Store.Links();int pending=links.Count(x=>x.Pending!=null),conflicts=links.Count(x=>x.Conflict!=null||x.Error!="");Status="PDV CONECTADO • "+CompanyName()+"\nÚltima sincronização: "+DateTime.Now.ToString("g")+"\nPendentes: "+pending+" • Revisões necessárias: "+conflicts+(photosPending>0?"\nFotos pendentes: "+photosPending:"");
  }catch(Exception e){Status="OFFLINE / SINCRONIZAÇÃO PENDENTE\n"+e.Message+"\nDados e alterações pendentes preservados.";}finally{gate.Release();}
 }
 internal async Task<string> Ticket(){if(Store==null)throw new InvalidOperationException("Autorize primeiro este PDV no navegador.");var result=await Transport.Call("/sync/ticket",new(){["shortCode"]=true});return AngraProjectData.Text(result["code"]);}
 internal void UseTestTenant(string value){tenant=value;enabled=true;Store=new(value);}
 private static string CompanyName(){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT value FROM settings WHERE key='company_name'";string name=Convert.ToString(q.ExecuteScalar())??"";return (string.IsNullOrWhiteSpace(name)?"PDV":name)+" • "+Environment.MachineName;}
 internal static string PhotoPath(string projectId,string sha){if(!Guid.TryParse(projectId,out _)||!System.Text.RegularExpressions.Regex.IsMatch(sha,@"^[a-f0-9]{64}$"))throw new InvalidDataException("Identificação de anexo inválida.");string folder=Path.Combine(Database.AppFolder,"AngraAttachments",projectId);Directory.CreateDirectory(folder);return Path.Combine(folder,sha+".jpg");}
 // A project receipt is emitted only after its current photo is available and verified locally.
 private JsonArray ReadyChanges(JsonArray changes){var ready=new JsonArray();foreach(var item in changes){if(item is not JsonObject change)continue;if(change["entity"]?.ToString()=="projects"&&change["payload"]?["photoAttachment"] is JsonObject manifest){string sha=AngraProjectData.Text(manifest["sha256"]);string file=PhotoPath(change["id"]!.ToString(),sha);if(!File.Exists(file)||Hash(File.ReadAllBytes(file))!=sha)continue;}ready.Add(change.DeepClone());}return ready;}
 private async Task Photos(){photosPending=0;if(Store==null)return;foreach(var l in Store.Links().Where(x=>x.Entity=="projects"&&x.Revision>0&&x.Conflict==null)){
  var payload=Store.ReadLocal(l);if(payload?["photoAttachment"] is not JsonObject manifest)continue;string sha=AngraProjectData.Text(manifest["sha256"]);long size=manifest["sizeBytes"]?.GetValue<long>()??0;if(size is <16 or >8_000_000)continue;string file=PhotoPath(l.Id,sha);if(File.Exists(file)&&Hash(File.ReadAllBytes(file))==sha)continue;
  try{var cap=await Transport.Call("/sync/photo-get",new(){["projectId"]=l.Id,["sha256"]=sha});byte[] bytes=await Transport.Binary(AngraProjectData.Text(cap["url"]));if(bytes.Length!=size||Hash(bytes)!=sha||bytes[0]!=255||bytes[1]!=216)throw new InvalidDataException("Hash de foto inválido.");string temp=file+".tmp";await File.WriteAllBytesAsync(temp,bytes);File.Move(temp,file,true);}catch{photosPending++;}
 }}
 internal static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
 public void Dispose(){disposed=true;timer.Dispose();if(Current==this)Current=null;}
}
