import json,hashlib,zipfile,shutil,os
from pathlib import Path
r=Path('candidate');s=json.loads(Path('scope003.json').read_text())
for n,h in s['Protected'].items():assert hashlib.sha256((r/n).read_bytes()).hexdigest()==h,n
assert json.loads((r/'evidence/testes003-delivery.json').read_text())['Success']
assert json.loads((r/'evidence/testes003-rollback.json').read_text(encoding='utf-8-sig'))['Success']
def norm(p):
 d=json.loads(p.read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert norm(Path('installed003/LealInfoPDV.deps.json'))==norm(r/'publish/LealInfoPDV.deps.json')
out=Path('output');out.mkdir(exist_ok=True);name='LEAL_INFO_PDV_TESTES_003_DELIVERY_UPDATE.zip'
with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ['LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json']:z.write(r/'publish'/n,n)
for p in (r/'evidence').glob('testes003-*.json'):shutil.copy2(p,out/p.name)
shutil.copy2('scope003.json',out/'scope003.json')
(out/'candidate.json').write_text(json.dumps(dict(Client='003',GlobalVersion='10.457',DeliveryVersion='10.459',Package=name,Sha256=hashlib.sha256((out/name).read_bytes()).hexdigest(),Baseline=json.loads(Path('baseline003.json').read_text()),SourceCommit=os.environ['GITHUB_SHA'],FeedPath='clients/003/global/version.json',DependencySetUnchanged=True,ProtectedWorkerUnchanged=True,InstalledOnRealComputer=False),indent=2))
