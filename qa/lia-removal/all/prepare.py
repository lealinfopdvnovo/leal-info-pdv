"""Reproduce each existing distribution; never substitute Casa's executable."""
import hashlib, io, json, os, re, subprocess, urllib.request, zipfile
from pathlib import Path
C=os.environ['CHANNEL']; mode=os.sys.argv[1]; R=Path('candidate'); K=Path('controller')
FEEDS={'GLOBAL':'version.json',**{c:f'clients/{c}/version.json' for c in ('001','002','004')}}
PINS={'GLOBAL':('10.453','18935f8aff97e2d2516a8702bac8c220387e4b2e19403cb3651221e268398fdd'),'001':('10.456','71fc3d92a01910141031160517f1df02801b9a4cc1689966b487bdaed5b15c0f'),'002':('10.458','5836ea9c12aeecc2de0776a45e7dbd68c4c5f793a2c13dc79b4db9acd8d0284c'),'004':('10.457','13a526b90aaafe82b6088528ea20e411c82dccd6e54552f687165fe6e3451331')}
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Leal-LIA-removal'}),timeout=90) as r:return r.read()
def run(path):subprocess.run(['python',str(path)],check=True)
if mode=='baseline':
 feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/'+FEEDS[C]));assert (feed['Version'],feed['Sha256'])==PINS[C], 'Distribution advanced'
 raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256']
 z=zipfile.ZipFile(io.BytesIO(raw));assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 Path('published').mkdir()
 for n in z.namelist():Path('published',n).write_bytes(z.read(n))
 Path('current-feed.json').write_text(json.dumps(feed))
 if C in ('002','004'):run(K/'qa/login457/prepare.py')
 else:
  p=K/'qa/global-453/prepare.py';s=p.read_text();s=re.sub(r"PINS=\{.*?\}\nfeedpath",'PINS='+repr(PINS)+'\nfeedpath',s,flags=re.S);p.write_text(s);run(p)
 print('PASS exact current package integrity and pinned baseline recipe',C)
elif mode=='finish':
 if C in ('002','004'):run(K/'qa/login457/finish.py')
 if C=='002':
  p=K/'qa/gelamaq-delivery/prepare.py';s=p.read_text().replace("feed['Version']=='10.457' and feed['Sha256']=='f03f15bcb412086a5340fcf9f5c968c12fc6f82b93f282850965086bc4bd56f4'", "feed['Version']=='10.458' and feed['Sha256']=='"+PINS[C][1]+"'");p.write_text(s);run(p)
 if C=='001':
  # Preserve the exact installed protected worker and PET as embedded bytes.
  (R/'TransactionalUpdate.ps1').write_bytes(Path('native/TransactionalUpdateWorker').read_bytes())
  p=R/'UpdateManager.cs';s=p.read_text();needle='  return SelectManifest(clientCode,currentVersion,global,targeted);';assert needle in s
  patch='''  if(clientCode=="001" && Version.TryParse(currentVersion,out var bridgeVersion) && bridgeVersion>=new Version(10,456))
  {
   var lia=await TryLoadManifestAsync(http,"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/001/lia-mobile/version.json");
   if(lia!=null && ClientUpdateIdentity.ManifestMatches(clientCode,lia.TargetClientCode) && IsSafeClientPackage(lia,clientCode) && (targeted==null || IsNewer(lia.Version,targeted.Version)))targeted=lia;
  }
'''
  s=s.replace(needle,patch+needle);s=s.replace('CurrentVersion="10.453"','CurrentVersion="10.456"');p.write_text(s)
  p=R/'LealInfoPDV.csproj';p.write_text(p.read_text().replace('10.453.0','10.456.0'))
 # Use exact SDK and dependency framework versions of each current distribution.
 Path('global.json').write_bytes((R/'global.json').read_bytes())
 deps=json.loads(Path('published/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
 sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'))
 Path('sdk-version.txt').write_text(sdk)
 subprocess.run(['python',__file__,'version'],check=True)
 # Snapshot source BEFORE only the authorized button removal.
 subprocess.run(['python','removal/qa/lia-removal/windows_prepare.py','candidate','snapshot'],check=True)
elif mode=='version':
 # Source preservation was verified already; only release metadata changes next.
 p=R/'UpdateManager.cs';s=p.read_text();s=re.sub(r'(InstalledUpdateVersion=)' + r'"10\.\d+"',r'\1"10.463"',s) if C=='002' else re.sub(r'(CurrentVersion|InstalledUpdateVersion)="10\.\d+"',r'\1="10.463"',s);p.write_text(s)
 p=R/'LealInfoPDV.csproj';s=p.read_text();s=re.sub(r'<(' + ('FileVersion' if C=='002' else 'Version|AssemblyVersion|FileVersion') + ')>[^<]+</\1>',lambda m:f'<{m[1]}>10.463.'+('0' if m[1]=='Version' else '0.0')+f'</{m[1]}>',s);p.write_text(s)
else:raise ValueError(mode)
