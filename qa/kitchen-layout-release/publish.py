"""Publish only the immutable Windows-validated 003 package; never build or fan out."""
import base64,hashlib,json,os,subprocess,time,urllib.request,zipfile
from pathlib import Path
repo='lealinfopdvnovo/leal-info-pdv-updates'
def api(path,data=None):
 args=['gh','api',path]
 if data is not None:
  Path('request.json').write_text(json.dumps(data),encoding='utf-8');args+=['--method','POST','--input','request.json']
 return json.loads(subprocess.check_output(args))
def feed(ch):
 path='version.json' if ch=='GLOBAL' else f'clients/{ch}/version.json'
 d=api(f'repos/{repo}/contents/{path}?ref=main')
 return dict(sha=d['sha'],manifest=json.loads(base64.b64decode(d['content']).decode('utf-8-sig')))
def download(url):
 with urllib.request.urlopen(urllib.request.Request(url+'?t='+str(time.time_ns()),headers={'User-Agent':'LEAL-INFO-003-Updater-Validation','Cache-Control':'no-cache'}),timeout=45) as r:return r.read()
def move_ref(sha):
 Path('request.json').write_text(json.dumps(dict(sha=sha,force=False)))
 subprocess.run(['gh','api',f'repos/{repo}/git/refs/heads/main','--method','PATCH','--input','request.json'],check=True,stdout=subprocess.DEVNULL)
e=json.loads(Path('validated/release-output/candidate.json').read_text(encoding='utf-8-sig'))
assert e['Channel']=='003' and e['Version']=='10.425' and e['SourceCommit']==os.environ['LAYOUT_SOURCE']
assert str(e['BuildRunId'])==os.environ['LAYOUT_RUN']
assert json.loads(Path('validated/layout-evidence/results.json').read_text())['success'] is True
proof=json.loads(Path('validated/release-output/layout-rollback-results.json').read_text(encoding='utf-8-sig'))
assert {p['scenario'] for p in proof}=={'success','interruption','bad-sha'} and all(p['pass'] and p['candidateSha256']==e['PackageSha256'] for p in proof)
assert os.environ['CUSTOMER_CONFIRMED_VERSION']=='10.422'
package=Path('validated/release-output')/e['Package']
assert hashlib.sha256(package.read_bytes()).hexdigest()==e['PackageSha256']
with zipfile.ZipFile(package) as z:
 assert len(z.namelist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 assert all(hashlib.sha256(z.read(n)).hexdigest()==e['TargetFiles'][n] for n in z.namelist())
channels=('GLOBAL','001','002','003','004')
before={c:feed(c) for c in channels}
expected=json.loads(Path('validated/layout-evidence/feeds-unchanged.json').read_text())
assert all(before[c]['sha']==expected[c] for c in channels),'Feed changed since Windows validation'
assert before['003']['manifest']['Version']=='10.424'
notes='Cozinha Premium conforme referência: mesas 3D, pedido integrado, envio, prévia de comanda, marcação de pronto e pedidos em andamento com cronômetros. Exclusivo 003. Banco, estoque, licença e configurações preservados.'
manifest=dict(Version='10.425',TargetClientCode='003',PackageUrl=e['Url'],Sha256=e['PackageSha256'],SourceCommit=e['SourceCommit'],Mandatory=False,Notes=notes,BuildRunId=e['BuildRunId'],ValidationRunId=os.environ['GITHUB_RUN_ID'],ProtectedUpdaterRequired=True,LastUserConfirmedProtectedVersion='10.422',PublishedBaselineVersion='10.424')
Path('release-notes.txt').write_text(notes)
# The existing repository token is used only after all Windows checks succeed.
view=subprocess.run(['gh','release','view',e['Tag'],'--repo',repo],capture_output=True)
if view.returncode:subprocess.run(['gh','release','create',e['Tag'],str(package),'--repo',repo,'--target','main','--title','PDV 003 V10.425 - Cozinha Premium','--notes-file','release-notes.txt','--latest=false'],check=True)
assert hashlib.sha256(download(e['Url'])).hexdigest()==e['PackageSha256'],'Public package differs'
head=api(f'repos/{repo}/git/ref/heads/main')['object']['sha'];old=api(f'repos/{repo}/git/commits/{head}')
assert all(feed(c)['sha']==before[c]['sha'] for c in channels),'Concurrent feed change'
tree=api(f'repos/{repo}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path='clients/003/version.json',mode='100644',type='blob',content=json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')]))
commit=api(f'repos/{repo}/git/commits',dict(message='003 V10.425: Cozinha layout validado Windows e rollback 10.424',tree=tree['sha'],parents=[head]))
move_ref(commit['sha'])
try:
 detail=api(f"repos/{repo}/commits/{commit['sha']}")
 assert {f['filename'] for f in detail['files']}=={'clients/003/version.json'}
 live=None
 for attempt in range(8):
  try:live=json.loads(download(f'https://raw.githubusercontent.com/{repo}/main/clients/003/version.json'))
  except (OSError,ValueError):pass
  if live==manifest:break
  time.sleep(3)
 assert live==manifest,'Updater URL not serving the new manifest'
 after={c:feed(c) for c in channels}
 assert all(after[c]['sha']==before[c]['sha'] for c in channels if c!='003')
 Path('published-003.json').write_text(json.dumps(dict(Published=True,Version='10.425',Client='003',Commit=commit['sha'],Manifest=manifest,OtherFeedsUnchanged=True,PublicPackageHashVerified=True,PublicManifestVerified=True,InstalledOnCustomerComputer=False),indent=2))
 print('PASS PUBLISHED ONLY 003 V10.425; exact public package and updater manifest verified; all other feeds unchanged')
except Exception:
 if api(f'repos/{repo}/git/ref/heads/main')['object']['sha']==commit['sha']:
  recovery=api(f'repos/{repo}/git/commits',dict(message='Restore feeds after failed 003 verification',tree=old['tree']['sha'],parents=[commit['sha']]))
  move_ref(recovery['sha'])
 raise
