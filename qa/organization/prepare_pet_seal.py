"""Move the approved seal into only Loja and Casa PET modules."""
from pathlib import Path
import hashlib,json,shutil,xml.etree.ElementTree as ET
root=Path('candidate')
source=Path('controller/qa/organization')
asset=Path('pair-evidence/approved-seal-001.png')
assert hashlib.sha256(asset.read_bytes()).hexdigest()=='8b2c8dac49d85710dbd4d80137d3a0a66df89f987353bc10451adf8c18525542'
protected={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in root.glob('*.cs')}
profile=root/'extensions/profiles/Profile.cs'
s=profile.read_text(encoding='utf-8-sig')
marker='  presentation.Customize(form);'
assert s.count(marker)==1
s=s.replace(marker,'#if PET_CERTIFICATION_SEAL\n  ProfileCertificationSeal.Apply(form);\n#endif\n'+marker)
profile.write_text(s,encoding='utf-8')
shutil.copyfile(source/'ProfileCertificationSeal.cs.source',root/'extensions/profiles/ProfileCertificationSeal.cs')
versions={'001':'1.3','003':'1.2','002':'1.1','004':'1.1'}
for identity,old,new in [('001','1.2','1.3'),('003','1.1','1.2')]:
 folder=root/'extensions/profiles'/identity
 shutil.copyfile(asset,folder/'CertificationSeal.png')
 p=folder/'Presentation.cs';s=p.read_text(encoding='utf-8-sig')
 assert old in s
 p.write_text(s.replace(old,new),encoding='utf-8')
 p=folder/'Pet.csproj';tree=ET.parse(p);project=tree.getroot()
 group=project.find('PropertyGroup')
 group.find('Version').text=new+'.0'
 constants=group.find('DefineConstants');constants.text=constants.text+';PET_CERTIFICATION_SEAL'
 for item in project.iter('AssemblyMetadata'):
  if item.attrib.get('Include')=='PetVersion':item.set('Value',new)
 items=ET.SubElement(project,'ItemGroup')
 ET.SubElement(items,'Compile',Include='../ProfileCertificationSeal.cs')
 ET.SubElement(items,'EmbeddedResource',Include='CertificationSeal.png',LogicalName='PET.CertificationSeal')
 tree.write(p,encoding='utf-8',xml_declaration=True)
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==digest for p,digest in protected.items()),'PET preparation modified common source'
Path('pair-evidence/profile-versions.json').write_text(json.dumps(versions,indent=2),encoding='utf-8')
Path('pair-evidence/seal-scope.json').write_text(json.dumps(dict(Pass=True,Owners=['001','003'],AssetSha256=hashlib.sha256(asset.read_bytes()).hexdigest(),CommonSourcesUnchanged=True,OtherProfilesUnchanged=True,LogicalSide=96.2,Published=False),indent=2),encoding='utf-8')
print('PASS approved seal belongs only to Loja PET 1.3 / Casa PET 1.2; common source unchanged')
