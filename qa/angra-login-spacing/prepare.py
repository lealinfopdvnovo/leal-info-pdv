from pathlib import Path
import hashlib,zipfile
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_8.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='6da4bf7a9359a77be4863f6a5ccf4340936844c9dbee7a6ce10503c945a5657e'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
Path('spacing-evidence').mkdir(exist_ok=True)
