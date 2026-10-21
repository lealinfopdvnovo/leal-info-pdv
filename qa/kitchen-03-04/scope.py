import os,re,subprocess
base={'001':'1076a54d560609a2bf251e0e9d1a9d104cd090fe','003':'1a8fd9770b6a2bf87307fa31d002d3126b22747e'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenConsumption.cs','KitchenCatalogView.cs','KitchenCheckout.cs','KitchenForm.cs','KitchenOrder.cs','KitchenOrderPanel.cs','KitchenSeatingStore.cs','KitchenStore.cs','KitchenTablesView.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-03-04/Program.cs','qa/kitchen-03-04/CorrectionsTests.csproj','qa/kitchen-03-04/scope.py','qa/kitchen-03-04/rollback.ps1','qa/kitchen-03-04/publish.py','.github/workflows/cozinha_03_04_001_003.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 assert re.sub(r'10\.(433|434)','VERSION',git('show','HEAD:'+f))==re.sub(r'10\.(433|434)','VERSION',git('show',base+':'+f)),f
print('PASS own-channel scope: only kitchen consumption and cancellation, candidate version and directed tests')
