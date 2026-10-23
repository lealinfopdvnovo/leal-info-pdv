"""Publish exactly 003 after independent Windows validation; atomic feeds."""
import base64,hashlib,json,os,subprocess,time,urllib.request,zipfile
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
CHANNELS=('GLOBAL','001','002','003','004')
SOURCE='98915ae5a9eb02825d77f74bb179e0ce27d65bd9'
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
 req=urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'User-Agent':'LEAL-INFO-003-Release','Cache-Control':'no-cache,no-store'})
 with urllib.request.urlopen(req,timeout=45) as r:return r.read()
def move(sha):api(f'repos/{REPO}/git/refs/heads/main',dict(sha=sha,force=False),'PATCH')
root=Path('proof');candidate=json.loads((root/'evidence/candidate.json').read_text(encoding='utf-8-sig'));tests=json.loads((root/'evidence/results.json').read_text(encoding='utf-8-sig'))
check(candidate['Source']==SOURCE and candidate['Version']=='10.445' and not candidate['Published'],'Wrong validated candidate')
check(tests['Success'] and tests['Checks']==22,'Required Windows tests absent')
rollback=json.loads((root/'rollback.json').read_text(encoding='utf-8-sig'))
check(len(rollback)==3 and all(x['Pass'] for x in rollback),'Protected rollback not approved')
package=Path('LEAL_INFO_PDV_V10_445_UPDATE.zip')
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
 for n,h in candidate['Files'].items():
  check(n in {'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Unauthorized member')
  p=root/'publish'/n;check(hashlib.sha256(p.read_bytes()).hexdigest()==h,'Validated binary differs');z.write(p,n)
check(set(candidate['Files'])=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Wrong common payload')
sha=hashlib.sha256(package.read_bytes()).hexdigest()
before={c:feed(c) for c in CHANNELS}
check(before['GLOBAL']['manifest']['Version']=='10.408' and before['GLOBAL']['manifest']['Sha256']=='b8fc9814924bb27baf57e9d2084fb0bac8eb8f92b757a08c0a0f9ac39fadca95','GLOBAL baseline changed')
Path('global-previous.json').write_text(json.dumps(before['GLOBAL']['manifest'],ensure_ascii=False,indent=2),encoding='utf-8')
tag='v10.445';url=f'https://github.com/{REPO}/releases/download/{tag}/{package.name}'
notes='GLOBAL 10.445: botão SAIR vermelho com acabamento 3D; aviso Nova atualização após login, com Atualizar agora ou Depois. Base comum para instalações compatíveis com PET API 1. PETs, banco, licença e configurações preservados. Instalações antigas em canais próprios exigem transição validada separadamente; esta publicação não força sua migração.'
exists=subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True)
if exists.returncode:
 subprocess.run(['gh','release','create',tag,str(package),'global-previous.json','--repo',REPO,'--target','main','--title','GLOBAL 10.445 - SAIR vermelho e aviso de atualização','--notes',notes,'--latest=false'],check=True)
check(hashlib.sha256(download(url)).hexdigest()==sha,'Public binary package differs; feeds unchanged')
manifest=dict(Version='10.445',Component='GLOBAL',PetApiVersion=1,PackageUrl=url,Sha256=sha,SourceCommit=SOURCE,Mandatory=False,Notes=notes,BuildRunId='39877162683',ValidationRunId=os.environ['GITHUB_RUN_ID'],ProtectedUpdaterRequired=True,RequiresIndependentPet=True,InstalledOnRealComputers=False,Delivery='preserve-existing-core',InstalledCoreChannels=before['GLOBAL']['manifest'].get('InstalledCoreChannels',{}))
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(feed(c)['sha']==before[c]['sha'] for c in CHANNELS),'Concurrent feed change')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path='version.json',mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
commit=api(f'repos/{REPO}/git/commits',dict(message='GLOBAL 10.445: aviso após login e SAIR vermelho; PETs e canais antigos preservados',tree=tree['sha'],parents=[head]));move(commit['sha'])
try:
 detail=api(f"repos/{REPO}/commits/{commit['sha']}");check({x['filename'] for x in detail['files']}=={'version.json'},'Unexpected modified channels')
 live=None
 for _ in range(12):
  try:live=json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/version.json'))
  except (OSError,ValueError):pass
  if live==manifest:break
  time.sleep(3)
 check(live==manifest,'Live GLOBAL updater manifest differs')
 after={c:feed(c) for c in CHANNELS};check(all(after[c]['sha']==before[c]['sha'] for c in CHANNELS if c!='GLOBAL'),'Client channels changed')
 Path('published-GLOBAL.json').write_text(json.dumps(dict(Published=True,Version='10.445',SourceCommit=SOURCE,FeedCommit=commit['sha'],Manifest=manifest,Hashes=candidate['Files'],PublicPackageVerified=True,UpdaterVerified=True,Untouched={c:after[c]['sha'] for c in CHANNELS if c!='GLOBAL'},InstalledOnRealComputers=False),indent=2),encoding='utf-8')
 print('PASS GLOBAL 10.445 PUBLISHED; public package verified; compatible neutral updater available; legacy client channels unchanged')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Rollback GLOBAL feed after failed release verification',tree=old['tree']['sha'],parents=[commit['sha']]));move(restore['sha'])
 raise
