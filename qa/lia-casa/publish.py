import base64,hashlib,json,os,subprocess,sys,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv';REPO=SOURCE+'-updates';FEED='clients/003/global/version.json';TAG='v10.458-casa-lia'
EXPECTED_SHA='869871bdb0065cbc7fc00f2d3cba61194ecea32a9dbbaae0367c3ec717a65ed8'
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(path,data=None,method='POST'):
 args=['gh','api',path]
 if data is not None:args+=['--method',method,'--input','-']
 return json.loads(subprocess.check_output(args,input=None if data is None else json.dumps(data).encode()))
def read(p):
 d=api(f'repos/{REPO}/contents/{p}?ref=main');return json.loads(base64.b64decode(d['content']).decode('utf-8-sig'))
def snapshot():
 head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];c=api(f'repos/{REPO}/git/commits/{head}');t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1');check(not t.get('truncated'),'Tree truncated');return head,c,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
def get(url):
 for attempt in range(6):
  try:
   with urllib.request.urlopen(urllib.request.Request(url+'?verify='+str(time.time_ns()),headers={'Cache-Control':'no-cache'}),timeout=45) as r:return r.read()
  except OSError:
   if attempt==5:raise
   time.sleep(3)
if sys.argv[1]=='download':
 expected=os.environ['VALIDATED_SOURCE']
 runs=api(f'repos/{SOURCE}/actions/runs?head_sha={expected}&per_page=20')['workflow_runs']
 rs=[r for r in runs if r['name']=='LIA Casa do Adriano - candidata sem publicar' and r['head_sha']==expected and r['conclusion']=='success']
 check(len(rs)==1,'Exact successful Windows run required');run=rs[0]['id']
 subprocess.run(['gh','run','download',str(run),'--repo',SOURCE,'--name','lia-casa-NAO-PUBLICADO','--dir','proof'],check=True)
 Path('validated-run.txt').write_text(str(run));sys.exit(0)
head,old,protected=snapshot();before=read(FEED)
check(before['Version']=='10.457' and before['Sha256']==EXPECTED_SHA and before['TargetClientCode']=='003','Casa published baseline moved')
paths=list(Path('proof').rglob('candidate.json'));check(len(paths)==1,'Ambiguous candidate');root=paths[0].parent;e=json.loads(paths[0].read_text())
check(e['Version']=='10.458' and e['Channel']=='CASA' and e['SourceCommit']==os.environ['VALIDATED_SOURCE'],'Wrong candidate')
check(json.loads((root/'lia-results.json').read_text())['Success'],'LIA test failed')
check(json.loads((root/'login457-results.json').read_text())['Success'],'Login test failed')
rb=json.loads((root/'rollback.json').read_text(encoding='utf-8-sig'));check(len(rb)==3 and all(x['Pass'] for x in rb),'Rollback tests failed')
p=root/e['Package'];check(hashlib.sha256(p.read_bytes()).hexdigest()==e['Sha256'],'Package hash differs')
with zipfile.ZipFile(p) as z:check(set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'} and len(z.namelist())==3,'Unexpected payload')
check(subprocess.run(['gh','release','view',TAG,'--repo',REPO],capture_output=True).returncode!=0,'Release exists; refuse overwrite')
notes='Casa do Adriano: restaura o botão original da LIA omitido no pacote anterior. Preserva PET V1.1, licença, banco, configurações, atualizador e demais clientes. Voz e instalação real ainda exigem conferência no computador.'
Path('previous-casa.json').write_text(json.dumps(before,ensure_ascii=False,indent=2))
subprocess.run(['gh','release','create',TAG,str(p),'previous-casa.json','--repo',REPO,'--target','main','--title','Casa do Adriano 10.458 — restaurar LIA','--notes',notes,'--latest=false'],check=True)
url=f'https://github.com/{REPO}/releases/download/{TAG}/{p.name}'
check(hashlib.sha256(get(url)).hexdigest()==e['Sha256'],'Public package hash mismatch')
check(snapshot()[0]==head,'Concurrent change; feed untouched')
m=dict(before);m.update(Version='10.458',PackageUrl=url,Sha256=e['Sha256'],SourceCommit=e['SourceCommit'],BuildRunId=Path('validated-run.txt').read_text(),ValidationRunId=Path('validated-run.txt').read_text(),PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion='10.457',Notes=notes,InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True)
t=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=FEED,mode='100644',type='blob',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n')]))
c=api(f'repos/{REPO}/git/commits',dict(message='Casa do Adriano only: 10.458 restore original LIA button; GLOBAL root and PETs unchanged',tree=t['sha'],parents=[head]))
api(f'repos/{REPO}/git/refs/heads/main',dict(sha=c['sha'],force=False),'PATCH')
try:
 after=snapshot()[2];check({p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}=={FEED},'Unexpected changed files')
 check(read(FEED)==m,'API readback mismatch')
 for i in range(20):
  if json.loads(get(f'https://raw.githubusercontent.com/{REPO}/main/{FEED}'))==m:break
  time.sleep(3)
 else:raise RuntimeError('Live feed mismatch')
 Path('published-lia-casa.json').write_text(json.dumps(dict(Published=True,InstalledOnCustomer=False,Manifest=m,FeedCommit=c['sha'],ProtectedFilesUnchanged={p:h for p,h in protected.items() if p!=FEED}),indent=2))
 print('PASS: Casa 10.458 published; public package and feed verified; only Casa feed changed; no customer installation claimed')
except Exception:
 if snapshot()[0]==c['sha']:
  rc=api(f'repos/{REPO}/git/commits',dict(message='Rollback Casa LIA feed: publication verification failed',tree=old['tree']['sha'],parents=[c['sha']]))
  api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rc['sha'],force=False),'PATCH')
 raise
