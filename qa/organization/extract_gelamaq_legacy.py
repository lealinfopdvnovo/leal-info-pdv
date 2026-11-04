from pathlib import Path
import hashlib,io,json,urllib.request,zipfile
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-PET-preservation'}),timeout=90) as r:return r.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/002/version.json'))
assert feed['Version']=='10.463' and feed['TargetClientCode']=='002','Gelamaq baseline advanced'
raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256']
folder=Path('legacy-002');folder.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(raw)) as z:
 assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for name in z.namelist():(folder/name).write_bytes(z.read(name))
assert hashlib.sha256((folder/'LealInfoPDV.dll').read_bytes()).hexdigest()=='676f29dc0f4ef828278552dd43a7a083a5fc13ffafb129314a7d87f0266567ed'
print('PASS actual published Gelamaq core integrity for embedded PET contract preservation')
