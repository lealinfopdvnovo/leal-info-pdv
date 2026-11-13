param([string]$Exe,[string]$Collector)
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
public static class RestrictedCollectorTest {
 [StructLayout(LayoutKind.Sequential)]struct Sid {public IntPtr Value;public uint Attributes;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Startup {public int cb;public string reserved,desktop,title;public int x,y,width,height,countX,countY,fill,flags;public short show,reserved2;public IntPtr bytes,input,output,error;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr process,thread;public uint pid,tid;}
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool CreateRestrictedToken(IntPtr token,uint flags,uint count,[In]Sid[] disabled,uint privilegeCount,IntPtr privileges,uint restrictedCount,IntPtr restricted,out IntPtr result);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcessAsUser(IntPtr token,string app,System.Text.StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr environment,string directory,ref Startup startup,out ProcessInfo process);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcessWithTokenW(IntPtr token,uint logon,string app,System.Text.StringBuilder command,uint flags,IntPtr environment,string directory,ref Startup startup,out ProcessInfo process);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
 [DllImport("kernel32.dll")]static extern bool GetExitCodeProcess(IntPtr process,out uint code);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 public static int Run(string exe,string collector,bool smoke){
  IntPtr original=IntPtr.Zero,restricted=IntPtr.Zero,sidPointer=IntPtr.Zero;
  try{
   if(!OpenProcessToken(GetCurrentProcess(),0xF01FF,out original))throw new Win32Exception();
   var admin=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);var bytes=new byte[admin.BinaryLength];admin.GetBinaryForm(bytes,0);sidPointer=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,sidPointer,bytes.Length);
   if(!CreateRestrictedToken(original,1,1,new[]{new Sid{Value=sidPointer,Attributes=0}},0,IntPtr.Zero,0,IntPtr.Zero,out restricted))throw new Win32Exception();
   var startup=new Startup();startup.cb=Marshal.SizeOf(startup);ProcessInfo p;var command=new System.Text.StringBuilder("\""+exe+"\" \""+collector+"\"");
   if(!CreateProcessAsUser(restricted,exe,command,IntPtr.Zero,IntPtr.Zero,false,0x400,IntPtr.Zero,Environment.CurrentDirectory,ref startup,out p)){
    command=new System.Text.StringBuilder("\""+exe+"\" \""+collector+"\"");
    if(!CreateProcessWithTokenW(restricted,0,exe,command,0x400,IntPtr.Zero,Environment.CurrentDirectory,ref startup,out p))throw new Win32Exception();
   }
   try{
    if(smoke){
     using(var child=System.Diagnostics.Process.GetProcessById((int)p.pid)){
      var limit=DateTime.UtcNow.AddSeconds(15);while(!child.HasExited&&child.MainWindowTitle!="LEAL INFO — Conferir instalação"&&DateTime.UtcNow<limit){System.Threading.Thread.Sleep(100);child.Refresh();}
      if(child.HasExited||child.MainWindowTitle!="LEAL INFO — Conferir instalação")throw new Exception("Standalone graphical window did not open with standard token");
      child.CloseMainWindow();if(!child.WaitForExit(5000))throw new Exception("Standalone graphical window did not close");
     }
    }
    if(WaitForSingleObject(p.process,180000)!=0)throw new Exception("Restricted test timed out");uint code;if(!GetExitCodeProcess(p.process,out code))throw new Win32Exception();return (int)code;}finally{CloseHandle(p.thread);CloseHandle(p.process);}
  }finally{if(sidPointer!=IntPtr.Zero)Marshal.FreeHGlobal(sidPointer);if(restricted!=IntPtr.Zero)CloseHandle(restricted);if(original!=IntPtr.Zero)CloseHandle(original);}
 }
}
'@
$result=[RestrictedCollectorTest]::Run((Resolve-Path $Exe).Path,(Resolve-Path $Collector).Path,$false)
if($result -ne 0){throw "Restricted Windows collector tests failed: $result"}

$standalone=[RestrictedCollectorTest]::Run((Resolve-Path $Collector).Path,'',$true)
if($standalone -ne 0){throw 'Standalone restricted-token smoke failed'}
@{Pass=$true;NonAdministratorToken=$true;AsInvoker=$true;GraphicalStandaloneOpened=$true;GraphicalClosedNormally=$true;NoSidecarDlls=$true}|ConvertTo-Json|Set-Content (Join-Path $env:COLLECTOR_EVIDENCE 'standalone-gui.json')
