import base64,concurrent.futures,hashlib,json,os,subprocess,time,urllib.error,urllib.request,zipfile
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates';CHANNELS=('GLOBAL','CASA','001','002','004')
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(path,data=None,method=None):
 a=['gh','api',path]
 if data is not None:a+=['--method',method or 'POST','--input','-']
 return json.loads(subprocess.check_output(a,input=None if data is None else json.dumps(data).encode()))
def read(path):
 d=api(f'repos/{REPO}/contents/{path}?ref=main');return dict(sha=d['sha'],manifest=json.loads(base64.b64decode(d['content']).decode('utf-8-sig')))
def download(url):
 for attempt in range(8):
  try:
   req=urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'User-Agent':'LEAL-GLOBAL-453','Cache-Control':'no-cache,no-store'})
   with urllib.request.urlopen(req,timeout=45) as r:return r.read()
  except (OSError,ValueError):
   if attempt==7:raise
   time.sleep(3*(attempt+1))
entries={};before={};manifests={};tree=[]
# Fully validate all five artifacts before creating any channel update.
for c in CHANNELS:
 root=Path('proof')/c;e=json.loads((root/'candidate.json').read_text());check(e['Channel']==c and e['Version']=='10.453' and e['SourceCommit']==os.environ['GITHUB_SHA'],'Unexpected candidate '+c)
 check(e['DependenciesEqual'] and e['DesktopNoticeChecks']>=14 and e['LoginExitApproved'] and e['RollbackApproved'],'Approval missing '+c)
 for name in ('notifier-results.json','native-results.json','login-exit-results.json'):check(json.loads((root/name).read_text())['Success'],'Failed test '+c)
 rollback=json.loads((root/'rollback.json').read_text(encoding='utf-8-sig'));check(len(rollback)==3 and all(x['Pass'] for x in rollback),'Rollback missing '+c)
 before[c]=read(e['FeedPath']);check(before[c]['manifest']==e['Baseline'],'Baseline moved '+c)
 package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Package hash '+c)
 with zipfile.ZipFile(package) as z:
  check(len(z.namelist())==3 and set(z.namelist())==set(e['Files']),'Payload path '+c)
  check(all(hashlib.sha256(z.read(n)).hexdigest()==e['Files'][n] for n in z.namelist()),'Binary hash '+c)
 entries[c]=e
legacy003=read('clients/003/version.json')
notes='GLOBAL 10.453: aviso automático no desktop com Atualizar agora e Depois; consulta por minuto; adiamento de 30 minutos. SAIR e Esc no login. Tutorial acessível em Ajuda sem abrir automaticamente. Preserva funções, PETs, dados, licença e configurações. Atualização pelo PDV, sem setup.'
urls={}
for c,e in entries.items():
 root=Path('proof')/c;tag='v10.453' if c=='GLOBAL' else ('v10.453-casa' if c=='CASA' else f'client-{c}-v10.453');urls[c]=f'https://github.com/{REPO}/releases/download/{tag}/{e["Package"]}'
 old=Path(f'previous-{c}.json');old.write_text(json.dumps(e['Baseline'],ensure_ascii=False,indent=2))
 if subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True).returncode:
  subprocess.run(['gh','release','create',tag,str(root/e['Package']),str(old),'--repo',REPO,'--target','main','--title',f'GLOBAL 10.453 • {c}','--notes',notes,'--latest=false'],check=True)
# Public download verification also finishes before any notifier can see the new feeds.
def verify(c):check(hashlib.sha256(download(urls[c])).hexdigest()==entries[c]['Sha256'],'Public package mismatch '+c)
with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:list(pool.map(verify,CHANNELS))
for c,e in entries.items():
 m=dict(e['Baseline']);m.update(Version='10.453',PackageUrl=urls[c],Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],SourceBase=e['SourceBase'],Mandatory=False,Notes=notes,BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],GlobalFeatureVersion='10.453',PublishedBaselineVersion=e['Baseline']['Version'],InstalledOnRealComputers=False,DesktopNotifier=True,PollSeconds=60,DeferredMinutes=30,PackagesVerifiedBeforeFeed=True)
 if c in ('GLOBAL','CASA'):m.update(Component='GLOBAL',PetApiVersion=1)
 if c=='GLOBAL':m.pop('TargetClientCode',None)
 else:m['TargetClientCode']='003' if c=='CASA' else c
 if c in ('002','004'):m['LegacyFirstHop']=True
 for stale in ('SourceBaseVersion','InstalledOnCustomer'):m.pop(stale,None)
 manifests[c]=m
# Preserve and extend installer route compatibility; PET contents are never overwritten.
routes=manifests['GLOBAL'].get('InstalledCoreChannels',{})
for c,e in entries.items():
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll'):routes[e['Files'][n]]='GLOBAL' if c in ('GLOBAL','CASA') else c
manifests['GLOBAL']['InstalledCoreChannels']=routes
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(read(entries[c]['FeedPath'])['sha']==before[c]['sha'] for c in CHANNELS) and read('clients/003/version.json')['sha']==legacy003['sha'],'Concurrent publication')
for c,e in entries.items():tree.append(dict(path=e['FeedPath'],mode='100644',type='blob',content=json.dumps(manifests[c],ensure_ascii=False,indent=2)+'\n'))
new=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=tree));commit=api(f'repos/{REPO}/git/commits',dict(message='GLOBAL 10.453: recursos aprovados para todos os canais preservando bases e PETs',tree=new['sha'],parents=[head]))
api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
try:
 check({f['filename'] for f in api(f'repos/{REPO}/commits/{commit["sha"]}')['files']}=={e['FeedPath'] for e in entries.values()},'Unexpected feed changes')
 for c,e in entries.items():check(read(e['FeedPath'])['manifest']==manifests[c],'Published manifest differs '+c)
 check(read('clients/003/version.json')['sha']==legacy003['sha'],'Legacy Casa migration bridge changed')
 def checklive(c):
  for attempt in range(30):
   if json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/'+entries[c]['FeedPath']))==manifests[c]:return
   time.sleep(3)
  raise RuntimeError('Live updater feed mismatch '+c)
 with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:list(pool.map(checklive,CHANNELS))
 Path('published-global453.json').write_text(json.dumps(dict(Published=True,Version='10.453',SourceCommit=os.environ['GITHUB_SHA'],Channels=CHANNELS,FeedCommit=commit['sha'],Manifests=manifests,PackageFiles={c:e['Files'] for c,e in entries.items()},AllPackagesVerifiedBeforeFeeds=True,Legacy003BridgePreserved=True,InstalledOnCustomer=False),indent=2))
 print('PASS GLOBAL 10.453 published for root, Loja, Casa, Gelamac and Genilson; all five packages passed before atomic feeds; PET/data preserved; no installation claimed')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Rollback GLOBAL 10.453 feeds after failed publication verification',tree=old['tree']['sha'],parents=[commit['sha']]));api(f'repos/{REPO}/git/refs/heads/main',dict(sha=restore['sha'],force=False),'PATCH')
 raise
