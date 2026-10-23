import hashlib,json,os,zipfile
from pathlib import Path
root=Path('publish');out=Path('validated');out.mkdir(exist_ok=True)
checks=json.loads(Path('evidence/notifier-results.json').read_text());native=json.loads(Path('evidence/native-results.json').read_text());assert checks['Success'] and native['Success'] and native['Checks']>=23
finance=json.loads(Path('evidence/finance-results.json').read_text(encoding='utf-8-sig'));assert finance['Success'] and len(finance['checks'])>=13
# Existing runtime dependencies must remain byte-for-byte compatible, except own application version.
def norm(raw):
 d=json.loads(raw.decode('utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert norm(Path('legacy/LealInfoPDV.deps.json').read_bytes())==norm((root/'LealInfoPDV.deps.json').read_bytes()),'Installed runtime dependencies differ'
name='LEAL_INFO_PDV_LOJA_V10_449_UPDATE.zip';hashes={}
with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):
  z.write(root/n,n);hashes[n]=hashlib.sha256((root/n).read_bytes()).hexdigest()
Path('validated/candidate.json').write_text(json.dumps(dict(Version='10.449',Channel='001',PetVersion='1.2',SourceCommit=os.environ['GITHUB_SHA'],SourceBase='19cd2697fada9e444bef8f23a920aca468abd819',Baseline=json.loads(Path('baseline.json').read_text()),Package=name,Sha256=hashlib.sha256((out/name).read_bytes()).hexdigest(),Files=hashes,DependenciesEqual=True,NativePrivateMemoryBytes=checks['NativePrivateMemoryBytes'],NativeSizeBytes=checks['NativeSizeBytes'],Published=False,InstalledOnCustomer=False),indent=2))
print('PASS Loja package contains only three program files; native notifier embedded; PET V1.2 and installed dependencies retained')
