import re,subprocess
base='1362b1501a9e63dd7605c3a0786c7ea8fad5034e'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenForm.cs','KitchenOrderPanel.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_confirmacao_destinos_003.yml'}|{'qa/confirm-routing-003/'+f for f in ['Program.cs','Tests.csproj','scope.py','rollback.ps1','publish.py']}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(438|439)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['KitchenCheckout.cs','KitchenPartialPayments.cs','KitchenPartialPaymentForm.cs','KitchenConsumption.cs','KitchenStore.cs','KitchenOrder.cs','KitchenDraftDestinations.cs','KitchenOutbox.cs','Database.cs','MainForm.cs','ProductRegistrationForm.cs','KitchenProductRouting.cs','KitchenRoutingReviewForm.cs','KitchenCatalogView.cs','KitchenPremiumStyle.cs','Program.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),'Protected core changed: '+f
s=git('show','HEAD:KitchenForm.cs')
assert 'KitchenProductRouting.Confirm' not in s and 'SubmitConsumption(bool' not in s and 'SubmitConsumption(true)' not in s
assert 'private Task SubmitDraft()=>SubmitConsumption();' in s
assert 'l.Choice.TableOnly)).ToArray();' in s
assert 'Name="AddToTable"' not in s
assert 'DashboardAddToTable' not in git('show','HEAD:KitchenOrderPanel.cs')
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS single confirmation respects all visible destinations; no bulk override; financial queue classification license updater and premium rendering unchanged')
