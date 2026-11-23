from pathlib import Path
import json,hashlib,zipfile
root=Path('destination-10469');proof=json.loads(Path('completion-evidence/destination.json').read_text())
assert proof['Pass'] and proof['Target']=='10.469' and proof['Pet']=='1.3'
# Validate the exact dependency graph against both installed origin packages.
def normalized(p):
 x=json.loads(p.read_text(encoding='utf-8-sig'))
 for bag in [x['libraries'],*x['targets'].values()]:
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):
    v=bag.pop(k);bag['LealInfoPDV/APP']=v
 return x
for origin in ['legacy-001','legacy-001-10468']:
 assert normalized(root/'LealInfoPDV.deps.json')==normalized(Path(origin)/'LealInfoPDV.deps.json'), 'Dependency graph incompatible with '+origin
pkg=root/'initial-transition.zip'
assert hashlib.sha256(pkg.read_bytes()).hexdigest()==proof['PackageSha256']
with zipfile.ZipFile(pkg) as z:
 assert len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for n in z.namelist():assert hashlib.sha256(z.read(n)).hexdigest()==proof['TargetHashes'][n]
for path in ['completion-evidence/joint-recovery.json','recovery468-evidence/joint-recovery.json']:
 checks=json.loads(Path(path).read_text(encoding='utf-8-sig'));assert len(checks)==7 and all(x['Pass'] for x in checks)
# No claim of real licensed startup. Publisher uses only these validated bytes.
proof.update(OriginVersions=['10.467','10.468'],DependenciesPreserved=True,BackupWindowProbePassed=True,RecoveryBothOrigins=True,SourceCommit=__import__('os').environ['GITHUB_SHA'],RunId=__import__('os').environ['GITHUB_RUN_ID'],Published=False)
Path('distribution-evidence/distribution.json').write_text(json.dumps(proof,indent=2))
print('PASS distribution policy: three allowed files, exact SHA, dependencies match BOTH installed origins, recovery matrix approved')
