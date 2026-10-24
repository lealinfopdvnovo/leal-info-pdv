import hashlib,json,os,shutil,zipfile
from pathlib import Path
r=Path('candidate');p=json.loads(Path('gelamaq-scope.json').read_text())
for name,h in p['ProtectedSourceHashes'].items():assert hashlib.sha256((r/name).read_bytes()).hexdigest()==h,name
q=json.loads((r/'evidence/gelamaq-finance.json').read_text());assert q['Success']
checks=json.loads((r/'evidence/gelamaq-rollback.json').read_text(encoding='utf-8-sig'));assert len(checks)==7 and all(x['Pass'] for x in checks)
def norm(path):
 d=json.loads(path.read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert norm(Path('published002/LealInfoPDV.deps.json'))==norm(r/'publish/LealInfoPDV.deps.json'),'New dependency would be incompatible with installed update'
out=Path('output');out.mkdir(exist_ok=True);name='LEAL_INFO_PDV_GELAMAQ_PET_V1_1_UPDATE.zip'
with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(r/'publish'/n,n)
shutil.copy2('gelamaq-scope.json',out/'scope.json')
for path in (r/'evidence').glob('*'):
 if path.is_file():shutil.copy2(path,out/path.name)
baseline=json.loads(Path('baseline-002.json').read_text())
(out/'candidate.json').write_text(json.dumps(dict(Client='002',PetVersion='1.1',GlobalVersion='10.457',DeliveryRevision='10.458',SourceCommit=os.environ['GITHUB_SHA'],SourceBase='735f3f6b0f933f25585c0c9e7e50551dae9a056d',Package=name,Sha256=hashlib.sha256((out/name).read_bytes()).hexdigest(),Baseline=baseline,FeedPath='clients/002/version.json',ProtectedWorkerUnchanged=True,DependencySetUnchanged=True,PublicationAllowedAfterTests=True,Published=False,InstalledOnCustomer=False),indent=2))
print('PASS Gelamaq PET V1.1 package; GLOBAL 10.457; isolated delivery revision 10.458; exact 3-file protected payload')
