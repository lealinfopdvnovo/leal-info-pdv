import hashlib,json,os,sys,shutil,zipfile
from pathlib import Path
assert os.environ['CHANNEL']=='CASA'
r=Path('candidate')
def hashes():
 return {p.relative_to(r).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in r.rglob('*') if p.is_file() and not any(x in ('.git','bin','obj','publish','evidence') for x in p.relative_to(r).parts)}
mode=sys.argv[1] if len(sys.argv)>1 else 'prepare'
if mode=='prepare':
 assert 'btnAssistenteAI' not in (r/'MainForm.cs').read_text(encoding='utf-8-sig'), 'Unexpected baseline: LIA button already present'
 Path('lia-before.json').write_text(json.dumps(hashes()))
elif mode=='verify':
 before=json.loads(Path('lia-before.json').read_text());after=hashes()
 assert before.keys()==after.keys()
 assert [n for n in before if before[n]!=after[n]]==['MainForm.cs']
 s=(r/'MainForm.cs').read_text(encoding='utf-8-sig')
 for marker in ('btnAssistenteAI','Visible = Licensing.InstallationLicense.HasLia','licAiButton.Click +=','--voice','EncerrarLicAiComPdv','LealInfoPDV.LicAiStatus'):
  assert marker in s,marker
 # Retain original baseline proof; the only additional allowed source change is MainForm.
 proof=json.loads(Path('scope-proof.json').read_text())
 assert proof['ProtectedSourceHashes']['MainForm.cs']==before['MainForm.cs']
 proof['Changes'].append('MainForm.cs');proof['ProtectedSourceHashes'].pop('MainForm.cs')
 Path('lia-scope-proof.json').write_text(json.dumps(proof,indent=2))
 tests=r/'qa/lia-casa';tests.mkdir(parents=True,exist_ok=True)
 for n in ('Program.cs','Tests.csproj'):shutil.copyfile(Path('controller/qa/lia-casa')/n,tests/n)
elif mode=='package':
 proof=json.loads(Path('lia-scope-proof.json').read_text())
 for n,h in proof['ProtectedSourceHashes'].items():assert hashlib.sha256((r/n).read_bytes()).hexdigest()==h,n
 assert json.loads((r/'evidence/lia-results.json').read_text())['Success']
 assert json.loads((r/'evidence/login457-results.json').read_text())['Success']
 assert all(x['Pass'] for x in json.loads((r/'evidence/rollback.json').read_text(encoding='utf-8-sig')))
 out=Path('output/CASA');out.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(out/'CANDIDATA_LIA_CASA_NAO_PUBLICADA.zip','w',zipfile.ZIP_DEFLATED) as z:
  for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(r/'publish'/n,n)
 shutil.copyfile('lia-scope-proof.json',out/'scope-proof.json')
 for p in (r/'evidence').glob('*'):
  if p.is_file():shutil.copyfile(p,out/p.name)
 print('Candidate only; no feed, release, credentials or customer installation changed')
else:raise ValueError(mode)
