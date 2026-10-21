using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
namespace LealInfoPDV;
internal static class UpdateManager
{
    public const string CurrentVersion="10.434";
 private const string DistributionChannel="003";
 private const string LatestReleaseApi="https://api.github.com/repos/lealinfopdvnovo/leal-info-pdv-updates/releases/latest";
 private static readonly string UpdatesFolder=Path.Combine(Database.AppFolder,"Updates");
 private sealed class UpdateManifest{public string Version{get;set;}="";public string PackageUrl{get;set;}="";public string Notes{get;set;}="";public string Sha256{get;set;}="";public string SourceCommit{get;set;}="";public string TargetClientCode{get;set;}="";}
 private sealed class GitHubRelease{public string tag_name{get;set;}="";public string body{get;set;}="";public GitHubAsset[] assets{get;set;}=Array.Empty<GitHubAsset>();}
 private sealed class GitHubAsset{public string name{get;set;}="";public string browser_download_url{get;set;}="";}
 public static async Task CheckForUpdatesAsync(IWin32Window? owner,bool silent){try{Directory.CreateDirectory(UpdatesFolder);var manifest=await LoadManifestAsync();if(manifest==null){if(!silent)MessageBox.Show(owner,"Nenhuma versão publicada foi encontrada agora.","Atualizações do LEAL INFO PDV");return;}if(!IsNewer(manifest.Version,CurrentVersion)){if(!silent)MessageBox.Show(owner,$"Seu LEAL INFO PDV já está atualizado.\n\nVersão instalada: V{CurrentVersion} • {Licensing.InstallationLicense.Edition}","Atualizações do LEAL INFO PDV");return;}var result=ConfirmUpdate(owner,manifest);if(result==DialogResult.Yes)await DownloadAndApplyAsync(manifest);}catch(Exception ex){LogCheck("https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json", "Falha na verificacao: "+ex.GetType().Name);if(!silent)MessageBox.Show(owner,"Não foi possível atualizar agora.\n\n"+ex.Message,"Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
 private static DialogResult ConfirmUpdate(IWin32Window? owner,UpdateManifest manifest)=>MessageBox.Show(owner,$"NOVA ATUALIZAÇÃO DISPONÍVEL\n\nVersão instalada: V{CurrentVersion} • {Licensing.InstallationLicense.Edition}\nNova versão: V{manifest.Version}\n\n{manifest.Notes}\n\nDeseja atualizar agora?","Atualização disponível",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
 public static async Task ShowUpdateCenterAsync(IWin32Window owner){await CheckForUpdatesAsync(owner,false);}
 public static void ShowUpdateCenter(IWin32Window owner){_ = ShowUpdateCenterAsync(owner);}
 private static async Task<UpdateManifest?> LoadManifestAsync()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(8)};
  http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/"+CurrentVersion);
  return await LoadManifestAsync(http,GetClientCode(),CurrentVersion);
 }
 private static async Task<UpdateManifest?> LoadManifestAsync(HttpClient http,string clientCode,string currentVersion)
 {
  // Consultas independentes; selecao preserva a identidade de cada canal.
  var globalTask=TryLoadManifestAsync(http,"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json");
  var targetedTask=ClientUpdateIdentity.GetManifestPath(clientCode) is string path
   ? TryLoadManifestAsync(http,"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/"+path)
   : Task.FromResult<UpdateManifest?>(null);
  await Task.WhenAll(globalTask,targetedTask);
  var global=await globalTask;
  var targeted=await targetedTask;
  return SelectManifest(clientCode,currentVersion,global,targeted);
 }
 private static UpdateManifest? SelectManifest(string clientCode,string currentVersion,UpdateManifest? global,UpdateManifest? targeted)
 {
  // A client build never changes distribution channel because of a newer global number.
  if(DistributionChannel!="GLOBAL" && !ClientUpdateIdentity.ManifestMatches(DistributionChannel,clientCode))return null;
  bool validTargeted=targeted!=null && ClientUpdateIdentity.ManifestMatches(clientCode,targeted.TargetClientCode) && IsSafeClientPackage(targeted,clientCode);
  if(validTargeted)return IsNewer(targeted!.Version,currentVersion)?targeted:null;
  // Published personalized channels fail closed if their feed is unavailable or invalid.
  // A neutral build remains able to serve other clients through GLOBAL.
  if(DistributionChannel!="GLOBAL" || clientCode is "001" or "002" or "003" or "004")return null;
  return global!=null && string.IsNullOrEmpty(global.TargetClientCode) && IsSafeGlobalPackage(global) && IsNewer(global.Version,currentVersion)?global:null;
 }
 private static bool IsValidManifest(UpdateManifest manifest)=>
  Version.TryParse(manifest.Version,out _) && System.Text.RegularExpressions.Regex.IsMatch(manifest.Sha256??"",@"\A[0-9a-fA-F]{64}\z");
 private static bool IsSafeGlobalPackage(UpdateManifest manifest)
 {
  if(!Uri.TryCreate(manifest.PackageUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com")return false;
  return uri.AbsolutePath.StartsWith("/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v",StringComparison.OrdinalIgnoreCase)&&IsValidManifest(manifest);
 }
 private static async Task<UpdateManifest?> TryLoadManifestAsync(HttpClient http,string url)
 {
  for(int attempt=0;attempt<3;attempt++)
  {
   try
   {
    using var request=new HttpRequestMessage(HttpMethod.Get,url+"?t="+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    request.Headers.CacheControl=new CacheControlHeaderValue{NoCache=true,NoStore=true};
    using var response=await http.SendAsync(request);
    if(response.IsSuccessStatusCode)
    {
     var json=await response.Content.ReadAsStringAsync();
     return JsonSerializer.Deserialize<UpdateManifest>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
    }
    LogCheck(url,$"HTTP {(int)response.StatusCode}; tentativa {attempt+1}");
    // 404 significa canal ausente. Erros permanentes nao merecem novas consultas.
    if(response.StatusCode!=HttpStatusCode.RequestTimeout && (int)response.StatusCode!=429 && (int)response.StatusCode<500)return null;
   }
   catch(JsonException){LogCheck(url,"Manifesto invalido; consulta recusada");return null;}
   catch(HttpRequestException){LogCheck(url,$"Falha de rede; tentativa {attempt+1}");}
   catch(TaskCanceledException){LogCheck(url,$"Timeout; tentativa {attempt+1}");}
   if(attempt<2)await Task.Delay(attempt==0?2000:5000);
  }
  return null;
 }
 private static void LogCheck(string url,string message)
 {
  try
  {
   Directory.CreateDirectory(UpdatesFolder);
   var file=Path.Combine(UpdatesFolder,"update-check.log");
   if(File.Exists(file)&&new FileInfo(file).Length>262144)File.WriteAllText(file,"");
   File.AppendAllText(file,$"{DateTimeOffset.UtcNow:O} {new Uri(url).AbsolutePath}: {message}{Environment.NewLine}");
  }
  catch { /* Diagnostico nunca deve impedir abertura ou atualizacao. */ }
 }
 private static string GetClientCode()
 {
  var code=Licensing.InstallationLicense.Current?.ClientCode??"";
  if(ClientUpdateIdentity.IsValidClientCode(code))return code;
  try
  {
   using var cn=Database.Open();using var cmd=cn.CreateCommand();
   cmd.CommandText="SELECT value FROM settings WHERE key='client_update_id'";
   return Convert.ToString(cmd.ExecuteScalar())??"";
  }
  catch{return "";}
 }
 private static bool IsSafeClientPackage(UpdateManifest manifest,string clientCode)
 {
  if(!Uri.TryCreate(manifest.PackageUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com")return false;
  var prefix=$"/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-{clientCode}-v";
  return uri.AbsolutePath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&IsValidManifest(manifest);
 }

 private static bool IsNewer(string candidate,string current)=>Version.TryParse(candidate.TrimStart('v','V'),out var n)&&Version.TryParse(current,out var c)&&n>c;
 private static async Task DownloadAndApplyAsync(UpdateManifest m)
 {
  var work=Path.Combine(Path.GetTempPath(),"LealInfoPDV_Update_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
  var zip=Path.Combine(work,"update.zip");
  using(var http=new HttpClient{Timeout=TimeSpan.FromMinutes(5)})
  {
   http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/"+CurrentVersion);
   var bytes=await http.GetByteArrayAsync(m.PackageUrl);await File.WriteAllBytesAsync(zip,bytes);
  }
  if(m.Sha256.Length!=64 || !System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(zip)).SequenceEqual(Convert.FromHexString(m.Sha256)))
   throw new InvalidDataException("O pacote não corresponde à atualização publicada.");
  Licensing.UpdatePayloadPolicy.Validate(zip);
  await TransactionalUpdate.BeginAsync(zip,m.Sha256,m.Version,DistributionChannel);
 }
}



