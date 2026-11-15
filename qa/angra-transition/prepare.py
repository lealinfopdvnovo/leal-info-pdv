from pathlib import Path
import hashlib,zipfile,json
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_5.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='c2ee306da15e99699f49b8f2b67dffb9e5548d8865bf0003326ff2e0d50ce765'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
Path('transition-evidence').mkdir(exist_ok=True)
