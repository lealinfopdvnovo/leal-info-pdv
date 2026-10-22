import re,subprocess
base='5b1442868533274df04374c2a8b600c794ba48ae'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenPremiumStyle.cs','KitchenPartialPayments.cs','KitchenPartialPaymentForm.cs','KitchenCheckout.cs','KitchenStore.cs','KitchenOrder.cs','KitchenForm.cs','KitchenOrderPanel.cs','MainForm.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-partial-001/Program.cs','qa/kitchen-partial-001/CorrectionsTests.csproj','qa/kitchen-partial-001/scope.py','qa/kitchen-partial-001/rollback.ps1','qa/kitchen-partial-001/publish.py','.github/workflows/cozinha_pagamento_parcial_001.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 assert re.sub(r'10\.(434|435)','VERSION',git('show','HEAD:'+f))==re.sub(r'10\.(434|435)','VERSION',git('show',base+':'+f)),f
old=git('show',base+':MainForm.cs');new=git('show','HEAD:MainForm.cs')
a='    internal bool ReceiveKitchenOrder(';b='    private string BuildReceipt('
assert old[:old.index(a)]==new[:new.index(a)] and old[old.index(b):]==new[new.index(b):],'Unexpected main application change'
print('PASS partial-payment-only scope; own 001 identity and unrelated modules preserved')
