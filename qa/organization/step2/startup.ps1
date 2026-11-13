$ErrorActionPreference='Stop'
$status=Get-Content step2-evidence/clone-integrity.json -Raw|ConvertFrom-Json
if(!$status.StartupPermitted){@{Executed=$false;Blocked='Exact runtimeconfig or declared runtime assets unavailable';LicensedStartup=$false}|ConvertTo-Json|Set-Content step2-evidence/startup.json;exit 0}
Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class InstalledWindowProbe {
 public delegate bool EnumProc(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr parent,EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,StringBuilder text);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 public static bool Activation(int pid){bool activate=false,import=false;
  EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p!=(uint)pid)return true;
   EnumChildWindows(h,(child,unused)=>{var text=new StringBuilder(1024);SendMessage(child,13,(IntPtr)1024,text);var s=text.ToString().ToUpperInvariant();if(s.Contains("ATIVAR CHAVE"))activate=true;if(s.Contains("IMPORTAR LICEN"))import=true;return true;},IntPtr.Zero);return true;},IntPtr.Zero);
  return activate&&import;
 }
 public static void Close(int pid){EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==(uint)pid)PostMessage(h,16,IntPtr.Zero,IntPtr.Zero);return true;},IntPtr.Zero);}
}
'@
$exe=(Resolve-Path step2-work/identified-clone/LealInfoPDV.exe).Path
$rule='LEAL-isolated-startup-'+[Guid]::NewGuid().ToString('N')
New-NetFirewallRule -DisplayName $rule -Program $exe -Direction Outbound -Action Block|Out-Null
$process=$null;$forced=$false
try{
 $process=Start-Process $exe -WorkingDirectory (Split-Path $exe) -PassThru
 $limit=(Get-Date).AddSeconds(60);$gate=$false
 do{Start-Sleep -Milliseconds 200;$process.Refresh();if(!$process.HasExited){$gate=[InstalledWindowProbe]::Activation($process.Id)}}while(!$process.HasExited -and !$gate -and (Get-Date) -lt $limit)
 @{Executed=$true;ExactCoreFiles=$true;ExactRuntimeconfig=$true;ActivationGateObserved=$gate;ProcessExited=$process.HasExited;LicenseInvented=$false;ActivationBypassed=$false;LoginOrBusinessExecuted=$false;LicensedStartup=$false}|ConvertTo-Json|Set-Content step2-evidence/startup.json
 if(!$gate){throw 'Original installation did not reach the expected activation gate; inspect isolated evidence'}
 Write-Output 'PASS exact published 10.467 starts and correctly stops at the license gate; business startup remains BLOCKED'
}finally{
 if($process -and !$process.HasExited){[InstalledWindowProbe]::Close($process.Id);if(!$process.WaitForExit(5000)){$forced=$true;$process.Kill();$process.WaitForExit()}}
 Remove-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue
}
