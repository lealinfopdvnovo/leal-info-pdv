"""Read-only comparison of exact published dependencies, never executes packages."""
import hashlib,json,urllib.request,zipfile,io
from pathlib import Path

def get(url):
 return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-INFO-isolated-validation'}),timeout=90).read()
def normalized(raw):
 data=json.loads(raw.decode('utf-8-sig'))
 for bag in (data['libraries'],*data['targets'].values()):
  for key in list(bag):
   if key.startswith('LealInfoPDV/'):del bag[key]
 return data
candidate=normalized(Path('publish/LealInfoPDV.deps.json').read_bytes())
results=[]
for identity in ('001','002','003','004'):
 feed_url=f'https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/{identity}/version.json'
 feed=json.loads(get(feed_url).decode('utf-8-sig'))
 assert feed['TargetClientCode']==identity
 package=get(feed['PackageUrl'])
 digest=hashlib.sha256(package).hexdigest()
 assert digest==feed['Sha256'].lower(),f'Published package integrity failed {identity}'
 archive=zipfile.ZipFile(io.BytesIO(package))
 deps=[n for n in archive.namelist() if n.replace('\\','/').split('/')[-1]=='LealInfoPDV.deps.json']
 assert len(deps)==1,(identity,deps)
 old=normalized(archive.read(deps[0]))
 equal=old==candidate
 results.append(dict(Identity=identity,PublishedVersion=feed['Version'],Source=feed.get('SourceCommit'),PackageSha256=digest,ExactDependencyCompatibility=equal))
 print('PASS' if equal else 'BLOCKED',identity,'published package SHA verified; dependency equality:',equal,flush=True)
Path('evidence/dependencies.json').write_text(json.dumps(results,indent=2))
assert all(r['ExactDependencyCompatibility'] for r in results),'Published dependencies differ; do not approve migration'
