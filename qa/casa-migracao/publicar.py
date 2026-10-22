"""Publish only the already validated Casa migration; never rebuild."""
import base64,hashlib,json,os,shutil,subprocess,time,urllib.request,zipfile
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
SOURCE='a7ea289f587eb6650fb9d8de152fb7781a20e5ef'
RUN=39871413552
SHA='bb8a0e425f3d5b401bc0f12c69e7a4afa61ae1ec78372078bae8ed24c737a6cd'
CHANNELS=('GLOBAL','001','002','003','004')
def check(ok,msg):
 if not ok: raise RuntimeError(msg)
def api(path,data=None,method=None,source=False):
 args=['gh','api',path];env=os.environ.copy()
 if source:env['GH_TOKEN']=env['SOURCE_TOKEN']
 if data is not None:
  Path('request.json').write_text(json.dumps(data),encoding='utf-8');args+=['--method',method or 'POST','--input','request.json']
 return json.loads(subprocess.check_output(args,env=env))
def feed(c):
 p='version.json' if c=='GLOBAL' else f'clients/{c}/version.json'
 d=api(f'repos/{REPO}/contents/{p}?ref=main')
 return dict(sha=d['sha'],manifest=json.loads(base64.b64decode(d['content']).decode('utf-8-sig')))
def download(url):
 req=urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'User-Agent':'LEAL-CASA-Release','Cache-Control':'no-cache,no-store'})
 with urllib.request.urlopen(req,timeout=60) as r:return r.read()
def move(sha):api(f'repos/{REPO}/git/refs/heads/main',dict(sha=sha,force=False),'PATCH')
r=api(f'repos/lealinfopdvnovo/leal-info-pdv/actions/runs/{RUN}',source=True)
check(r['conclusion']=='success' and r['head_sha']==SOURCE,'Windows source not approved')
check(api('repos/lealinfopdvnovo/leal-info-pdv/git/ref/heads/prepare/casa-migracao-protegida',source=True)['object']['sha']==SOURCE,'Candidate source changed')
root=Path('proof')
def read(n):return json.loads((root/n).read_text(encoding='utf-8-sig'))
v=read('validation.json');m=read('migration-results.json');t=read('transition.json')
check(v['Success'] and v['Source']==SOURCE and v['Version']=='10.444' and not v['InstalledOnCustomer'],'Validation differs')
check(m['Success'] and len(m['checks'])>=45,'Migration proof incomplete')
check(len(t)==4 and {x['Scenario'] for x in t}=={'success','interruption','bad-sha','health-failure'} and all(x['Pass'] and x['ActualLegacyFiles'] and x['OriginalWorker'] for x in t),'Rollback proof incomplete')
package=root/'migration-update.zip'
check(hashlib.sha256(package.read_bytes()).hexdigest()==SHA,'Package hash mismatch')
expected={'LealInfoPDV.exe':'a3e1b3c56d271111f501f67c6c47b68eb2108700071241d0763d53a4bc258cdf','LealInfoPDV.dll':'2162db5b2720ffa0115b2c5131b221084c9d8b780b848e0fe12f955c45471afd','LealInfoPDV.deps.json':'bb125a2c71419b5cb0bc423da27ed01860c7f7d779522962217716eed64653d6'}
with zipfile.ZipFile(package) as z:
 check(len(z.namelist())==3 and set(z.namelist())==set(expected),'Unexpected payload')
 check(all(hashlib.sha256(z.read(n)).hexdigest()==h for n,h in expected.items()),'Payload mismatch')
before={c:feed(c) for c in CHANNELS}
check(before['003']['manifest']['Version']=='10.443' and before['003']['manifest']['Sha256']=='254e7531cd8bb0171afd86521730b7c8b5b0f908939dd77f01051e4eba480998','Baseline changed')
Path('version-previous.json').write_text(json.dumps(before['003']['manifest'],ensure_ascii=False,indent=2),encoding='utf-8')
tag='client-003-v10.444';asset=Path('LEAL_INFO_PDV_CASA_V10_444_UPDATE.zip');shutil.copyfile(package,asset)
url=f'https://github.com/{REPO}/releases/download/{tag}/{asset.name}'
notes='Casa do Leal: migração protegida para base comum 10.444 e PET V1.1 separado. Backup e rollback validados em Windows isolado. Homologação na instalação real pendente. Atualizador independente do PET permanece em etapa posterior.'
exists=subprocess.run(['gh','release','view',tag,'--repo',REPO],capture_output=True)
if exists.returncode:
 subprocess.run(['gh','release','create',tag,str(asset),'version-previous.json','--repo',REPO,'--target','main','--title','Casa do Leal 10.444 - migração protegida','--notes',notes,'--prerelease','--latest=false'],check=True)
check(hashlib.sha256(download(url)).hexdigest()==SHA,'Public package differs; feeds unchanged')
manifest=dict(Version='10.444',TargetClientCode='003',PackageUrl=url,Sha256=SHA,SourceCommit=SOURCE,Mandatory=False,Notes=notes,BuildRunId=str(RUN),ValidationRunId=str(RUN),PublicationRunId=os.environ['GITHUB_RUN_ID'],ProtectedUpdaterRequired=True,PublishedBaselineVersion='10.443',PetVersion='1.1',RealWindowsHomologated=False)
head=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{REPO}/git/commits/{head}')
check(all(feed(c)['sha']==before[c]['sha'] for c in CHANNELS),'Concurrent channel change')
tree=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path='clients/003/version.json',mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
commit=api(f'repos/{REPO}/git/commits',dict(message='Casa do Leal 10.444: publicar migração protegida aprovada, exclusivamente canal Casa',tree=tree['sha'],parents=[head]))
move(commit['sha'])
try:
 detail=api(f"repos/{REPO}/commits/{commit['sha']}")
 check({x['filename'] for x in detail['files']}=={'clients/003/version.json'},'Unexpected changed files')
 live=None
 for _ in range(12):
  try:live=json.loads(download(f'https://raw.githubusercontent.com/{REPO}/main/clients/003/version.json'))
  except (OSError,ValueError):pass
  if live==manifest:break
  time.sleep(3)
 check(live==manifest,'Updater public manifest differs')
 after={c:feed(c) for c in CHANNELS}
 check(all(after[c]['sha']==before[c]['sha'] for c in CHANNELS if c!='003'),'Protected channel changed')
 Path('published-CASA.json').write_text(json.dumps(dict(Published=True,Version='10.444',PetVersion='1.1',SourceCommit=SOURCE,FeedCommit=commit['sha'],Manifest=manifest,PayloadHashes=expected,Untouched={c:after[c]['sha'] for c in CHANNELS if c!='003'},PublicPackageVerified=True,UpdaterVerified=True,InstalledOnRealComputers=False),indent=2),encoding='utf-8')
 print('PASS: Casa 10.444 published; public updater and package verified; other channels unchanged')
except Exception:
 if api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']==commit['sha']:
  restore=api(f'repos/{REPO}/git/commits',dict(message='Rollback Casa feed after failed publication verification',tree=old['tree']['sha'],parents=[commit['sha']]))
  move(restore['sha'])
 raise
