import os,re,subprocess
base={'001':'8ff95a56116bd00f9d56c8e30de2d07323782942','003':'7e7f317ec10c47cd2e72f4383a4e9a604013b9f5'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'ReadyKitchenCatalog.cs','ReadyKitchenImportForm.cs','ReadyKitchenExpanded.cs','ReadyKitchenPrices.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-layout/Program.cs','qa/kitchen-layout/DashboardTests.cs','qa/kitchen-layout/KitchenCorrectionsTests.cs','qa/ready-kitchen/Program.cs','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','.github/workflows/cozinha_final_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
norm=lambda s:re.sub(r'10\.(?:429|430)','VERSION',s)
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['MainForm.cs','Database.cs','ProductRegistrationForm.cs','KitchenForm.cs','KitchenOrderPanel.cs','KitchenCheckout.cs','Program.cs','DatabaseBackupService.cs','KitchenStore.cs','KitchenSeatingStore.cs','KitchenOrder.cs','KitchenOutbox.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','Licensing/UpdatePayloadPolicy.cs']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),f
print('PASS catalog-only scope; own-channel base, payments, stock, restore, license and updater preserved')
