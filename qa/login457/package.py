import hashlib,json,os,shutil,zipfile
from pathlib import Path
c=os.environ['CHANNEL'];assert c in ('002','CASA','004')
r=Path('candidate');proof=json.loads(Path('scope-proof.json').read_text());assert proof['Changes']==['LoginForm.cs','UpdateManager.cs','LealInfoPDV.csproj'] or set(proof['Changes'])=={'LoginForm.cs','UpdateManager.cs','LealInfoPDV.csproj'}
for n,h in proof['ProtectedSourceHashes'].items():assert hashlib.sha256((r/n).read_bytes()).hexdigest()==h,n
q=json.loads((r/'evidence/login457-results.json').read_text());rollback=json.loads((r/'evidence/rollback.json').read_text(encoding='utf-8-sig'));assert q['Success'] and len(rollback)==3 and all(x['Pass'] for x in rollback)
def norm(p):
 d=json.loads(p.read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert norm(Path('legacy/LealInfoPDV.deps.json'))==norm(r/'publish/LealInfoPDV.deps.json')
out=Path('output')/c;out.mkdir(parents=True,exist_ok=True);name=f'LEAL_INFO_PDV_{c}_V10_457_UPDATE.zip'
with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(r/'publish'/n,n)
for p in (r/'evidence').glob('*'):
 if p.is_file():shutil.copy2(p,out/p.name)
shutil.copy2('scope-proof.json',out/'scope-proof.json')
(out/'candidate.json').write_text(json.dumps(dict(Channel=c,Version='10.457',SourceCommit=os.environ['GITHUB_SHA'],SourceBase=os.environ['SOURCE_BASE'],Package=name,Sha256=hashlib.sha256((out/name).read_bytes()).hexdigest(),Baseline=json.loads(Path('baseline.json').read_text()),FeedPath=Path('feed-path.txt').read_text(),ScopeApproved=True,UpdaterLogicChanged=False,PetChanged=False,Loja001Touched=False,Published=False,InstalledOnCustomer=False),indent=2))
print('PASS',c,'login-only highlight, version 10.457, 3-file internal package, unchanged dependencies and rollback')
