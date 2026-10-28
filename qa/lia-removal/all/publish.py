"""Publish only the exact Windows-approved Casa003 artifact; no compilation."""
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.request
import zipfile

SOURCE='lealinfopdvnovo/leal-info-pdv'
REPO=SOURCE+'-updates'
def check(ok,message):
    if not ok: raise RuntimeError(message)
def api(path,data=None,method='POST'):
    args=['gh','api',path]
    if data is not None: args+=['--method',method,'--input','-']
    return json.loads(subprocess.check_output(args,input=None if data is None else json.dumps(data).encode()))
def read(path):
    obj=api(f'repos/{REPO}/contents/{path}?ref=main')
    return json.loads(base64.b64decode(obj['content']).decode('utf-8-sig'))
def get(url):
    with urllib.request.urlopen(urllib.request.Request(url+'?verify='+str(time.time_ns()),headers={'User-Agent':'Casa-462-protected-publication','Cache-Control':'no-cache'}),timeout=90) as r:return r.read()
def snapshot():
    head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']
    commit=api(f'repos/{REPO}/git/commits/{head}')
    tree=api(f'repos/{REPO}/git/trees/{commit["tree"]["sha"]}?recursive=1')
    check(not tree.get('truncated'),'Truncated update tree')
    return head,commit,{x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'}

# Authorized scope: common root and each remaining original client channel.
FEEDS={'GLOBAL':'version.json',**{c:f'clients/{c}/version.json' for c in ('001','002','004')}}
head,old,protected=snapshot();manifests={};proof=[]
for channel,path in FEEDS.items():
 root=Path('proof')/f'lia-removal-10463-{channel}'/'release-output'
 candidate=json.loads((root/f'{channel}.json').read_text())
 gate=json.loads((root/f'{channel}-gate.json').read_text())
 check(gate['Pass'] and gate['TransactionScenarios']==3 and gate['Channel']==channel,'Missing channel gate')
 check(candidate['SourceCommit']==os.environ['GITHUB_SHA'] and candidate['Version']=='10.463' and candidate['Channel']==channel,'Wrong candidate')
 check(candidate['DependenciesUnchanged'] and candidate['EntryAnimationPreserved'] and candidate['LiaButtonRemoved'],'Preservation gate missing')
 before=read(path);check(before==candidate['Feed'],'Live feed advanced; no feed changed')
 package=root/candidate['Package'];digest=hashlib.sha256(package.read_bytes()).hexdigest();check(digest==candidate['Sha256'],'Hash mismatch')
 with zipfile.ZipFile(package) as z:check(len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'},'Unexpected payload')
 tag=f'v10.463-{channel.lower()}-sem-botao-lia';url=f'https://github.com/{REPO}/releases/download/{tag}/{package.name}'
 notes='Retirada somente do botão LIA e acionamentos exclusivos. Animação de entrada, funcionalidades operacionais, PET, dados, licença, configurações e atualizador preservados.'
 exists=subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True).returncode==0
 if not exists:subprocess.run(['gh','release','create',tag,str(package),'--repo',REPO,'--target',head,'--title',f'{channel}10.463 — retirada do botão LIA','--notes',notes,'--latest=false'],check=True)
 check(hashlib.sha256(get(url)).hexdigest()==digest,'Public package mismatch; feeds unchanged')
 m=dict(before);m.update(Version='10.463',PackageUrl=url,Sha256=digest,SourceCommit=os.environ['GITHUB_SHA'],BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion=before['Version'],Notes=notes,InstalledOnRealComputers=False,LiaButtonRemoved=True,EntryAnimationPreserved=True,PackagesVerifiedBeforeFeed=True)
 manifests[path]=m;proof.append(dict(Channel=channel,Feed=path,Sha256=digest,RecoveryScenarios=3))
check(snapshot()[0]==head,'Concurrent update; feeds unchanged')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=p,mode='100644',type='blob',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n') for p,m in manifests.items()]))
commit=api(f'repos/{REPO}/git/commits',dict(message='Authorized common LIA button removal10.463: GLOBAL,001,002,004; keep Casa approved462 and client PETs',tree=tree['sha'],parents=[head]))
api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
try:
 after=snapshot()[2];changed={p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}
 check(changed==set(manifests),'Unexpected distribution change')
 for p,m in manifests.items():
  check(read(p)==m,'API feed mismatch')
  for attempt in range(40):
   if json.loads(get(f'https://raw.githubusercontent.com/{REPO}/main/{p}'))==m:break
   time.sleep(3)
  else:raise RuntimeError('Public feed convergence failed')
 Path('publication-proof.json').write_text(json.dumps(dict(Published=True,Version='10.463',FeedCommit=commit['sha'],Channels=proof,ChangedFiles=sorted(changed),Casa462Unchanged=True,AllOtherBlobsUnchanged=True,RealInstallationConfirmed=False),indent=2))
 print('PASS publication GLOBAL001002004; four public packages and feeds verified; Casa462 and PET feeds unchanged')
except Exception:
 if snapshot()[0]==commit['sha']:
  rollback=api(f'repos/{REPO}/git/commits',dict(message='Restore prior feeds after failed LIA removal publication verification',tree=old['tree']['sha'],parents=[commit['sha']]))
  api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rollback['sha'],force=False),'PATCH')
 raise
