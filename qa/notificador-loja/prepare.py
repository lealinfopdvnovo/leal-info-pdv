import hashlib,io,json,os,subprocess,urllib.request,zipfile
from pathlib import Path
BASE='19cd2697fada9e444bef8f23a920aca468abd819'
changed=subprocess.check_output(['git','diff','--name-only',BASE,'HEAD'],text=True,encoding='utf-8').splitlines()
allowed={'MainForm.cs','UpdateManager.cs','LealInfoPDV.csproj','UpdateNotificationForm.cs','UpdateNotifierHost.cs','MainForm.Notificador.cs','.github/workflows/notificador_loja_publicar.yml'}
assert all(p in allowed or p.startswith(('qa/notificador/','qa/notificador-loja/','tools/UpdateNotifier/')) for p in changed),changed
for p in ('Program.cs','Database.cs','Auth.cs','PetRuntime.cs','LojaPetBootstrap.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1'):
 assert Path(p).read_text(encoding='utf-8-sig')==subprocess.check_output(['git','show',BASE+':'+p],text=True,encoding='utf-8'),'Preserved file changed '+p
s=Path('UpdateManager.cs').read_text(encoding='utf-8');assert 'DistributionChannel="001"' in s and 'NotifierRoute => "CLIENT"' in s
assert 'Route={UpdateManager.NotifierRoute}' in Path('UpdateNotifierHost.cs').read_text(encoding='utf-8')
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Loja-Notifier-Release'}),timeout=90) as r:return r.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/001/version.json'))
assert feed['Version']=='10.448' and feed['Sha256']=='5e1305d640e6e192a7a31c44e5b53268e65514ef5997709e1ceb6910612eb7a3','Published baseline changed'
raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256'];archive=zipfile.ZipFile(io.BytesIO(raw));Path('legacy').mkdir(exist_ok=True)
for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):Path('legacy',n).write_bytes(archive.read(n))
deps=json.loads(Path('legacy/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'));sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'));Path('sdk-version.txt').write_text(sdk)
Path('baseline.json').write_text(json.dumps(feed,indent=2));print('PASS original Loja business database finance credentials PET and transactional updater unchanged; published 10.448 baseline SHA verified')
