using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
namespace LealInfoPDV;
internal static class UpdateManager
{
 public const string CurrentVersion="10.141";
 private const string DefaultFeedUrl="https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json";
 private static readonly string UpdatesFolder=Path.Combine(Database.AppFolder,"Updates"),LocalManifest=Path.Combine(UpdatesFolder,"manifest.json"),FeedConfig=Path.Combine(UpdatesFolder,"feed.txt");
 private sealed class UpdateManifest{public string Version{get;set;}="";public string PackageUrl{get;set;}="";public string Sha256{get;set;}="";public string Notes{get;set;}="";}
 public static async Task CheckForUpdatesAsync(IWin32Window? owner,bool silent){try{Directory.CreateDirectory(UpdatesFolder);var manifest=await LoadManifestAsync();if(manifest==null){if(!silent)MessageBox.Show(owner,"O atualizador está instalado e funcionando.\n\nNenhuma publicação de atualização foi encontrada agora.","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}if(!IsNewer(manifest.Version,CurrentVersion)){if(!silent)MessageBox.Show(owner,$"Seu LEAL INFO PDV já está atualizado.\n\nVersão instalada: V{CurrentVersion}","Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}var notes=string.IsNullOrWhiteSpace(manifest.Notes)?"Melhorias e correções do sistema.":manifest.Notes.Trim();var result=MessageBox.Show(owner,$"NOVA ATUALIZAÇÃO DISPONÍVEL\n\nVersão instalada: V{CurrentVersion}\nNova versão: V{manifest.Version}\n\n{notes}\n\nDeseja atualizar agora?","Atualização disponível",MessageBoxButtons.YesNo,MessageBoxIcon.Information);if(result==DialogResult.Yes)await DownloadAndApplyAsync(manifest,owner);}catch(Exception ex){if(!silent)MessageBox.Show(owner,"Não foi possível verificar atualizações agora.\n\n"+ex.Message,"Atualizações do LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
 public static void ShowUpdateCenter(IWin32Window owner){using var f=new Form{Text="Atualizações do LEAL INFO PDV",StartPosition=FormStartPosition.CenterParent,Width=640,Height=410,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(7,31,52),Font=new Font("Segoe UI",10)};var title=new Label{Text="CENTRAL DE ATUALIZAÇÕES",ForeColor=Color.White,Font=new Font("Segoe UI",20,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=74};f.Controls.Add(title);var version=new Label{Text=$"VERSÃO INSTALADA  •  V{CurrentVersion}",ForeColor=Color.FromArgb(74,215,255),Font=new Font("Segoe UI",13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=44};f.Controls.Add(version);var info=new Label{Text="O LEAL INFO PDV pode procurar novas versões e instalar a atualização sem reinstalar o sistema.\nSeus cadastros e banco de dados permanecem preservados.",ForeColor=Color.Gainsboro,TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=78,Padding=new Padding(35,8,35,0)};f.Controls.Add(info);var status=new Label{Text="● ATUALIZAÇÃO AUTOMÁTICA ATIVA",ForeColor=Color.FromArgb(105,240,170),Font=new Font("Segoe UI",11,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=42};f.Controls.Add(status);var check=new Button{Text="VERIFICAR ATUALIZAÇÕES AGORA",Width=310,Height=48,Left=155,Top=258,BackColor=Color.FromArgb(0,133,194),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold),Cursor=Cursors.Hand};check.FlatAppearance.BorderSize=0;check.Click+=async(_,_)=>{check.Enabled=false;check.Text="VERIFICANDO...";await CheckForUpdatesAsync(f,false);if(!f.IsDisposed){check.Text="VERIFICAR ATUALIZAÇÕES AGORA";check.Enabled=true;}};f.Controls.Add(check);var close=new Button{Text="FECHAR",Width=130,Height=38,Left=245,Top=320,BackColor=Color.FromArgb(32,49,65),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};close.FlatAppearance.BorderColor=Color.FromArgb(80,120,145);close.Click+=(_,_)=>f.Close();f.Controls.Add(close);f.ShowDialog(owner);}
 private static async Task<UpdateManifest?> LoadManifestAsync(){if(File.Exists(LocalManifest)){var localJson=await File.ReadAllTextAsync(LocalManifest);return JsonSerializer.Deserialize<UpdateManifest>(localJson,JsonOptions());}var feed=DefaultFeedUrl;if(File.Exists(FeedConfig)){var configured=(await File.ReadAllTextAsync(FeedConfig)).Trim();if(!string.IsNullOrWhiteSpace(configured))feed=configured;}using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(7)};http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/10.141");using var response=await http.GetAsync(feed);if(!response.IsSuccessStatusCode)return null;var json=await response.Content.ReadAsStringAsync();return JsonSerializer.Deserialize<UpdateManifest>(json,JsonOptions());}
 private static JsonSerializerOptions JsonOptions()=>new(){PropertyNameCaseInsensitive=true};
 private static bool IsNewer(string candidate,string current){if(!Version.TryParse(candidate.TrimStart('v','V'),out var n))return false;if(!Version.TryParse(current.TrimStart('v','V'),out var c))return false;return n>c;}
 private static async Task DownloadAndApplyAsync(UpdateManifest manifest,IWin32Window? owner){if(string.IsNullOrWhiteSpace(manifest.PackageUrl))throw new InvalidOperationException("O manifesto da atualização não informou o pacote de instalação.");var work=Path.Combine(Path.GetTempPath(),"LealInfoPDV_Update_"+Guid.NewGuid().ToString("N"));var zip=Path.Combine(work,"update.zip");var stage=Path.Combine(work,"payload");Directory.CreateDirectory(work);Directory.CreateDirectory(stage);if(Uri.TryCreate(manifest.PackageUrl,UriKind.Absolute,out var uri)&&(uri.Scheme=="http"||uri.Scheme=="https")){using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(4)};http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater/10.141");var bytes=await http.GetByteArrayAsync(uri);await File.WriteAllBytesAsync(zip,bytes);}else{var source=Path.IsPathRooted(manifest.PackageUrl)?manifest.PackageUrl:Path.Combine(UpdatesFolder,manifest.PackageUrl);if(!File.Exists(source))throw new FileNotFoundException("Pacote de atualização não encontrado.",source);File.Copy(source,zip,true);}if(!string.IsNullOrWhiteSpace(manifest.Sha256)){using var sha=SHA256.Create();await using var fs=File.OpenRead(zip);var hash=Convert.ToHexString(await sha.ComputeHashAsync(fs));if(!hash.Equals(manifest.Sha256.Replace(" ",""),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A verificação de segurança do pacote falhou (SHA-256 diferente).");}ZipFile.ExtractToDirectory(zip,stage,true);var exe=Environment.ProcessPath??Path.Combine(AppContext.BaseDirectory,"LealInfoPDV.exe");var appDir=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);var backupDir=Path.Combine(Database.AppFolder,"UpdateBackups","V"+CurrentVersion+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(backupDir);var ps=Path.Combine(work,"apply-update.ps1");var log=Path.Combine(Database.AppFolder,"Updates","apply-update.log");var script=$$"""
$ErrorActionPreference = 'Stop'
$pidToWait = {{Environment.ProcessId}}
$stage = '{{EscapePs(stage)}}'
$app = '{{EscapePs(appDir)}}'
$backup = '{{EscapePs(backupDir)}}'
$exe = '{{EscapePs(exe)}}'
$log = '{{EscapePs(log)}}'
try {
 "$(Get-Date -Format s) - Iniciando atualização" | Out-File -FilePath $log -Encoding UTF8 -Append
 try { Wait-Process -Id $pidToWait -Timeout 30 -ErrorAction SilentlyContinue } catch {}
 Start-Sleep -Milliseconds 1200
 Get-ChildItem -Path $stage -File -Recurse | ForEach-Object {
  $rel = $_.FullName.Substring($stage.Length).TrimStart('\\')
  $dest = Join-Path $app $rel
  if (Test-Path $dest) { $b = Join-Path $backup $rel; New-Item -ItemType Directory -Force -Path (Split-Path $b) | Out-Null; Copy-Item -Force $dest $b }
  New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
  Copy-Item -Force $_.FullName $dest
 }
 "$(Get-Date -Format s) - Arquivos atualizados com sucesso" | Out-File -FilePath $log -Encoding UTF8 -Append
 Start-Process -FilePath $exe -WorkingDirectory $app
} catch { "$(Get-Date -Format s) - ERRO: $($_.Exception.Message)" | Out-File -FilePath $log -Encoding UTF8 -Append; throw }
""";await File.WriteAllTextAsync(ps,script);try{Process.Start(new ProcessStartInfo{FileName="powershell.exe",Arguments=$"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps}\"",UseShellExecute=true,Verb="runas",WorkingDirectory=work});}catch(System.ComponentModel.Win32Exception ex)when(ex.NativeErrorCode==1223){throw new InvalidOperationException("A atualização precisa da autorização do Windows. Clique em SIM na janela de Controle de Conta de Usuário para concluir.");}MessageBox.Show(owner,"A atualização foi preparada.\n\nO PDV será fechado, os arquivos serão atualizados e o sistema abrirá novamente automaticamente.\n\nSe o Windows pedir autorização, clique em SIM.","LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);Application.Exit();}
 private static string EscapePs(string value)=>value.Replace("'","''");
}
