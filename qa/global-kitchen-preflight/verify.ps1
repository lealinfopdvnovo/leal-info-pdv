$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Disposable Windows only'}
New-Item -ItemType Directory evidence -Force|Out-Null
$repo='lealinfopdvnovo/leal-info-pdv-updates';$records=@();$lease=@{}
foreach($channel in @('GLOBAL','001','002','003','004')){
 $path=if($channel -eq 'GLOBAL'){'version.json'}else{"clients/$channel/version.json"}
 $f=gh api "repos/$repo/contents/$path?ref=main"|ConvertFrom-Json
 if($LASTEXITCODE -ne 0){throw 'Feed read failed'}
 $lease[$channel]=$f.sha
 $m=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($f.content))|ConvertFrom-Json
 $s=gh api "repos/lealinfopdvnovo/leal-info-pdv/contents/UpdateManager.cs?ref=$($m.SourceCommit)"|ConvertFrom-Json
 if($LASTEXITCODE -ne 0){throw 'Source read failed'}
 $source=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($s.content))
 if($source -notmatch ('CurrentVersion\s*=\s*"'+[Regex]::Escape($m.Version)+'"') -or $source -notmatch ('DistributionChannel="'+$channel+'"')){throw 'Published source identity differs'}
 $zip=Join-Path $env:RUNNER_TEMP "$channel.zip";Invoke-WebRequest $m.PackageUrl -OutFile $zip
 if((Get-FileHash $zip).Hash.ToLowerInvariant() -ne $m.Sha256){throw 'Published package integrity failed'}
 $dir=Join-Path $env:RUNNER_TEMP "$channel-fixture";Expand-Archive $zip $dir
 if(!(Test-Path (Join-Path $dir 'LealInfoPDV.exe'))){throw 'Missing executable'}
 $protected=$source.Contains('await TransactionalUpdate.BeginAsync(')
 $unsafe=$source.Contains('Copy-Item -LiteralPath $_.FullName -Destination $destination -Force')
 $test=$null
 if($unsafe){
  $start=$source.IndexOf('Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {')
  $end=$source.IndexOf('Start-Process -FilePath $exe -WorkingDirectory $app',$start)
  if($start -lt 0 -or $end -le $start){throw 'Published copy block missing'}
  $block=$source.Substring($start,$end-$start)
  $app=Join-Path $dir 'app';$stage=Join-Path $dir 'stage'
  New-Item -ItemType Directory $app,$stage -Force|Out-Null
  $names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
  foreach($name in $names){[IO.File]::WriteAllText((Join-Path $app $name),"PREVIOUS-$name");[IO.File]::WriteAllText((Join-Path $stage $name),"NEW-$name")}
  foreach($name in @('customer.db','license.protected','settings.config')){[IO.File]::WriteAllText((Join-Path $app $name),"PRESERVE-$name")}
  $script:copies=0
  function Copy-Item {
   [CmdletBinding()]param([string]$LiteralPath,[string]$Destination,[switch]$Force)
   $script:copies++;if($script:copies -eq 2){throw 'SIMULATED INTERRUPTION'}
   Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
  }
  $injected=$false
  try{& ([scriptblock]::Create($block))}catch{if($_.Exception.Message -notlike '*SIMULATED INTERRUPTION*'){throw};$injected=$true}
  Remove-Item Function:\Copy-Item
  if(!$injected){throw 'Failure injection not exercised'}
  $old=0;$new=0
  foreach($name in $names){$v=[IO.File]::ReadAllText((Join-Path $app $name));if($v -eq "PREVIOUS-$name"){$old++};if($v -eq "NEW-$name"){$new++}}
  if($old -ne 2 -or $new -ne 1){throw 'Expected interrupted mixed executable set not reproduced'}
  foreach($name in @('customer.db','license.protected','settings.config')){if([IO.File]::ReadAllText((Join-Path $app $name)) -ne "PRESERVE-$name"){throw 'Sentinel changed'}}
  $test=@{MixedExecutables=$true;OldFiles=$old;NewFiles=$new;CustomerSentinelsPreserved=$true;ActualPublishedCopyBlock=$true;IsolatedTempOnly=$true}
  Write-Host "BLOCK $channel V$($m.Version): published copy loop leaves mixed executables after interruption"
 }elseif(!$protected){throw 'Unknown apply implementation'}
 else{Write-Host "PASS $channel V$($m.Version): transactional apply entry present"}
 $records+=@{Channel=$channel;PublishedVersion=$m.Version;SourceCommit=$m.SourceCommit;PackageSha256=$m.Sha256;PublicIntegrity=$true;TransactionalEntry=$protected;DirectCopy=$unsafe;Interruption=$test}
}
foreach($channel in $lease.Keys){
 $path=if($channel -eq 'GLOBAL'){'version.json'}else{"clients/$channel/version.json"}
 $r=gh api "repos/$repo/contents/$path?ref=main"|ConvertFrom-Json
 if($r.sha -ne $lease[$channel]){throw 'Concurrent feed modification'}
}
$blocked=@($records|Where-Object {$_.DirectCopy}|ForEach-Object {$_.Channel})
@{Source=$env:GITHUB_SHA;PublicationReady=($blocked.Count -eq 0);Published=$false;FeedsUnchanged=$true;BlockedChannels=$blocked;Channels=$records;RealCustomerWindowsAccess=$false}|ConvertTo-Json -Depth 12|Set-Content evidence/global-preflight.json -Encoding UTF8
Write-Host "PREFLIGHT FINISHED; no releases or feeds modified; blocked: $($blocked -join ',')"
