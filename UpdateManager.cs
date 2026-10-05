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
    public const string CurrentVersion="10.368";
 private const string LatestReleaseApi="https://api.github.com/repos/lealinfopdvnovo/leal-info-pdv-updates/releases/latest";
 private static readonly string UpdatesFolder=Path.Combine(Database.AppFolder,"Updates");
 private sealed class UpdateManifest{public string Version{get;set;}="";public string PackageUrl{get;set;}="";public string Notes{get;set;}="";public string Sha256{get;set;}="";public string SourceCommit{get;set;}="";public string TargetClientCode{get;set;}="";}
 private sealed class GitHubRelease{public string tag_name{get;set;}="";public string body{get;set;}="";public GitHubAsset[] assets{get;set;}=Array.Empty<GitHubAsset>();}
 private sealed class GitHubAsset{public string name{get;set;}="";public string browser_download_url{get;set;}="";}
 public static async Task CheckForUpdatesAsync(IWin32Window? owner,bool silent){try{Directory.CreateDirectory(UpdatesFolder);var manifest=await LoadManifestAsync();if(manifest==null){if(!silent)MessageBox.Show(owner,"Nenhuma versão publicada foi encontrada agora.","Atualizações do LEAL INFO PDV");return;}if(!IsNewer(manifest.Version,CurrentVersion)){if(!silent)MessageBox.Show(owner,$"Seu LEAL INFO PDV já está atualizado.\n\nVersão instalada: V{CurrentVersion} • {Licensing.InstallationLicense.Edition}","Atualizações do LEAL INFO PDV");return;}var result=MessageBox.Show(owner,$"NOVA ATUALIZAÇÃO DISPONÍVEL\n\nVersão instalada: V{CurrentVersion} • {Licensing.InstallationLicense.Edition}\nNova versão: V{manifest.Version}\n\n{manifest.Notes}\n\nDeseja atualizar agora?","Atualização disponível",MessageBoxButtons.YesNo,MessageBoxIcon.Information);if(result==DialogResult.Yes)await DownloadAndApplyAsync(manifest);}catch(Exception ex){if(!silent)MessageBox.Show(owner,"Não foi possível atualizar agora.\n\n"+ex.Message,"Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
 public static async Task ShowUpdateCenterAsync(IWin32Window owner){await CheckForUpdatesAsync(owner,false);}
 public static void ShowUpdateCenter(IWin32Window owner){_ = ShowUpdateCenterAsync(owner);}
 private static async Task<UpdateManifest?> LoadManifestAsync()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};
  http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/"+CurrentVersion);
  var clientCode=GetClientCode();
  if(ClientUpdateIdentity.GetManifestPath(clientCode) is string path)
  {
   var targeted=await TryLoadManifestAsync(http,"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/"+path);
   if(targeted!=null && ClientUpdateIdentity.ManifestMatches(clientCode,targeted.TargetClientCode) && IsNewer(targeted.Version,CurrentVersion) && IsSafeClientPackage(targeted,clientCode))return targeted;
  }
  return await TryLoadManifestAsync(http,"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json");
 }
 private static async Task<UpdateManifest?> TryLoadManifestAsync(HttpClient http,string url)
 {
  try
  {
   using var request=new HttpRequestMessage(HttpMethod.Get,url+"?t="+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
   request.Headers.CacheControl=new CacheControlHeaderValue{NoCache=true,NoStore=true};
   using var response=await http.SendAsync(request);
   if(!response.IsSuccessStatusCode)return null;
   var json=await response.Content.ReadAsStringAsync();
   return JsonSerializer.Deserialize<UpdateManifest>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
  }
  catch{return null;}
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
  return uri.AbsolutePath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&manifest.Sha256.Length==64;
 }

 private static bool IsNewer(string candidate,string current)=>Version.TryParse(candidate.TrimStart('v','V'),out var n)&&Version.TryParse(current,out var c)&&n>c;
 private static async Task DownloadAndApplyAsync(UpdateManifest m){var work=Path.Combine(Path.GetTempPath(),"LealInfoPDV_Update_"+Guid.NewGuid().ToString("N"));var zip=Path.Combine(work,"update.zip");var stage=Path.Combine(work,"payload");Directory.CreateDirectory(stage);using(var http=new HttpClient{Timeout=TimeSpan.FromMinutes(5)}){http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/10.309");var bytes=await http.GetByteArrayAsync(m.PackageUrl);await File.WriteAllBytesAsync(zip,bytes);}if(m.Sha256.Length!=64 || !System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(zip)).SequenceEqual(Convert.FromHexString(m.Sha256)))throw new InvalidDataException("O pacote não corresponde à atualização publicada.");Licensing.UpdatePayloadPolicy.Validate(zip);ZipFile.ExtractToDirectory(zip,stage,true);var exe=Environment.ProcessPath??Path.Combine(AppContext.BaseDirectory,"LealInfoPDV.exe");var app=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);var ps=Path.Combine(work,"apply-update.ps1");var script=$$"""
$ErrorActionPreference='Stop'
$pidToWait={{Environment.ProcessId}}
$stage='{{EscapePs(stage)}}'
$app='{{EscapePs(app)}}'
$exe='{{EscapePs(exe)}}'
Start-Sleep -Milliseconds 500
Get-Process -Name 'LicAi' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Id $pidToWait -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$limit=(Get-Date).AddSeconds(12)
while((Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) -and (Get-Date) -lt $limit){Start-Sleep -Milliseconds 250}
Get-Process -Id $pidToWait -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700
# Copia somente arquivos aprovados pelo PDV. Nao remove pastas ou dados do cliente.
Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {
  $relative=$_.FullName.Substring($stage.Length).TrimStart('\')
  $destination=Join-Path $app $relative
  New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
  Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}
Start-Process -FilePath $exe -WorkingDirectory $app
""";await File.WriteAllTextAsync(ps,script);Process.Start(new ProcessStartInfo("powershell.exe",$"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps}\""){UseShellExecute=true});Application.Exit();}
 private static string EscapePs(string value)=>value.Replace("'","''");
}
