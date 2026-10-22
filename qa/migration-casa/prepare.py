import hashlib,io,json,struct,urllib.request,zipfile,subprocess
from pathlib import Path
legacy='3907959a57b7fa032b8a58392f27d4962ab64f54'
Path('evidence').mkdir(exist_ok=True)
url='https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/003/version.json'
feed=json.load(urllib.request.urlopen(url));assert feed['Version']=='10.443' and feed['TargetClientCode']=='003' and feed['SourceCommit']==legacy
package=urllib.request.urlopen(feed['PackageUrl']).read();assert hashlib.sha256(package).hexdigest()==feed['Sha256']
names=['LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json']
with zipfile.ZipFile(io.BytesIO(package)) as archive:
 assert sorted(archive.namelist())==sorted(names)
 Path('legacy').mkdir(exist_ok=True)
 for name in names:Path('legacy',name).write_bytes(archive.read(name))
def normalized(p):
 d=json.loads(p.read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert normalized(Path('legacy/LealInfoPDV.deps.json'))==normalized(Path('publish/LealInfoPDV.deps.json'))
module=Path('extensions/profiles/003/bin/Release/net8.0-windows10.0.19041.0/LealInfoPDV.Pet.Profile.dll').read_bytes()
manifest=dict(Identity='003',Version='1.1',ApiVersion=1,Assembly='Pet.dll',Sha256=hashlib.sha256(module).hexdigest())
stream=io.BytesIO()
with zipfile.ZipFile(stream,'w',compression=zipfile.ZIP_DEFLATED) as capsule:
 capsule.writestr('manifest.json',json.dumps(manifest).encode());capsule.writestr('Pet.dll',module)
capsule=stream.getvalue();exe=Path('publish/LealInfoPDV.exe').read_bytes()+capsule+struct.pack('<q',len(capsule))+hashlib.sha256(capsule).digest()+b'LEAL-PET-BOOT-01'
Path('payload').mkdir(exist_ok=True)
for name in names:Path('payload',name).write_bytes(exe if name.endswith('.exe') else Path('publish',name).read_bytes())
assert Path('payload/LealInfoPDV.dll').read_bytes()==Path('publish/LealInfoPDV.dll').read_bytes()
with zipfile.ZipFile('migration-update.zip','w',compression=zipfile.ZIP_DEFLATED) as archive:
 for name in names:archive.write(Path('payload',name),name)
Path('qa/migration-casa/legacy-worker.ps1').write_bytes(subprocess.check_output(['git','show',legacy+':TransactionalUpdate.ps1']))
result=dict(CandidateVersion='10.444',PetVersion='1.1',LegacyVersion=feed['Version'],LegacySource=legacy,LegacyPackageSha256=feed['Sha256'],PackageSha256=hashlib.sha256(Path('migration-update.zip').read_bytes()).hexdigest(),Files={n:hashlib.sha256(Path('payload',n).read_bytes()).hexdigest() for n in names},Profile=manifest,Published=False,InstalledOnCustomer=False,IndependentPetUpdaterCompleted=False)
Path('evidence/preparation.json').write_text(json.dumps(result,indent=2))
print('PASS verified actual published package; exact dependency compatibility; neutral DLL; three-file bridge with independent profile')
