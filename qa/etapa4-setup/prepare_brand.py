from pathlib import Path
import xml.etree.ElementTree as ET,shutil
root=Path('candidate');here=Path('controller/qa/etapa4-setup')
p=root/'extensions/profiles/001/Pet.csproj';tree=ET.parse(p);pr=tree.getroot()
for item in pr.iter('AssemblyMetadata'):
 if item.attrib.get('Include')=='PetVersion':item.set('Value','1.4')
pr.find('PropertyGroup/Version').text='1.4.0'
items=ET.SubElement(pr,'ItemGroup');ET.SubElement(items,'Compile',Include='AngraBrand.cs');ET.SubElement(items,'EmbeddedResource',Include='AngraOpening.mp4',LogicalName='ANGRA.Opening.mp4')
tree.write(p,encoding='utf-8',xml_declaration=True)
shutil.copyfile(here/'AngraBrand.cs.source',root/'extensions/profiles/001/AngraBrand.cs')
shutil.copyfile('angra-abertura.mp4',root/'extensions/profiles/001/AngraOpening.mp4')
p=root/'extensions/profiles/Profile.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace(' public void Initialize(){',' public void Initialize(){\n  AngraBrand.ShowOpening();')
s=s.replace('  presentation.Customize(form);','  presentation.Customize(form);\n  AngraBrand.Apply(form);')
p.write_text(s,encoding='utf-8')
print('PASS QA PET 005 / 1.4 brand and offline narrated MP4 embedded; original profile API retained')
