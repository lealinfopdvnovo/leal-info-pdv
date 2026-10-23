"""Publish exactly 003 after independent Windows validation; atomic feeds."""
import base64,hashlib,json,os,subprocess,time,urllib.request,zipfile
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
CHANNELS=('GLOBAL','001','002','003','004')
SELECTED=('003',)
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
root=Path('proof');e=json.loads((root/'evidence/candidate.json').read_text());tests=json.loads((root/'evidence/consulta-results.json').read_text());rollback=json.loads((root/'evidence/rollback.json').read_text(encoding='utf-8-sig'))
check(e['Version']=='10.446' and e['TargetClientCode']=='003' and e['SourceCommit']==os.environ['GITHUB_SHA'] and e['DependenciesEqual'],'Candidate not approved')
check(tests['Success'] and len(tests['Checks'])>=30 and len(rollback)==3 and all(x['Pass'] for x in rollback),'Windows or rollback proof absent')
before={c:feed(c) for c in CHANNELS};check(before['GLOBAL']['manifest']==e['InstalledGlobalBaseline'],'GLOBAL baseline changed')
path='clients/003/global/version.json'
existing=api(f'repos/{REPO}/git/trees/main?recursive=1');oldentry=next((x for x in existing['tree'] if x['path']==path),None)
check(oldentry is None,'Scoped Casa feed already exists; review concurrent work')
package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Package hash differs')
with zipfile.ZipFile(package) as z:
 check(len(z.namelist())==3 and set(z.namelist())==set(e['Files']),'Unexpected payload')
 check(all(hashlib.sha256(z.read(n)).hexdigest()==e['Files'][n] for n in z.namelist()),'Payload hash differs')
tag='v10.446-casa';url=f'https://github.com/{REPO}/releases/download/{tag}/{package.name}'
notes='Teste direcionado Casa do Leal: menu Consulta abre painel flutuante com Produtos, Clientes, Histórico de vendas, Ordens / OS, Motoboy / Entregas e Orçamentos. Funções e permissões preservadas. GLOBAL permanece 10.445.'
exists=subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True)
if exists.returncode:subprocess.run(['gh','release','create',tag,str(package),'--repo',REPO,'--target','main','--title','Casa do Leal 10.446 - Consulta flutuante','--notes',notes,'--prerelease','--latest=false'],check=True)
check(hashlib.sha256(download(url)).hexdigest()==e['Sha256'],'Public package differs; feeds unchanged')
manifest=dict(Version='10.446',TargetClientCode='003',Component='GLOBAL',PetApiVersion=1,PackageUrl=url,Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],Mandatory=False,Notes=notes,BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],ProtectedUpdaterRequired=True,PublishedBaselineVersion='10.445')
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(feed(c)['sha']==before[c]['sha'] for c in CHANNELS),'Concurrent feed change')
check(not any(x['path']==path for x in api(f'repos/{REPO}/git/trees/{old["tree"]["sha"]}?recursive=1')['tree']),'Scoped feed changed')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=path,mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
commit=api(f'repos/{REPO}/git/commits',dict(message='Casa do Leal 10.446: teste Consulta flutuante somente Casa; GLOBAL e demais instalações preservados',tree=tree['sha'],parents=[head]));move(commit['sha'])
try:
 detail=api(f"repos/{REPO}/commits/{commit['sha']}");check({x['filename'] for x in detail['files']}=={path},'Unexpected changed channel')
 live=None
 for _ in range(60):
  try:live=json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/{path}'))
  except (OSError,ValueError):pass
  if live==manifest:break
  time.sleep(3)
 check(live==manifest,'Live scoped updater manifest differs')
 after={c:feed(c) for c in CHANNELS};check(all(after[c]['sha']==before[c]['sha'] for c in CHANNELS),'Existing published channels changed')
 Path('published-CASA-446.json').write_text(json.dumps(dict(Published=True,Version='10.446',ScopedChannel=path,SourceCommit=e['SourceCommit'],FeedCommit=commit['sha'],Manifest=manifest,Files=e['Files'],UpdaterVerified=True,PublicPackageVerified=True,GlobalAndOtherChannelsUnchanged=True,InstalledOnRealComputers=False),indent=2),encoding='utf-8')
 print('PASS Casa-only 10.446 PUBLISHED; public package and updater verified; GLOBAL and other client feeds unchanged')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Rollback Casa scoped feed after failed publication validation',tree=old['tree']['sha'],parents=[commit['sha']]));move(restore['sha'])
 raise
