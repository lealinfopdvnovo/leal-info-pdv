using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LealInfoPDV;

// First transition only: a validated capsule travels with the protected apphost.
// The shared business DLL remains identical for every identity. Legacy rollback
// restores the original apphost/DLL; it never reads the newly added PET directory.
internal static class PetBootstrap
{
 const int Trailer=56,Limit=32*1024*1024;
 static readonly byte[] Magic=Encoding.ASCII.GetBytes("LEAL-PET-BOOT-01");
 sealed record Manifest(string Identity,string Version,int ApiVersion,string Assembly,string Sha256);
 static void NoLink(string path){if(Directory.Exists(path)||File.Exists(path))if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Caminho de personalização redirecionado.");}
 static byte[] Read(ZipArchiveEntry entry,int maximum){
  if(entry.Length<=0||entry.Length>maximum)throw new InvalidDataException("Tamanho da personalização inválido.");
  using var source=entry.Open();using var output=new MemoryStream();var buffer=new byte[81920];int count;
  while((count=source.Read(buffer))>0){if(output.Length+count>maximum)throw new InvalidDataException("Personalização excede o limite.");output.Write(buffer,0,count);}
  if(output.Length!=entry.Length)throw new InvalidDataException("Personalização incompleta.");return output.ToArray();
 }
 static void BackupLocalData(){
  if(Network.NetworkConfiguration.Current.Mode=="terminal"||!File.Exists(Database.DbPath))throw new InvalidOperationException("A primeira migração deve ser feita no servidor com banco local disponível.");
  NoLink(Database.AppFolder);NoLink(Database.BackupFolder);Directory.CreateDirectory(Database.BackupFolder);
  string folder=Path.Combine(Database.BackupFolder,"MIGRACAO_PET_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string database=Path.Combine(folder,"lealinfo.db");
  using(var source=Database.Open())using(var target=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+database+";Pooling=False")){target.Open();source.BackupDatabase(target);}
  RestoreEngine.Validate(database);
  var hashes=new Dictionary<string,string>{{"lealinfo.db",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(database)))}};
  foreach(string path in Directory.GetFiles(Database.AppFolder)){
   if(Path.GetExtension(path).ToLowerInvariant() is not (".license" or ".config" or ".identity" or ".protected" or ".dat"))continue;
   NoLink(path);string name=Path.GetFileName(path);byte[] bytes=File.ReadAllBytes(path);
   using(var output=new FileStream(Path.Combine(folder,name),FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes);output.Flush(true);}
   hashes[name]=Convert.ToHexString(SHA256.HashData(bytes));
  }
  foreach(var pair in hashes)if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,pair.Key))))!=pair.Value)throw new InvalidDataException("A cópia anterior à migração não pôde ser verificada.");
  using var index=new FileStream(Path.Combine(folder,"integrity.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(index,hashes);index.Flush(true);
 }
 internal static bool EnsurePresent(string app,string identity,Action<string>? checkpoint=null){
  if(checkpoint!=null){
   string? qaRoot=Environment.GetEnvironmentVariable("RUNNER_TEMP");
   if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||string.IsNullOrEmpty(qaRoot)||!Path.GetFullPath(app).StartsWith(Path.GetFullPath(qaRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(app,"QA-ISOLATED")))throw new InvalidOperationException("Test checkpoint refused");
  }
  app=Path.GetFullPath(app).TrimEnd(Path.DirectorySeparatorChar);NoLink(app);
  string parent=Path.Combine(app,"Extensions"),target=Path.Combine(parent,"PET");NoLink(parent);NoLink(target);
  if(File.Exists(Path.Combine(target,"manifest.json")))return false; // Existing independent PET always wins; loader validates it.
  string executable=Path.Combine(app,"LealInfoPDV.exe");NoLink(executable);
  using var file=new FileStream(executable,FileMode.Open,FileAccess.Read,FileShare.Read);
  if(file.Length<Trailer)return false;
  file.Position=file.Length-Trailer;var trailer=new byte[Trailer];file.ReadExactly(trailer);
  if(!trailer.AsSpan(40,16).SequenceEqual(Magic))return false; // Ordinary neutral apphost has no first-transition capsule.
  long length=BinaryPrimitives.ReadInt64LittleEndian(trailer);
  if(length<=0||length>Limit||file.Length-Trailer-length<64)throw new InvalidDataException("Cápsula de migração inválida.");
  file.Position=file.Length-Trailer-length;var bytes=new byte[(int)length];file.ReadExactly(bytes);
  if(!SHA256.HashData(bytes).AsSpan().SequenceEqual(trailer.AsSpan(8,32)))throw new InvalidDataException("Integridade da cápsula de migração inválida.");
  using var archive=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
  if(archive.Entries.Count!=2||archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=2||archive.Entries.Any(e=>e.FullName is not ("manifest.json" or "Pet.dll")))throw new InvalidDataException("Conteúdo de migração não autorizado.");
  byte[] manifestBytes=Read(archive.GetEntry("manifest.json")!,16384),module=Read(archive.GetEntry("Pet.dll")!,Limit);
  using var reader=new StreamReader(new MemoryStream(manifestBytes),Encoding.UTF8,true);
  var manifest=JsonSerializer.Deserialize<Manifest>(reader.ReadToEnd())??throw new InvalidDataException("Manifesto ausente.");
  if(!ClientUpdateIdentity.IsValidClientCode(identity)||manifest.Identity!=identity||manifest.ApiVersion!=PetRuntime.ApiVersion||manifest.Assembly!="Pet.dll"||!System.Text.RegularExpressions.Regex.IsMatch(manifest.Version??"",@"\A1\.\d+(?:\.\d+)*\z")||!Convert.ToHexString(SHA256.HashData(module)).Equals(manifest.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Identidade, contrato ou integridade da personalização incompatível.");
  if(Directory.Exists(target))throw new InvalidDataException("Pasta de personalização existente sem manifesto. Nenhum arquivo foi substituído.");
  string protectedRoot=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  string? runner=Environment.GetEnvironmentVariable("RUNNER_TEMP");
  bool fixture=Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!string.IsNullOrEmpty(runner)&&app.StartsWith(Path.GetFullPath(runner).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(app,"QA-ISOLATED"));
  if(!app.StartsWith(protectedRoot,StringComparison.OrdinalIgnoreCase)&&!fixture)throw new InvalidOperationException("A migração exige a pasta protegida da instalação.");
  Directory.CreateDirectory(parent);NoLink(parent);
  string stage=Path.Combine(parent,".pet-bootstrap-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  try{
   void Write(string name,byte[] content){using var output=new FileStream(Path.Combine(stage,name),FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(content);output.Flush(true);}
   Write("Pet.dll",module);Write("manifest.json",manifestBytes);NoLink(target);
   BackupLocalData();
   checkpoint?.Invoke("before-activate");
   Directory.Move(stage,target); // Same-volume atomic first install; never overwrite an existing PET.
   return true;
  }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
 }
}
