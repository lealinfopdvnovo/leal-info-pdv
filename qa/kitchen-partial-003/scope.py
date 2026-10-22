import re,subprocess
base='ffd58131f8dcd1279a019c50a78d2cc83913f133'
approved='f2e822fbccbe07b349150a1e8997994c634f9d50'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
production={'KitchenCheckout.cs','KitchenPartialPayments.cs','KitchenPartialPaymentForm.cs','KitchenStore.cs','KitchenOrder.cs','KitchenForm.cs','KitchenOrderPanel.cs','KitchenPremiumStyle.cs'}
allowed=production|{'MainForm.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_pagamento_parcial_003.yml','qa/kitchen-partial-003/Program.cs','qa/kitchen-partial-003/CorrectionsTests.csproj','qa/kitchen-partial-003/rollback.ps1','qa/kitchen-partial-003/publish.py','qa/kitchen-partial-003/scope.py'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in production:assert git('rev-parse','HEAD:'+f)==git('rev-parse',approved+':'+f),f
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(434|436)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
old=git('show',base+':MainForm.cs');new=git('show','HEAD:MainForm.cs');ported=git('show',approved+':MainForm.cs')
a='    internal bool ReceiveKitchenOrder(';b='    private string BuildReceipt('
assert old[:old.index(a)]==new[:new.index(a)] and old[old.index(b):]==new[new.index(b):]
assert new[new.index(a):new.index(b)]==ported[ported.index(a):ported.index(b)]
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS exact approved payment logic ported; 003-specific restore, startup, licensing and updater preserved')
