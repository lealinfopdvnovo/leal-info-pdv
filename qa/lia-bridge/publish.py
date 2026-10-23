import base64,hashlib,io,json,os,subprocess,sys,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv'
REPO='lealinfopdvnovo/leal-info-pdv-updates'
COMMIT='91f59a5282e064e7190ed98da1e01c597410dba9'
RUN=40015881058
ARTIFACT=12529050871
DIGEST='edcb72b6199bebdf5f1abc8f8b4361f89afca764129b8b08221352daccccab49'
PRIMARY='clients/001/version.json'
SECONDARY='clients/001/lia/version.json'
PRIMARY_SHA='64210e6693be6fc43240deb4116bc1199e5a3c27'
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(path,data=None,method=None):
 args=['gh','api',path]
 if data is not None:args+=['--method',method or 'POST','--input','-']
 return json.loads(subprocess.check_output(args,input=None if data is None else json.dumps(data).encode()))
def sha(b):return hashlib.sha256(b).hexdigest()
def read(path,missing=False):
 r=subprocess.run(['gh','api',f'repos/{REPO}/contents/{path}?ref=main'],capture_output=True)
 if r.returncode:
  if missing and '404' in r.stderr.decode():return None
  raise RuntimeError('Cannot read '+path)
 d=json.loads(r.stdout);return {'sha':d['sha'],'manifest':json.loads(base64.b64decode(d['content']).decode('utf-8-sig'))}
def download(url):
 for attempt in range(12):
  try:
   req=urllib.request.Request(url+'?verify='+str(time.time_ns()),headers={'User-Agent':'LEAL-LIA-001-RELEASE','Cache-Control':'no-cache'})
   with urllib.request.urlopen(req,timeout=45) as response:return response.read()
  except OSError:
   if attempt==11:raise
   time.sleep(3)
if sys.argv[1]=='download':
 run=api(f'repos/{SOURCE}/actions/runs/{RUN}')
 check(run['conclusion']=='success' and run['head_sha']==COMMIT,'Approved Windows run differs')
 artifact=api(f'repos/{SOURCE}/actions/artifacts/{ARTIFACT}')
 check(not artifact['expired'] and artifact['workflow_run']['head_sha']==COMMIT and artifact['digest']=='sha256:'+DIGEST,'Approved artifact differs')
 raw=subprocess.check_output(['gh','api',f'repos/{SOURCE}/actions/artifacts/{ARTIFACT}/zip'])
 check(sha(raw)==DIGEST,'Artifact archive SHA-256 mismatch')
 allowed={'bridge-results.json','bridge-packages.json','lia-hashes.json','bridge-first.zip','bridge-final.zip'}
 with zipfile.ZipFile(io.BytesIO(raw)) as z:
  check(len(z.namelist())==len(allowed) and set(z.namelist())==allowed,'Artifact member mismatch')
  Path('proof').mkdir()
  for n in allowed:Path('proof',n).write_bytes(z.read(n))
 print('PASS: reused exact approved Windows artifact; no rebuild')
 sys.exit(0)
root=Path('proof')
results=json.loads((root/'bridge-results.json').read_text(encoding='utf-8-sig'))
expected={'success','bad-sha','bad-lia-pin','duplicate-core','interrupt-core','interrupt-lia','interrupt-new','interrupt-recovery'}
check(len(results)==8 and {r['Scenario'] for r in results}==expected and all(r['Pass'] for r in results),'Backup/rollback validation missing')
hashes=json.loads((root/'bridge-packages.json').read_text())
lia=json.loads((root/'lia-hashes.json').read_text())
core={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
entries={}
for version,file in [('10.454','bridge-first.zip'),('10.455','bridge-final.zip')]:
 raw=(root/file).read_bytes();check(sha(raw)==hashes[file],'Approved package SHA mismatch')
 with zipfile.ZipFile(io.BytesIO(raw)) as z:
  names=z.namelist();wanted=core if version=='10.454' else core|set(lia)
  check(len(names)==len(wanted) and set(names)==wanted,'Exact payload mismatch')
  check(all(sha(z.read(n))==h for n,h in lia.items()) if version=='10.455' else True,'Pinned LIA files differ')
  files={n:sha(z.read(n)) for n in names}
 name='LEAL_INFO_PDV_001_V'+version.replace('.','_')+'_UPDATE.zip'
 Path(name).write_bytes(raw)
 entries[version]={'Package':name,'Sha256':sha(raw),'Files':files}
before=read(PRIMARY)
check(before['sha']==PRIMARY_SHA and before['manifest']['Version']=='10.453' and before['manifest']['TargetClientCode']=='001','001 published baseline moved')
check(read(SECONDARY,True) is None,'Unexpected existing LIA feed')
protected=['version.json','clients/002/version.json','clients/003/version.json','clients/003/global/version.json','clients/004/version.json']
protected_before={p:read(p)['sha'] for p in protected}
Path('previous-001.json').write_text(json.dumps(before['manifest'],ensure_ascii=False,indent=2))
Path('validated-bridge.json').write_text(json.dumps({'Run':RUN,'SourceCommit':COMMIT,'Scenarios':results,'Packages':entries},indent=2))
for version,e in entries.items():
 tag='client-001-v'+version
 e['PackageUrl']=f'https://github.com/{REPO}/releases/download/{tag}/{e["Package"]}'
 if subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True).returncode:
  subprocess.run(['gh','release','create',tag,e['Package'],'previous-001.json','validated-bridge.json','--repo',REPO,'--target','main','--title',f'LOJA 001 {version} - LIA','--notes','Exclusivo LOJA 001. Etapas 10.454 ponte protegida e 10.455 correção da janela CONFIGURAR CHAVE da LIA. Backup e rollback Windows isolado aprovados. GLOBAL 10.453 e PET preservados. Instalação real ainda não confirmada.','--latest=false'],check=True)
 check(sha(download(e['PackageUrl']))==e['Sha256'],'Public release package mismatch')
