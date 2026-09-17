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
using System.Security.Cryptography;
using System.Text.Json;
namespace LealInfoPDV;
internal static class UpdateManager
{
 public const string CurrentVersion="10.264";
 private const string LatestReleaseApi="https://api.github.com/repos/lealinfopdvnovo/leal-info-pdv-updates/releases/latest";
 private static readonly string UpdatesFolder=Path.Combine(Database.AppFolder,"Updates");
 private sealed class UpdateManifest{public string Version{get;set;}="";public string PackageUrl{get;set;}="";public string Sha256{get;set;}="";public string Notes{get;set;}="";}
 private sealed class GitHubRelease{public string tag_name{get;set;}="";public string body{get;set;}="";public GitHubAsset[] assets{get;set;}=Array.Empty<GitHubAsset>();}
 private sealed class GitHubAsset{public string name{get;set;}="";public string browser_download_url{get;set;}="";}
 public static async Task CheckForUpdatesAsync(IWin32Window? owner,bool silent)
 {
  try
  {
   Directory.CreateDirectory(UpdatesFolder);var manifest=await LoadManifestAsync();
   if(manifest==null){if(!silent)MessageBox.Show(owner,"O servidor de atualizações está disponível, mas nenhuma versão publicada foi encontrada agora.","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   if(!IsNewer(manifest.Version,CurrentVersion)){if(!silent)MessageBox.Show(owner,$"Seu LEAL INFO PDV já está atualizado.\n\nVersão instalada: V{CurrentVersion}","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   var notes=string.IsNullOrWhiteSpace(manifest.Notes)?"Melhorias e correções do sistema.":manifest.Notes.Trim();
   var result=MessageBox.Show(owner,$"NOVA ATUALIZAÇÃO DISPONÍVEL\n\nVersão instalada: V{CurrentVersion}\nNova versão: V{manifest.Version}\n\n{notes}\n\nDeseja atualizar agora?","Atualização disponível",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
   if(result==DialogResult.Yes)await DownloadAndApplyAsync(manifest,owner);
  }
  catch(HttpRequestException){if(!silent)MessageBox.Show(owner,"Não foi possível acessar o servidor de atualizações.\n\nVerifique sua conexão com a internet e tente novamente.","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  catch(TaskCanceledException){if(!silent)MessageBox.Show(owner,"O servidor de atualizações demorou para responder.\n\nTente novamente em alguns instantes.","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  catch(Exception){if(!silent)MessageBox.Show(owner,"Não foi possível verificar as atualizações neste momento.","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }
 public static void ShowUpdateCenter(IWin32Window owner){using var f=new Form{Text="Atualizações do LEAL INFO PDV",StartPosition=FormStartPosition.CenterParent,Width=640,Height=410,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(7,31,52),Font=new Font("Segoe UI",10)};var title=new Label{Text="CENTRAL DE ATUALIZAÇÕES",ForeColor=Color.White,Font=new Font("Segoe UI",20,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=74};f.Controls.Add(title);var version=new Label{Text=$"VERSÃO INSTALADA  •  V{CurrentVersion}",ForeColor=Color.FromArgb(74,215,255),Font=new Font("Segoe UI",13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=44};f.Controls.Add(version);var info=new Label{Text="O LEAL INFO PDV pode procurar novas versões e instalar a atualização sem reinstalar o sistema.\nSeus cadastros e banco de dados permanecem preservados.",ForeColor=Color.Gainsboro,TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=78,Padding=new Padding(35,8,35,0)};f.Controls.Add(info);var status=new Label{Text="● ATUALIZAÇÃO AUTOMÁTICA ATIVA",ForeColor=Color.FromArgb(105,240,170),Font=new Font("Segoe UI",11,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=42};f.Controls.Add(status);var check=new Button{Text="VERIFICAR ATUALIZAÇÕES AGORA",Width=310,Height=48,Left=155,Top=258,BackColor=Color.FromArgb(0,133,194),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold),Cursor=Cursors.Hand};check.FlatAppearance.BorderSize=0;check.Click+=async(_,_)=>{check.Enabled=false;check.Text="VERIFICANDO...";await CheckForUpdatesAsync(f,false);if(!f.IsDisposed){check.Text="VERIFICAR ATUALIZAÇÕES AGORA";check.Enabled=true;}};f.Controls.Add(check);var close=new Button{Text="FECHAR",Width=130,Height=38,Left=245,Top=320,BackColor=Color.FromArgb(32,49,65),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};close.FlatAppearance.BorderColor=Color.FromArgb(80,120,145);close.Click+=(_,_)=>f.Close();f.Controls.Add(close);f.ShowDialog(owner);}
 private static async Task<UpdateManifest?> LoadManifestAsync()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};
  http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/10.264");
  http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
  http.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2022-11-28");
  using var response=await http.GetAsync(LatestReleaseApi);
  if(response.StatusCode==HttpStatusCode.NotFound)return null;
  response.EnsureSuccessStatusCode();
  var json=await response.Content.ReadAsStringAsync();
  var release=JsonSerializer.Deserialize<GitHubRelease>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
  if(release==null||string.IsNullOrWhiteSpace(release.tag_name))return null;
  var zip=release.assets.FirstOrDefault(a=>a.name.EndsWith("_UPDATE.zip",StringComparison.OrdinalIgnoreCase));
  if(zip==null||string.IsNullOrWhiteSpace(zip.browser_download_url))throw new InvalidOperationException("O Release foi encontrado, mas o pacote ZIP ainda não está disponível.");
  return new UpdateManifest{Version=release.tag_name.TrimStart('v','V'),PackageUrl=zip.browser_download_url,Notes=release.body??""};
 }
 private static bool IsNewer(string candidate,string current){if(!Version.TryParse(candidate.TrimStart('v','V'),out var n))return false;if(!Version.TryParse(current.TrimStart('v','V'),out var c))return false;return n>c;}
 private static Form CriarProgresso(string versao,out Label status,out ProgressBar barra,out Label percentual){var f=new Form{Text="Atualizando LEAL INFO PDV",StartPosition=FormStartPosition.CenterScreen,Width=520,Height=225,FormBorderStyle=FormBorderStyle.FixedDialog,ControlBox=false,TopMost=true,BackColor=Color.FromArgb(7,31,52),Font=new Font("Segoe UI",10)};var titulo=new Label{Text=$"ATUALIZAÇÃO V{versao}",Dock=DockStyle.Top,Height=62,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",17,FontStyle.Bold)};status=new Label{Text="PREPARANDO DOWNLOAD...",Left=35,Top=72,Width=435,Height=30,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Gainsboro};barra=new ProgressBar{Left=45,Top=112,Width=410,Height=24,Minimum=0,Maximum=100,Value=0,Style=ProgressBarStyle.Continuous};percentual=new Label{Text="0%",Left=35,Top=143,Width=435,Height=28,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(74,215,255),Font=new Font("Segoe UI",11,FontStyle.Bold)};f.Controls.AddRange(new Control[]{titulo,status,barra,percentual});return f;}
 private static async Task DownloadAndApplyAsync(UpdateManifest manifest,IWin32Window? owner){if(string.IsNullOrWhiteSpace(manifest.PackageUrl))throw new InvalidOperationException("O manifesto da atualização não informou o pacote de instalação.");using var progresso=CriarProgresso(manifest.Version,out var status,out var barra,out var percentual);progresso.Show();progresso.Refresh();void Atualizar(int p,string texto){p=Math.Clamp(p,0,100);barra.Value=p;percentual.Text=$"{p}%";status.Text=texto;progresso.Refresh();Application.DoEvents();}var work=Path.Combine(Path.GetTempPath(),"LealInfoPDV_Update_"+Guid.NewGuid().ToString("N"));var zip=Path.Combine(work,"update.zip");var stage=Path.Combine(work,"payload");Directory.CreateDirectory(work);Directory.CreateDirectory(stage);using(var http=new HttpClient{Timeout=TimeSpan.FromMinutes(4)}){http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/10.264");using var response=await http.GetAsync(manifest.PackageUrl,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();var total=response.Content.Headers.ContentLength;await using var origem=await response.Content.ReadAsStreamAsync();await using var destino=new FileStream(zip,FileMode.Create,FileAccess.Write,FileShare.None,81920,true);var buffer=new byte[81920];long recebido=0;int lido;while((lido=await origem.ReadAsync(buffer))>0){await destino.WriteAsync(buffer.AsMemory(0,lido));recebido+=lido;if(total.HasValue&&total.Value>0)Atualizar((int)Math.Min(90,recebido*90/total.Value),"BAIXANDO ATUALIZAÇÃO...");}}Atualizar(92,"DOWNLOAD CONCLUÍDO. PREPARANDO...");ZipFile.ExtractToDirectory(zip,stage,true);var exe=Environment.ProcessPath??Path.Combine(AppContext.BaseDirectory,"LealInfoPDV.exe");var appDir=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);var backupDir=Path.Combine(Database.AppFolder,"UpdateBackups","V"+CurrentVersion+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(backupDir);var ps=Path.Combine(work,"apply-update.ps1");var log=Path.Combine(Database.AppFolder,"Updates","apply-update.log");var script=$$"""
$ErrorActionPreference = 'Stop'
$pidToWait = {{Environment.ProcessId}}
$stage = '{{EscapePs(stage)}}'
$app = '{{EscapePs(appDir)}}'
$backup = '{{EscapePs(backupDir)}}'
$exe = '{{EscapePs(exe)}}'
$log = '{{EscapePs(log)}}'
try {
 $deadline=(Get-Date).AddSeconds(45)
 while ((Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $deadline)) { Start-Sleep -Milliseconds 250 }
 Get-ChildItem -Path $app -File | Where-Object { $_.Name -match '\.(exe|dll|deps\.json|runtimeconfig\.json)$' } | ForEach-Object { Copy-Item $_.FullName -Destination $backup -Force -ErrorAction SilentlyContinue }
 Copy-Item -Path (Join-Path $stage '*') -Destination $app -Recurse -Force
 Start-Process -FilePath $exe -WorkingDirectory $app
} catch {
 "$(Get-Date -Format s) - ERRO: $($_.Exception.Message)" | Out-File -FilePath $log -Encoding UTF8 -Append
 try { Copy-Item -Path (Join-Path $backup '*') -Destination $app -Force -ErrorAction SilentlyContinue } catch {}
 try { Start-Process -FilePath $exe -WorkingDirectory $app } catch {}
}
""";await File.WriteAllTextAsync(ps,script);Atualizar(100,"PRONTO. REINICIANDO O SISTEMA...");await Task.Delay(600);Process.Start(new ProcessStartInfo("powershell.exe",$"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps}\""){UseShellExecute=true,WorkingDirectory=work});progresso.Close();Application.Exit();}
 private static string EscapePs(string value)=>value.Replace("'","''");
}
