import json,base64,hashlib,os,subprocess,sys,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv';REPO=SOURCE+'-updates';FEED='clients/003/global/version.json'
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(p,data=None,method=None):
 a=['gh','api',p]
 if data is not None:a+=['--method',method or 'POST','--input','-']
 return json.loads(subprocess.check_output(a,input=None if data is None else json.dumps(data).encode()))
def read():
 d=api(f'repos/{REPO}/contents/{FEED}?ref=main');return json.loads(base64.b64decode(d['content']))
def snap():
 head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];c=api(f'repos/{REPO}/git/commits/{head}');t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1');check(not t.get('truncated'),'Incomplete scope snapshot');return head,c,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
def get(url):
 for i in range(6):
  try:
   with urllib.request.urlopen(urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'Cache-Control':'no-cache','User-Agent':'LEAL-TESTES003'}),timeout=60) as f:return f.read()
  except OSError:
   if i==5:raise
   time.sleep(3)
def download():
 r=api(f'repos/{SOURCE}/actions/runs/'+os.environ['VALIDATION_RUN']);check(r['head_sha']==os.environ['VALIDATION_COMMIT'] and r['conclusion']=='success','Windows tests not approved')
 subprocess.run(['gh','run','download',os.environ['VALIDATION_RUN'],'--repo',SOURCE,'--name','TESTES003-DELIVERY-VALIDACAO','--dir','proof'],check=True)
def publish():
 paths=list(Path('proof').rglob('candidate.json'));check(len(paths)==1,'Ambiguous approved artifact');root=paths[0].parent;e=json.loads(paths[0].read_text())
 check(e['Client']=='003' and e['SourceCommit']==os.environ['VALIDATION_COMMIT'] and e['GlobalVersion']=='10.457' and e['DeliveryVersion']=='10.458' and e['DependencySetUnchanged'] and e['ProtectedWorkerUnchanged'],'Scope/version mismatch')
 for name in ['testes003-delivery.json','testes003-rollback.json']:check(json.loads((root/name).read_text(encoding='utf-8-sig'))['Success'],'Test failed '+name)
 package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Approved package integrity failed')
 with zipfile.ZipFile(package) as z:check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Forbidden update members')
 head,previous,protected=snap();baseline=read();check(baseline==e['Baseline'],'Published 003 baseline changed')
 Path('previous003-feed.json').write_text(json.dumps(baseline,ensure_ascii=False,indent=2))
 tag='v10.458-casa-testes';url=f'https://github.com/{REPO}/releases/download/{tag}/{e["Package"]}'
 notes='SOMENTE CASA DO ADRIANO 003: entrega automática do LEAL INFO PDV Testes, programa independente com ícone próprio na bandeja. GLOBAL permanece 10.457; 10.458 é revisão técnica de entrega exclusiva do 003. Preserva a cópia anterior 003 10.457 verificada. Sem erros fictícios, sem setup, sem download manual. Atualizador normal, banco, licença, configurações e demais clientes preservados. Instalação no computador real ainda não confirmada.'
 if subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True).returncode:
  subprocess.run(['gh','release','create',tag,str(package),'previous003-feed.json','--repo',REPO,'--target','main','--title','TESTES 003 • programa separado • entrega automática','--notes',notes,'--latest=false'],check=True)
 check(hashlib.sha256(get(url)).hexdigest()==e['Sha256'],'Public artifact differs')
 check(snap()[0]==head,'Concurrent publication; no feed activated')
 m=dict(baseline);m.update(Version='10.458',GlobalFeatureVersion='10.457',PackageUrl=url,Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],SourceBase='874bc1e77fc3fb9b643ed17ceb0e1e1c91ad2ac8',Notes=notes,BuildRunId=os.environ['VALIDATION_RUN'],ValidationRunId=os.environ['VALIDATION_RUN'],PublicationRunId=os.environ['GITHUB_RUN_ID'],InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True,AuxiliaryProgram='LEAL INFO PDV Testes 003',AuxiliaryVersion='1.0',ApprovedAgentRun='40115164625')
 tree=api(f'repos/{REPO}/git/trees',dict(base_tree=previous['tree']['sha'],tree=[dict(path=FEED,type='blob',mode='100644',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n')]))
 commit=api(f'repos/{REPO}/git/commits',dict(message='003 only: deliver independent TESTES tray program through protected updater; GLOBAL and other clients unchanged',tree=tree['sha'],parents=[head]))
 api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
 try:
  after=snap()[2];check({p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}=={FEED},'Unexpected changed feed')
  check(read()==m,'API readback differs')
  for i in range(30):
   if json.loads(get(f'https://raw.githubusercontent.com/{REPO}/main/'+FEED))==m:break
   time.sleep(3)
  else:raise RuntimeError('Public updater feed not confirmed')
  Path('published-testes003.json').write_text(json.dumps(dict(Published=True,Client='003',GlobalVersion='10.457',DeliveryVersion='10.458',SourceCommit=e['SourceCommit'],ValidationRun=os.environ['VALIDATION_RUN'],FeedCommit=commit['sha'],Manifest=m,OnlyChangedPath=FEED,OtherFeedsUnchanged=True,RealInstallationConfirmed=False),indent=2))
  print('PASS TESTES 003 PUBLICADO: artefato aprovado, hash público e feed exclusivo confirmados; instalação real não afirmada')
 except Exception:
  if snap()[0]==commit['sha']:
   c=api(f'repos/{REPO}/git/commits',dict(message='Rollback TESTES003 feed: public confirmation failed',tree=previous['tree']['sha'],parents=[commit['sha']]))
   api(f'repos/{REPO}/git/refs/heads/main',dict(sha=c['sha'],force=False),'PATCH')
  raise
if __name__=='__main__':{'download':download,'publish':publish}[sys.argv[1]]()