manifests={}
for version,e in entries.items():
 m=dict(before['manifest'])
 m.update(Version=version,PackageUrl=e['PackageUrl'],Sha256=e['Sha256'],SourceCommit=COMMIT,BuildRunId=str(RUN),ValidationRunId=str(RUN),TargetClientCode='001',Mandatory=False,ProtectedUpdaterRequired=True,GlobalFeatureVersion='10.453',PetVersion=before['manifest'].get('PetVersion','1.2'),InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True,PublishedBaselineVersion='10.453' if version=='10.454' else '10.454',BridgeStage=1 if version=='10.454' else 2,MinimumInstalledVersion='10.453' if version=='10.454' else '10.454',Notes='LIA - etapa 1 de 2: atualiza a ponte protegida. Após reabrir o PDV, instale a etapa 10.455 para corrigir CONFIGURAR CHAVE.' if version=='10.454' else 'LIA - etapa 2 de 2: CONFIGURAR CHAVE abre na frente, permite colar, salvar e fechar. Chave protegida pelo Windows. Preserva GLOBAL, PET e dados.')
 manifests[PRIMARY if version=='10.454' else SECONDARY]=m
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']
old=api(f'repos/{REPO}/git/commits/{head}')
check(read(PRIMARY)['sha']==PRIMARY_SHA and read(SECONDARY,True) is None and all(read(p)['sha']==h for p,h in protected_before.items()),'Concurrent feed change')
tree=api(f'repos/{REPO}/git/trees',{'base_tree':old['tree']['sha'],'tree':[{'path':p,'mode':'100644','type':'blob','content':json.dumps(m,ensure_ascii=False,indent=2)+'\n'} for p,m in manifests.items()]})
commit=api(f'repos/{REPO}/git/commits',{'message':'LOJA 001: liberar ponte 10.454 e LIA 10.455 validadas, demais canais preservados','tree':tree['sha'],'parents':[head]})
api(f'repos/{REPO}/git/refs/heads/main',{'sha':commit['sha'],'force':False},'PATCH')
try:
 check({f['filename'] for f in api(f'repos/{REPO}/commits/{commit["sha"]}')['files']}==set(manifests),'Unexpected publication files')
 check(all(read(p)['sha']==h for p,h in protected_before.items()),'Protected channels changed')
 for p,m in manifests.items():
  check(read(p)['manifest']==m,'API manifest mismatch')
  for attempt in range(25):
   try:
    if json.loads(download('https://raw.githubusercontent.com/'+REPO+'/main/'+p))==m:break
   except OSError:pass
   time.sleep(3)
  else:raise RuntimeError('Public updater feed mismatch '+p)
 Path('published-lia-001.json').write_text(json.dumps({'Published':True,'FeedCommit':commit['sha'],'SourceCommit':COMMIT,'WindowsRun':RUN,'Manifests':manifests,'Packages':entries,'ProtectedFeedHashes':protected_before,'InstalledOnCustomer':False},ensure_ascii=False,indent=2))
 print('PASS: 001 10.454 -> 10.455 public packages + both updater feeds verified; GLOBAL/PET/002/003/004 unchanged; real installation not claimed')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',{'message':'Rollback feeds LIA 001 after failed public validation','tree':old['tree']['sha'],'parents':[commit['sha']]})
  api(f'repos/{REPO}/git/refs/heads/main',{'sha':restore['sha'],'force':False},'PATCH')
 raise
