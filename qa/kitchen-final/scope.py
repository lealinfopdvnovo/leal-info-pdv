import os,re,subprocess
base={'001':'7ea2e37803f97b7ea6d6e1bf466dea75da6fa1f2','003':'6c0fb5ed6445fec4f1e8cf15bb6acbb788f73bdf'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'MainForm.cs','qa/kitchen-layout/FinanceReportTests.cs','KitchenCatalogView.cs','ReadyKitchenPhotoSync.cs','ready-kitchen/photos.zip','ready-kitchen/photo-sources.json','ready-kitchen/photo-assignments.json','scripts/verify_ready_photos.py','qa/kitchen-layout/KitchenPhotoTests.cs','ReadyKitchenCatalog.cs','ReadyKitchenImportForm.cs','ReadyKitchenExpanded.cs','ReadyKitchenPrices.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-layout/Program.cs','qa/kitchen-layout/DashboardTests.cs','qa/kitchen-layout/KitchenCorrectionsTests.cs','qa/ready-kitchen/Program.cs','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','.github/workflows/cozinha_final_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
norm=lambda s:re.sub(r'10\.(?:431|432)','VERSION',s).replace('ready-kitchen/generated/*\" LogicalName=\"ReadyKitchen.%(Filename)%(Extension)','ready-kitchen/photos.zip\" LogicalName=\"ReadyKitchen.photos.zip')
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['ReadyKitchenPrices.cs','Database.cs','ProductRegistrationForm.cs','KitchenForm.cs','KitchenOrderPanel.cs','KitchenCheckout.cs','Program.cs','DatabaseBackupService.cs','KitchenStore.cs','KitchenSeatingStore.cs','KitchenOrder.cs','KitchenOutbox.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','Licensing/UpdatePayloadPolicy.cs']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),f
print('PASS catalog-only scope; own-channel base, payments, stock, restore, license and updater preserved')

old="COALESCE(m.sale_id,'') AS Venda"
new="COALESCE(CAST(m.sale_id AS TEXT),'') AS Venda"
prior=git("show",base+":MainForm.cs")
assert prior.count(old)==1
assert git("show","HEAD:MainForm.cs")==prior.replace(old,new),"Unexpected MainForm changes"
print("PASS exact financial query correction; no data mutation")
