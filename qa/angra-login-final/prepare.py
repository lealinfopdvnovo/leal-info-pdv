from pathlib import Path
import hashlib,zipfile
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_7.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='941bd3f24957b459a3f2beb95ac8a299470f2d347e4182c8126e3518c9ea7fac'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
Path('login-evidence').mkdir(exist_ok=True)
