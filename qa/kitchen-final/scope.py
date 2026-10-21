import os,re,subprocess
base={'001':'3cb3dc3d877f2e8cec2c67df98dc8246060d0539','003':'ef42c64d47573289cea2572abd7431bcb8accd8d'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenCatalogView.cs','ReadyKitchenPhotoSync.cs','ready-kitchen/photos.zip','ready-kitchen/photo-sources.json','ready-kitchen/photo-assignments.json','scripts/verify_ready_photos.py','qa/kitchen-layout/KitchenPhotoTests.cs','ReadyKitchenCatalog.cs','ReadyKitchenImportForm.cs','ReadyKitchenExpanded.cs','ReadyKitchenPrices.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-layout/Program.cs','qa/kitchen-layout/DashboardTests.cs','qa/kitchen-layout/KitchenCorrectionsTests.cs','qa/ready-kitchen/Program.cs','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','.github/workflows/cozinha_final_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
norm=lambda s:re.sub(r'10\.(?:430|431)','VERSION',s).replace('ready-kitchen/generated/*\" LogicalName=\"ReadyKitchen.%(Filename)%(Extension)','ready-kitchen/photos.zip\" LogicalName=\"ReadyKitchen.photos.zip')
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['ReadyKitchenPrices.cs','MainForm.cs','Database.cs','ProductRegistrationForm.cs','KitchenForm.cs','KitchenOrderPanel.cs','KitchenCheckout.cs','Program.cs','DatabaseBackupService.cs','KitchenStore.cs','KitchenSeatingStore.cs','KitchenOrder.cs','KitchenOutbox.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','Licensing/UpdatePayloadPolicy.cs']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),f
print('PASS catalog-only scope; own-channel base, payments, stock, restore, license and updater preserved')
