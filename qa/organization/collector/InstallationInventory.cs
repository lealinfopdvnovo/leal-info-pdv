using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
#if NETFRAMEWORK
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif
namespace LealInfo.Inventory
{
 public sealed class InventoryFile
 {
  public string Name {get;private set;} public long Size {get;private set;} public string Sha256 {get;private set;}
  public string FileVersion {get;private set;} public string ProductVersion {get;private set;}
  public InventoryFile(string name,long size,string sha256,string fileVersion,string productVersion){Name=name;Size=size;Sha256=sha256;FileVersion=fileVersion;ProductVersion=productVersion;}
 }
 public sealed class InventoryReport
 {
  public int SchemaVersion {get;private set;} public IReadOnlyList<InventoryFile> Files {get;private set;} public IReadOnlyList<string> Missing {get;private set;}
  public bool ContainsFileContents {get{return false;}} public bool ContainsLicenseOrDatabase {get{return false;}}
  internal string RootIdentity,CanonicalRoot;
  public InventoryReport(IReadOnlyList<InventoryFile> files,IReadOnlyList<string> missing){SchemaVersion=2;Files=files;Missing=missing;}
 }
 public static class InstallationInventory
 {
  static readonly string[] Required={"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json","LealInfoPDV.runtimeconfig.json"};
  public static InventoryReport Collect(string folder){return Collect(folder,CancellationToken.None);}
  public static InventoryReport Collect(string folder,CancellationToken cancel)
  {
   using(var guard=new PathGuard(folder)){
    var files=new List<InventoryFile>();var missing=new List<string>();
    if(!File.Exists(Path.Combine(guard.Root,"LealInfoPDV.dll"))&&!File.Exists(Path.Combine(guard.Root,"LealInfoPDV.exe")))throw new IOException("Pasta incorreta. Selecione a pasta que contém LealInfoPDV.exe ou LealInfoPDV.dll.");
    foreach(var name in Required)if(!File.Exists(Path.Combine(guard.Root,name)))missing.Add(name);
    Action<string> add=path=>{
     cancel.ThrowIfCancellationRequested();PathGuard.Info before;
     using(var stream=guard.OpenRead(path,out before)){
      var hash=Hash(stream,cancel);stream.Position=0;var repeated=Hash(stream,cancel);
      if(hash!=repeated)throw new IOException("Arquivo alterado durante a leitura. Aguarde e tente novamente.");
      var stamp=PathGuard.Windows?PathGuard.Describe(stream.SafeFileHandle):before;
      if(PathGuard.Windows&&(stamp.Stamp!=before.Stamp||stamp.Links!=1))throw new IOException("Arquivo alterado durante a leitura. Aguarde e tente novamente.");
      string version=null,product=null;
      var ext=Path.GetExtension(path);
      if(ext.Equals(".dll",StringComparison.OrdinalIgnoreCase)||ext.Equals(".exe",StringComparison.OrdinalIgnoreCase)){
       try{var v=FileVersionInfo.GetVersionInfo(path);version=v.FileVersion;product=v.ProductVersion;}catch(ArgumentException){}
      }
      files.Add(new InventoryFile(path.Substring(guard.Root.TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace('\\','/'),stream.Length,hash,version,product));
     }
    };
    foreach(var path in Directory.EnumerateFiles(guard.Root)){
     cancel.ThrowIfCancellationRequested();var name=Path.GetFileName(path);var ext=Path.GetExtension(path);
     if(Required.Contains(name,StringComparer.OrdinalIgnoreCase)||ext.Equals(".dll",StringComparison.OrdinalIgnoreCase)||ext.Equals(".exe",StringComparison.OrdinalIgnoreCase))add(path);
    }
    Action<string,bool> tree=null;
    tree=(path,pet)=>{
     cancel.ThrowIfCancellationRequested();if(!Directory.Exists(path))return;guard.LockDirectory(path);
     foreach(var file in Directory.EnumerateFiles(path)){
      var ext=Path.GetExtension(file);
      if(ext.Equals(".dll",StringComparison.OrdinalIgnoreCase)||ext.Equals(".exe",StringComparison.OrdinalIgnoreCase)||(pet&&Path.GetFileName(file).Equals("manifest.json",StringComparison.OrdinalIgnoreCase)))add(file);
     }
     foreach(var child in Directory.EnumerateDirectories(path))tree(child,pet);
    };
    var extensions=Path.Combine(guard.Root,"Extensions");if(Directory.Exists(extensions)){guard.LockDirectory(extensions);tree(Path.Combine(extensions,"PET"),true);}
    tree(Path.Combine(guard.Root,"runtimes"),false);
    cancel.ThrowIfCancellationRequested();
    return new InventoryReport(files.OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray(),missing){RootIdentity=guard.Identity,CanonicalRoot=guard.Canonical};
   }
  }
  static string Hash(Stream stream,CancellationToken cancel)
  {
   using(var sha=SHA256.Create()){
    var buffer=new byte[65536];int count;
    while((count=stream.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,null,0);}
    sha.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
   }
  }
  public static string Serialize(InventoryReport report)
  {
#if NETFRAMEWORK
   return new JavaScriptSerializer().Serialize(report);
#else
   return JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});
#endif
  }
  public static void SaveReport(string selectedFolder,string output,InventoryReport report,CancellationToken cancel)
  {
   cancel.ThrowIfCancellationRequested();
   using(var source=new PathGuard(selectedFolder)){
    if(source.Identity!=report.RootIdentity||source.Canonical!=report.CanonicalRoot)throw new IOException("A pasta mudou desde a conferência. Faça a leitura novamente.");
    var full=PathGuard.Local(output);var name=Path.GetFileName(full);
    var stem=name.Split('.')[0].TrimEnd(' ','.').ToUpperInvariant();
    if(new[]{"CON","PRN","AUX","NUL","CONIN$","CONOUT$","COM¹","COM²","COM³","LPT¹","LPT²","LPT³","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem)||name.EndsWith(" ",StringComparison.Ordinal)||name.EndsWith(".",StringComparison.Ordinal))throw new IOException("Escolha um nome comum para o arquivo JSON.");
    if(!name.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||name.IndexOf(':')>=0)throw new IOException("Salve o relatório com a extensão .json.");
    using(var target=new PathGuard(Path.GetDirectoryName(full))){
     if(PathGuard.Inside(target.Canonical,source.Canonical))throw new IOException("Salve o relatório fora da pasta do PDV.");
     var bytes=new UTF8Encoding(false).GetBytes(Serialize(report));cancel.ThrowIfCancellationRequested();
     // CreateNew refuses existing files, links and overwrite; no staging or temporary files.
     using(var file=target.CreateOutput(full)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
    }
   }
  }
 }
}
