import json,base64,hashlib,os,subprocess,sys,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv';REPO=SOURCE+'-updates'
FEEDS=['clients/003/global/version.json','clients/003/version.json']
OLD_SHA='018739b0c9a29cca026f5b05e8a61a473d9036e38b6943243e66adcceac9fd09'
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(p,data=None,method=None):
 a=['gh','api',p]
 if data is not None:a+=['--method',method or 'POST','--input','-']
 return json.loads(subprocess.check_output(a,input=None if data is None else json.dumps(data).encode()))
def read(path):
 d=api(f'repos/{REPO}/contents/{path}?ref=main');return json.loads(base64.b64decode(d['content']))
def snap():
 head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];c=api(f'repos/{REPO}/git/commits/{head}');t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1');check(not t.get('truncated'),'Incomplete scope snapshot')
 return head,c,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
def get(url):
 for i in range(6):
  try:
   with urllib.request.urlopen(urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'Cache-Control':'no-cache','User-Agent':'LEAL-TESTES003'}),timeout=60) as f:return f.read()
  except OSError:
   if i==5:raise
   time.sleep(3)
def download():
 r=api(f'repos/{SOURCE}/actions/runs/'+os.environ['VALIDATION_RUN'])
 check(r['head_sha']==os.environ['VALIDATION_COMMIT'] and r['conclusion']=='success','Windows package tests not approved')
 a=api(f'repos/{SOURCE}/actions/runs/40122015436');check(a['head_sha']=='fe80ccf9bdb670d2fd3853621f9bff29c87e6160' and a['conclusion']=='success','Button-click tests not approved')
 subprocess.run(['gh','run','download',os.environ['VALIDATION_RUN'],'--repo',SOURCE,'--name','TESTES003-DELIVERY-VALIDACAO','--dir','proof'],check=True)
def publish():
 paths=list(Path('proof').rglob('candidate.json'));check(len(paths)==1,'Ambiguous approved artifact');root=paths[0].parent;e=json.loads(paths[0].read_text())
 check(e['Client']=='003' and e['SourceCommit']==os.environ['VALIDATION_COMMIT'] and e['GlobalVersion']=='10.457' and e['DeliveryVersion']=='10.459' and e['DependencySetUnchanged'] and e['ProtectedWorkerUnchanged'],'Scope/version mismatch')
 for name in ['testes003-delivery.json','testes003-rollback.json']:check(json.loads((root/name).read_text(encoding='utf-8-sig'))['Success'],'Failed gate '+name)
 package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Approved package integrity failed')
 with zipfile.ZipFile(package) as z:check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Forbidden update members')
 head,previous,protected=snap();baselines={p:read(p) for p in FEEDS}
 check(baselines[FEEDS[0]]==e['Baseline'],'003 build baseline changed')
 for p,m in baselines.items():check(m['Version']=='10.458' and m['Sha256']==OLD_SHA and m['TargetClientCode']=='003','003 predecessor changed: '+p)
 Path('previous003-feeds.json').write_text(json.dumps(baselines,ensure_ascii=False,indent=2))
 notes='SOMENTE CASA DO ADRIANO 003: corrige o botão Verificar recuperação do programa TESTES 003, com resposta ao clique, progresso e resultado visível. GLOBAL funcional permanece 10.457; 10.459 é revisão de entrega exclusiva do 003. Testes de clique, atualização 10.458 para 10.459 e rollback aprovados em Windows isolado. Cópia de recuperação 10.457 preservada. Atualizador protegido, banco, licença, configurações e demais clientes preservados. Sem instalação manual, sem falha fictícia. Instalação desta correção no computador real ainda não confirmada.'
 manifests={};urls={}
 for path,tag in zip(FEEDS,['v10.459-casa-testes','client-003-v10.459-testes']):
  url=f'https://github.com/{REPO}/releases/download/{tag}/{e["Package"]}';urls[path]=url
  if subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True).returncode:
   subprocess.run(['gh','release','create',tag,str(package),'previous003-feeds.json','--repo',REPO,'--target','main','--title','TESTES 003 • correção Verificar recuperação','--notes',notes,'--latest=false'],check=True)
  check(hashlib.sha256(get(url)).hexdigest()==e['Sha256'],'Public artifact integrity failed: '+tag)
  m=dict(baselines[path]);m.update(Version='10.459',GlobalFeatureVersion='10.457',PackageUrl=url,Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],Notes=notes,BuildRunId=os.environ['VALIDATION_RUN'],ValidationRunId=os.environ['VALIDATION_RUN'],PublicationRunId=os.environ['GITHUB_RUN_ID'],InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True,AuxiliaryVersion='1.1',ApprovedAgentRun='40122015436')
  if path==FEEDS[1]:m['LegacyDiscoveryMirror']=True
  manifests[path]=m
 check(manifests[FEEDS[0]]['Component']=='GLOBAL' and manifests[FEEDS[0]]['PetApiVersion']==1,'Common notifier contract changed')
 check(urls[FEEDS[1]].startswith(f'https://github.com/{REPO}/releases/download/client-003-v'),'Legacy notifier URL incompatible')
 check(snap()[0]==head,'Concurrent publication; no feed activated')
 tree=api(f'repos/{REPO}/git/trees',dict(base_tree=previous['tree']['sha'],tree=[dict(path=p,type='blob',mode='100644',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n') for p,m in manifests.items()]))
 commit=api(f'repos/{REPO}/git/commits',dict(message='003 only: deliver tested recovery verification fix on both existing discovery routes',tree=tree['sha'],parents=[head]))
 api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
 try:
  after=snap()[2];check({p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}==set(FEEDS),'Unexpected changed feed')
  for p,m in manifests.items():
   check(read(p)==m,'API readback differs: '+p)
   for i in range(30):
    if json.loads(get(f'https://raw.githubusercontent.com/{REPO}/main/'+p))==m:break
    time.sleep(3)
   else:raise RuntimeError('Public updater feed not confirmed: '+p)
  Path('published-testes003.json').write_text(json.dumps(dict(Published=True,Client='003',GlobalVersion='10.457',DeliveryVersion='10.459',SourceCommit=e['SourceCommit'],ValidationRun=os.environ['VALIDATION_RUN'],ApprovedAgentRun='40122015436',FeedCommit=commit['sha'],Manifests=manifests,OnlyChangedPaths=FEEDS,OtherFeedsUnchanged=True,PackageSha256=e['Sha256'],RealInstallationConfirmed=False),indent=2))
  print('PASS: 003 10.459 public artifact hashes and both existing updater feeds confirmed; other feeds unchanged; real installation not claimed')
 except Exception:
  if snap()[0]==commit['sha']:
   c=api(f'repos/{REPO}/git/commits',dict(message='Rollback 003 feeds: public confirmation failed',tree=previous['tree']['sha'],parents=[commit['sha']]))
   api(f'repos/{REPO}/git/refs/heads/main',dict(sha=c['sha'],force=False),'PATCH')
  raise
if __name__=='__main__':{'download':download,'publish':publish}[sys.argv[1]]()
