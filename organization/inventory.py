import hashlib,io,json,os,struct,urllib.request,zipfile
from pathlib import Path
ROOT='https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/'
FEEDS={'GLOBAL':'version.json','001':'clients/001/version.json','002':'clients/002/version.json','003':'clients/003/global/version.json','004':'clients/004/version.json'}
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-organization-inventory'}),timeout=120) as r:return r.read()
out=Path('organization-evidence');out.mkdir(exist_ok=True)
entries=[]
for identity,path in FEEDS.items():
 feed=json.loads(get(ROOT+path));raw=get(feed['PackageUrl'])
 assert hashlib.sha256(raw).hexdigest().lower()==feed['Sha256'].lower(),identity
 folder=Path('inventory-packages')/identity;folder.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(io.BytesIO(raw)) as z:
  names=z.namelist()
  assert len(names)==len(set(n.casefold() for n in names))
  assert set(names)=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
  hashes={}
  for name in names:
   content=z.read(name);(folder/name).write_bytes(content);hashes[name]=hashlib.sha256(content).hexdigest()
 exe=(folder/'LealInfoPDV.exe').read_bytes();capsule=None
 if len(exe)>=56 and exe[-16:]==b'LEAL-PET-BOOT-01':
  length=struct.unpack('<q',exe[-56:-48])[0];assert 0<length<=32*1024*1024
  payload=exe[-56-length:-56];assert hashlib.sha256(payload).digest()==exe[-48:-16]
  with zipfile.ZipFile(io.BytesIO(payload)) as z:
   assert set(z.namelist())=={'manifest.json','Pet.dll'}
   m=json.loads(z.read('manifest.json'));assert m['Identity']==identity
   assert hashlib.sha256(z.read('Pet.dll')).hexdigest().lower()==m['Sha256'].lower()
   capsule=dict(Identity=m['Identity'],Version=m['Version'],AssemblySha256=m['Sha256'])
 deps=json.loads((folder/'LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
 entry=dict(Identity=identity,Feed=path,Manifest=feed,PackageVerified=True,Files=hashes,InitialPetCapsule=capsule,Dependencies=sorted(deps['libraries']))
 entries.append(entry)
(out/'distribution-inventory.json').write_text(json.dumps(entries,ensure_ascii=False,indent=2))
print('PASS: five exact published packages verified; no publication')
