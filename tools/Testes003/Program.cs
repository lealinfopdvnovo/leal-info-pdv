using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
class Core {
 public const string Url="https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.457-casa/LEAL_INFO_PDV_CASA_V10_457_UPDATE.zip";
 public const string PackageHash="869871bdb0065cbc7fc00f2d3cba61194ecea32a9dbbaae0367c3ec717a65ed8";
 public static string[] Names={"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"};
 public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 public static string HashFile(string p){using(var h=SHA256.Create())using(var stream=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 public static void NoLinks(string path){for(var p=Path.GetFullPath(path);!String.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)){if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Pasta redirecionada recusada.");}}
 public static void Closed(string app){foreach(var p in Process.GetProcessesByName("LealInfoPDV"))using(p){string exe;try{exe=p.MainModule.FileName;}catch{throw new IOException("Não foi possível verificar se o PDV está fechado.");}if(String.Equals(Path.GetFullPath(exe),Path.Combine(app,"LealInfoPDV.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("Feche o PDV antes de preparar ou recuperar.");}}
 public static Dictionary<string,string> Validate(string zip){
  if(HashFile(zip)!=PackageHash)throw new IOException("Integridade da versão anterior inválida.");
  var hashes=new Dictionary<string,string>();using(var z=ZipFile.OpenRead(zip)){
   if(z.Entries.Count!=3||z.Entries.Any(e=>!Names.Contains(e.FullName))||z.Entries.Select(e=>e.FullName).Distinct().Count()!=3)throw new IOException("Pacote fora do escopo do 003.");
   foreach(var e in z.Entries){if(e.Length>512L*1024*1024)throw new IOException("Arquivo excede limite.");using(var s=e.Open())using(var b=new MemoryStream()){s.CopyTo(b);hashes[e.FullName]=Hash(b.ToArray());}}
  }return hashes;
 }
 public static string Root(string app){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LEAL INFO PDV Testes","003",Hash(Encoding.UTF8.GetBytes(app.ToUpperInvariant())).Substring(0,16));}
 public static async Task<string> Prepare(string app){
  app=Path.GetFullPath(app).TrimEnd(Path.DirectorySeparatorChar);NoLinks(app);Closed(app);
  var root=Root(app);NoLinks(root);Directory.CreateDirectory(root);
  var candidate=Path.Combine(root,Guid.NewGuid().ToString("N")+".zip");
  using(var http=new HttpClient()){http.Timeout=TimeSpan.FromMinutes(3);File.WriteAllBytes(candidate,await http.GetByteArrayAsync(Url));}
  try{
   var hashes=Validate(candidate);
   foreach(var n in Names){NoLinks(Path.Combine(app,n));if(!File.Exists(Path.Combine(app,n))||HashFile(Path.Combine(app,n))!=hashes[n])throw new IOException("A instalação não corresponde à base publicada do 003 10.457. Nenhum arquivo foi alterado.");}
   Closed(app);
   var zip=Path.Combine(root,"previous-003-10.457.zip");
   if(File.Exists(zip)){Validate(zip);File.Delete(candidate);}else File.Move(candidate,zip);
   var state=new Dictionary<string,object>{{"Client","003"},{"App",app},{"Version","10.457"},{"PackageSha256",PackageHash},{"Files",hashes},{"PreparedUtc",DateTime.UtcNow.ToString("o")}};
   var pending=Path.Combine(root,"prepared.json.tmp");File.WriteAllText(pending,new JavaScriptSerializer().Serialize(state));
   var target=Path.Combine(root,"prepared.json");if(File.Exists(target))File.Replace(pending,target,null);else File.Move(pending,target);
   return root;
  }finally{if(File.Exists(candidate))File.Delete(candidate);}
 }
 public static string Ready(string app){
  app=Path.GetFullPath(app).TrimEnd(Path.DirectorySeparatorChar);NoLinks(app);Closed(app);var root=Root(app);NoLinks(root);
  var state=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"prepared.json")));
  if((string)state["Client"]!="003"||(string)state["App"]!=app||(string)state["Version"]!="10.457"||(string)state["PackageSha256"]!=PackageHash)throw new IOException("Cópia não pertence a este PDV 003.");
  Validate(Path.Combine(root,"previous-003-10.457.zip"));return root;
 }
 public static byte[] Worker(){using(var s=typeof(Core).Assembly.GetManifestResourceStream("Worker"))using(var b=new MemoryStream()){if(s==null)throw new IOException("Proteção externa ausente.");s.CopyTo(b);return b.ToArray();}}
 static string Q(string s){return "'"+s.Replace("'","''")+"'";}
 public static Process Recover(string app,string nonce){
  var root=Ready(app);var zip=Path.Combine(root,"previous-003-10.457.zip");
  var trusted=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!app.StartsWith(trusted,StringComparison.OrdinalIgnoreCase))throw new IOException("A recuperação exige a instalação protegida em Arquivos de Programas.");
  var temp=Path.Combine(Path.GetTempPath(),"LealTests003-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  var worker=Path.Combine(temp,"worker.ps1");File.WriteAllBytes(worker,Worker());
  var dest=Path.Combine(app,".pdv-update","test003-"+nonce+".ps1");
  var current=Process.GetCurrentProcess();
  var script="$ErrorActionPreference='Stop'\n"+
   "$app="+Q(app)+";$src="+Q(worker)+";$dest="+Q(dest)+"\n"+
   "if((Get-FileHash -LiteralPath $src).Hash.ToLowerInvariant() -ne '"+HashFile(worker)+"'){throw 'Worker integrity'}\n"+
   "if((Get-FileHash -LiteralPath "+Q(zip)+").Hash.ToLowerInvariant() -ne '"+PackageHash+"'){throw 'Backup integrity'}\n"+
   "for($p=$app;$p;$p=[IO.Path]::GetDirectoryName($p)){if((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Redirected installation'}}\n"+
   "$r=Join-Path $app '.pdv-update';if(Test-Path $r){if((Get-Item -LiteralPath $r).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Redirected worker root'}}else{New-Item -ItemType Directory $r|Out-Null}\n"+
   "if(@(Get-ChildItem -LiteralPath $r -Recurse -Force|Where-Object{($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count){throw 'Redirected transaction member'}\n"+
   "[IO.File]::Copy($src,$dest,$false)\n"+
   "& $dest -Mode PrepareApply -App $app -Package "+Q(zip)+" -Sha256 '"+PackageHash+"' -Version 10.457 -Channel 003 -PreviousPid "+current.Id+" -PreviousStarted "+current.StartTime.ToUniversalTime().Ticks+" -Nonce '"+nonce+"'\nexit $LASTEXITCODE";
  var ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
  return Process.Start(new ProcessStartInfo(ps,"-NoProfile -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
 }
}
class Window:Form {
 NotifyIcon tray;TextBox path;Label status;Button prepare,verify,restore;bool busy;
 public Window(){Text="LEAL INFO PDV Testes — SOMENTE 003";Width=650;Height=340;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(15,25,40);ForeColor=Color.White;Font=new Font("Segoe UI",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
  var title=new Label{Text="TESTE • CASA DO ADRIANO • 003",Left=20,Top=18,Width=600,Height=35,ForeColor=Color.Gold,Font=new Font("Segoe UI",16,FontStyle.Bold)};
  path=new TextBox{Left=20,Top=65,Width=485,Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LEAL INFO PDV")};
  var choose=new Button{Text="Localizar",Left=515,Top=62,Width=100};choose.Click+=delegate{using(var f=new FolderBrowserDialog()){f.Description="Selecione exclusivamente o PDV da CASA DO ADRIANO (003).";if(f.ShowDialog()==DialogResult.OK)path.Text=f.SelectedPath;}};
  status=new Label{Text="Programa separado. O atualizador atual permanece preservado.\nPrepare e confira a cópia com o PDV fechado. Nenhuma falha será provocada.",Left=20,Top=110,Width=590,Height=75};
  prepare=new Button{Text="Preparar cópia anterior",Left=20,Top=200,Width=190,Height=40};verify=new Button{Text="Verificar recuperação",CausesValidation=false,Left=220,Top=200,Width=185,Height=40};restore=new Button{Text="Recuperar anterior",Left=415,Top=200,Width=195,Height=40};
  prepare.Click+=async delegate{if(busy)return;busy=true;Toggle(false);try{await Core.Prepare(path.Text);status.Text="PREPARADO: versão anterior 003 10.457 preservada e verificada.\nNão foi feita instalação nem restauração do PDV.";}catch(Exception e){status.Text=e.Message;}finally{busy=false;Toggle(true);}};
  verify.Click+=async delegate{await VerifyAsync();};
  restore.Click+=async delegate{if(busy)return;if(MessageBox.Show(this,"RECUPERAÇÃO EXCLUSIVA DO 003.\nRestaurar os três arquivos do programa para a cópia preservada 10.457?\nBanco, licença e configurações não serão substituídos.","TESTE 003 — confirmação",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;busy=true;Toggle(false);try{var app=Path.GetFullPath(path.Text).TrimEnd(Path.DirectorySeparatorChar);var nonce=Guid.NewGuid().ToString("N");using(var p=Core.Recover(app,nonce)){var deadline=DateTime.UtcNow.AddSeconds(60);while(DateTime.UtcNow<deadline){if(p.HasExited)throw new IOException("A proteção recusou a recuperação. O PDV não foi fechado por este programa.");var ready=Path.Combine(app,".pdv-update","prepared.ready");if(File.Exists(ready)&&File.ReadAllText(ready)==nonce){tray.Visible=false;Application.Exit();return;}await Task.Delay(200);}throw new IOException("Preparação não confirmada. Não foi autorizado substituir o PDV.");}}catch(Exception e){status.Text=e.Message;}finally{busy=false;Toggle(true);}};
  Controls.AddRange(new Control[]{title,path,choose,status,prepare,verify,restore});
  tray=new NotifyIcon{Icon=SystemIcons.Information,Text="LEAL INFO PDV Testes — 003",Visible=true};var menu=new ContextMenuStrip();menu.Items.Add("Abrir TESTES 003",null,delegate{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("Sair de TESTES",null,delegate{if(!busy)Application.Exit();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Show();Activate();};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"TESTES 003","O programa continua perto do relógio. O PDV não precisa abrir.",ToolTipIcon.Info);}};FormClosed+=delegate{tray.Dispose();};
 }
 internal string VerificationStatus { get { return status.Text; } }
 internal bool VerificationBusy { get { return busy; } }
 internal void SetVerificationPath(string value){path.Text=value;}
 internal void ClickVerify(){verify.PerformClick();}
 internal string ButtonState { get { return "enabled="+verify.Enabled+" select="+verify.CanSelect+" visible="+verify.Visible+" active="+(ActiveControl==null?"null":ActiveControl.GetType().Name); } }
 internal async Task VerifyAsync(){
  if(busy)return;
  var selected=path.Text;
  busy=true;Toggle(false);UseWaitCursor=true;
  status.Text="VERIFICANDO A CÓPIA ANTERIOR... Aguarde a conferência de integridade.";
  status.Refresh();
  try{
   await Task.Run(delegate{Core.Ready(selected);});
   status.Text="CÓPIA ÍNTEGRA • 003 10.457 • PDV FECHADO.\nVerificação concluída. Nenhum arquivo do PDV foi alterado.";
   status.ForeColor=Color.LightGreen;
  }catch(Exception e){
   status.Text="VERIFICAÇÃO NÃO APROVADA: "+e.Message;
   status.ForeColor=Color.Salmon;
  }finally{UseWaitCursor=false;Toggle(true);status.Refresh();busy=false;}
 }
 void Toggle(bool value){prepare.Enabled=verify.Enabled=restore.Enabled=path.Enabled=value;}
}
class Program {
 [STAThread]static int Main(string[] args){System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0&&args[0]=="--self-test")return Test.Run();Application.Run(new Window());return 0;
 }
}
