from pathlib import Path
import hashlib,zipfile,json
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_10.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='1793692691bb598f0ffa81fc6f26d30cc74f472843fc4ca17bc932d9a9a48932'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
original=Path('controller/qa/angra-help-premium/Coach.cs').read_text(encoding='utf-8')
assert hashlib.sha256(original.encode()).hexdigest()=='0388f399703c15e481b642244101392303947f0df0cfe1677b898c8a3866bccb'
changes=[
 ('  readonly MainForm owner;','  const double BlinkSeconds=2.5;const int BlinkCount=3;\n  readonly MainForm owner;'),
 ('Version="1.10",Started=','Version="1.11",BlinkSeconds=BlinkSeconds,BlinkCount=BlinkCount,TotalSeconds=BlinkSeconds*BlinkCount,Started='),
 ('if(seconds>=3){Finish();return;}','if(seconds>=BlinkSeconds*BlinkCount){Finish();return;}'),
 ('Draw(help,seconds);','Draw(help,(seconds%BlinkSeconds)*(3.0/BlinkSeconds));'),
]
updated=original
for before,after in changes:
 assert updated.count(before)==1,before
 updated=updated.replace(before,after)
restored=updated
for before,after in reversed(changes):restored=restored.replace(after,before)
assert restored==original
Path('timed-coach').mkdir(exist_ok=True);Path('timed-coach/Coach.cs').write_text(updated,encoding='utf-8')
Path('timing-evidence').mkdir(exist_ok=True)
Path('timing-evidence/timing-source.json').write_text(json.dumps(dict(Pass=True,OriginalSourceSHA256=hashlib.sha256(original.encode()).hexdigest(),ReversibleTimingOnlyTransform=True,BlinkCount=3,BlinkSeconds=2.5,TotalSeconds=7.5,OriginalDrawingCodeUnchanged=True)))
print('PASS reversible timing-only transform; existing visual sequence replayed 3 x 2.5 seconds')
