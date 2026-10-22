import re,subprocess
base='8210e6ef5777d96fc3317711b034ed86647b16e7'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'MainForm.cs','KitchenPaymentLayout.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_alinhamento_recebimento_003.yml'}|{'qa/payment-layout-003/'+f for f in ['Program.cs','Tests.csproj','scope.py','rollback.ps1','publish.py']}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(441|442)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
assert git('show','HEAD:MainForm.cs')==git('show',base+':MainForm.cs'),'Tender logic must remain identical'
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS 003 only: layout helper; payment selection handlers financial writes premium status destinations license and updater preserved exactly')
