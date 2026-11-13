# Historical verified fixture; NOT qualification of the current installed Loja 1.3.
from pathlib import Path
import hashlib,io,json,urllib.request,zipfile
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-migration-compatibility'}),timeout=90) as r:return r.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/54fe308664ef8fcd08c9e06784c19b3e2cff9510/clients/001/version.json'))
assert feed['Version']=='10.464' and feed['TargetClientCode']=='001','Loja baseline advanced'
raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256']
folder=Path('legacy-001');folder.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(raw)) as z:
 assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for name in z.namelist():(folder/name).write_bytes(z.read(name))
assert hashlib.sha256((folder/'LealInfoPDV.dll').read_bytes()).hexdigest()=='502ae32947232439cec342944bebff155e67f93ffec272b3a0066dd87545473e'
Path('pair-evidence').mkdir(exist_ok=True)
Path('pair-evidence/loja-legacy-feed.json').write_text(json.dumps(feed,indent=2),encoding='utf-8')
