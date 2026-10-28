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
FEED='clients/003/global/version.json'
VERSION='10.462'
TAG='v10.462-casa-sem-botao-lia'
PACKAGE='LEAL_INFO_PDV_CASA_V10_462_UPDATE.zip'
BASE_SHA='d0e78ae6e9baef1aad05b4ce4847d156940cd8a9322a7d8b909aa2f0ac1a58fc'
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

if os.environ.get('PUBLISH_PHASE')=='download':
    run=api(f'repos/{SOURCE}/actions/runs/{os.environ["GITHUB_RUN_ID"]}')
    check(run['head_sha']==os.environ['GITHUB_SHA'] and run['head_branch']=='release/lia-removal-casa-10462','Wrong source run')
    jobs=api(f'repos/{SOURCE}/actions/runs/{run["id"]}/jobs')['jobs']
    check(any(j['name']=='build' and j['conclusion']=='success' for j in jobs),'Windows build not approved')
    artifact=os.environ['APPROVED_ARTIFACT_ID'];digest=os.environ['APPROVED_ARTIFACT_SHA']
    raw=subprocess.check_output(['gh','api',f'repos/{SOURCE}/actions/artifacts/{artifact}/zip'])
    check(hashlib.sha256(raw).hexdigest()==digest.removeprefix('sha256:'),'Artifact SHA mismatch')
    z=zipfile.ZipFile(io.BytesIO(raw))
    check(all(not n.startswith('/') and '\\' not in n and '..' not in n.split('/') for n in z.namelist()),'Unsafe artifact')
    z.extractall('proof')
    print('PASS exact approved Windows artifact; no rebuild')
    raise SystemExit(0)

root=Path('proof/release-output')
candidate=json.loads((root/'candidate.json').read_text())
gate=json.loads((root/'validation-gate.json').read_text())
check(candidate['Version']==VERSION and candidate['TargetClientCode']=='003' and candidate['SourceCommit']==os.environ['GITHUB_SHA'],'Candidate identity mismatch')
check(gate['Pass'] and gate['TransactionScenarios']==19 and gate['LiaButtonRemoved'] and gate['EntryAnimationPreserved'] and gate['DependenciesUnchanged'],'Windows gate not approved')
results=json.loads((root/'transaction-results.json').read_text(encoding='utf-8-sig'))
check(len(results)==19 and all(x['pass'] for x in results),'Recovery results not approved')
package=root/PACKAGE;digest=hashlib.sha256(package.read_bytes()).hexdigest()
check(digest==candidate['Sha256'],'Approved package hash mismatch')
with zipfile.ZipFile(package) as z:
    check(set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'} and len(z.namelist())==3,'Unexpected payload')
head,old,protected=snapshot();before=read(FEED)
check(before['Version']=='10.461' and before['TargetClientCode']=='003' and before['Sha256']==BASE_SHA,'Casa baseline moved; no feed changed')
url=f'https://github.com/{REPO}/releases/download/{TAG}/{PACKAGE}'
notes='Exclusiva Casa do Adriano: retirada do botão e acionamento da LIA. Animação de entrada mantida. Dados, licença, configurações, PET e atualizador preservados.'
exists=subprocess.run(['gh','release','view',TAG,'--repo',REPO],capture_output=True).returncode==0
if not exists:
    subprocess.run(['gh','release','create',TAG,str(package),'--repo',REPO,'--target',head,'--title','Casa do Adriano10.462 — retirada do botão LIA','--notes',notes,'--latest=false'],check=True)
check(hashlib.sha256(get(url)).hexdigest()==digest,'Public package hash mismatch; feed untouched')
check(snapshot()[0]==head,'Concurrent update; feed untouched')
manifest=dict(before)
manifest.update(Version=VERSION,TargetClientCode='003',Component='GLOBAL',PackageUrl=url,Sha256=digest,
    SourceCommit=os.environ['GITHUB_SHA'],BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],
    PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion='10.461',Notes=notes,
    InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True,ExperimentalGemini=False,
    LiaButtonRemoved=True,EntryAnimationPreserved=True,ProtectedUpdaterRequired=True)
# Existing binary-format enum. The only writable distribution path is Casa003.
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=FEED,mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
commit=api(f'repos/{REPO}/git/commits',dict(message='Casa003 ONLY: authorized LIA button removal10.462; preserve GLOBAL root and other clients',tree=tree['sha'],parents=[head]))
api(f'repos/{REPO}/git/refs/heads/main',dict(sha=commit['sha'],force=False),'PATCH')
try:
    after=snapshot()[2]
    changed={p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}
    check(changed=={FEED},'Unexpected changed distribution file')
    check(read(FEED)==manifest,'API manifest mismatch')
    for attempt in range(40):
        if json.loads(get(f'https://raw.githubusercontent.com/{REPO}/main/{FEED}'))==manifest:break
        time.sleep(3)
    else:raise RuntimeError('Public feed did not converge')
    Path('publication-proof.json').write_text(json.dumps(dict(
        Published=True,Version=VERSION,TargetClientCode='003',Feed=FEED,FeedCommit=commit['sha'],
        PackageSha256=digest,ChangedFiles=sorted(changed),GlobalRootUnchanged=after['version.json']==protected['version.json'],
        AllOtherDistributionBlobsUnchanged=True,LiaButtonRemoved=True,EntryAnimationPreserved=True,
        InstalledOnCustomer=False,Manifest=manifest),indent=2))
    print('PASS PUBLICATION Casa003 only; public package and feed verified; other clients and GLOBAL root unchanged')
except Exception:
    if snapshot()[0]==commit['sha']:
        rollback=api(f'repos/{REPO}/git/commits',dict(message='Rollback Casa10.462 feed after failed publication verification',tree=old['tree']['sha'],parents=[commit['sha']]))
        api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rollback['sha'],force=False),'PATCH')
    raise
