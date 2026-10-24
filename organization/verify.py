import hashlib,json,re,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1];lock=json.loads((root/'organization/lineage.lock.json').read_text())
def blob(p):
 data=p.read_bytes();return hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
checks=[]
for old,item in lock['PreservedFiles'].items():
 p=root/item['Path'];assert p.is_file(),item['Path']
 if item['Path'] not in lock['AdjustedProjectPaths']:assert blob(p)==item['Sha'],item['Path']
checks.append('all original C# sources assets license database and updater bytes preserved')
core=(root/'GLOBAL/LealInfoPDV.csproj').resolve();assert core.is_file()
assert not ET.parse(core).findall('.//ProjectReference'),'GLOBAL references a PET'
versions={}
for id in lock['Children']:
 p=root/f'PETs/{id}/Pet.csproj';doc=ET.parse(p)
 refs=doc.findall('.//ProjectReference');assert len(refs)==1 and (p.parent/refs[0].attrib['Include']).resolve()==core,id
 assert doc.find(".//AssemblyMetadata[@Include='PetIdentity']").attrib['Value']==id
 version=doc.find(".//AssemblyMetadata[@Include='PetVersion']").attrib['Value'];versions[id]=version
 identity=json.loads((p.parent/'identity.json').read_text());assert identity['Identity']==id and identity['Version']==version
 for element in doc.findall('.//Compile'):
  include=element.attrib.get('Include','').replace('\\','/');resolved=(p.parent/include).resolve()
  assert not any(str(resolved).startswith(str((root/f'PETs/{other}').resolve())) for other in lock['Children'] if other!=id),'PET references sibling'
 checks.append(id+': same GLOBAL parent, isolated identity and independent version '+version)
for p in root.rglob('*.csproj'):
 if 'archive' in p.parts:continue
 for ref in ET.parse(p).findall('.//ProjectReference'):assert (p.parent/ref.attrib['Include']).resolve().is_file(),(str(p),ref.attrib['Include'])
assert not list((root/'GLOBAL').glob('Glass*.cs')) and not list((root/'GLOBAL').glob('Angra*.cs'))
assert len(list((root/'GLOBAL').rglob('LealInfoPDV.csproj')))==1
checks.append('all project references resolve; one GLOBAL implementation; no cross-client project links')
checks.append('historical publishers archived only in this branch; no production or feed mutation')
(root/'evidence').mkdir(exist_ok=True)
(root/'evidence/organization.json').write_text(json.dumps(dict(Success=True,Checks=checks,Versions=versions,Published=False,ProductionModified=False,Scope='source organization; no migration approval'),indent=2))
for check in checks:print('PASS',check)
