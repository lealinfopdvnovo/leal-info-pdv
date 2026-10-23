"""Publish exactly 001 after independent Windows validation; atomic feeds."""
import base64,hashlib,json,os,subprocess,time,urllib.request,zipfile
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
CHANNELS=('GLOBAL','001','002','003','004')
SELECTED=('001',)
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(path,data=None,method=None):
 args=['gh','api',path]
 if data is not None:
  Path('request.json').write_text(json.dumps(data),encoding='utf-8');args+=['--method',method or 'POST','--input','request.json']
 return json.loads(subprocess.check_output(args))
def feed(c):
 p='version.json' if c=='GLOBAL' else f'clients/{c}/version.json'
 d=api(f'repos/{REPO}/contents/{p}?ref=main')
 return dict(sha=d['sha'],manifest=json.loads(base64.b64decode(d['content']).decode('utf-8-sig')))
def download(url):
 req=urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'User-Agent':'LEAL-INFO-001-003-Release','Cache-Control':'no-cache,no-store'})
 with urllib.request.urlopen(req,timeout=45) as r:return r.read()
def move(sha):api(f'repos/{REPO}/git/refs/heads/main',dict(sha=sha,force=False),'PATCH')
root=Path('proof');e=json.loads((root/'candidate.json').read_text());tests=json.loads((root/'notifier-results.json').read_text());native=json.loads((root/'native-results.json').read_text());finance=json.loads((root/'finance-results.json').read_text(encoding='utf-8-sig'));rollback=json.loads((root/'rollback.json').read_text(encoding='utf-8-sig'))
check(e['Channel']=='001' and e['Version']=='10.448' and e['PetVersion']=='1.2' and e['SourceCommit']==os.environ['GITHUB_SHA'] and e['DependenciesEqual'],'Wrong candidate')
check(tests['Success'] and native['Success'] and native['Checks']>=23 and finance['Success'] and len(finance['checks'])>=13,'Required Windows validation missing')
check(len(rollback)==3 and all(x['Pass'] for x in rollback),'Protected rollback approval missing')
before={c:feed(c) for c in CHANNELS};check(before['001']['manifest']==e['Baseline'],'Published Loja baseline changed')
# Preserve the independent Casa pilot manifest as well as every published legacy/GLOBAL channel.
oldtree=api(f'repos/{REPO}/git/trees/main?recursive=1');casa=next((x['sha'] for x in oldtree['tree'] if x['path']=='clients/003/global/version.json'),None)
package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Package hash mismatch')
with zipfile.ZipFile(package) as z:
 check(len(z.namelist())==3 and set(z.namelist())==set(e['Files']),'Unexpected payload')
 check(all(hashlib.sha256(z.read(n)).hexdigest()==e['Files'][n] for n in z.namelist()),'Binary hash mismatch')
tag='client-001-v10.448';url=f'https://github.com/{REPO}/releases/download/{tag}/{package.name}'
notes='Teste real do aviso de nova atualização na Loja do Leal. Versão de manutenção 10.448, sem mudanças nas funções. PET V1.2, dados, licença e configurações preservados. Instalação somente pelo atualizador existente, com integridade, backup e rollback.'
Path('previous-001.json').write_text(json.dumps(before['001']['manifest'],ensure_ascii=False,indent=2),encoding='utf-8')
exists=subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True)
if exists.returncode:subprocess.run(['gh','release','create',tag,str(package),'previous-001.json','--repo',REPO,'--target','main','--title','Loja do Leal 10.448 - Notificador Windows','--notes',notes,'--prerelease','--latest=false'],check=True)
check(hashlib.sha256(download(url)).hexdigest()==e['Sha256'],'Public release package differs; feeds unchanged')
manifest=dict(Version='10.448',TargetClientCode='001',PackageUrl=url,Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],SourceBase=e['SourceBase'],PetVersion='1.2',Mandatory=False,Notes=notes,BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],ProtectedUpdaterRequired=True,PublishedBaselineVersion='10.447',InstalledOnRealComputers=False)
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(feed(c)['sha']==before[c]['sha'] for c in CHANNELS),'Concurrent feed change')
check(next((x['sha'] for x in api(f'repos/{REPO}/git/trees/{old["tree"]["sha"]}?recursive=1')['tree'] if x['path']=='clients/003/global/version.json'),None)==casa,'Casa scoped channel changed')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path='clients/001/version.json',mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
commit=api(f'repos/{REPO}/git/commits',dict(message='Loja do Leal 10.448: piloto notificador Windows exclusivo, PET V1.2 preservado',tree=tree['sha'],parents=[head]));move(commit['sha'])
try:
 detail=api(f"repos/{REPO}/commits/{commit['sha']}");check({x['filename'] for x in detail['files']}=={'clients/001/version.json'},'Unexpected published paths')
 live=None
 for _ in range(60):
  try:live=json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/clients/001/version.json'))
  except (OSError,ValueError):pass
  if live==manifest:break
  time.sleep(3)
 check(live==manifest,'Live Loja updater manifest differs')
 after={c:feed(c) for c in CHANNELS};check(all(after[c]['sha']==before[c]['sha'] for c in CHANNELS if c!='001'),'Protected channel changed')
 newtree=api(f'repos/{REPO}/git/trees/main?recursive=1');check(next((x['sha'] for x in newtree['tree'] if x['path']=='clients/003/global/version.json'),None)==casa,'Casa scoped channel changed')
 Path('published-LOJA-448.json').write_text(json.dumps(dict(Published=True,Version='10.448',PetVersion='1.2',SourceCommit=e['SourceCommit'],FeedCommit=commit['sha'],Manifest=manifest,Files=e['Files'],PublicPackageVerified=True,UpdaterVerified=True,OtherChannelsUnchanged=True,InstalledOnRealComputers=False),indent=2),encoding='utf-8')
 print('PASS Loja-only 10.448 PUBLISHED; native notifier and original PET verified; public package and updater verified; all other channels unchanged')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Rollback Loja feed after unsuccessful notifier publication verification',tree=old['tree']['sha'],parents=[commit['sha']]));move(restore['sha'])
 raise
