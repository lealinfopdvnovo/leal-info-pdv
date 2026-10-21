import os,re,subprocess
base={'001':'2fd04619c33dfa6774e1700461e1a8b3e40a5bf9','003':'c6e3eb9ff4c87fa089c55078804311ee6fb88d23'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenTableButton.cs','KitchenTablesView.cs','KitchenForm.cs','KitchenPremiumStyle.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-layout/Program.cs','qa/kitchen-layout/TableDoubleClickTests.cs','qa/kitchen-final/scope.py','qa/kitchen-final/rollback.ps1','qa/kitchen-final/publish.py','.github/workflows/cozinha_final_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
norm=lambda s:re.sub(r'10\.42[67]','VERSION',s)
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['MainForm.cs','Program.cs','DatabaseBackupService.cs','KitchenStore.cs','KitchenSeatingStore.cs','KitchenOrder.cs','KitchenOutbox.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','Licensing/UpdatePayloadPolicy.cs']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),f
print('PASS own channel baseline; approved visual renderers, database, orders, license, cash, stock and updater preserved')
