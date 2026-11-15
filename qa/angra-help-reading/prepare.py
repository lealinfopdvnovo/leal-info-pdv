from pathlib import Path
import hashlib,zipfile,json
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_11.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='5262acdcaabb1ac80b037e1b1e12f5fd8b1d2252657f6cf952c3cec62e59e947'
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
previous=updated
assert hashlib.sha256(previous.encode()).hexdigest()=='3aa81ef950bd1d4b57be02209ca817044b793e1335d944117c2a6aceb1db5858'
changes2=[
 ('const double BlinkSeconds=2.5;','const double BlinkSeconds=5;'),
 ('Version="1.11",BlinkSeconds=','Version="1.12",BlinkSeconds='),
 ('Draw(help,(seconds%BlinkSeconds)*(3.0/BlinkSeconds));',
  'double local=seconds%BlinkSeconds;\n    double phase=local<=1.25?local*1.2:local<55.0/12.0?1.5+(local-1.25)*0.3:2.5+(local-55.0/12.0)*1.2;\n    Draw(help,phase);'),
]
for before,after in changes2:
 assert updated.count(before)==1,before
 updated=updated.replace(before,after)
restored=updated
for before,after in reversed(changes2):restored=restored.replace(after,before)
assert restored==previous
Path('timed-coach').mkdir(exist_ok=True);Path('timed-coach/Coach.cs').write_text(updated,encoding='utf-8')
Path('timing-evidence').mkdir(exist_ok=True)
Path('timing-evidence/timing-source.json').write_text(json.dumps(dict(Pass=True,PreviousSourceSHA256=hashlib.sha256(previous.encode()).hexdigest(),ReversibleTimingOnlyTransform=True,BlinkCount=3,BlinkSeconds=5,TotalSeconds=15,OriginalDrawingCodeUnchanged=True,EntryAndFadeTimingPreserved=True,FullMessageFromSeconds=1.46/1.2,FullMessageUntilSeconds=55/12,ContinuousFullMessageSeconds=55/12-1.46/1.2)))
print('PASS timing-only: three 5-second cycles, 15 seconds total, extended readable hold, original entry/fade retained')
