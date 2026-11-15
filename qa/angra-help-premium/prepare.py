from pathlib import Path
import hashlib,zipfile
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_9.zip')
assert hashlib.sha256(p.read_bytes()).hexdigest()=='e61b914da110dec282f124daeeed52b7da1b2db514264224084b09be313cc9de'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
Path('help-evidence').mkdir(exist_ok=True)
