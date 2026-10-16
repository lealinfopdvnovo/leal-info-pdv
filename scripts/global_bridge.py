"""Global packages never overwrite LealInfoPDV.dll: route to each tested client core."""
import hashlib,json,os,shutil,subprocess,sys,zipfile,time,base64
from pathlib import Path
from distribute_channels import api,readfeed,run,write,digest,download,check,normalize_deps,CHANNELS,UPDATES,SOURCE
ROOT=Path(__file__).resolve().parents[1]
PACKAGE='LEAL_INFO_PDV_V10_407_CHANNELS_UPDATE.zip'
TAG='v10.407-channels'
URL=f'https://github.com/{UPDATES}/releases/download/{TAG}/{PACKAGE}'
REPAIR=json.loads((ROOT/'distribution/publication-repair.json').read_text(encoding='utf-8'))
def prepare():
 installed={};proofs={};out=Path('bridge-files');out.mkdir(exist_ok=True)
 for ch in CHANNELS:
  folder=Path('artifacts')/('candidate-'+ch);p=json.loads((folder/'candidate.json').read_text(encoding='utf-8-sig'))
  check(p['PackageSha256']==REPAIR['Packages'][ch] and p['TestsPassed'] and p['PayloadPolicyPassed'],'Unapproved core '+ch)
  zpath=folder/p['Package'];check(digest(zpath)==p['PackageSha256'],'Core package hash changed')
  name=f'LealInfoPDV.Core.{ch}.dll'
  with zipfile.ZipFile(zpath) as z:
   b=z.read('LealInfoPDV.dll');check(hashlib.sha256(b).hexdigest()==p['TargetFiles']['LealInfoPDV.dll'],'Core hash differs')
   (out/name).write_bytes(b)
   if ch=='GLOBAL':Path('original.deps.json').write_bytes(z.read('LealInfoPDV.deps.json'))
  profile={'Channel':ch,'File':name,'Sha256':p['TargetFiles']['LealInfoPDV.dll']}
  # Generic GLOBAL baseline is also 004's previous baseline: keep the generic mapping.
  for h in [p['PreviousFiles']['LealInfoPDV.dll'],p['TargetFiles']['LealInfoPDV.dll']]:installed.setdefault(h,profile)
  proofs[ch]=p
 write(ROOT/'qa/channel-launcher/Map.json',{'Installed':installed})
 write('bridge-proofs.json',proofs)
 deps=json.loads(Path('original.deps.json').read_text(encoding='utf-8-sig'))
 runtime=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.NETCore.App.Runtime.win-x64/'))
 Path('runtime-version.txt').write_text(runtime)
 for ch,p in proofs.items():
  baseline=p['Baseline'];download(baseline['PackageUrl'],f'baseline-{ch}.zip');check(digest(f'baseline-{ch}.zip')==baseline['Sha256'],'Wrong baseline '+ch)
 print('PASS all five exact approved core hashes and dependencies; no business source rebuilt')
