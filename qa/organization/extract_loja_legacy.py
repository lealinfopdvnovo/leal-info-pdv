# Verified 10.465 publication fixture; NOT proof of the installed Loja/PET 1.3.
from pathlib import Path
import hashlib,io,json,urllib.request,zipfile
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-migration-compatibility'}),timeout=90) as r:return r.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/52165590baf9e191c3f80ff987ea7e2524e2f07b/clients/001/version.json'))
assert feed['Version']=='10.465' and feed['TargetClientCode']=='001','Loja baseline advanced'
assert feed['Sha256']=='728b20ff2e38d96deaf538ad45e6297bced845a6460c9a827a17ade3960d5fd6'
raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256']
folder=Path('legacy-001');folder.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(raw)) as z:
 assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for name in z.namelist():(folder/name).write_bytes(z.read(name))
# Every binary is authenticated by the pinned full-package SHA-256 above.
Path('pair-evidence').mkdir(exist_ok=True)
Path('pair-evidence/loja-legacy-feed.json').write_text(json.dumps(feed,indent=2),encoding='utf-8')
