from pathlib import Path
import re,shutil
root=Path('candidate')
source=Path('controller/qa/organization')
for name in ['IndependentUpdater.cs','ComponentUpdateForm.cs']:
 shutil.copyfile(source/name,root/name)
shutil.copyfile(source/'ComponentUpdateCenter.cs.source',root/'ComponentUpdateCenter.cs')
shutil.copyfile(source/'PetTransactionalUpdate.cs.source',root/'PetTransactionalUpdate.cs')
shutil.copyfile(source/'PetTransactionalUpdate.ps1',root/'PetTransactionalUpdate.ps1')
p=root/'LealInfoPDV.csproj'
s=p.read_text(encoding='utf-8-sig')
assert 'PetTransactionalUpdateWorker' not in s
s=s.replace('</Project>','<ItemGroup><EmbeddedResource Include="PetTransactionalUpdate.ps1" LogicalName="PetTransactionalUpdateWorker" /></ItemGroup>\n</Project>')
p.write_text(s,encoding='utf-8')
p=root/'UpdateManager.cs';s=p.read_text(encoding='utf-8-sig')
marker='public static async Task CheckForUpdatesAsync(IWin32Window? owner,bool silent)\n {'
assert s.count(marker)==1
s=s.replace(marker,marker+'\n  if(!silent){await ComponentUpdateCenter.ShowAsync(owner,GetClientCode(),CurrentVersion);return;}')
s=s.replace("  var globalTask=TryLoadManifestAsync(http,\"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json\");\n  var scopedTask=ClientUpdateIdentity.IsValidClientCode(clientCode)\n   ? TryLoadManifestAsync(http,\"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/\"+clientCode+\"/global/version.json\")\n   : Task.FromResult<UpdateManifest?>(null);\n  await Task.WhenAll(globalTask,scopedTask);\n  return SelectManifest(clientCode,currentVersion,await globalTask,await scopedTask);","  var global=await TryLoadManifestAsync(http,\"https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json\");\n  return SelectManifest(clientCode,currentVersion,global,null);")
s=s.replace('  if(targeted!=null&&ClientUpdateIdentity.ManifestMatches(clientCode,targeted.TargetClientCode)&&Compatible(targeted))return targeted;','')
p.write_text(s,encoding='utf-8')
p=root/'TransactionalUpdate.cs';s=p.read_text(encoding='utf-8-sig')
marker='if(channel != "GLOBAL")'
assert s.count(marker)==1
s=s.replace(marker,'PetTransactionalUpdate.AssertNoPendingTransaction();\n        '+marker)
p.write_text(s,encoding='utf-8')
p=root/'Program.cs';s=p.read_text(encoding='utf-8-sig')
assert s.count('TransactionalUpdate.BeforeStartup();')==1
s=s.replace('TransactionalUpdate.BeforeStartup();','TransactionalUpdate.BeforeStartup(); PetTransactionalUpdate.BeforeStartup();')
assert s.count('PetRuntime.ReportReady();')==1
s=s.replace('PetRuntime.ReportReady();','PetRuntime.ReportReady();\n            PetTransactionalUpdate.ReportReady();')
p.write_text(s,encoding='utf-8')
print('PASS production source integration prepared on isolated common base; no feed modified')

# Keep regression fixtures relative to the actual source baseline.
p=root/'qa/global-core/Program.cs';s=p.read_text(encoding='utf-8-sig')
current=re.search(r'CurrentVersion="([^"]+)"',(root/'UpdateManager.cs').read_text()).group(1)
newer=current.rsplit('.',1)[0]+'.'+str(int(current.rsplit('.',1)[1])+1)
s=s.replace('"10.443"','"'+current+'"').replace('"10.444"','"'+newer+'"')
p.write_text(s,encoding='utf-8')
