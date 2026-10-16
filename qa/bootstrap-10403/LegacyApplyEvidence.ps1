param([ValidateSet('001','003')][string]$Client)
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows'){throw 'Run only on disposable Windows CI, never a customer computer.'}
$source=if($Client -eq '001'){'3dfab93e04ebb1cfef72606c2d52a76c05eead94'}else{'d1525b64db03bc4608aabe020ecfc6e200ec2f05'}
$text=(git show "${source}:UpdateManager.cs") -join "`n"
if($LASTEXITCODE -ne 0){throw 'Cannot read exact published source'}
$match=[regex]::Match($text,'(?s)var script=\$\$"""\r?\n(.*?)\r?\n""";')
if(!$match.Success){throw 'Legacy apply script not found'}
$template=$match.Groups[1].Value
if($template.Contains('catch') -or $template -match '(?i)backup|rollback'){throw 'Source changed; audit must be reviewed'}
if(!$template.Contains('Copy-Item -LiteralPath $_.FullName -Destination $destination -Force')){throw 'Unexpected apply implementation'}
$out=Join-Path $PWD 'qa/bootstrap-10403/evidence'
New-Item -ItemType Directory -Force $out | Out-Null
[IO.File]::WriteAllText((Join-Path $out "legacy-$Client.ps1.txt"),$template)
$root=Join-Path $env:RUNNER_TEMP ('legacy-bootstrap-'+$Client+'-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
# Fixtures are harmless Framework EXEs, NOT customer PDV builds. They exercise
# Windows copy/lock/process behavior of the exact extracted installer script.
foreach($v in @('old','new')){
  $code='using System;using System.IO;public class Fixture'+$Client+$v+' {public static void Main(){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"started.txt"),"'+$v+'");}}'
  Add-Type -TypeDefinition $code -OutputAssembly (Join-Path $root ($v+'.exe')) -OutputType ConsoleApplication
}
function Hash($p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function Q($p){$p.Replace("'","''")}
$results=@()
foreach($mode in @('normal','copy-failure','process-exit','process-kill','locked-file','restart-failure')){
  $case=Join-Path $root $mode;$app=Join-Path $case 'app';$stage=Join-Path $case 'stage';$data=Join-Path $case 'user-data'
  New-Item -ItemType Directory -Force $app,$stage,$data | Out-Null
  Copy-Item (Join-Path $root 'old.exe') (Join-Path $app 'LealInfoPDV.exe')
  Copy-Item (Join-Path $root 'new.exe') (Join-Path $stage 'LealInfoPDV.exe')
  [IO.File]::WriteAllText((Join-Path $app 'a.dll'),'OLD-DLL-A')
  [IO.File]::WriteAllText((Join-Path $app 'z.dll'),'OLD-DLL-Z')
  [IO.File]::WriteAllText((Join-Path $stage 'a.dll'),'NEW-DLL-A')
  [IO.File]::WriteAllText((Join-Path $stage 'z.dll'),'NEW-DLL-Z')
  # Real SQLite fixture; license/config are opaque sentinels, not real licenses.
  $db=Join-Path $data 'lealinfo.db'
  $seed=Join-Path $case 'seed.py'
  @'
import sqlite3,sys
c=sqlite3.connect(sys.argv[1])
c.execute("create table customers(id integer primary key,name text)")
c.execute("insert into customers values(1,?)",("BOB",))
c.commit()
c.close()
'@ | Set-Content $seed -Encoding UTF8
  python $seed $db
  if($LASTEXITCODE -ne 0){throw 'SQLite fixture failed'}
  [IO.File]::WriteAllText((Join-Path $data 'license.protected'),'OPAQUE-TEST-LICENSE')
  [IO.File]::WriteAllText((Join-Path $data 'network.config'),'OPAQUE-TEST-CONFIG')
  $before=@{};Get-ChildItem $app -File | ForEach-Object {$before[$_.Name]=Hash $_.FullName}
  $dataBefore=@{};Get-ChildItem $data -File | ForEach-Object {$dataBefore[$_.Name]=Hash $_.FullName}
  $exe=Join-Path $app 'LealInfoPDV.exe'
  if($mode -eq 'restart-failure'){$exe=Join-Path $app 'missing.exe'}
  # Only interpolate the five values normally supplied by the original C# code.
  $script=$template.Replace('{{Environment.ProcessId}}','2147483647').Replace('{{EscapePs(stage)}}',(Q $stage)).Replace('{{EscapePs(app)}}',(Q $app)).Replace('{{EscapePs(exe)}}',(Q $exe))
  if($script.Contains('{{')){throw 'Unresolved template'}
  $signal=Join-Path $case 'first-copy.signal'
  # Controlled fault injection at the second copy, or kill after first copy.
  # The production copy loop and order remain unchanged.
  $prefix=''
  if($mode -in @('copy-failure','process-exit','process-kill')){
    $prefix=@'
$script:copyCount=0
function Copy-Item {
 param([string]$LiteralPath,[string]$Destination,[switch]$Force)
 $script:copyCount++
 if($script:copyCount -eq 2 -and '__MODE__' -eq 'copy-failure'){throw 'INJECTED_COPY_FAILURE'}
 Microsoft.PowerShell.Management\Copy-Item -LiteralPath $LiteralPath -Destination $Destination -Force
 if($script:copyCount -eq 1){
  [IO.File]::WriteAllText('__SIGNAL__','first file replaced')
  if('__MODE__' -eq 'process-exit'){[Environment]::Exit(91)}
  if('__MODE__' -eq 'process-kill'){Start-Sleep -Seconds 25}
 }
}
'@
    $prefix=$prefix.Replace('__MODE__',$mode).Replace('__SIGNAL__',(Q $signal))+"`r`n"
  }
  $path=Join-Path $case 'apply.ps1';[IO.File]::WriteAllText($path,$prefix+$script)
  $lock=$null
  if($mode -eq 'locked-file'){$lock=[IO.File]::Open((Join-Path $app 'z.dll'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
  try{
    $p=Start-Process powershell.exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$path+'"')) -PassThru -RedirectStandardOutput (Join-Path $out "$Client-$mode-stdout.txt") -RedirectStandardError (Join-Path $out "$Client-$mode-stderr.txt")
    if($mode -eq 'process-kill'){
      $until=(Get-Date).AddSeconds(15)
      while(!(Test-Path $signal) -and !$p.HasExited -and (Get-Date) -lt $until){Start-Sleep -Milliseconds 50}
      if(!(Test-Path $signal)){throw 'Fault boundary was not reached'}
      $p.Kill()
    }
    if(!$p.WaitForExit(20000)){$p.Kill();throw 'Bounded fixture timeout'}
    $p.Refresh();$exit=$p.ExitCode
  }finally{if($lock){$lock.Dispose()}}
  $unchanged=@($before.Keys | Where-Object {(Hash (Join-Path $app $_)) -eq $before[$_]})
  $changed=@($before.Keys | Where-Object {(Hash (Join-Path $app $_)) -ne $before[$_]})
  $dataSafe=$true;foreach($name in $dataBefore.Keys){if((Hash (Join-Path $data $name)) -ne $dataBefore[$name]){$dataSafe=$false}}
  $started=Join-Path $app 'started.txt'
  if($mode -eq 'normal'){
    $until=(Get-Date).AddSeconds(4);while(!(Test-Path $started) -and (Get-Date) -lt $until){Start-Sleep -Milliseconds 50}
    if($exit -ne 0 -or $changed.Count -ne 3 -or !(Test-Path $started) -or (Get-Content $started -Raw) -ne 'new'){throw 'Normal apply fixture failed'}
  }elseif($mode -eq 'restart-failure'){
    if($exit -eq 0 -or $changed.Count -ne 3 -or (Test-Path $started)){throw 'Restart failure not demonstrated'}
  }else{
    if($exit -eq 0 -or $changed.Count -eq 0 -or $unchanged.Count -eq 0){throw "Partial installation not demonstrated: $mode exit=$exit changed=$($changed.Count) unchanged=$($unchanged.Count)"}
  }
  if(!$dataSafe){throw 'Data fixture changed'}
  $results+=@{client=$Client;source=$source;scenario=$mode;processExit=$exit;filesChanged=$changed;filesOld=$unchanged;automaticRestoration=($changed.Count -eq 0);sqliteAndOpaqueLicenseConfigPreserved=$dataSafe;classification='isolated Windows filesystem/process integration with fixtures';outcome=if($mode -eq 'normal'){'normal apply demonstrated'}else{'unsafe legacy behavior reproduced'}}
  Write-Host "EVIDENCE $Client $mode exit=$exit changed=$($changed.Count) old=$($unchanged.Count) dataPreserved=$dataSafe"
}
$results+=@{client=$Client;scenario='power-loss';outcome='NOT TESTED: process-kill is not physical power loss or disk-cache durability proof'}
$results+=@{client=$Client;scenario='second-update-after-safe-transition';outcome='BLOCKED: no safe first transition implemented'}
$results | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out "results-$Client.json") -Encoding UTF8
Write-Host 'PASS evidence harness; NOT an approval of bootstrap safety. No customer data or updater feeds accessed.'
