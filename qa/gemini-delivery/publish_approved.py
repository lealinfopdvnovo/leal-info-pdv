import base64,hashlib,io,json,os,subprocess,sys,time,urllib.request,zipfile
from pathlib import Path
SOURCE='lealinfopdvnovo/leal-info-pdv';REPO=SOURCE+'-updates'
FEED='clients/003/global/version.json';TAG='v10.461-casa-gemini'
RUN=40611351782;COMMIT='be7c9adb6ca04b88637eb323d96b344a660d07a5'
PACKAGE='LEAL_INFO_PDV_CASA_V10_461_UPDATE.zip';SHA='d0e78ae6e9baef1aad05b4ce4847d156940cd8a9322a7d8b909aa2f0ac1a58fc'
ARTIFACT=12783028030;DIGEST='7ffe870f0ca64034d99196967f042cfcd9c102bd54e6b607563b3944ab0e8d87'
def check(ok,msg):
 if not ok:raise RuntimeError(msg)
def api(path,data=None,method='POST'):
 args=['gh','api',path]
 if data is not None:args+=['--method',method,'--input','-']
 return json.loads(subprocess.check_output(args,input=None if data is None else json.dumps(data).encode()))
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url+'?verify='+str(time.time_ns()),headers={'User-Agent':'Casa-461-publication','Cache-Control':'no-cache'}),timeout=60) as r:return r.read()
def read(p):
 d=api(f'repos/{REPO}/contents/{p}?ref=main');return json.loads(base64.b64decode(d['content']).decode('utf-8-sig'))
def snapshot():
 h=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];c=api(f'repos/{REPO}/git/commits/{h}');t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1');check(not t.get('truncated'),'Tree truncated');return h,c,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
if sys.argv[1]=='download':
 run=api(f'repos/{SOURCE}/actions/runs/{RUN}');check(run['head_sha']==COMMIT and run['conclusion']=='success','Approved run mismatch')
 raw=subprocess.check_output(['gh','api',f'repos/{SOURCE}/actions/artifacts/{ARTIFACT}/zip']);check(hashlib.sha256(raw).hexdigest()==DIGEST,'Approved artifact digest mismatch')
 z=zipfile.ZipFile(io.BytesIO(raw));check(all(not n.startswith('/') and '\\' not in n and '..' not in n.split('/') for n in z.namelist()),'Unsafe artifact path');z.extractall('proof');print('PASS exact approved artifact recovered; no rebuild');sys.exit(0)
root=Path('proof/output');e=json.loads((root/'candidate.json').read_text());gate=json.loads((root/'validation-gate.json').read_text())
check(e['Version']=='10.461' and e['TargetClientCode']=='003' and e['SourceCommit']==COMMIT and e['Sha256']==SHA,'Candidate identity mismatch')
check(gate['Pass'] and gate['TransactionScenarios']==19 and gate['OriginalLiaPreserved'] and gate['GlobalReferenceUnchanged'] and gate['UpdaterMechanismUnchanged'],'Validation gate mismatch')
rb=json.loads((root/'transaction-results.json').read_text(encoding='utf-8-sig'));check(len(rb)==19 and all(x['pass'] for x in rb),'Recovery suite mismatch')
p=root/PACKAGE;check(hashlib.sha256(p.read_bytes()).hexdigest()==SHA,'Package SHA mismatch')
with zipfile.ZipFile(p) as z:check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Payload mismatch')
head,old,protected=snapshot();before=read(FEED)
check(before['Version']=='10.460' and before['TargetClientCode']=='003' and before['Sha256']=='885835900b60a51f99c22b61ac2c30cd78b468ad348e8721e243ccac71ad2b68','Casa baseline moved; feed untouched')
check(FEED=='clients/003/global/version.json','Wrong destination')
url=f'https://github.com/{REPO}/releases/download/{TAG}/{PACKAGE}'
notes='EXCLUSIVA Casa do Adriano: corrige leitura de JSON em mensagens WebSocket de texto e binárias, preservando limites e validação. Prepara áudio antes de consumir respostas imediatas. LIA original, PET, licença, dados e rollback preservados. Conexão Gemini e cota real ainda exigem teste no Windows.'
exists=subprocess.run(['gh','release','view',TAG,'--repo',REPO],capture_output=True).returncode==0
if not exists:
 subprocess.run(['gh','release','create',TAG,str(p),'--repo',REPO,'--target',head,'--title','Casa do Adriano 10.461 — LIA Gemini experimental','--notes',notes,'--latest=false'],check=True)
check(hashlib.sha256(get(url)).hexdigest()==SHA,'Public asset hash mismatch; feed untouched')
check(snapshot()[0]==head,'Concurrent change; feed untouched')
m=dict(before);m.update(Version='10.461',TargetClientCode='003',Component='GLOBAL',PackageUrl=url,Sha256=SHA,SourceCommit=COMMIT,BuildRunId=str(RUN),ValidationRunId=str(RUN),PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion='10.460',Notes=notes,InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True,ExperimentalGemini=True,RealApiTested=False,FreeQuotaConfirmed=False)
# Component is the existing binary format contract, NOT a global publication route.
# The ONLY writable path is the licensed Casa scoped feed; every other blob is immutable here.
t=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=FEED,mode='100644',type='blob',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n')]))
c=api(f'repos/{REPO}/git/commits',dict(message='Casa do Adriano ONLY: authorized 10.461 from validated run40611351782; preserve GLOBAL root and all other clients',tree=t['sha'],parents=[head]))
api(f'repos/{REPO}/git/refs/heads/main',dict(sha=c['sha'],force=False),'PATCH')
try:
 after=snapshot()[2];changed={p for p in set(after)|set(protected) if after.get(p)!=protected.get(p)};check(changed=={FEED},'Unexpected changed file')
 check(read(FEED)==m,'API feed mismatch')
 for attempt in range(12):
  if json.loads(get(f'https://raw.githubusercontent.com/{REPO}/main/{FEED}'))==m:break
  time.sleep(3)
 else:raise RuntimeError('Public feed did not converge')
 Path('publication-proof.json').write_text(json.dumps(dict(Published=True,Version='10.461',TargetClientCode='003',Feed=FEED,FeedCommit=c['sha'],PackageSha256=SHA,ApprovedRun=RUN,ApprovedCommit=COMMIT,ChangedFiles=sorted(changed),ProtectedBlobCount=len(protected)-1,GlobalRootUnchanged=after['version.json']==protected['version.json'],AllOtherBlobsUnchanged=True,InstalledOnCustomer=False,RealGeminiApiTested=False,Manifest=m),indent=2))
 print('PASS REAL PUBLICATION: only Casa003 feed changed; public package SHA and feed verified; GLOBAL root, PETs and other clients unchanged')
except Exception:
 if snapshot()[0]==c['sha']:
  rc=api(f'repos/{REPO}/git/commits',dict(message='Rollback Casa459 feed: publication verification failed',tree=old['tree']['sha'],parents=[c['sha']]))
  api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rc['sha'],force=False),'PATCH')
 raise
