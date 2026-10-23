import hashlib,json,zipfile,shutil
from pathlib import Path
core=['LealInfoPDV.deps.json','LealInfoPDV.dll','LealInfoPDV.exe']
def normalized(p):
 d=json.loads(p.read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert normalized(Path('legacy/LealInfoPDV.deps.json'))==normalized(Path('candidate/bridge458/LealInfoPDV.deps.json'))
assert normalized(Path('legacy/LealInfoPDV.deps.json'))==normalized(Path('candidate/bridge459/LealInfoPDV.deps.json'))
def archive(folder,name):
 with zipfile.ZipFile(name,'w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(folder.rglob('*')):
   if p.is_file():z.write(p,p.relative_to(folder).as_posix())
first=Path('first-payload');first.mkdir(exist_ok=True)
final=Path('final-payload');final.mkdir(exist_ok=True)
for n in core:
 shutil.copy2(Path('candidate/bridge458')/n,first/n)
 shutil.copy2(Path('candidate/bridge459')/n,final/n)
for n in json.loads(Path('lia-hashes.json').read_text()):
 dest=final/n;dest.parent.mkdir(parents=True,exist_ok=True)
 shutil.copy2(Path('controller/lia-candidate')/n.removeprefix('LIC-AI/'),dest)
archive(first,'bridge-first.zip');archive(final,'bridge-final.zip')
p=final/'LIC-AI/LicAi.dll';old=p.read_bytes();p.write_bytes(b'TAMPERED')
archive(final,'bridge-tampered.zip');p.write_bytes(old)
Path('bridge-packages.json').write_text(json.dumps({n:hashlib.sha256(Path(n).read_bytes()).hexdigest() for n in ('bridge-first.zip','bridge-final.zip')},indent=2))
print('PASS: exact installed runtime dependency compatibility; separate 3-file bootstrap and pinned LIA payload')

with zipfile.ZipFile('bridge-duplicate.zip','w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.dll','LealInfoPDV.dll','LealInfoPDV.exe'):z.write(first/n,n)
