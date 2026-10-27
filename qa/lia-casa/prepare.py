import hashlib,json,os,sys,shutil,zipfile
from pathlib import Path
assert os.environ['CHANNEL']=='CASA'
r=Path('candidate')
def hashes():
 return {p.relative_to(r).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in r.rglob('*') if p.is_file() and not any(x in ('.git','bin','obj','publish','evidence') for x in p.relative_to(r).parts)}
mode=sys.argv[1] if len(sys.argv)>1 else 'prepare'
if mode=='prepare':
 for n in ('LoginForm.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/login457/Program.cs'):
  p=r/n;t=p.read_text(encoding='utf-8-sig').replace('10.457','10.458').replace('Minor==457','Minor==458');p.write_text(t)
 # Existing proof lists these version files as allowed changes; refresh the test source hash.
 proof=json.loads(Path('scope-proof.json').read_text())
 for n in ('qa/login457/Program.cs',):
  if n in proof['ProtectedSourceHashes']:proof['ProtectedSourceHashes'][n]=hashlib.sha256((r/n).read_bytes()).hexdigest()
 Path('scope-proof.json').write_text(json.dumps(proof))
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
 def normalized(p):
  d=json.loads(p.read_text(encoding='utf-8-sig'))
  for bag in (d['libraries'],*d['targets'].values()):
   for k in list(bag):
    if k.startswith('LealInfoPDV/'):del bag[k]
  return d
 assert normalized(Path('legacy/LealInfoPDV.deps.json'))==normalized(r/'publish/LealInfoPDV.deps.json')
 out=Path('output/CASA');out.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(out/'LEAL_INFO_PDV_CASA_V10_458_UPDATE.zip','w',zipfile.ZIP_DEFLATED) as z:
  for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(r/'publish'/n,n)
 shutil.copyfile('lia-scope-proof.json',out/'scope-proof.json')
 for p in (r/'evidence').glob('*'):
  if p.is_file():shutil.copyfile(p,out/p.name)
 (out/'candidate.json').write_text(json.dumps(dict(Version='10.458',Channel='CASA',SourceCommit=os.environ['GITHUB_SHA'],Package='LEAL_INFO_PDV_CASA_V10_458_UPDATE.zip',Sha256=hashlib.sha256((out/'LEAL_INFO_PDV_CASA_V10_458_UPDATE.zip').read_bytes()).hexdigest(),Published=False)))
 print('Candidate only; no feed, release, credentials or customer installation changed')
else:raise ValueError(mode)
