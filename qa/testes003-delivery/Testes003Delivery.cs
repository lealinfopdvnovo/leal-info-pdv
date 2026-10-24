using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LealInfoPDV;
internal static class Testes003Delivery
{
 const string PreviousHash="869871bdb0065cbc7fc00f2d3cba61194ecea32a9dbbaae0367c3ec717a65ed8";
 static readonly Dictionary<string,string> DeliveryPrevious=JsonSerializer.Deserialize<Dictionary<string,string>>("__DELIVERY_PREVIOUS_HASHES__")!;
 static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
 static void NoLinks(string path){for(string? p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Pasta de testes redirecionada.");}
 static byte[] Resource(string name){using var s=typeof(Testes003Delivery).Assembly.GetManifestResourceStream(name)??throw new IOException("Componente de testes ausente.");using var b=new MemoryStream();s.CopyTo(b);return b.ToArray();}
 internal static string? Prepare(string app,string identity)
 {
  if(identity!="003")return null;
  app=Path.GetFullPath(app).TrimEnd(Path.DirectorySeparatorChar);NoLinks(app);
  var archive=Resource("Testes003.Previous");if(Hash(archive)!=PreviousHash)throw new IOException("Cópia anterior do 003 inválida.");
  using var zip=new ZipArchive(new MemoryStream(archive));
  var names=new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"};
  if(zip.Entries.Count!=3||zip.Entries.Any(x=>!names.Contains(x.FullName)))throw new IOException("Cópia fora do escopo.");
  var hashes=new Dictionary<string,string>();
  foreach(var n in names){using var s=zip.GetEntry(n)!.Open();using var b=new MemoryStream();s.CopyTo(b);hashes[n]=Hash(b.ToArray());}
  // Accept the two explicitly approved 003 predecessors; never a sibling/client build.
  var actual=new Dictionary<string,string>();
  foreach(var n in names){var previous=Path.Combine(app,".pdv-update","previous",n);NoLinks(previous);if(!File.Exists(previous))throw new IOException("Backup protegido do 003 ausente.");actual[n]=Hash(File.ReadAllBytes(previous));}
  if(!names.All(n=>actual[n]==hashes[n])&&!names.All(n=>actual[n]==DeliveryPrevious[n]))throw new IOException("Backup protegido do 003 não corresponde às versões aprovadas. Preparação recusada.");
  var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LEAL INFO PDV Testes","003",Hash(Encoding.UTF8.GetBytes(app.ToUpperInvariant())).Substring(0,16));
  NoLinks(root);Directory.CreateDirectory(root);
  void Store(string name,byte[] bytes){var path=Path.Combine(root,name);NoLinks(path);var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes);f.Flush(true);}File.Move(tmp,path,true);}
  var preserved=Path.Combine(root,"previous-003-10.457.zip");NoLinks(preserved);
  if(File.Exists(preserved)){if(Hash(File.ReadAllBytes(preserved))!=PreviousHash)throw new IOException("Cópia preservada do 003 inválida.");}
  else Store("previous-003-10.457.zip",archive);
  Store("prepared.json",JsonSerializer.SerializeToUtf8Bytes(new{Client="003",App=app,Version="10.457",PackageSha256=PreviousHash,Files=hashes,PreparedUtc=DateTime.UtcNow.ToString("o")}));
  var exe=Resource("Testes003.Agent");var exeHash=Hash(exe);
  var expected=Encoding.ASCII.GetString(Resource("Testes003.AgentHash")).Trim();if(exeHash!=expected)throw new IOException("Integridade do programa de testes inválida.");
  var bin=Path.Combine(root,exeHash);NoLinks(bin);Directory.CreateDirectory(bin);
  var target=Path.Combine(bin,"LEAL_INFO_PDV_Testes_003.exe");NoLinks(target);
  if(!File.Exists(target)){using var file=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(exe);file.Flush(true);}
  if(Hash(File.ReadAllBytes(target))!=exeHash)throw new IOException("Programa de testes local adulterado.");
  return target;
 }
 internal static void Start()
 {
  var id=Licensing.InstallationLicense.Current?.ClientCode??"";
  if(!ClientUpdateIdentity.IsValidClientCode(id)){using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText="SELECT value FROM settings WHERE key='client_update_id'";id=Convert.ToString(cmd.ExecuteScalar())??"";}
  if(id!="003")return;
  try{
   var exe=Prepare(AppContext.BaseDirectory,id)!;
   foreach(var process in Process.GetProcessesByName("LEAL_INFO_PDV_Testes_003"))using(process){try{if(string.Equals(process.MainModule?.FileName,exe,StringComparison.OrdinalIgnoreCase))return;}catch{return;}}
   Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)!});
  }catch(Exception ex){MessageBox.Show("O PDV continua disponível. Não foi possível preparar TESTES 003: "+ex.Message,"TESTES 003",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }
}
