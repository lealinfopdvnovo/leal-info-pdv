import os,re,subprocess
base={'001':'3991f8482db4768ca939a0429254f6ea2aa3d1f4','003':'14666ed23953586f183483369b2f5f11e3871071'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'Database.cs','KitchenCheckout.cs','KitchenCatalogView.cs','KitchenOrderPanel.cs','MainForm.cs','ProductRegistrationForm.cs','ReadyKitchenCatalog.cs','ReadyKitchenImportForm.cs','qa/kitchen-layout/KitchenCorrectionsTests.cs','qa/kitchen-layout/DashboardTests.cs','KitchenTableButton.cs','KitchenTablesView.cs','KitchenForm.cs','KitchenPremiumStyle.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-layout/Program.cs','qa/kitchen-layout/TableDoubleClickTests.cs','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','.github/workflows/cozinha_final_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
norm=lambda s:re.sub(r'10\.42[78]','VERSION',s)
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['Program.cs','DatabaseBackupService.cs','KitchenStore.cs','KitchenSeatingStore.cs','KitchenOrder.cs','KitchenOutbox.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','Licensing/UpdatePayloadPolicy.cs']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),f
print('PASS own channel baseline; explicit kitchen scope; existing order protocol, licensing, restore and updater preserved')
