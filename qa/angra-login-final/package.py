from pathlib import Path
import hashlib,json,zipfile
root=Path('transition-channel');root.mkdir(exist_ok=True)
original=json.loads(Path('baseline/Extensions/PET/manifest.json').read_text(encoding='utf-8-sig'))
module=Path('Pet-1.8.dll').read_bytes();m=dict(original,Version='1.8',Sha256=hashlib.sha256(module).hexdigest())
p=root/'ANGRA_005_PET_1_8.zip'
with zipfile.ZipFile(p,'w',zipfile.ZIP_DEFLATED) as z:
 z.writestr('Extensions/PET/manifest.json',json.dumps(m));z.writestr('Extensions/PET/Pet.dll',module)
feed=json.loads(Path('controller/qa/etapa4-setup/channel/version.json').read_text(encoding='utf-8-sig'));feed.update(Version='1.8',Sha256=hashlib.sha256(p.read_bytes()).hexdigest(),PackageUrl='https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/'+p.name)
(root/'version.json').write_text(json.dumps(feed,indent=2))
print('PASS package PET 005 / 1.8',feed['Sha256'])