def package():
 # Identical full runtime/package dependency bags: only the bootstrap assembly changes.
 a=normalize_deps('original.deps.json')
 b=json.loads(Path('launcher-publish/LealInfoPDV.ChannelLauncher.deps.json').read_text(encoding='utf-8-sig'))
 for bag in (b['libraries'],*b['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV.ChannelLauncher/'):del bag[k]
 check(a==b,'Launcher dependencies differ from installed program')
 out=Path('bridge-files')
 for name in ['LealInfoPDV.ChannelLauncher.dll','LealInfoPDV.ChannelLauncher.deps.json','LealInfoPDV.ChannelLauncher.runtimeconfig.json']:
  shutil.copyfile(Path('launcher-publish')/name,out/name)
 shutil.copyfile('launcher-publish/LealInfoPDV.ChannelLauncher.exe',out/'LealInfoPDV.exe')
 check(not (out/'LealInfoPDV.dll').exists(),'Global must never replace the installed business DLL')
 with zipfile.ZipFile(PACKAGE,'w',zipfile.ZIP_DEFLATED) as z:
  for p in out.iterdir():z.write(p,p.name)
 write('bridge-candidate.json',{'Version':'10.407','Package':PACKAGE,'Sha256':digest(PACKAGE),'SourceRun':REPAIR['RunId'],'Files':{p.name:digest(p) for p in out.iterdir()}})
def test():
 proofs=json.loads(Path('bridge-proofs.json').read_text(encoding='utf-8-sig'));records=[]
 root=Path.cwd()
 for ch,p in proofs.items():
  folder=Path('fixtures')/ch;shutil.copytree('launcher-publish',folder,dirs_exist_ok=True)
  # A legacy installation has its own DLL/deps. Apply exactly the existing PDV copy block.
  with zipfile.ZipFile(f'baseline-{ch}.zip') as z:z.extractall(folder)
  sentinels=['lealinfo.db','network.license','network.config','users.protected','client-code.protected','Dados/empresa.json','Assets/logo-customizado-cliente.png']
  for name in sentinels:
   f=folder/name;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(('PRESERVE '+name).encode())
  previous={name:digest(folder/name) for name in ['LealInfoPDV.dll','LealInfoPDV.deps.json',*sentinels]}
  for f in Path('bridge-files').iterdir():shutil.copyfile(f,folder/f.name)
  check(all(digest(folder/name)==sha for name,sha in previous.items()),'A legacy store file was overwritten '+ch)
  result=json.loads(run(str((folder/'LealInfoPDV.exe').resolve()),'--distribution-probe',str((root/PACKAGE).resolve())))
  expected='GLOBAL' if ch=='004' else ch
  check(result['Decision']==expected and result['Channel']==expected and result['Version']=='10.407' and result['EntryPoint']=='Main' and result['STA']=='STA' and result['HasMain'],'Wrong actual native startup '+ch)
  if ch=='002':check(result['HasServiceNote'] and result['HasParts'],'002 exclusive modules missing')
  check(all(digest(folder/name)==sha for name,sha in previous.items()),'Probe modified local files '+ch)
  records.append({'Scenario':'legacy-'+ch,'Result':'PASS','Probe':result,'Preserved':previous})
 # Simulate an unknown customized binary: preserve and run that original, never substitute GLOBAL.
 folder=Path('fixtures/002');original=folder/'LealInfoPDV.dll';original.write_bytes(original.read_bytes()+b'UNKNOWN_CUSTOMIZATION')
 h=digest(original);r=json.loads(run(str((folder/'LealInfoPDV.exe').resolve()),'--distribution-probe'))
 check(r['Decision']=='ORIGINAL' and r['HasServiceNote'] and r['HasParts'] and digest(original)==h,'Unknown custom build was replaced')
 records.append({'Scenario':'unknown-customized-binary','Result':'PASS','Probe':r})
 # Corrupt/missing profile also preserves the previous application rather than changing channels.
 with zipfile.ZipFile('baseline-002.zip') as z:original.write_bytes(z.read('LealInfoPDV.dll'))
 core=folder/'LealInfoPDV.Core.002.dll';core.write_bytes(core.read_bytes()+b'TAMPER')
 r=json.loads(run(str((folder/'LealInfoPDV.exe').resolve()),'--distribution-probe'))
 check(r['Decision']=='ORIGINAL' and r['HasServiceNote'] and r['HasParts'],'Corrupt core removed client functionality')
 records.append({'Scenario':'corrupt-profile-preserves-original','Result':'PASS','Probe':r})
 core.unlink();r=json.loads(run(str((folder/'LealInfoPDV.exe').resolve()),'--distribution-probe'))
 check(r['Decision']=='ORIGINAL' and r['HasParts'],'Missing profile changed channel')
 records.append({'Scenario':'missing-profile-preserves-original','Result':'PASS','Probe':r})
 write('bridge-tests.json',records)
 print('PASS real native Windows startup, actual CLR load and legacy payload policy; all client cores; unknown/corrupt/missing fallback preserves 002 modules and existing files')
def publish():
 tests=json.loads(Path('bridge-tests.json').read_text(encoding='utf-8-sig'));check(len(tests)==8 and all(x['Result']=='PASS' for x in tests),'Windows bridge checks failed')
 proof=json.loads(Path('bridge-candidate.json').read_text(encoding='utf-8-sig'));check(digest(PACKAGE)==proof['Sha256'],'Package differs from tested bytes')
 with zipfile.ZipFile(PACKAGE) as z:
  check('LealInfoPDV.dll' not in z.namelist() and set(z.namelist())==set(proof['Files']),'Global could overwrite original application')
  for name,h in proof['Files'].items():check(hashlib.sha256(z.read(name)).hexdigest()==h,'Program file differs')
 protected={ch:readfeed(ch) for ch in CHANNELS[1:]};before=readfeed('GLOBAL');check(before['manifest']['Version']=='10.385','Global baseline changed')
 if api(f'repos/{UPDATES}/releases/tags/{TAG}',missing=True) is None:run('gh','release','create',TAG,PACKAGE,'--repo',UPDATES,'--target','main','--title','PDV GLOBAL 10.407 - compatibilidade com canais personalizados','--notes','Distribuicao global segura para versoes antigas. Preserva o DLL original e seleciona a base propria de cada cliente.','--latest=false')
 download(URL,'remote-global.zip');check(digest('remote-global.zip')==proof['Sha256'],'Public package hash differs')
 bootstrap=json.loads((ROOT/'distribution/bootstrap.json').read_text(encoding='utf-8'))
 m={'Version':'10.407','PackageUrl':URL,'Sha256':proof['Sha256'],'SourceCommit':bootstrap['GLOBAL']['SourceCommit'],'Mandatory':False,'Editions':['STANDARD','PLUS','PRO'],'Notes':'Canais de atualizacao independentes. Preserva o programa personalizado, banco, licenca e configuracoes. Atualize pelo proprio PDV.','Delivery':'preserve-existing-core','DistributionRevision':os.environ['GITHUB_SHA'],'BuildRunId':os.environ['GITHUB_RUN_ID']}
 raw=base64.b64encode((json.dumps(m,ensure_ascii=False,indent=2)+'\n').encode()).decode()
 result=api(f'repos/{UPDATES}/contents/version.json',{'message':'GLOBAL 10.407: preservar DLL instalado e bases exclusivas; ponte validada no Windows','content':raw,'sha':before['sha'],'branch':'main'},method='PUT')
 try:
  live=None
  from urllib.request import urlopen,Request
  for _ in range(8):
   with urlopen(Request(f'https://raw.githubusercontent.com/{UPDATES}/main/version.json?t={time.time_ns()}',headers={'Cache-Control':'no-cache'}),timeout=20) as r:live=json.load(r)
   if live==m:break
   time.sleep(3)
  check(live==m,'Public updater manifest differs')
  check(all(readfeed(ch)['sha']==protected[ch]['sha'] for ch in protected),'An individual feed changed')
  write('bridge-publication.json',{'Manifest':m,'RunId':os.environ['GITHUB_RUN_ID'],'PublicSha256Verified':True,'NativeWindowsTests':tests,'ProtectedIndividualFeeds':{ch:p['sha'] for ch,p in protected.items()},'Commit':result['commit']['sha']})
  print('PASS PUBLISHED GLOBAL 10.407: public asset/SHA/feed verified; 001/002/003/004 manifests intact; original business DLL never in global package')
 except Exception:
  now=readfeed('GLOBAL')
  if now['sha']==result['content']['sha']:
   raw=base64.b64encode((json.dumps(before['manifest'],ensure_ascii=False,indent=2)+'\n').encode()).decode();api(f'repos/{UPDATES}/contents/version.json',{'message':'Restore GLOBAL after failed verification','content':raw,'sha':now['sha'],'branch':'main'},method='PUT')
  raise
if __name__=='__main__':{'prepare':prepare,'package':package,'test':test,'publish':publish}[sys.argv[1]]()
