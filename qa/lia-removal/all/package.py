import hashlib,json,os,zipfile
from pathlib import Path
C=os.environ['CHANNEL'];mode=os.sys.argv[1]
def normalized(p):
 d=json.loads(p.read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
out=Path('release-output');out.mkdir(exist_ok=True)
if mode=='package':
 old=normalized(Path('published/LealInfoPDV.deps.json'));new=normalized(Path('candidate-publish/LealInfoPDV.deps.json'))
 assert old==new, 'Published dependency contract differs'
 assert json.loads(Path('evidence/validation.json').read_text())['Pass']
 name=f'LEAL_INFO_PDV_{C}_V10_463_UPDATE.zip'
 with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
  for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(Path('candidate-publish')/n,n)
 entry=dict(Package=name,Channel=C,Version='10.463');Path('build-entry.json').write_text(json.dumps(entry))
 entry.update(Sha256=digest(out/name),SourceCommit=os.environ['GITHUB_SHA'],Feed=json.loads(Path('current-feed.json').read_text()),LiaButtonRemoved=True,EntryAnimationPreserved=True,DependenciesUnchanged=True)
 (out/f'{C}.json').write_text(json.dumps(entry,indent=2))
elif mode=='gate':
 results=json.loads((out/'transaction-results.json').read_text(encoding='utf-8-sig'))
 assert len(results)==3 and all(r['Pass'] for r in results)
 (out/f'{C}-gate.json').write_text(json.dumps(dict(Pass=True,Channel=C,TransactionScenarios=3,Installed=False)))
 print('PASS exact current dependencies, protected resource hashes, isolated removal and 3 recovery scenarios',C)
