import re,subprocess
base='a2f7d7e812e30ec25ea71aa43bf9342f734939c2'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'qa/kitchen-partial-001/VisualOnly.cs','qa/kitchen-partial-001/VisualTests.csproj','qa/kitchen-partial-001/combine.py','KitchenPremiumStyle.cs','KitchenPartialPayments.cs','KitchenPartialPaymentForm.cs','KitchenCheckout.cs','KitchenStore.cs','KitchenOrder.cs','KitchenForm.cs','KitchenOrderPanel.cs','MainForm.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-partial-001/Program.cs','qa/kitchen-partial-001/CorrectionsTests.csproj','qa/kitchen-partial-001/scope.py','qa/kitchen-partial-001/rollback.ps1','qa/kitchen-partial-001/publish.py','.github/workflows/cozinha_pagamento_parcial_001.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 assert re.sub(r'10\.(435|436)','VERSION',git('show','HEAD:'+f))==re.sub(r'10\.(435|436)','VERSION',git('show',base+':'+f)),f
old=git('show',base+':MainForm.cs');new=git('show','HEAD:MainForm.cs')
a='    internal bool ReceiveKitchenOrder(';b='    private string BuildReceipt('
assert old[:old.index(a)]==new[:new.index(a)] and old[old.index(b):]==new[new.index(b):],'Unexpected main application change'
print('PASS partial-payment-only scope; own 001 identity and unrelated modules preserved')

for f in ["KitchenCheckout.cs","KitchenPartialPayments.cs","KitchenPartialPaymentForm.cs","KitchenStore.cs","KitchenOrder.cs","KitchenForm.cs","KitchenOrderPanel.cs","MainForm.cs","KitchenConsumption.cs","Database.cs"]:
 assert git("rev-parse","HEAD:"+f)==git("rev-parse",base+":"+f),"Financial core differs from approved Windows run: "+f
old=git("show",base+":KitchenPremiumStyle.cs")
assert git("show","HEAD:KitchenPremiumStyle.cs")==old.replace('or "KitchenManualPrice";','or "KitchenManualPrice" or "KitchenPartialPayment";')
print("PASS exact approved financial core reused; only payment dialog theme ownership changed")
