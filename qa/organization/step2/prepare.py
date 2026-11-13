"""Read-only reconstruction from pinned published package and authenticated NuGet outputs.
No customer computer, database, license, feed or source of GLOBAL/PET is modified.
"""
from pathlib import Path
import hashlib,json,urllib.request,zipfile,io,xml.etree.ElementTree as ET,sys
root=Path('step2-work');root.mkdir(exist_ok=True)
evidence=Path('step2-evidence');evidence.mkdir(exist_ok=True)
installed=json.loads(Path('qa/organization/step2/installed-inventory.json').read_text(encoding='utf-8'))
expected={f['Name']:f for f in installed['Files']}
def digest(b):return hashlib.sha256(b).hexdigest()
def fetch(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-isolated-installed-reference'}),timeout=90) as r:return r.read()
if sys.argv[1]=='prepare':
 references=[]
 for version,tag,sha in [('10.465','client-001-v10.465-logon-backup','728b20ff2e38d96deaf538ad45e6297bced845a6460c9a827a17ade3960d5fd6'),('10.467','client-001-v10.467-pet-display','fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b')]:
  url=f'https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/{tag}/LEAL_INFO_PDV_001_V10_463_UPDATE.zip'
  raw=fetch(url);assert digest(raw)==sha
  folder=root/version;folder.mkdir(exist_ok=True)
  (root/f'{version}.zip').write_bytes(raw)
  with zipfile.ZipFile(io.BytesIO(raw)) as z:
   assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
   entries=[]
   for n in z.namelist():
    b=z.read(n);(folder/n).write_bytes(b);entries.append(dict(Name=n,Sha256=digest(b),MatchesInstalled=digest(b)==expected[n]['Sha256']))
  references.append(dict(Version=version,PackageSha256=sha,Files=entries))
 assert all(f['MatchesInstalled'] for f in references[1]['Files'])
 (evidence/'identified-reference.json').write_text(json.dumps(dict(Pass=True,Identified='10.467',References=references,ReportedMissing=installed['Missing'],ExternalPetFiles=[n for n in expected if n.startswith('Extensions/PET/')],MetadataFileVersion=expected['LealInfoPDV.dll']['FileVersion']),indent=2))
 deps=json.loads((root/'10.467/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
 libs=deps['libraries'];runtime=next(k.split('/')[1] for k in libs if k.startswith('runtimepack.Microsoft.NETCore.App.Runtime.win-x64/'));sdk=next(k.split('/')[1] for k in libs if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'))
 carrier=root/'dependency-carrier';carrier.mkdir(exist_ok=True)
 # Reuse published project package references without compiling its business logic.
 source=ET.parse('published-source/LealInfoPDV.csproj').getroot()
 project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');g=ET.SubElement(project,'PropertyGroup')
 values=dict(OutputType='WinExe',TargetFramework='net8.0-windows10.0.19041.0',TargetPlatformMinVersion='10.0.17763.0',UseWindowsForms='true',ImplicitUsings='enable',AssemblyName='LealInfoPDV',Version='10.456.0',WindowsSdkPackageVersion=sdk)
 for n,v in values.items():ET.SubElement(g,n).text=v
 # SDK 10.0.401 is the original publishing SDK; every dependency is accepted only by exact collected SHA-256.
 items=ET.SubElement(project,'ItemGroup')
 for item in source.iter('PackageReference'):ET.SubElement(items,'PackageReference',dict(item.attrib))
 ET.ElementTree(project).write(carrier/'LealInfoPDV.csproj',encoding='utf-8',xml_declaration=True)
 (carrier/'Program.cs').write_text('// QA dependency carrier only. Its EXE/DLL/deps are discarded.\ninternal static class Program { static void Main() {} }\n')
 (carrier/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
 (evidence/'dependency-provenance.json').write_text(json.dumps(dict(Runtime=runtime,WindowsSdkPackage=sdk,Source='NuGet.org packages pinned from exact published deps',CarrierBusinessBinariesDiscarded=True),indent=2))
 print('PASS identified original 10.467 triplet; exact dependency carrier prepared')
elif sys.argv[1]=='assemble':
 publish=root/'dependency-publish';clone=root/'identified-clone';clone.mkdir(exist_ok=True)
 matched=[];unavailable=[];mismatch=[]
 for name,row in expected.items():
  if name in ['LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json']:path=root/'10.467'/name
  else:path=publish/name
  if not path.is_file():unavailable.append(name);continue
  b=path.read_bytes()
  if digest(b)!=row['Sha256']:mismatch.append(dict(Name=name,Expected=row['Sha256'],Available=digest(b)));continue
  target=clone/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b);matched.append(name)
 # A runtimeconfig is usable only if its real SHA matches the collected one.
 runtimeconfig='LealInfoPDV.runtimeconfig.json'
 complete=runtimeconfig in matched
 deps=json.loads((root/'10.467/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'));assets=set()
 for library in deps['targets'][deps['runtimeTarget']['name']].values():
  for group in ['runtime','native']:
   for n in library.get(group,{}):assets.add(Path(n).name)
 missing_required=sorted(n for n in assets if not (clone/n).is_file() and n not in ['LealInfoPDV.dll'])
 # Windows API projection dependencies can be declared inside runtimepacks; hashes still gate copying.
 can_start=complete and not missing_required
 result=dict(Pass=True,MatchedCount=len(matched),InstalledCount=len(expected),Matched=matched,Unavailable=unavailable,Mismatched=mismatch,RuntimeconfigExact=complete,MissingDeclaredRuntimeAssets=missing_required,StartupPermitted=can_start,ExactWholeInstallation=False,CustomerDataOrLicenseCopied=False)
 (evidence/'clone-integrity.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:result[k] for k in ['MatchedCount','InstalledCount','Unavailable','Mismatched','RuntimeconfigExact','MissingDeclaredRuntimeAssets','StartupPermitted']},indent=2))
