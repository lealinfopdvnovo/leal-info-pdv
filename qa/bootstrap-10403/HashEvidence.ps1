param([ValidateSet('001','003')][string]$Client)
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows'){throw 'Disposable Windows CI only'}
$source=if($Client -eq '001'){'3dfab93e04ebb1cfef72606c2d52a76c05eead94'}else{'d1525b64db03bc4608aabe020ecfc6e200ec2f05'}
$text=(git show "${source}:UpdateManager.cs") -join "`n"
$start=$text.IndexOf('if(m.Sha256.Length!=64');$end=$text.IndexOf('Licensing.UpdatePayloadPolicy.Validate(zip);',$start)
if($start -lt 0 -or $end -le $start){throw 'Cannot extract actual hash guard'}
$guard=$text.Substring($start,$end-$start)
$code='using System;using System.IO;using System.Linq;public static class HashGuard'+$Client+' {public static void Check(string zip,string expected){var m=new {Sha256=expected};'+$guard+'}}'
$type=Add-Type -TypeDefinition $code -PassThru
$file=Join-Path $env:RUNNER_TEMP ('hash-fixture-'+$Client+'.zip')
[IO.File]::WriteAllBytes($file,[byte[]](1,2,3,4))
$expected=(Get-FileHash $file).Hash
$type.GetMethod('Check').Invoke($null,@($file,$expected))
[IO.File]::WriteAllBytes($file,[byte[]](1,2,3,5))
$rejected=$false
try{$type.GetMethod('Check').Invoke($null,@($file,$expected))}catch{if($_.Exception.InnerException -is [IO.InvalidDataException] -or $_.Exception.ToString().Contains('InvalidDataException')){$rejected=$true}else{throw}}
if(!$rejected){throw 'Corruption not rejected'}
@{client=$Client;source=$source;classification='unit: exact C# hash guard, local corrupted download fixture, no network';validAccepted=$true;corruptedRejected=$true} | ConvertTo-Json | Set-Content "qa/bootstrap-10403/evidence/hash-$Client.json"
Write-Host "PASS exact hash guard $Client; corrupted bytes rejected before application"
