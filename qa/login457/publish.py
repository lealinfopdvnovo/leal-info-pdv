import base64,concurrent.futures,hashlib,json,os,subprocess,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv';REPO=SOURCE+'-updates';RUNS={'002':40026078388,'CASA':40027170070,'004':40027170070};COMMITS={'002':'23788f93bb5475806ffd8c211ffb5c06a8a42f90','CASA':'e691d9e8d548ce30c54653698a98370e0a2838e8','004':'e691d9e8d548ce30c54653698a98370e0a2838e8'};CHANNELS=('002','CASA','004')
PATHS={'002':'clients/002/version.json','CASA':'clients/003/global/version.json','004':'clients/004/version.json'}
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(path,data=None,method=None):
 a=['gh','api',path]
 if data is not None:a+=['--method',method or 'POST','--input','-']
 return json.loads(subprocess.check_output(a,input=None if data is None else json.dumps(data).encode()))
def read(path):
 d=api(f'repos/{REPO}/contents/{path}?ref=main');return dict(sha=d['sha'],manifest=json.loads(base64.b64decode(d['content']).decode('utf-8-sig')))
def download(url):
 for i in range(6):
  try:
   with urllib.request.urlopen(urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'Cache-Control':'no-cache','User-Agent':'LEAL-LOGIN-457'}),timeout=60) as r:return r.read()
  except OSError:
   if i==5:raise
   time.sleep(3)
def download_proof():
 for c in CHANNELS:
  run=RUNS[c];r=api(f'repos/{SOURCE}/actions/runs/{run}');check(r['head_sha']==COMMITS[c],'Unexpected source')
  jobs=api(f'repos/{SOURCE}/actions/runs/{run}/jobs')['jobs'];matching=[j for j in jobs if j['name'].startswith('windows ('+c+',')]
  check(len(matching)==1 and matching[0]['conclusion']=='success','Windows job not approved '+c)
  subprocess.run(['gh','run','download',str(run),'--repo',SOURCE,'--name','login457-'+c+'-NAO-PUBLICADO','--dir','proof/'+c],check=True)
def snapshot():
 head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];commit=api(f'repos/{REPO}/git/commits/{head}');t=api(f'repos/{REPO}/git/trees/{commit["tree"]["sha"]}?recursive=1');check(not t.get('truncated'),'Incomplete tree')
 return head,commit,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
def publish():
 head,old,protected=snapshot();entries={};before={};manifests={};packages={}
 for c in CHANNELS:
  matches=list(Path('proof').rglob(c+'/candidate.json'));check(len(matches)==1,'Missing/ambiguous proof '+c);root=matches[0].parent;e=json.loads(matches[0].read_text())
  check(e['Channel']==c and e['Version']=='10.457' and e['SourceCommit']==COMMITS[c] and e['FeedPath']==PATHS[c] and e['ScopeApproved'] and not e['UpdaterLogicChanged'] and not e['PetChanged'] and not e['Loja001Touched'],'Invalid scope '+c)
  check(json.loads((root/'login457-results.json').read_text())['Success'],'Login test failed '+c)
  checks=json.loads((root/'rollback.json').read_text(encoding='utf-8-sig'));check(len(checks)==3 and all(x['Pass'] for x in checks),'Rollback test failed '+c)
  before[c]=read(PATHS[c]);check(before[c]['manifest']==e['Baseline'],'Published baseline changed '+c)
  package=root/e['Package'];check(hashlib.sha256(package.read_bytes()).hexdigest()==e['Sha256'],'Wrong package hash '+c)
  with zipfile.ZipFile(package) as z:check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Forbidden payload/PET file '+c)
  entries[c]=e;packages[c]=package
 notes='GLOBAL 10.457: somente destaque dourado e legível da versão na tela de login. Preserva PET, funções, banco, licença, estoque e configurações. Atualizador existente sem alteração. Loja 001 intocada.'
 for c,e in entries.items():
  tag='v10.457-casa' if c=='CASA' else f'client-{c}-v10.457';url=f'https://github.com/{REPO}/releases/download/{tag}/{e["Package"]}'
  backup=Path(f'previous-{c}.json');backup.write_text(json.dumps(before[c]['manifest'],ensure_ascii=False,indent=2))
  check(subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True).returncode!=0,'Release already exists; refuse overwrite '+tag)
  subprocess.run(['gh','release','create',tag,str(packages[c]),str(backup),'--repo',REPO,'--target','main','--title','GLOBAL 10.457 • '+('003' if c=='CASA' else c),'--notes',notes,'--latest=false'],check=True)
  m=dict(e['Baseline']);m.update(Version='10.457',GlobalFeatureVersion='10.457',PackageUrl=url,Sha256=e['Sha256'],SourceCommit=COMMITS[c],BuildRunId=str(RUNS[c]),ValidationRunId=str(RUNS[c]),PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion=e['Baseline']['Version'],Notes=notes,InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True)
  manifests[c]=m
 def verify(c):check(hashlib.sha256(download(manifests[c]['PackageUrl'])).hexdigest()==entries[c]['Sha256'],'Public hash differs '+c)
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(verify,CHANNELS))
 check(snapshot()[0]==head,'Concurrent publication; feeds not touched')
 tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=PATHS[c],mode='100644',type='blob',content=json.dumps(manifests[c],ensure_ascii=False,indent=2)+'\n') for c in CHANNELS]))
 commit=api(f'repos/{REPO}/git/commits',dict(message='002 003 004: GLOBAL 10.457 somente destaque da versão no login; 001 e PETs intocados',tree=tree['sha'],parents=[head]))
 api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
 try:
  after=snapshot()[2];changed={p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)};check(changed==set(PATHS.values()),'Unexpected changed feed/PET/001/GLOBAL file')
  for c in CHANNELS:
   check(read(PATHS[c])['manifest']==manifests[c],'API readback differs '+c)
   for i in range(30):
    if json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/'+PATHS[c]))==manifests[c]:break
    time.sleep(3)
   else:raise RuntimeError('Live updater differs '+c)
  Path('published-login457.json').write_text(json.dumps(dict(Published=True,Version='10.457',SourceCommits=COMMITS,ValidationRuns=RUNS,FeedCommit=commit['sha'],Channels=['002','003','004'],Manifests=manifests,ProtectedFilesUnchanged={p:h for p,h in protected.items() if p not in changed},Loja001Untouched=True,PetsUntouched=True,GlobalRootUntouched=True,InstalledOnCustomer=False),indent=2))
  print('PASS PUBLICADO 002 003 004 GLOBAL 10.457; hashes e feeds publicos confirmados; 001 PETs e root GLOBAL intocados; instalacao nao afirmada')
 except Exception:
  if snapshot()[0]==commit['sha']:
   rollback=api(f'repos/{REPO}/git/commits',dict(message='Rollback feeds login 10.457: verificacao de publicacao falhou',tree=old['tree']['sha'],parents=[commit['sha']]))
   api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rollback['sha'],force=False),'PATCH')
  raise
if __name__=='__main__':
 import sys
 {'download':download_proof,'publish':publish}[sys.argv[1]]()
