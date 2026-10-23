import hashlib,io,json,os,subprocess,urllib.request,zipfile
from pathlib import Path
BASE='19cd2697fada9e444bef8f23a920aca468abd819'
changed=subprocess.check_output(['git','diff','--name-only',BASE,'HEAD'],text=True,encoding='utf-8').splitlines()
allowed={'MainForm.cs','LoginForm.cs','SplashForm.cs','UpdateManager.cs','LealInfoPDV.csproj','UpdateNotificationForm.cs','UpdateNotifierHost.cs','MainForm.Notificador.cs','.github/workflows/notificador_loja_publicar.yml'}
assert all(p in allowed or p.startswith(('qa/notificador/','qa/notificador-loja/','tools/UpdateNotifier/')) for p in changed),changed
for p in ('Program.cs','Database.cs','Auth.cs','PetRuntime.cs','LojaPetBootstrap.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1'):
 assert Path(p).read_text(encoding='utf-8-sig')==subprocess.check_output(['git','show',BASE+':'+p],text=True,encoding='utf-8'),'Preserved file changed '+p
s=Path('UpdateManager.cs').read_text(encoding='utf-8');assert 'DistributionChannel="001"' in s and 'NotifierRoute => "CLIENT"' in s
assert 'Route={UpdateManager.NotifierRoute}' in Path('UpdateNotifierHost.cs').read_text(encoding='utf-8')
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Loja-Notifier-Release'}),timeout=90) as r:return r.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/001/version.json'))
assert feed['Version']=='10.451' and feed['Sha256']=='e1c3030a32ab05b5a411d769274c5d66b96bdd0ef8a736836dca32116908ff2a','Published baseline changed'
raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256'];archive=zipfile.ZipFile(io.BytesIO(raw));Path('legacy').mkdir(exist_ok=True)
for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):Path('legacy',n).write_bytes(archive.read(n))
deps=json.loads(Path('legacy/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'));sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'));Path('sdk-version.txt').write_text(sdk)
Path('baseline.json').write_text(json.dumps(feed,indent=2));print('PASS original Loja business database finance credentials PET and transactional updater unchanged; published 10.451 baseline SHA verified')

main=Path("MainForm.cs").read_text(encoding="utf-8")
assert "OpenFirstAccessTutorial(true);" not in main
assert 'AddMenu("Tutorial de Primeiro Acesso", () => OpenFirstAccessTutorial(false));' in main
assert "if (automatic) return;" in main
print("PASS tutorial does not open at login and remains available from Ajuda")
