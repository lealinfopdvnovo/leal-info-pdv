using System.IO.Compression;
using System.Security.Cryptography;
namespace LealInfoPDV;
internal static class CasaGeminiBundleStore
{
 internal static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
 static void NoLinks(string path){for(string? p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Pasta redirecionada recusada.");}
 static string Safe(string root,string relative){
  if(string.IsNullOrEmpty(relative)||relative.Contains('\\')||relative.StartsWith('/')||relative.Split('/').Any(x=>x is "" or "." or ".."||x.Contains(':')))throw new InvalidDataException("Caminho de pacote inválido.");
  if(Path.GetExtension(relative).ToLowerInvariant() is not (".exe" or ".dll" or ".json" or ".pdb"))throw new InvalidDataException("Tipo de arquivo não autorizado.");
  var path=Path.GetFullPath(Path.Combine(root,relative));if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho fora do pacote.");NoLinks(path);return path;
 }
 internal static void Verify(string folder,IReadOnlyDictionary<string,string> hashes){
  NoLinks(folder);var actual=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Select(x=>Path.GetRelativePath(folder,x).Replace('\\','/')).ToHashSet(StringComparer.Ordinal);
  if(!actual.SetEquals(hashes.Keys))throw new InvalidDataException("Arquivos da candidata divergentes.");
  foreach(var (name,hash) in hashes){var path=Safe(folder,name);if(Hash(File.ReadAllBytes(path))!=hash)throw new InvalidDataException("Integridade da candidata inválida.");}
 }
 internal static string Install(byte[] zip,string expected,IReadOnlyDictionary<string,string> hashes,string cache,Action<string>? checkpoint=null){
  if(Hash(zip)!=expected||expected.Length!=64||hashes.Count==0)throw new InvalidDataException("SHA-256 do pacote inválido.");
  if(checkpoint!=null&&(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!Path.GetFullPath(cache).StartsWith(Path.GetFullPath(Environment.GetEnvironmentVariable("RUNNER_TEMP")??"/invalid")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Falha simulada restrita ao runner isolado.");
  NoLinks(cache);Directory.CreateDirectory(cache);NoLinks(cache);
  using var gate=new FileStream(Path.Combine(cache,".extract.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  var target=Path.Combine(cache,expected);NoLinks(target);if(Directory.Exists(target)){Verify(target,hashes);return target;}
  var stage=Path.Combine(cache,".stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  try{
   using var archive=new ZipArchive(new MemoryStream(zip),ZipArchiveMode.Read);
   if(archive.Entries.Count!=hashes.Count||archive.Entries.Select(x=>x.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=hashes.Count)throw new InvalidDataException("Lista do pacote inválida.");
   long total=0;
   foreach(var entry in archive.Entries){
    if(!hashes.TryGetValue(entry.FullName,out var pinned)||entry.Length<0||entry.Length>128*1024*1024||(total+=entry.Length)>350*1024*1024)throw new InvalidDataException("Conteúdo do pacote inválido.");
    var dest=Safe(stage,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    using(var stream=entry.Open())using(var file=new FileStream(dest,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.CopyTo(file);file.Flush(true);}
    if(Hash(File.ReadAllBytes(dest))!=pinned)throw new InvalidDataException("Hash de arquivo inválido.");checkpoint?.Invoke("file-"+entry.FullName);
   }
   Verify(stage,hashes);checkpoint?.Invoke("before-activate");NoLinks(target);Directory.Move(stage,target);return target;
  }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
 }
}
