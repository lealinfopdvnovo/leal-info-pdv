import re,subprocess
base='f239894967689ac123af976168088e55f155de72'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenProductRouting.cs','KitchenRoutingReviewForm.cs','KitchenPremiumStyle.cs','KitchenCatalogView.cs','KitchenForm.cs','ReadyKitchenCatalog.cs','ProductRegistrationForm.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_classificacao_003.yml'}|{'qa/product-routing-003/'+f for f in ['Program.cs','Tests.csproj','scope.py','rollback.ps1','publish.py']}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(436|437)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['KitchenCheckout.cs','KitchenPartialPayments.cs','KitchenPartialPaymentForm.cs','KitchenConsumption.cs','KitchenStore.cs','KitchenOrder.cs','KitchenOrderPanel.cs','Database.cs','MainForm.cs','Program.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),'Protected core changed: '+f
old=git('show',base+':KitchenForm.cs');new=git('show','HEAD:KitchenForm.cs')
assert new==old.replace('            var draft = new KitchenDraft(Guid.NewGuid(), reference.Text.Trim(), notes.Text.Trim(), Auth.OperatorName, DateTimeOffset.UtcNow, rows,tableOnly);','            if(!KitchenProductRouting.Confirm(this,rows,tableOnly))return;\n            var draft = new KitchenDraft(Guid.NewGuid(), reference.Text.Trim(), notes.Text.Trim(), Auth.OperatorName, DateTimeOffset.UtcNow, rows,tableOnly);')
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS only product classification 003; exact approved payments inventory order status startup license updater and 3D table implementations preserved')
