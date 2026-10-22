$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Windows isolado somente'}
$e=Get-Content validated/release-output/candidate.json -Raw|ConvertFrom-Json
if($e.Channel -ne $env:CHANNEL -or $e.Version -ne '10.436'){throw 'Wrong package identity'}
if($e.SourceCommit -ne $env:APPROVED_SOURCE){throw 'Wrong source'}
$r=Get-Content validated/layout-evidence/results.json -Raw|ConvertFrom-Json
if($r.success -ne $true){throw 'No Windows UI approval'}
$package=(Resolve-Path (Join-Path validated/release-output $e.Package)).Path
if((Get-FileHash $package).Hash.ToLowerInvariant() -ne $e.PackageSha256){throw 'Changed target package'}
Invoke-WebRequest $e.Baseline.PackageUrl -OutFile baseline.zip
if((Get-FileHash baseline.zip).Hash.ToLowerInvariant() -ne $e.Baseline.Sha256){throw 'Changed published baseline'}
$base=Join-Path $env:RUNNER_TEMP ('baseline-'+[Guid]::NewGuid().ToString('N'))
Expand-Archive baseline.zip $base
$worker=(Resolve-Path TransactionalUpdate.ps1).Path
function Norm($s){$s.Replace("`r`n","`n").Trim().TrimStart([char]0xfeff)}
foreach($n in @('TransactionalUpdate.ps1','TransactionalUpdate.cs','Program.cs','Licensing/UpdatePayloadPolicy.cs')){
 $a=(git show ($e.BaselineSource+':'+$n)) -join "`n";if($LASTEXITCODE -ne 0){throw 'Missing baseline source'}
 $b=(git show ($e.SourceCommit+':'+$n)) -join "`n";if($LASTEXITCODE -ne 0){throw 'Missing target source'}
 if((Norm $a) -ne (Norm $b)){throw ('Protected source changed: '+$n)}
}
$a=(git show ($e.BaselineSource+':UpdateManager.cs')) -join "`n"
$b=(git show ($e.SourceCommit+':UpdateManager.cs')) -join "`n"
if(((Norm $a) -replace 'CurrentVersion="[0-9.]+"','CurrentVersion="VERSION"') -ne ((Norm $b) -replace 'CurrentVersion="[0-9.]+"','CurrentVersion="VERSION"')){throw 'Updater changed beyond version'}
$actual=(git show ($e.SourceCommit+':TransactionalUpdate.ps1')) -join "`n"
if((Norm $actual) -ne (Norm (Get-Content $worker -Raw))){throw 'Fixture worker differs from actual target'}
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
foreach($n in $names){if((Get-FileHash (Join-Path $base $n)).Hash.ToLowerInvariant() -ne $e.PreviousFiles.$n){throw 'Baseline binary mismatch'}}
function TaskName($app){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}}
$results=@()
foreach($scenario in @('success','interruption','bad-sha')){
 $app=Join-Path $env:RUNNER_TEMP ('final-'+$e.Channel+'-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory $app|Out-Null;Copy-Item ($base+'/*') $app
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Disposable exact published executable fixture'
 $sentinels=@('lealinfo.db','license.dat','network.config','cash.dat','stock.dat','settings.json')
 foreach($n in $sentinels){[IO.File]::WriteAllText((Join-Path $app $n),'UNCHANGED-'+$n)}
 try{
  $hash=if($scenario -eq 'bad-sha'){'0'*64}else{$e.PackageSha256}
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Channel $e.Channel -Version $e.Version -Package $package -Sha256 $hash
  if($scenario -eq 'bad-sha'){if($LASTEXITCODE -eq 0){throw 'Wrong hash accepted'}}else{
   if($LASTEXITCODE -ne 0){throw 'Prepare failed'}
   foreach($n in $names){if((Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash.ToLowerInvariant() -ne $e.PreviousFiles.$n){throw 'Backup mismatch'}}
   $fault=if($scenario -eq 'interruption'){'copy-LealInfoPDV.dll'}else{''}
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel $e.Channel -Version $e.Version -Package $package -Sha256 $hash -Fault $fault
   if($scenario -eq 'success'){
    if($LASTEXITCODE -ne 0){throw 'Apply failed'}
    if((Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json).Phase -ne 'Committed'){throw 'No commit'}
   }else{
    if($LASTEXITCODE -ne 73){throw 'No actual interruption'}
    if(Test-Path (Join-Path $app 'LealInfoPDV.exe')){throw 'Mixed executable set exposed'}
    Start-ScheduledTask -TaskName (TaskName $app)
    $limit=(Get-Date).AddSeconds(35)
    do{Start-Sleep -Milliseconds 250;$s=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}while($s.Phase -ne 'Recovered' -and (Get-Date) -lt $limit)
    if($s.Phase -ne 'Recovered'){throw 'SYSTEM recovery failed'}
   }
  }
  foreach($n in $names){$expected=if($scenario -eq 'success'){$e.TargetFiles.$n}else{$e.PreviousFiles.$n};if((Get-FileHash (Join-Path $app $n)).Hash.ToLowerInvariant() -ne $expected){throw ('Wrong final binary '+$n)}}
  foreach($n in $sentinels){if([IO.File]::ReadAllText((Join-Path $app $n)) -ne ('UNCHANGED-'+$n)){throw ('Changed data '+$n)}}
  $results+=@{scenario=$scenario;pass=$true;channel=$e.Channel;baseline=$e.Baseline.Version;target=$e.Version;sha256=$e.PackageSha256}
  Write-Output ('PASS '+$e.Channel+' '+$e.Baseline.Version+' -> '+$e.Version+' '+$scenario)
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $app -Recurse -Force
 }
}
Remove-Item $base -Recurse -Force
$results|ConvertTo-Json -Depth 8|Set-Content validated/release-output/final-rollback.json -Encoding UTF8
$global:LASTEXITCODE=0
