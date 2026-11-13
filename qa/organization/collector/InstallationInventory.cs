using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace LealInfo.Inventory;
public sealed record InventoryFile(string Name,long Size,string Sha256,string? FileVersion,string? ProductVersion);
public sealed record InventoryReport(int SchemaVersion,IReadOnlyList<InventoryFile> Files,IReadOnlyList<string> Missing,bool ContainsFileContents=false,bool ContainsLicenseOrDatabase=false);
public static class InstallationInventory
{
 static readonly string[] Required={"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json","LealInfoPDV.runtimeconfig.json"};
 static void NoLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Pasta/arquivo redirecionado não permitido.");}
 public static InventoryReport Collect(string folder)
 {
  folder=Path.GetFullPath(folder);NoLink(folder);var files=new List<InventoryFile>();var missing=new List<string>();
  foreach(var name in Required)if(!File.Exists(Path.Combine(folder,name)))missing.Add(name);
  void Add(string path){
   NoLink(path);var before=new FileInfo(path);long size=before.Length;var time=before.LastWriteTimeUtc;
   using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
   string hash=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
   var after=new FileInfo(path);if(after.Length!=size||after.LastWriteTimeUtc!=time)throw new IOException("O arquivo mudou durante a conferência. Tente novamente.");
   string? version=null,product=null;
   if(Path.GetExtension(path) is ".exe" or ".dll"){try{var info=FileVersionInfo.GetVersionInfo(path);version=info.FileVersion;product=info.ProductVersion;}catch(ArgumentException){}catch(IOException){}}
   files.Add(new(Path.GetRelativePath(folder,path).Replace('\\','/'),size,hash,version,product));
  }
  foreach(var path in Directory.EnumerateFiles(folder)){
   var name=Path.GetFileName(path);var ext=Path.GetExtension(path).ToLowerInvariant();
   if(Required.Contains(name,StringComparer.OrdinalIgnoreCase)||ext is ".dll" or ".exe")Add(path);
  }
  void Subtree(string path,bool pet){
   if(!Directory.Exists(path))return;NoLink(path);
   foreach(var file in Directory.EnumerateFiles(path)){
    var ext=Path.GetExtension(file).ToLowerInvariant();
    if(ext is ".dll" or ".exe"||(pet&&Path.GetFileName(file).Equals("manifest.json",StringComparison.OrdinalIgnoreCase)))Add(file);
   }
   foreach(var child in Directory.EnumerateDirectories(path))Subtree(child,pet);
  }
  var extensions=Path.Combine(folder,"Extensions");if(Directory.Exists(extensions)){NoLink(extensions);Subtree(Path.Combine(extensions,"PET"),true);}
  Subtree(Path.Combine(folder,"runtimes"),false);
  return new(1,files.OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray(),missing);
 }
 public static string Serialize(InventoryReport report)=>JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});
}
