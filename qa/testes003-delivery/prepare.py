import hashlib,json,os,urllib.request,zipfile,io
from pathlib import Path
r=Path('candidate');c=Path('delivery');assert os.environ['CHANNEL']=='CASA'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def get(url):
 with urllib.request.urlopen(url,timeout=90) as f:return f.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/003/global/version.json'))
assert feed['Version']=='10.457' and feed['Sha256']=='869871bdb0065cbc7fc00f2d3cba61194ecea32a9dbbaae0367c3ec717a65ed8'
data=get(feed['PackageUrl']);assert hashlib.sha256(data).hexdigest()==feed['Sha256']
Path('previous003.zip').write_bytes(data);Path('previous003').mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(data)) as z:
 for n in z.namelist():Path('previous003',n).write_bytes(z.read(n))
Path('baseline003.json').write_text(json.dumps(feed))
before={str(p.relative_to(r)):h(p) for p in r.rglob('*') if p.is_file() and not any(x in p.parts for x in ('.git','bin','obj'))}
(r/'Testes003Delivery.cs').write_bytes((c/'qa/testes003-delivery/Testes003Delivery.cs').read_bytes())
p=r/'MainForm.Notificador.cs';s=p.read_text();assert 'base.OnShown(e);' in s;s=s.replace('base.OnShown(e);','base.OnShown(e);\n        Testes003Delivery.Start();',1);p.write_text(s)
p=r/'UpdateManager.cs';s=p.read_text();assert 'CurrentVersion="10.457"' in s;s=s.replace('public const string CurrentVersion="10.457";','public const string CurrentVersion="10.457";\n internal const string InstalledUpdateVersion="10.458";',1)
s=s.replace('LoadManifestAsync(http,GetClientCode(),CurrentVersion)','LoadManifestAsync(http,GetClientCode(),InstalledUpdateVersion)').replace('!IsNewer(manifest.Version,CurrentVersion)','!IsNewer(manifest.Version,InstalledUpdateVersion)');p.write_text(s)
p=r/'TransactionalUpdate.cs';s=p.read_text();assert 'Version=UpdateManager.CurrentVersion' in s;p.write_text(s.replace('Version=UpdateManager.CurrentVersion','Version=UpdateManager.InstalledUpdateVersion',1))
p=r/'LealInfoPDV.csproj';s=p.read_text().replace('<FileVersion>10.457.0.0</FileVersion>','<FileVersion>10.458.0.0</FileVersion>');s=s.replace('</Project>',"<ItemGroup><EmbeddedResource Include=\"$(TestsAgent)\" LogicalName=\"Testes003.Agent\"/><EmbeddedResource Include=\"$(TestsAgentHash)\" LogicalName=\"Testes003.AgentHash\"/><EmbeddedResource Include=\"$(TestsPrevious)\" LogicalName=\"Testes003.Previous\"/></ItemGroup></Project>");p.write_text(s)
changed=[n for n,v in before.items() if h(r/n)!=v];assert set(changed)=={'MainForm.Notificador.cs','UpdateManager.cs','TransactionalUpdate.cs','LealInfoPDV.csproj'},changed
Path('scope003.json').write_text(json.dumps(dict(Client='003',GlobalVersion='10.457',DeliveryVersion='10.458',Changed=changed,Protected={n:v for n,v in before.items() if n not in changed},Published=False),indent=2))
q=r/'qa/testes003-delivery';q.mkdir(parents=True,exist_ok=True)
for n in ['Tests.csproj','Program.cs','rollback.ps1']:(q/n).write_bytes((c/'qa/testes003-delivery'/n).read_bytes())
