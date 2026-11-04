from pathlib import Path
import hashlib,shutil,xml.etree.ElementTree as ET
root=Path('candidate');source=Path('controller/qa/organization')
shutil.copyfile(source/'IFinancialMovementsPet.cs.source',root/'IFinancialMovementsPet.cs')
p=root/'PetRuntime.cs';s=p.read_text(encoding='utf-8-sig')
marker='    internal const int ApiVersion=1;'
assert s.count(marker)==1
s=s.replace(marker,marker+'\n    internal static IFinancialMovementsPet? FinancialMovements=>extension as IFinancialMovementsPet;')
p.write_text(s,encoding='utf-8')
p=root/'MainForm.cs';s=p.read_text(encoding='utf-8-sig')
a=s.index('        DataTable QueryTable(');b=s.index('        double QueryValue(',a);part=s[a:b]
assert part.count('using var cmd = cn.CreateCommand();')==1
part=part.replace('            using var cmd = cn.CreateCommand();','            var petReport = includeMovementType ? PetRuntime.FinancialMovements : null;\n            if (petReport != null) sql = petReport.PrepareSql(sql);\n            using var cmd = cn.CreateCommand();',1)
assert part.count('            var table = new DataTable(); table.Load(reader); return table;')==1
part=part.replace('            var table = new DataTable(); table.Load(reader); return table;','            if (petReport != null) return petReport.ReadTable(reader);\n            var table = new DataTable(); table.Load(reader); return table;',1)
p.write_text(s[:a]+part+s[b:],encoding='utf-8')
p=root/'extensions/profiles/Profile.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('public sealed class ProfileExtension : IPetExtension,IPetStartup,IPetReady','public sealed class ProfileExtension : IPetExtension,IPetStartup,IPetReady\n#if PET_GELAMAQ_FINANCE\n,IFinancialMovementsPet\n#endif',1)
marker=' readonly IPetExtension presentation=ProfilePresentation.Create();'
assert s.count(marker)==1
s=s.replace(marker,'#if PET_ORIGINAL_PRESENTATION\n readonly IPetExtension presentation=PreservedPresentation.Create();\n#else\n'+marker+'\n#endif')
marker=' public void ReportReady(){'
s=s.replace(marker,'#if PET_GELAMAQ_FINANCE\n public string PrepareSql(string sql)=>((IFinancialMovementsPet)presentation).PrepareSql(sql);\n public System.Data.DataTable ReadTable(System.Data.IDataReader reader)=>((IFinancialMovementsPet)presentation).ReadTable(reader);\n#endif\n'+marker,1)
p.write_text(s,encoding='utf-8')
shutil.copyfile(source/'PreservedPresentation.cs.source',root/'extensions/profiles/PreservedPresentation.cs')
for identity in ['001','002']:
 p=root/'extensions/profiles'/identity/'Pet.csproj';tree=ET.parse(p);project=tree.getroot();group=project.find('PropertyGroup')
 constants=group.find('DefineConstants')
 if constants is None:constants=ET.SubElement(group,'DefineConstants');constants.text='$(DefineConstants)'
 constants.text+=';PET_ORIGINAL_PRESENTATION'+(';PET_GELAMAQ_FINANCE' if identity=='002' else '')
 items=ET.SubElement(project,'ItemGroup')
 ET.SubElement(items,'Compile',Include='../PreservedPresentation.cs')
 ET.SubElement(items,'EmbeddedResource',Include='PreservedPresentation.bin',LogicalName='PET.PreservedPresentation')
 tree.write(p,encoding='utf-8',xml_declaration=True)
print('PASS approved optional financial contract recovered; actual original Loja/Gelamaq modules retained in their owning PETs')
