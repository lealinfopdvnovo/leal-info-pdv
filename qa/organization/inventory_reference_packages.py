"""Read-only inventory of both trusted Loja publications; neither proves installation."""
from pathlib import Path
import hashlib,io,json,urllib.request,zipfile
references=[('loja-10.465','client-001-v10.465-logon-backup','728b20ff2e38d96deaf538ad45e6297bced845a6460c9a827a17ade3960d5fd6'),('loja-display-1.3','client-001-v10.467-pet-display','fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b')]
rows=[]
for label,tag,expected in references:
 url=f'https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/{tag}/LEAL_INFO_PDV_001_V10_463_UPDATE.zip'
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-isolated-reference-inventory'}),timeout=90) as response:raw=response.read(32*1024*1024+1)
 assert len(raw)<=32*1024*1024 and hashlib.sha256(raw).hexdigest()==expected,'Trusted reference package differs'
 folder=Path('reference-packages')/label;folder.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(io.BytesIO(raw)) as package:
  assert set(package.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
  files=[]
  for name in package.namelist():
   b=package.read(name);(folder/name).write_bytes(b);files.append(dict(Name=name,Sha256=hashlib.sha256(b).hexdigest(),Size=len(b)))
 rows.append(dict(Label=label,Tag=tag,PackageSha256=expected,Files=files,RuntimeconfigIncluded=False,InstalledConfirmed=False))
Path('pair-evidence').mkdir(exist_ok=True)
Path('pair-evidence/published-loja-references.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('PASS both trusted Loja references inventoried; runtimeconfig/dependencies and installed identity remain unconfirmed')
