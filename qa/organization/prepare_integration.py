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
s=s.replace('if(channel != "GLOBAL")','PetTransactionalUpdate.AssertNoPendingTransaction();\\n        if(channel != "GLOBAL")') if False else s
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
