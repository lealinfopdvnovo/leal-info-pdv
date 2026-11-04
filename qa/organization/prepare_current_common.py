"""Reconstruct the CURRENT common distribution before adding component support."""
from pathlib import Path
import hashlib, io, json, os, re, runpy, sys, urllib.request, zipfile
root=Path('candidate')
recipe=Path('recipe')
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-component-migration-validation'}),timeout=90) as response:return response.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json'))
assert feed['Version']=='10.463' and feed['Sha256']=='4e9206eaf98cbdbdec01abfa30b4aa200aa0caebae65cb997fe38cc3b3656fcd', 'Published common baseline advanced: review new source'
assert feed['SourceBase']=='98915ae5a9eb02825d77f74bb179e0ce27d65bd9'
assert feed['Component']=='GLOBAL'
raw=get(feed['PackageUrl'])
assert hashlib.sha256(raw).hexdigest()==feed['Sha256']
published=Path('published-current');published.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(raw)) as z:
 assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for name in z.namelist():(published/name).write_bytes(z.read(name))
protected=['Auth.cs','Database.cs','Licensing/InstallationLicense.cs']
before={name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in protected}
# Reuse the exact approved 10.453 feature recipe behind the distributed 10.463 common build.
source=(recipe/'qa/global-453/prepare.py').read_text(encoding='utf-8-sig')
body=source[source.index('# Apply reviewed features only;'):source.index("allowed={'MainForm.cs'")]
namespace=dict(root=root,channel='GLOBAL',assets=recipe/'qa/global-453/assets',Path=Path,re=re,subprocess=__import__('subprocess'),json=json)
exec(compile(body,'approved-common-features','exec'),namespace)
sys.path.insert(0,str((recipe/'qa/lia-removal').resolve()))
from remove_button import transform, LISTENER
p=root/'MainForm.cs'
s=p.read_text(encoding='utf-8-sig')
if 'btnAssistenteAI' in s:
 s,removed=transform(s)
else:
 assert not any(token in s for token in ('licAiButton','licStatusCts','licPulseTimer','EncerrarLicAiComPdv'))
 assert s.count(LISTENER)==1
 s=s.replace(LISTENER,'',1)
p.write_text(s,encoding='utf-8')
# Metadata matches the CURRENT baseline; no downgrade to 10.445 or 10.453.
p=root/'UpdateManager.cs';s=p.read_text(encoding='utf-8-sig')
s=re.sub(r'CurrentVersion="[^"]+"','CurrentVersion="10.463"',s);p.write_text(s,encoding='utf-8')
p=root/'LealInfoPDV.csproj';s=p.read_text(encoding='utf-8-sig')
s=re.sub(r'<(Version|AssemblyVersion|FileVersion)>[^<]+</\1>',lambda m:f'<{m[1]}>10.463.'+('0' if m[1]=='Version' else '0.0')+f'</{m[1]}>',s)
p.write_text(s,encoding='utf-8')
assert all(hashlib.sha256((root/name).read_bytes()).hexdigest()==digest for name,digest in before.items())
assert hashlib.sha256((root/'TransactionalUpdate.ps1').read_bytes()).hexdigest()=='30297a15f4eaa69a4f10853a4accc764e54506db74a2256110ba1656d86d386d'
Path('pair-evidence').mkdir(exist_ok=True)
Path('pair-evidence/current-baseline.json').write_text(json.dumps(dict(Pass=True,Version=feed['Version'],PackageSha256=feed['Sha256'],SourceBase=feed['SourceBase'],ProtectedSourceHashes=before,ApprovedCommonFeatures='10.453',LiaButtonRemoved=True,Published=False),indent=2),encoding='utf-8')
print('PASS current 10.463 common baseline reconstructed; authentication, database, licensing and recovery worker preserved')

# All project-reference builds use the exact distributed Windows SDK package.
deps=json.loads((published/'LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'))
import xml.etree.ElementTree as ET
props=root/'Directory.Build.props'
tree=ET.parse(props) if props.exists() else ET.ElementTree(ET.Element('Project'))
group=ET.SubElement(tree.getroot(),'PropertyGroup')
ET.SubElement(group,'WindowsSdkPackageVersion').text=sdk
ET.SubElement(group,'NotifierPayload').text=str((root/'current-embedded/Notifier.exe').resolve())
tree.write(props,encoding='utf-8',xml_declaration=True)
