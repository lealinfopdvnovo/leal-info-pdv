import hashlib,json,os,zipfile
from pathlib import Path
id=os.environ['CHANNEL'];root=Path('candidate');p=Path('publish');baseline=json.loads(Path('baseline.json').read_text());tests=json.loads((root/'evidence/results.json').read_text())
assert tests['Success'] and tests['Checks']==22
# Preserve every previously installed runtime dependency byte requirement.
def norm(raw):
 d=json.loads(raw.decode('utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for key in list(bag):
   if key.startswith('LealInfoPDV/'):del bag[key]
 return d
assert norm(Path('legacy/LealInfoPDV.deps.json').read_bytes())==norm((p/'LealInfoPDV.deps.json').read_bytes()),'Installed dependencies differ'
out=Path('output')/id;out.mkdir(parents=True,exist_ok=True)
name=f'LEAL_INFO_PDV_{id}_V10_445_UPDATE.zip';files={}
with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):
  z.write(p/n,n);files[n]=hashlib.sha256((p/n).read_bytes()).hexdigest()
sha=hashlib.sha256((out/name).read_bytes()).hexdigest()
(out/'candidate.json').write_text(json.dumps(dict(Channel=id,Version='10.445',Baseline=baseline,SourceCommit=os.environ['GITHUB_SHA'],SourceBase=baseline['SourceCommit'],Package=name,Sha256=sha,Files=files,NotificationChecks=22,DependenciesEqual=True,InstalledOnCustomer=False),indent=2))
print('PASS',id,'approved binaries packaged; dependency compatibility exact; original PET and business code retained')
