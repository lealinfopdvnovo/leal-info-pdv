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
for channel,branch in [('001','release/pet-loja-v12')]:
 check(api('repos/lealinfopdvnovo/leal-info-pdv/git/ref/heads/'+branch)['object']['sha']==os.environ['SOURCE_'+channel],'Newer source exists; obsolete candidate must not be published')
before={c:feed(c) for c in CHANNELS}
check(before['001']['manifest']['Version']=='10.437','Published baseline changed')
entries=[];manifests={}
for c in SELECTED:
 root=Path('proof')/c/'validated';e=json.loads((root/'release-output/candidate.json').read_text(encoding='utf-8-sig'))
 check(e['Channel']==c and e['Version']=='10.438','Wrong release identity')
 check(e['ExpectedFeedSha']==before[c]['sha'],'Baseline changed after validation')
 ui=json.loads((root/'layout-evidence/results.json').read_text(encoding='utf-8-sig'))
 check(ui['success'] is True and len(ui['checks'])>=12 and ui['channel']==c and ui['source']==os.environ['SOURCE_'+c] and ui['published'] is False,'Required UI checks absent')
 proof=json.loads((root/'release-output/final-rollback.json').read_text(encoding='utf-8-sig'))
 check(len(proof)==3 and {x['scenario'] for x in proof}=={'success','interruption','bad-sha'} and all(x['pass'] and x['channel']==c and x['sha256']==e['PackageSha256'] for x in proof),'Rollback proof differs')
 package=root/'release-output'/e['Package']
 check(hashlib.sha256(package.read_bytes()).hexdigest()==e['PackageSha256'],'Package hash differs')
 with zipfile.ZipFile(package) as z:
  check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Unexpected payload')
  check(all(hashlib.sha256(z.read(n)).hexdigest()==e['TargetFiles'][n] for n in z.namelist()),'Member hash differs')
 check(e['SourceCommit']==(os.environ['SOURCE_001'] if c=='001' else os.environ['SOURCE_003']),'Wrong tested source')
 notes='LOJA LEAL INFO PET V1.2: logotipo maior no cabeçalho da tela de vendas, preservando proporções e imagem cadastrada. Botão de teste removido. Exclusivo 001.'
 manifests[c]=dict(Version=e['Version'],TargetClientCode=c,PackageUrl=e['Url'],Sha256=e['PackageSha256'],SourceCommit=e['SourceCommit'],Mandatory=False,Notes=notes,BuildRunId=str(e['BuildRunId']),ValidationRunId=os.environ['GITHUB_RUN_ID'],KitchenValidationRunId='39706043559',ProtectedUpdaterRequired=True,PublishedBaselineVersion=e['Baseline']['Version'])
 entries.append((e,package))
for e,package in entries:
 exists=subprocess.run(['gh','release','view',e['Tag'],'--repo',REPO],capture_output=True)
 if exists.returncode:subprocess.run(['gh','release','create',e['Tag'],str(package),'--repo',REPO,'--target','main','--title',f"PDV {e['Channel']} V10.438 - Cozinha Premium",'--notes',manifests[e['Channel']]['Notes'],'--latest=false'],check=True)
 check(hashlib.sha256(download(e['Url'])).hexdigest()==e['PackageSha256'],'Public release bytes differ; feeds unchanged')
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(feed(c)['sha']==before[c]['sha'] for c in CHANNELS),'Concurrent feed change')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=f'clients/{c}/version.json',mode='100644',type='blob',content=json.dumps(manifests[c],ensure_ascii=False,indent=2)+'\n') for c in SELECTED]))
commit=api(f'repos/{REPO}/git/commits',dict(message='001 V10.438: Cozinha Windows validada, bases e rollback independentes',tree=tree['sha'],parents=[head]))
move(commit['sha'])
try:
 detail=api(f"repos/{REPO}/commits/{commit['sha']}")
 check({x['filename'] for x in detail['files']}=={'clients/001/version.json'},'Unexpected modified feeds')
 for c in SELECTED:
  live=None
  for _ in range(8):
   try:live=json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/clients/{c}/version.json'))
   except (OSError,ValueError):pass
   if live==manifests[c]:break
   time.sleep(3)
  check(live==manifests[c],'Updater URL differs '+c)
 after={c:feed(c) for c in CHANNELS}
 check(all(after[c]['sha']==before[c]['sha'] for c in CHANNELS if c not in SELECTED),'Protected channel changed')
 Path('published-001.json').write_text(json.dumps(dict(Published=True,Version='10.438',Channels=SELECTED,Commit=commit['sha'],Manifests=manifests,Untouched={c:after[c]['sha'] for c in CHANNELS if c not in SELECTED},PublicPackagesVerified=True,UpdaterUrlsVerified=True,InstalledOnRealComputers=False),indent=2),encoding='utf-8')
 print('PASS PUBLISHED 001 ONLY 10.438; verified public bytes and updater URLs; 002/003/004/GLOBAL unchanged')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Restore feeds after unsuccessful 001/003 verification',tree=old['tree']['sha'],parents=[commit['sha']]))
  move(restore['sha'])
 raise
