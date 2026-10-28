"""Casa003 delivery only. No global feed or client data operations."""
from pathlib import Path
import hashlib
import json
import os
import sys
import zipfile

VERSION='10.462'
PACKAGE='LEAL_INFO_PDV_CASA_V10_462_UPDATE.zip'
ROOT=Path('candidate')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

if sys.argv[1]=='version':
    changed=[]
    for name in ('UpdateManager.cs','LealInfoPDV.csproj'):
        p=ROOT/name; text=p.read_text(encoding='utf-8-sig')
        assert '10.461' in text, name
        p.write_text(text.replace('10.461',VERSION),encoding='utf-8')
        changed.append(name)
    Path('delivery-version-proof.json').write_text(json.dumps(dict(
        Version=VERSION,MetadataOnly=changed,Client='003',Published=False)))
elif sys.argv[1]=='package':
    def normalized(path):
        d=json.loads(path.read_text(encoding='utf-8-sig'))
        for bag in (d['libraries'],*d['targets'].values()):
            for key in list(bag):
                if key.startswith('LealInfoPDV/'):del bag[key]
        return d
    assert normalized(Path('published-461/LealInfoPDV.deps.json'))==normalized(Path('candidate-publish/LealInfoPDV.deps.json')), 'Published dependency contract changed'
    assert json.loads(Path('evidence/validation.json').read_text())['Pass']
    assert 'PASS' in Path('evidence/cash-test-results.txt').read_text(encoding='utf-8-sig')
    out=Path('release-output');out.mkdir(exist_ok=True)
    with zipfile.ZipFile(out/PACKAGE,'w',zipfile.ZIP_DEFLATED) as z:
        for name in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):
            z.write(Path('candidate-publish')/name,name)
    Path('build-entry.json').write_text(json.dumps(dict(Package=PACKAGE,Channel='GLOBAL',Version=VERSION)))
    (out/'candidate.json').write_text(json.dumps(dict(
        Version=VERSION,TargetClientCode='003',SourceCommit=os.environ['GITHUB_SHA'],
        Package=PACKAGE,Sha256=sha(out/PACKAGE),Published=False,EntryAnimationPreserved=True,
        LiaButtonRemoved=True,DependenciesUnchanged=True),indent=2))
elif sys.argv[1]=='gate':
    results=json.loads(Path('release-output/transaction-results.json').read_text(encoding='utf-8-sig'))
    assert len(results)==19 and all(x['pass'] for x in results)
    candidate=json.loads(Path('release-output/candidate.json').read_text())
    assert sha(Path('release-output')/PACKAGE)==candidate['Sha256']
    (Path('release-output')/'validation-gate.json').write_text(json.dumps(dict(
        Pass=True,Version=VERSION,TargetClientCode='003',TransactionScenarios=19,
        LiaButtonRemoved=True,EntryAnimationPreserved=True,DependenciesUnchanged=True,
        ProductionChanged=False,Published=False),indent=2))
else:
    raise ValueError(sys.argv[1])
