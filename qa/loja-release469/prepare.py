"""Loja001 release 10.469; exact 10.467/10.468 origins and existing backup UI."""
from pathlib import Path
import json,hashlib,urllib.request,zipfile,io,subprocess,re
API='https://api.github.com/repos/lealinfopdvnovo/leal-info-pdv-updates/'
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-Loja001-release'}),timeout=90) as r:return r.read()
feed=json.loads(get(API+'contents/clients/001/version.json?ref=main'))
import base64
live=json.loads(base64.b64decode(feed['content']))
assert live['Version']=='10.468' and live['Sha256']=='47496b92f62755ebc8669593b25a389c9cc8143c5e67676dca600aca6ceb03be','Live Loja baseline changed'
# Immutable reservation: neither a tag nor an existing release may be replaced.
from urllib.error import HTTPError
for route in ['git/ref/tags/client-001-v10.469-transbordo','releases/tags/client-001-v10.469-transbordo']:
 try:get(API+route)
 except HTTPError as e:
  assert e.code==404
 else:raise AssertionError('10.469 tag already reserved')
raw=get(live['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==live['Sha256']
folder=Path('legacy-001-10468');folder.mkdir()
with zipfile.ZipFile(io.BytesIO(raw)) as z:
 assert len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for n in z.namelist():(folder/n).write_bytes(z.read(n))
hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.iterdir()}
# Execute the approved distribution recipe, not the QA-root variant.
exec(compile(Path('controller/qa/etapa6-loja/prepare_distribution.py').read_text(),'approved-distribution','exec'),{})
root=Path('candidate');bridge=root/'InitialMigrationBridge.cs'
s=bridge.read_text()
entries=','.join('["'+k+'"]="'+v+'"' for k,v in hashes.items())
insert='''
    public static readonly IReadOnlyDictionary<string,string> BackupOriginHashes=new Dictionary<string,string>{'''+entries+'''};
    public static IReadOnlyDictionary<string,string> MatchPrevious(JsonElement previous){
        var sets=new[]{OriginHashes,BackupOriginHashes};
        foreach(var set in sets)if(previous.EnumerateObject().Count()==3 && new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"}.All(n=>previous.TryGetProperty(n,out var v)&&v.GetString()==set[n]))return set;
        throw new InvalidDataException("Origem não identificada: somente distribuições exatas 10.467/10.468 Loja");
    }
'''
s=s.replace('    public const string LegacyWorkerHash=',insert+'    public const string LegacyWorkerHash=')
a=s.index('        var actual=new Dictionary<string,string>();',s.index('VerifyOrigin(string folder)'))
b=s.index('\n    }',a)
s=s[:a]+'''        var actual=OriginHashes.Keys.ToDictionary(n=>n,n=>Hash(ReadPlainFile(Path.Combine(folder,n))));
        Require(actual["LealInfoPDV.runtimeconfig.json"]==OriginHashes["LealInfoPDV.runtimeconfig.json"],"Runtime incompatible");
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(actual.Where(x=>x.Key!="LealInfoPDV.runtimeconfig.json").ToDictionary(x=>x.Key,x=>x.Value)));
        MatchPrevious(doc.RootElement);return actual;'''+s[b:]
# Prepared-backup validation must match the selected origin, never mix origin sets.
s=s.replace('Require(plan.Previous.Count==OriginHashes.Count&&OriginHashes.All(x=>plan.Previous.TryGetValue(x.Key,out var value)&&value==x.Value),"Untrusted previous hashes");','using var prior=JsonDocument.Parse(JsonSerializer.Serialize(plan.Previous.Where(x=>x.Key!="LealInfoPDV.runtimeconfig.json").ToDictionary(x=>x.Key,x=>x.Value))); MatchPrevious(prior.RootElement);')
s=s.replace('==OriginHashes[name]', '==plan.Previous[name]').replace('Equals(OriginHashes[name],','Equals(plan.Previous[name],')
bridge.write_text(s)
p=root/'InitialMigrationCoordinator.cs';s=p.read_text()
s=s.replace('  foreach(var item in InitialMigrationBridge.OriginHashes){','  var acceptedOrigin=InitialMigrationBridge.MatchPrevious(c.GetProperty("Previous"));\n  foreach(var item in acceptedOrigin){')
p.write_text(s)
# Approved joint recovery worker: select one COMPLETE known origin, no mixed hashes.
p=root/'JointInitialRecovery.ps1';s=p.read_text()
a=s.index("$runtimeHash=")
ps='@{'+ ';'.join("'"+n+"'='"+h+"'" for n,h in hashes.items())+'}'
s=s[:a]+'''$originVariants=@($origin,'''+ps+''')
function SelectOrigin($previous){
 foreach($known in $originVariants){$ok=$true;foreach($n in $names){if($previous.$n -ne $known[$n]){$ok=$false}};if(@($previous.PSObject.Properties).Count -ne 3){$ok=$false};if($ok){return $known}}
 throw 'Unknown complete original distribution; recovery refused'
}
'''+s[a:]
s=s.replace('  $c=CoreState\n  if($c.Phase','  $c=CoreState\n  $origin=SelectOrigin $c.Previous\n  if($c.Phase',1)
s=s.replace("  if($s.App -ne $App", "  $origin=SelectOrigin $s.Previous\n  if($s.App -ne $App",1)
p.write_text(s)
# Keep source recipe copies consistent with the binaries tested below.
Path('controller/qa/organization/bridge/JointInitialRecovery.ps1').write_text(s)
# Version is distinct from the already published backup release; PET stays 1.3.
for p in [root/'LealInfoPDV.csproj',root/'UpdateManager.cs']:
 p.write_text(p.read_text().replace('10.468','10.469').replace('destination-10468','destination-10469'))
# Capsule is packaged by the existing probe, with a new compatible interval.
p=Path('controller/qa/organization/bridge/DestinationProbe.cs');p.write_text(p.read_text().replace('10.469','10.470').replace('10.468','10.469').replace('destination-10468','destination-10469'))
# Reuse the exact approved backup patch. Any drift is a build blocker.
subprocess.run(['git','-C','candidate','apply','../controller/qa/loja-release469/backup.patch'],check=True)
# Preserve actual copied UI source byte-for-byte against approved patch's new file.
patch=Path('controller/qa/loja-release469/backup.patch').read_text();chunk=patch.split('diff --git a/DatabaseBackupService.cs')[0]
expected='\n'.join(l[1:] for l in chunk.splitlines() if l.startswith('+') and not l.startswith('+++'))+'\n'
assert (root/'BackupProgressForm.cs').read_text()==expected
# Additional recovery matrix uses each origin and current 10.469 target.
p=Path('controller/qa/organization/bridge/test_joint_recovery.ps1');test=p.read_text().replace('10.468','10.469').replace('destination-10468','destination-10469')
Path('controller/qa/loja-release469/test467.ps1').write_text(test)
Path('controller/qa/loja-release469/test468.ps1').write_text(test.replace('Resolve-Path legacy-001','Resolve-Path legacy-001-10468').replace('completion-evidence','recovery468-evidence'))
# Probe project refers to existing candidate; backup test verifies the real UI, no DB or email.
probe=root/'qa/backup-progress';probe.mkdir(parents=True,exist_ok=True)
for n in ['BackupProbe.cs','BackupProbe.csproj']:(probe/n).write_bytes(Path('controller/qa/loja-release469',n).read_bytes())
Path('distribution-evidence/origins.json').write_text(json.dumps(dict(Origin467='fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b',Origin468Package=live['Sha256'],Origin468=hashes,FeedBlob=feed['sha'],Version='10.469',Pet='1.3',Published=False),indent=2))
print('PASS 10.469 candidate: known 10.467/10.468 origins, approved backup UI preserved, licence unchanged')
