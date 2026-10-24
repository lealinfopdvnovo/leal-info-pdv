import base64,hashlib,json,os,subprocess,sys,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv';REPO=SOURCE+'-updates';FEED='clients/002/version.json'
def check(ok,message):
 if not ok:raise RuntimeError(message)
def api(path,data=None,method=None):
 args=['gh','api',path]
 if data is not None:args+=['--method',method or 'POST','--input','-']
 return json.loads(subprocess.check_output(args,input=None if data is None else json.dumps(data).encode()))
def read():
 d=api(f'repos/{REPO}/contents/{FEED}?ref=main')
 return json.loads(base64.b64decode(d['content']).decode('utf-8-sig'))
def snapshot():
 head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];commit=api(f'repos/{REPO}/git/commits/{head}')
 tree=api(f'repos/{REPO}/git/trees/{commit["tree"]["sha"]}?recursive=1');check(not tree.get('truncated'),'Incomplete protection snapshot')
 return head,commit,{x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'}
def public_get(url):
 for attempt in range(6):
  try:
   with urllib.request.urlopen(urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'Cache-Control':'no-cache','User-Agent':'LEAL-GELAMAQ-PET'}),timeout=60) as r:return r.read()
  except OSError:
   if attempt==5:raise
   time.sleep(3)
def download():
 run=os.environ['VALIDATION_RUN'];sha=os.environ['VALIDATION_COMMIT']
 d=api(f'repos/{SOURCE}/actions/runs/{run}')
 check(d['head_sha']==sha and d['conclusion']=='success','Windows validation not approved for pinned source')
 subprocess.run(['gh','run','download',run,'--repo',SOURCE,'--name','GELAMAQ-PET-V1_1-VALIDACAO','--dir','proof'],check=True)
def publish():
 run=os.environ['VALIDATION_RUN'];sha=os.environ['VALIDATION_COMMIT']
 files=list(Path('proof').rglob('candidate.json'));check(len(files)==1,'Ambiguous candidate proof');root=files[0].parent;e=json.loads(files[0].read_text())
 check(e['SourceCommit']==sha and e['Client']=='002' and e['PetVersion']=='1.1' and e['GlobalVersion']=='10.457' and e['DeliveryRevision']=='10.458' and e['ProtectedWorkerUnchanged'] and e['DependencySetUnchanged'],'Candidate identity/scope mismatch')
 check(json.loads((root/'gelamaq-finance.json').read_text())['Success'],'Financial report test not approved')
 checks=json.loads((root/'gelamaq-rollback.json').read_text(encoding='utf-8-sig'));check(len(checks)==7 and all(x['Pass'] for x in checks),'Update/rollback not approved')
 scope=json.loads((root/'scope.json').read_text());check(scope['Client']=='002' and scope['GlobalVersion']=='10.457' and set(scope['ChangedExistingSource'])=={'MainForm.cs','Program.cs','UpdateManager.cs','TransactionalUpdate.cs','LealInfoPDV.csproj'},'Unexpected code scope')
 package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Local payload integrity failure')
 with zipfile.ZipFile(package) as z:check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Payload is not compatible protected three-file update')
 head,previous,protected=snapshot();baseline=read();check(baseline==e['Baseline'],'Gelamaq published baseline changed')
 Path('previous-gelamaq.json').write_text(json.dumps(baseline,ensure_ascii=False,indent=2))
 tag='client-002-v10.458-pet1.1';url=f'https://github.com/{REPO}/releases/download/{tag}/{e["Package"]}'
 notes='PET GELAMAQ V1.1: correção do relatório financeiro para movimentos sem venda vinculada. GLOBAL permanece 10.457. Revisão técnica de entrega 10.458 exclusiva do canal 002, para compatibilidade com o atualizador existente. PET compilado em DLL própria e transportado no pacote protegido de três arquivos; backup e rollback incluem o PET. Não altera bancos, licenças, estoque, configurações ou outros clientes. Instalação no Windows real ainda não confirmada.'
 release=subprocess.run(['gh','release','view',tag,'--repo',REPO,'--json','tagName'],capture_output=True)
 if release.returncode:
  subprocess.run(['gh','release','create',tag,str(package),'previous-gelamaq.json','--repo',REPO,'--target','main','--title','GELAMAQ • PET V1.1 • GLOBAL 10.457 preservada','--notes',notes,'--latest=false'],check=True)
 # No feed changes until the public release bytes match the approved Windows artifact.
 check(hashlib.sha256(public_get(url)).hexdigest()==e['Sha256'],'Public payload differs from approved artifact')
 check(snapshot()[0]==head,'Concurrent feed changes; publication stopped before activation')
 manifest=dict(baseline)
 manifest.update(Version=e['DeliveryRevision'],Component='PET',PetVersion='1.1',GlobalFeatureVersion='10.457',PackageUrl=url,Sha256=e['Sha256'],SourceCommit=sha,BuildRunId=run,ValidationRunId=run,PublicationRunId=os.environ['GITHUB_RUN_ID'],Notes=notes,PublishedBaselineVersion=baseline['Version'],InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True,ProtectedWorkerUnchanged=True)
 tree=api(f'repos/{REPO}/git/trees',dict(base_tree=previous['tree']['sha'],tree=[dict(path=FEED,mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
 commit=api(f'repos/{REPO}/git/commits',dict(message='GELAMAQ only: PET V1.1 financial report; GLOBAL 10.457 preserved; tested protected delivery/rollback',tree=tree['sha'],parents=[head]))
 api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
 try:
  after=snapshot()[2];changed={p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}
  check(changed=={FEED},'Unexpected GLOBAL/other-client feed change')
  check(read()==manifest,'API feed readback mismatch')
  for attempt in range(30):
   if json.loads(public_get(f'https://raw.githubusercontent.com/{REPO}/main/'+FEED))==manifest:break
   time.sleep(3)
  else:raise RuntimeError('Live updater feed not confirmed')
  Path('published-gelamaq.json').write_text(json.dumps(dict(Published=True,Client='002',PetVersion='1.1',GlobalVersion='10.457',DeliveryRevision='10.458',SourceCommit=sha,ValidationRun=run,FeedCommit=commit['sha'],Manifest=manifest,ProtectedFilesUnchanged={p:h for p,h in protected.items() if p!=FEED},InstalledOnCustomer=False),indent=2))
  print('PASS PUBLICADO SOMENTE GELAMAQ PET V1.1; GLOBAL 10.457 preservada; pacote/hash/feed públicos confirmados; instalação real não afirmada')
 except Exception:
  if snapshot()[0]==commit['sha']:
   rollback=api(f'repos/{REPO}/git/commits',dict(message='Rollback Gelamaq PET feed: public readback failed',tree=previous['tree']['sha'],parents=[commit['sha']]))
   api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rollback['sha'],force=False),'PATCH')
  raise
if __name__=='__main__':{'download':download,'publish':publish}[sys.argv[1]]()
