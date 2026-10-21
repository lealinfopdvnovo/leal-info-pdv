import re,subprocess
from pathlib import Path
BASE='b156dc46d8b7e2d71e533f23449545c65f4e99ab'
VISUAL='c6e3eb9ff4c87fa089c55078804311ee6fb88d23'
def git(*args):return subprocess.check_output(['git',*args]).decode('utf-8').replace('\r\n','\n')
changed=set(git('diff','--name-only',BASE,'HEAD').splitlines())
kitchen={'KitchenCatalogView.cs','KitchenCategoryButton.cs','KitchenForm.cs','KitchenGold.cs','KitchenLighting.cs','KitchenOrderPanel.cs','KitchenPremiumHeader.cs','KitchenPremiumStyle.cs','KitchenProductPhoto.cs','KitchenTableButton.cs','Assets/KitchenTablesPremium.png'}
allowed=kitchen|{'InternalWorkspace.cs','PremiumGlobalTheme.cs','LealInfoPDV.csproj','UpdateManager.cs','.github/workflows/cozinha_final_001_003.yml','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','qa/kitchen-layout/DashboardTests.cs','qa/kitchen-layout/Program.cs','qa/kitchen-layout/LayoutTests.csproj'}
assert changed<=allowed,changed-allowed
for f in kitchen:assert git('rev-parse','HEAD:'+f).strip()==git('rev-parse',VISUAL+':'+f).strip(),f
for f,guard in [
('InternalWorkspace.cs','        if (KitchenPremiumStyle.Owns(f))\n        {\n            if (!f.IsDisposed && !Ready.TryGetValue(f, out _)) { Ready.Add(f, new object()); Fit(f); }\n            return;\n        }\n'),
('PremiumGlobalTheme.cs','        if (KitchenPremiumStyle.Owns(control)) return;\n')]:
 assert git('show','HEAD:'+f).replace(guard,'')==git('show',BASE+':'+f),f
norm=lambda s:re.sub(r'10\.42[26]','VERSION',s)
assert norm(git('show','HEAD:UpdateManager.cs'))==norm(git('show',BASE+':UpdateManager.cs'))
new=git('show','HEAD:LealInfoPDV.csproj').replace('  <ItemGroup><EmbeddedResource Include="Assets/KitchenTablesPremium.png" LogicalName="Kitchen.TablesPremium" /></ItemGroup>\n','')
assert norm(new)==norm(git('show',BASE+':LealInfoPDV.csproj'))
print('PASS 001 own baseline; exact 003 kitchen visual files; restore, license, data, cash, network, updater and all unrelated sources unchanged')
