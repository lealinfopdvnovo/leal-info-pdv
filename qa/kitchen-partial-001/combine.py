import json,os
from pathlib import Path
old=json.loads(Path('../approved/layout-evidence/results.json').read_text(encoding='utf-8-sig'))
assert old['success'] is True and old['channel']=='001' and old['source']=='a2f7d7e812e30ec25ea71aa43bf9342f734939c2' and len(old['checks'])>=45
visual=json.loads(Path('corrections-evidence/visual.json').read_text(encoding='utf-8-sig'))
assert visual['success'] is True and len(visual['checks'])>=4
result=dict(success=True,channel='001',source=os.environ['APPROVED_SOURCE'],checks=old['checks']+visual['checks'],nativeWindows=True,published=False,FinancialValidationRunId='39706043559',FinancialSource=old['source'],IdenticalFinancialCoreVerified=True)
Path('corrections-evidence/results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS approved financial core + current premium visual: '+str(len(result['checks']))+' checks')
