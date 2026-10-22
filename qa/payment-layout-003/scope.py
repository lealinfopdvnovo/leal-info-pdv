import re,subprocess
base='6006439d469dfc7af1b60cfbe1ca630d6abb80fa'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'MainForm.cs','KitchenPaymentLayout.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_alinhamento_recebimento_003.yml'}|{'qa/payment-layout-003/'+f for f in ['Program.cs','Tests.csproj','scope.py','rollback.ps1','publish.py']}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(440|441)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
old=git('show',base+':MainForm.cs');new=git('show','HEAD:MainForm.cs')
addition='        if (caption?.StartsWith("RECEBER / PAGAMENTO —", StringComparison.Ordinal) == true)\n            KitchenPaymentLayout.Apply(f, tabs, footer, cancel, cashConfirm, pixConfirm, cardConfirm, multiConfirm);\n'
assert new.replace(addition,'',1)==old,'Only kitchen dialog layout hook allowed; all tender calculations and handlers identical'
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS 003 only: layout helper; payment selection handlers financial writes premium status destinations license and updater preserved exactly')
