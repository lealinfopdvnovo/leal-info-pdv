from pathlib import Path
import hashlib,zipfile
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_6.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='25ab1b7ed0ffbb5b6bf8278d644384707fd11ced20ae6ef3e379ee3a0668c21b'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
Path('text-evidence').mkdir(exist_ok=True)
