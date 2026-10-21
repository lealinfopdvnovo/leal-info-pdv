import os,re,subprocess
base={'001':'9e33c72697acd91af859ecd2f3dc2ddc690ec648','003':'f82edaeca2dfe49f441ac7a324b2d6e32027a0bf'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'Database.cs','KitchenCheckout.cs','KitchenCatalogView.cs','KitchenOrderPanel.cs','MainForm.cs','ProductRegistrationForm.cs','ReadyKitchenCatalog.cs','ReadyKitchenImportForm.cs','qa/kitchen-layout/KitchenCorrectionsTests.cs','qa/kitchen-layout/DashboardTests.cs','KitchenTableButton.cs','KitchenTablesView.cs','KitchenForm.cs','KitchenPremiumStyle.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-layout/Program.cs','qa/kitchen-layout/TableDoubleClickTests.cs','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','.github/workflows/cozinha_final_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
norm=lambda s:re.sub(r'10\.42[89]','VERSION',s)
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['Program.cs','DatabaseBackupService.cs','KitchenStore.cs','KitchenSeatingStore.cs','KitchenOrder.cs','KitchenOutbox.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','Licensing/UpdatePayloadPolicy.cs']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),f
print('PASS own channel baseline; explicit kitchen scope; existing order protocol, licensing, restore and updater preserved')
