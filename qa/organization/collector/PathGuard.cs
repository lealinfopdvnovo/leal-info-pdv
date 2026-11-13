using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace LealInfo.Inventory
{
 internal sealed class PathGuard : IDisposable
 {
  readonly List<SafeFileHandle> directories=new List<SafeFileHandle>();
  public readonly string Root;
  public readonly string Canonical;
  public readonly string Identity;
  internal static bool Windows { get {return Environment.OSVersion.Platform==PlatformID.Win32NT;} }
  internal const uint Reparse=0x400;
  [StructLayout(LayoutKind.Sequential)]internal struct Info
  {
   public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
   public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
   public long Size {get{return ((long)SizeHigh<<32)|SizeLow;}}
   public string Identity {get{return Volume+":"+IndexHigh+":"+IndexLow;}}
   public string Stamp {get{return Identity+":"+Size+":"+Written.dwHighDateTime+":"+Written.dwLowDateTime;}}
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle file,out Info info);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint size,uint flags);
  static IOException Error(){return new IOException("Não foi possível ler com segurança. Confira a permissão da pasta e tente novamente.",new Win32Exception(Marshal.GetLastWin32Error()));}
  internal static Info Describe(SafeFileHandle file){Info info;if(!GetFileInformationByHandle(file,out info))throw Error();return info;}
  internal static string Final(SafeFileHandle file){var text=new StringBuilder(32768);uint size=GetFinalPathNameByHandle(file,text,(uint)text.Capacity,0);if(size==0||size>=text.Capacity)throw Error();var s=text.ToString();if(s.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))throw new IOException("Selecione uma pasta local do computador.");return s.StartsWith(@"\\?\",StringComparison.Ordinal)?s.Substring(4):s;}
  internal static string Local(string path)
  {
   if(string.IsNullOrWhiteSpace(path)||path.StartsWith(@"\\",StringComparison.Ordinal))throw new IOException("Selecione uma pasta local do computador.");
   var full=Path.GetFullPath(path);
   if(Windows){var drive=Path.GetPathRoot(full);if(drive==null||drive.Length!=3||new DriveInfo(drive).DriveType==DriveType.Network)throw new IOException("Selecione uma pasta local do computador.");}
   var trimmed=full.TrimEnd(Path.DirectorySeparatorChar);return Windows&&trimmed.Length==2&&trimmed[1]==':'?trimmed+Path.DirectorySeparatorChar:trimmed;
  }
  internal static bool Inside(string path,string folder){return path.Equals(folder,StringComparison.OrdinalIgnoreCase)||path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  public PathGuard(string root)
  {
   Root=Local(root);try{
    if(!Windows){if(!Directory.Exists(Root))throw new IOException("Pasta não encontrada.");NoLink(Root);Canonical=Root;Identity=Root;return;}
    string drive=Path.GetPathRoot(Root);string current=drive;
    var parts=Root.Substring(drive.Length).Split(new[]{Path.DirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries);
    LockDirectory(current);
    foreach(var part in parts){current=Path.Combine(current,part);LockDirectory(current);}
    var handle=directories[directories.Count-1];Canonical=Final(handle).TrimEnd(Path.DirectorySeparatorChar);Identity=Describe(handle).Identity;
   }catch{Dispose();throw;}
  }
  internal static void NoLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Pasta ou arquivo redirecionado não permitido. Selecione a pasta original.");}
  internal void LockDirectory(string path){if(!TryLockDirectory(path))throw new IOException("Pasta não encontrada. Selecione a pasta original do PDV.");}
  internal bool TryLockDirectory(string path)
  {
   if(!Windows){if(!Directory.Exists(path))return false;NoLink(path);return true;}
   // OPEN_REPARSE_POINT: even presence checks must not follow a link outside scope.
   var file=CreateFile(path,0x80,1,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
   if(file.IsInvalid){int error=Marshal.GetLastWin32Error();file.Dispose();if(error==2||error==3)return false;throw new IOException("Sem acesso à pasta. Escolha a pasta original à qual você tem acesso.",new Win32Exception(error));}
   try{var info=Describe(file);if((info.Attributes&Reparse)!=0)throw new IOException("Pasta redirecionada não permitida. Selecione a pasta original.");if((info.Attributes&0x10)==0)throw new IOException("Selecione uma pasta.");directories.Add(file);return true;}catch{file.Dispose();throw;}
  }
  internal FileStream OpenRead(string path,out Info info)
  {
   if(!Inside(Path.GetFullPath(path),Root))throw new IOException("Arquivo fora da pasta selecionada.");
   if(!Windows){NoLink(path);var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);info=new Info();return stream;}
   var file=CreateFile(path,0x80000000,1,IntPtr.Zero,3,0x08200000,IntPtr.Zero);
   if(file.IsInvalid){file.Dispose();throw new IOException("Arquivo indisponível ou em gravação. Aguarde e tente novamente.",new Win32Exception(Marshal.GetLastWin32Error()));}
   try{
    info=Describe(file);
    if((info.Attributes&Reparse)!=0||info.Links!=1)throw new IOException("Arquivo redirecionado ou com vínculo adicional não permitido.");
    if(!Inside(Final(file),Canonical))throw new IOException("Arquivo fora da pasta selecionada.");
    return new FileStream(file,FileAccess.Read,65536,false);
   }catch{file.Dispose();throw;}
  }
  internal FileStream CreateOutput(string path)
  {
   if(!Windows)return new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
   var file=CreateFile(path,0x40000000,0,IntPtr.Zero,1,0x00200080,IntPtr.Zero);
   if(file.IsInvalid){int error=Marshal.GetLastWin32Error();file.Dispose();throw new IOException(error==80||error==183?"Esse nome já existe. Escolha outro nome para o relatório.":"Não foi possível salvar. Escolha uma pasta fora do PDV à qual você tem acesso.",new Win32Exception(error));}
   try{if(!Inside(Final(file),Canonical))throw new IOException("Destino fora da pasta escolhida.");return new FileStream(file,FileAccess.Write,65536,false);}catch{file.Dispose();throw;}
  }
  public void Dispose(){for(int i=directories.Count-1;i>=0;i--)directories[i].Dispose();directories.Clear();}
 }
}
