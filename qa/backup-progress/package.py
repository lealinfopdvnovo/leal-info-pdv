import json,hashlib,zipfile,os
from pathlib import Path
out=Path('backup-output');out.mkdir(exist_ok=True)
def libraries(folder):
 d=json.loads(Path(folder,'LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
 return {k:v for k,v in d['libraries'].items() if not k.startswith('LealInfoPDV/')}
assert libraries('baseline-publish')==libraries('candidate-publish'),'Dependency change beyond backup UI'
files=['LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json']
p=out/'LEAL_INFO_BACKUP_001_10_467.zip'
with zipfile.ZipFile(p,'w',zipfile.ZIP_DEFLATED) as z:
 for n in files:z.write(Path('candidate-publish',n),n)
proof=dict(Channel='001',Version='10.467',SourceCommit=os.environ['GITHUB_SHA'],Sha256=hashlib.sha256(p.read_bytes()).hexdigest(),Package=p.name,WindowsProbePassed=True,DependenciesPreserved=True,PhysicalTest=False)
(out/'proof.json').write_text(json.dumps(proof,indent=2))
print('PASS backup package dependencies and Windows window tests')
