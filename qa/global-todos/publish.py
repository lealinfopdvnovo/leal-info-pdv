"""Publish exactly 003 after independent Windows validation; atomic feeds."""
import base64,hashlib,json,os,subprocess,time,urllib.request,zipfile
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
CHANNELS=('GLOBAL','001','002','003','004')
SELECTED=('001','002','004')
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
before={c:feed(c) for c in CHANNELS};entries={};manifests={}
check(before['GLOBAL']['manifest']['Version']=='10.445','GLOBAL approval absent')
for id in SELECTED:
 root=Path('proof')/id;e=json.loads((root/'candidate.json').read_text());check(e['Channel']==id and e['Version']=='10.445' and e['SourceCommit']=='006ecad77168de93d8bf1130c7bbf401e3c4d7b7','Wrong validated candidate')
 check(e['DependenciesEqual'] and e['NotificationChecks']==22,'Missing Windows approval')
 check(before[id]['manifest']==e['Baseline'],'Published baseline changed')
 rollback=json.loads((root/'rollback.json').read_text(encoding='utf-8-sig'));check(len(rollback)==3 and all(x['Pass'] for x in rollback),'Rollback evidence absent')
 package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Package hash differs')
 with zipfile.ZipFile(package) as z:
  check(len(z.namelist())==3 and set(z.namelist())==set(e['Files']),'Unexpected payload')
  check(all(hashlib.sha256(z.read(n)).hexdigest()==e['Files'][n] for n in z.namelist()),'Payload differs')
 old=Path(f'previous-{id}.json');old.write_text(json.dumps(e['Baseline'],ensure_ascii=False,indent=2),encoding='utf-8')
 tag=f'client-{id}-v10.445';url=f'https://github.com/{REPO}/releases/download/{tag}/{package.name}'
 notes='Atualização GLOBAL 10.445: botão SAIR vermelho com acabamento 3D e aviso Nova atualização após login com Atualizar agora ou Depois. Distribuição compatível com o atualizador já instalado; preserva funções e personalizações desta instalação. Sem instalador.'
 exists=subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True)
 if exists.returncode:subprocess.run(['gh','release','create',tag,str(package),str(old),'--repo',REPO,'--target','main','--title',f'GLOBAL 10.445 - compatibilidade {id}','--notes',notes,'--latest=false'],check=True)
 check(hashlib.sha256(download(url)).hexdigest()==e['Sha256'],'Public release differs; feeds unchanged')
 manifests[id]=dict(Version='10.445',TargetClientCode=id,PackageUrl=url,Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],SourceBase=e['SourceBase'],Mandatory=False,Notes=notes,BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],GlobalFeatureVersion='10.445',PublishedBaselineVersion=e['Baseline']['Version'])
 entries[id]=e
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(feed(c)['sha']==before[c]['sha'] for c in CHANNELS),'Concurrent channel change')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=f'clients/{id}/version.json',mode='100644',type='blob',content=json.dumps(manifests[id],ensure_ascii=False,indent=2)+'\n') for id in SELECTED]))
commit=api(f'repos/{REPO}/git/commits',dict(message='GLOBAL 10.445 para todas as instalações: compatibilidade Loja Gelamac e Genilson, sem alterar PETs',tree=tree['sha'],parents=[head]));move(commit['sha'])
try:
 detail=api(f"repos/{REPO}/commits/{commit['sha']}");check({x['filename'] for x in detail['files']}=={f'clients/{id}/version.json' for id in SELECTED},'Unexpected changed paths')
 for id in SELECTED:
  live=None
  for _ in range(60):
   try:live=json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/clients/{id}/version.json'))
   except (OSError,ValueError):pass
   if live==manifests[id]:break
   time.sleep(3)
  print("Public feed",id,"matches",live==manifests[id],"observed",live,flush=True)
  check(live==manifests[id],'Updater manifest differs '+id)
 after={c:feed(c) for c in CHANNELS};check(all(after[c]['sha']==before[c]['sha'] for c in ('GLOBAL','003')),'Common or Casa channel changed')
 Path('published-all.json').write_text(json.dumps(dict(Published=True,Version='10.445',Channels=SELECTED,FeedCommit=commit['sha'],Manifests=manifests,PublicPackagesVerified=True,UpdaterUrlsVerified=True,InstalledOnRealComputers=False),indent=2),encoding='utf-8')
 print('PASS GLOBAL 10.445 NOW AVAILABLE TO ALL ROUTES; 001 002 004 compatible packages published; GLOBAL and migrated Casa route retained')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Rollback compatibility feed publication after failed verification',tree=old['tree']['sha'],parents=[commit['sha']]));move(restore['sha'])
 raise
