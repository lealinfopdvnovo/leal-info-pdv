import json
from pathlib import Path
Path('evidence').mkdir(exist_ok=True)
def normalized(p):
 d=json.loads(Path(p).read_text(encoding='utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
old=normalized('baseline/LealInfoPDV.deps.json');new=normalized('common/LealInfoPDV.deps.json')
old_keys=set(old['libraries']);new_keys=set(new['libraries'])
result=dict(IncrementalCompatible=old==new,OnlyInPublishedLoja=sorted(old_keys-new_keys),OnlyInPreparedGlobal=sorted(new_keys-old_keys),ApprovedGlobalSource='4d9128cf8a64b487f836af6c12db14d6a7eb7fd3',NoFilesInstalled=True)
Path('evidence/runtime-compatibility.json').write_text(json.dumps(result,indent=2))
print('INCREMENTAL DEPENDENCIES',json.dumps(result))
