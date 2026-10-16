"""Independent channel publication. Builds on each channel's own source and dependencies.
GLOBAL fan-out reserves compatible individual versions for legacy updaters atomically.
No access to a store's database, activation, configuration, or remote desktop.
"""
import base64,hashlib,json,os,re,subprocess,sys,time,zipfile
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.error import HTTPError
SOURCE='lealinfopdvnovo/leal-info-pdv'
UPDATES='lealinfopdvnovo/leal-info-pdv-updates'
CHANNELS=('GLOBAL','001','002','003','004')
FILES={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
ROOT=Path(__file__).resolve().parents[1]
def check(ok,why):
 if not ok:raise RuntimeError(why)
def run(*args,cwd=None):
 p=subprocess.run(list(map(str,args)),cwd=cwd,capture_output=True,text=True,encoding='utf-8')
 check(p.returncode==0,'Command failed: '+str(args[:3])+' '+p.stderr[-1500:]+p.stdout[-1500:])
 return p.stdout

def api(path,data=None,method=None,missing=False):
 args=['gh','api',path]
 if data is not None:
  f=Path('api-request.json');f.write_text(json.dumps(data),encoding='utf-8');args+=['--method',method or 'POST','--input',str(f)]
 elif method:args+=['--method',method]
 p=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if p.returncode and missing and 'HTTP 404' in p.stderr:return None
 check(p.returncode==0,'GitHub API failed: '+p.stderr[:800])
 return json.loads(p.stdout)
def write(path,obj):
 p=Path(path);p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def digest(path):
 with open(path,'rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def ver(v):
 check(bool(re.fullmatch(r'\d+\.\d+(?:\.\d+)?',v)),'Invalid version '+str(v))
 t=tuple(map(int,v.split('.')));return t+(0,)*(3-len(t))
def feedpath(ch):return 'version.json' if ch=='GLOBAL' else f'clients/{ch}/version.json'
def readfeed(ch):
 d=api(f'repos/{UPDATES}/contents/{feedpath(ch)}?ref=main',missing=True)
 if d is None:return {'sha':None,'manifest':None}
 return {'sha':d['sha'],'manifest':json.loads(base64.b64decode(d['content']).decode('utf-8-sig'))}
def text_at(ref,path):
 d=api(f'repos/{SOURCE}/contents/{path}?ref={ref}')
 return base64.b64decode(d['content']).decode('utf-8-sig')
def source_version(ref):
 s=text_at(ref,'UpdateManager.cs');m=re.search(r'CurrentVersion\s*=\s*"([\d.]+)"',s)
 check(m is not None,'No source version');return m[1]
def plan():
 channel=os.environ.get('CHANNEL','ALL');check(channel in (*CHANNELS,'ALL'),'Unknown channel')
 feeds={ch:readfeed(ch) for ch in CHANNELS};bootstrap=json.loads((ROOT/'distribution/bootstrap.json').read_text(encoding='utf-8-sig'))
 entries=[]
 if channel=='ALL':
  # One-time migration: use the exact reviewed distribution-only commits.
  for ch in CHANNELS:
   c=bootstrap[ch];check(feeds[ch]['sha']==c['ExpectedFeedSha'],'Bootstrap baseline changed '+ch)
   entries.append(dict(Channel=ch,SourceCommit=c['SourceCommit'],Version='10.407',BaselineSource=c['BaselineSource'],Baseline=c['BaselineManifest'],Scope='distribution-only'))
 else:
  ref=os.environ.get('SOURCE_REF') or ('channel/global' if channel=='GLOBAL' else 'client/'+channel)
  commit=api(f'repos/{SOURCE}/commits/{ref}')['sha'];v=source_version(commit)
  previous=feeds[channel]['manifest'];check(previous is not None,'No installed baseline')
  check(ver(v)>ver(previous['Version']),'A new release needs a newer version')
  entries.append(dict(Channel=channel,SourceCommit=commit,Version=v,BaselineSource=previous['SourceCommit'],Baseline=previous,Scope='channel-release'))
  if channel=='GLOBAL':
   for ch in CHANNELS[1:]:
    old=feeds[ch]['manifest'];check(old is not None,'Independent feed missing '+ch)
    if ver(old['Version'])<ver(v):
     entries.append(dict(Channel=ch,SourceCommit=old['SourceCommit'],Version=v,BaselineSource=old['SourceCommit'],Baseline=old,Scope='version-only'))
  else:check(ver(v)>=ver(feeds['GLOBAL']['manifest']['Version']),'Legacy-compatible client version must not be below GLOBAL')
 for e in entries:
  e['FeedPath']=feedpath(e['Channel']);e['ExpectedFeedSha']=feeds[e['Channel']]['sha']
  ch=e['Channel'];v=e['Version'];e['Tag']=('v' if ch=='GLOBAL' else f'client-{ch}-v')+v
  e['Package']=('LEAL_INFO_PDV_' if ch=='GLOBAL' else f'LEAL_INFO_PDV_{ch}_')+'V'+v.replace('.','_')+'_UPDATE.zip'
  e['Url']=f"https://github.com/{UPDATES}/releases/download/{e['Tag']}/{e['Package']}"
  check(ver(v)>ver(e['Baseline']['Version']),'Version did not advance '+ch)
 result={'Entries':entries,'Before':feeds,'RunId':os.environ['GITHUB_RUN_ID'],'ControllerCommit':run('git','rev-parse','HEAD').strip()}
 write('release-plan.json',result)
 with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as f:f.write('matrix='+json.dumps({'include':[{'channel':e['Channel']} for e in entries]})+'\n');f.write('has-global='+str(any(e['Channel']=='GLOBAL' for e in entries)).lower()+'\n')
 print('PASS independent source plan: '+','.join(e['Channel'] for e in entries))
def download(url,path):
 for attempt in range(5):
  try:
   req=Request(url,headers={'User-Agent':'LEAL-INFO-ChannelPublisher','Cache-Control':'no-cache, no-store'})
   with urlopen(req,timeout=45) as r,open(path,'wb') as out:
    while chunk:=r.read(1024*1024):out.write(chunk)
   return
  except (HTTPError,OSError):
   if attempt==4:raise
   time.sleep(2)
def entry(ch):return next(e for e in json.loads(Path('release-plan.json').read_text(encoding='utf-8-sig'))['Entries'] if e['Channel']==ch)
def normalize_deps(path):
 obj=json.loads(Path(path).read_text(encoding='utf-8-sig'))
 for bag in (obj['libraries'],*obj['targets'].values()):
  for k in list(bag):
   if k.startswith(('LealInfoPDV/','LealInfoPDV.ChannelLauncher/')):del bag[k]
 return obj
def prepare(ch):
 check(run('git','rev-parse','HEAD').strip()==json.loads(Path('release-plan.json').read_text(encoding='utf-8-sig'))['ControllerCommit'],'Controller changed after planning')
 e=entry(ch);run('git','clone','--no-checkout','https://github.com/'+SOURCE+'.git','candidate');run('git','checkout',e['SourceCommit'],cwd='candidate')
 src=Path('candidate');run('git','fetch','origin',e['BaselineSource'],cwd=src)
 changed=run('git','diff','--name-only',e['BaselineSource'],e['SourceCommit'],cwd=src).splitlines()
 if e['Scope']=='distribution-only':
  allowed={'UpdateManager.cs','LealInfoPDV.csproj','qa/channel-isolation/Program.cs','qa/channel-isolation/LealInfoPDV.ChannelTests.csproj','.github/workflows/atualizacao_cliente.yml'}
  check(set(changed)<=allowed,'Non-distribution change '+str(set(changed)-allowed))
  old=run('git','show',e['BaselineSource']+':UpdateManager.cs',cwd=src)
  new=(src/'UpdateManager.cs').read_text(encoding='utf-8-sig')
  # Actual apply/copy/restart mechanism is unchanged; only channel selection changes.
  marker=' private static async Task DownloadAndApplyAsync('
  check(old[old.index(marker):].replace('\r\n','\n')==new[new.index(marker):].replace('\r\n','\n'),'Installer/apply path changed')
  oldproj=run('git','show',e['BaselineSource']+':LealInfoPDV.csproj',cwd=src)
  strip=lambda s:re.sub(r'(<(?:Version|AssemblyVersion|FileVersion)>)10\.\d+',r'\g<1>10.VERSION',s).replace('\r\n','\n')
  check(strip(oldproj)==strip((src/'LealInfoPDV.csproj').read_text(encoding='utf-8-sig')),'Dependencies/build settings changed')
  print('PASS all business/database/license/UI files byte-identical to channel baseline '+ch)
 else:
  # Never transplant a different client's application tree.
  run('git','merge-base','--is-ancestor',e['BaselineSource'],e['SourceCommit'],cwd=src)
 manager=src/'UpdateManager.cs';s=manager.read_text(encoding='utf-8-sig')
 check(f'DistributionChannel="{ch}"' in s,'Wrong compiled distribution identity')
 s=re.sub(r'CurrentVersion\s*=\s*"[\d.]+"',f'CurrentVersion="{e["Version"]}"',s);manager.write_text(s,encoding='utf-8')
 project=src/'LealInfoPDV.csproj';s=project.read_text(encoding='utf-8-sig');s=re.sub(r'(<(?:Version|AssemblyVersion|FileVersion)>)10\.\d+',lambda m:m[1]+e['Version'],s);project.write_text(s,encoding='utf-8')
 # Tests come from the reviewed controller; no store or activated profile is used.
 import shutil
 target=src/'qa/channel-isolation';target.mkdir(parents=True,exist_ok=True)
 for p in (ROOT/'qa/channel-isolation').iterdir():shutil.copyfile(p,target/p.name)
 download(e['Baseline']['PackageUrl'],'previous.zip');check(digest('previous.zip')==e['Baseline']['Sha256'],'Baseline package hash changed')
 with zipfile.ZipFile('previous.zip') as z:
  z.extractall('previous')
  if not Path('previous/LealInfoPDV.dll').exists():
   check(ch=='GLOBAL' and e['Baseline'].get('Delivery')=='preserve-existing-core','Unexpected previous payload')
   Path('previous/LealInfoPDV.dll').write_bytes(z.read('LealInfoPDV.Core.GLOBAL.dll'))
   Path('previous/LealInfoPDV.deps.json').write_bytes(z.read('LealInfoPDV.ChannelLauncher.deps.json'))
 deps=json.loads(Path('previous/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
 sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'))
 Path('sdk-version.txt').write_text(sdk)
 write('build-entry.json',e)
def package(ch):
 e=entry(ch);check(normalize_deps('previous/LealInfoPDV.deps.json')==normalize_deps('candidate-publish/LealInfoPDV.deps.json'),'Incremental runtime dependencies changed')
 folder=Path('release-output');folder.mkdir(exist_ok=True)
 prev={};target={}
 with zipfile.ZipFile(folder/e['Package'],'w',zipfile.ZIP_DEFLATED) as z:
  for name in sorted(FILES):
   prev[name]=digest(Path('previous')/name);target[name]=digest(Path('candidate-publish')/name);z.write(Path('candidate-publish')/name,name)
 e.update(PackageSha256=digest(folder/e['Package']),PreviousFiles=prev,TargetFiles=target,BuildRunId=os.environ['GITHUB_RUN_ID'])
 write(folder/'candidate.json',e)
 print('PASS tested three-file incremental package '+ch)
def publish():
 plan=json.loads(Path('release-plan.json').read_text(encoding='utf-8-sig'));check(str(plan['RunId'])==os.environ['GITHUB_RUN_ID'],'Wrong workflow plan')
 publisher_revision=run('git','rev-parse','HEAD').strip()
 repair=None
 if publisher_revision!=plan['ControllerCommit']:
  repair=json.loads((ROOT/'distribution/publication-repair.json').read_text(encoding='utf-8'))
  check(str(repair['RunId'])==os.environ['GITHUB_RUN_ID'] and repair['BuildController']==plan['ControllerCommit'],'Controller differs from Windows validation')
 before={ch:readfeed(ch) for ch in CHANNELS};check(all(before[ch]['sha']==plan['Before'][ch]['sha'] for ch in CHANNELS),'A channel changed during build; no feed written')
 entries=[];manifests={}
 for e in plan['Entries']:
  ch=e['Channel'];folder=Path('artifacts')/('candidate-'+ch)
  evidence=json.loads((folder/'candidate.json').read_text(encoding='utf-8-sig'))
  if repair:
   check(evidence['PackageSha256']==repair['Packages'][ch],'Immutable tested bytes differ from repair pins')
   # Old Windows Python decoded only the display notes using CP1252 twice.
   # Recover that text exactly; all identity, version, source and binary hashes remain mandatory.
   notes=evidence['Baseline'].get('Notes','')
   expected=e['Baseline'].get('Notes','')
   if notes!=expected:
    for _ in range(2):notes=notes.encode('cp1252').decode('utf-8')
    check(notes==expected,'Metadata repair did not reproduce the approved UTF-8 notes')
    evidence['Baseline']['Notes']=notes
  check(all(evidence[k]==v for k,v in e.items()),'Tested candidate identity differs: '+ch)
  check(str(evidence['BuildRunId'])==os.environ['GITHUB_RUN_ID'],'Wrong tested run')
  check(evidence['TestsPassed'] is True and evidence['PayloadPolicyPassed'] is True,'Required Windows checks failed')
  package=folder/e['Package'];check(digest(package)==evidence['PackageSha256'],'Tested package bytes changed')
  with zipfile.ZipFile(package) as z:
   allowed=FILES
   if ch=='GLOBAL':
    check(evidence.get('LegacySafeGlobal') is True,'GLOBAL must preserve the existing business DLL, including legacy feed outages')
    allowed={'LealInfoPDV.exe','LealInfoPDV.ChannelLauncher.dll','LealInfoPDV.ChannelLauncher.deps.json','LealInfoPDV.ChannelLauncher.runtimeconfig.json',*(f'LealInfoPDV.Core.{c}.dll' for c in CHANNELS)}
   check(set(z.namelist())==allowed and len(z.namelist())==len(allowed),'Unexpected payload')
   for name in allowed:check(hashlib.sha256(z.read(name)).hexdigest()==evidence['TargetFiles'][name],'Package member changed')
  m={'Version':e['Version'],'PackageUrl':e['Url'],'Sha256':evidence['PackageSha256'],'SourceCommit':e['SourceCommit'],'Mandatory':False,'Notes':'Correção da distribuição: canais independentes, sem substituição de personalizações pelo GLOBAL. Banco, licença, configurações e funções preservados. Atualização pelo próprio PDV.','DistributionRevision':plan['ControllerCommit'],'BuildRunId':os.environ['GITHUB_RUN_ID']}
  if ch!='GLOBAL':m['TargetClientCode']=ch
  else:
   m['Editions']=['STANDARD','PLUS','PRO'];m['Delivery']='preserve-existing-core';m['InstalledCoreChannels']=evidence['InstalledCoreChannels']
  manifests[ch]=m;entries.append((e,package,m))
 # Legacy installations choose highest version: every personalized feed must be >= GLOBAL.
 final={ch:manifests.get(ch) or before[ch]['manifest'] for ch in CHANNELS}
 check(all(final[ch] and ver(final[ch]['Version'])>=ver(final['GLOBAL']['Version']) for ch in CHANNELS[1:]),'Legacy version fence failed')
 for e,package,m in entries:
  existing=api(f"repos/{UPDATES}/releases/tags/{e['Tag']}",missing=True)
  if existing is None:run('gh','release','create',e['Tag'],package,'--repo',UPDATES,'--target','main','--title',f"PDV {e['Channel']} V{e['Version']} - canais independentes",'--notes',m['Notes'],'--latest=false')
  remote=Path('remote-'+e['Channel']+'.zip');download(e['Url'],remote);check(digest(remote)==m['Sha256'],'Remote package differs; no feed written')
 head=api(f'repos/{UPDATES}/git/ref/heads/main')['object']['sha'];commit=api(f'repos/{UPDATES}/git/commits/{head}')
 # Re-read after uploading packages to detect another publisher before the lease.
 check(all(readfeed(ch)['sha']==before[ch]['sha'] for ch in CHANNELS),'Feed changed before atomic commit')
 tree=api(f'repos/{UPDATES}/git/trees',{'base_tree':commit['tree']['sha'],'tree':[{'path':feedpath(ch),'mode':'100644','type':'blob','content':json.dumps(m,ensure_ascii=False,indent=2)+'\n'} for ch,m in manifests.items()]})
 new=api(f'repos/{UPDATES}/git/commits',{'message':'Atualizacoes independentes validadas Windows: '+','.join(manifests),'tree':tree['sha'],'parents':[head]})
 api(f'repos/{UPDATES}/git/refs/heads/main',{'sha':new['sha'],'force':False},method='PATCH')
 try:
  detail=api(f"repos/{UPDATES}/commits/{new['sha']}");check({x['filename'] for x in detail['files']}=={feedpath(ch) for ch in manifests},'Unexpected repository changes')
  for ch,m in manifests.items():
   live=None
   for _ in range(8):
    try:
     with urlopen(Request(f'https://raw.githubusercontent.com/{UPDATES}/main/{feedpath(ch)}?t={time.time_ns()}',headers={'Cache-Control':'no-cache'}),timeout=20) as r:live=json.load(r)
     if live==m:break
    except (OSError,ValueError):pass
    time.sleep(3)
   check(live==m,'Updater URL not serving exact manifest '+ch)
  after={ch:readfeed(ch) for ch in CHANNELS}
  check(all(after[ch]['sha']==before[ch]['sha'] for ch in CHANNELS if ch not in manifests),'Unselected channel changed')
  write('publication-evidence/published.json',{'RunId':os.environ['GITHUB_RUN_ID'],'Commit':new['sha'],'Manifests':manifests,'Untouched':{ch:after[ch]['sha'] for ch in CHANNELS if ch not in manifests},'VerifiedPublicAssets':True,'VerifiedPublicManifests':True,'LegacyFence':True,'BuildControllerCommit':plan['ControllerCommit'],'PublicationControllerCommit':publisher_revision,'MetadataEncodingRepair':bool(repair)})
  print('PASS PUBLISHED: '+','.join(manifests)+'; exact public URLs/SHA-256; atomic feeds; legacy priority preserved')
 except Exception:
  # Restore only if our atomic commit is still the head; never overwrite another publisher.
  current=api(f'repos/{UPDATES}/git/ref/heads/main')['object']['sha']
  if current==new['sha']:
   recovery=api(f'repos/{UPDATES}/git/commits',{'message':'Restore feeds after failed live verification','tree':commit['tree']['sha'],'parents':[current]})
   api(f'repos/{UPDATES}/git/refs/heads/main',{'sha':recovery['sha'],'force':False},method='PATCH')
  raise
if __name__=='__main__':
 action=sys.argv[1]
 if action=='plan':plan()
 elif action=='prepare':prepare(sys.argv[2])
 elif action=='package':package(sys.argv[2])
 elif action=='publish':publish()
 elif action=='passed':
  p=Path('release-output/candidate.json');e=json.loads(p.read_text(encoding='utf-8-sig'));e.update(TestsPassed=True,PayloadPolicyPassed=True);write(p,e)
 else:raise ValueError(action)
