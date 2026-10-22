import re,subprocess
base='875a170782290e5511e70ca2b3137bc9e30777df'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenItemStatuses.cs','KitchenStore.cs','KitchenOrder.cs','KitchenCheckout.cs','KitchenPartialPaymentForm.cs','KitchenForm.cs','KitchenOrderPanel.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_status_individual_003.yml'}|{'qa/item-status-003/'+f for f in ['Program.cs','Tests.csproj','scope.py','rollback.ps1','publish.py']}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(439|440)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['KitchenPartialPayments.cs','KitchenConsumption.cs','KitchenDraftDestinations.cs','KitchenOutbox.cs','Database.cs','MainForm.cs','ProductRegistrationForm.cs','KitchenProductRouting.cs','KitchenRoutingReviewForm.cs','KitchenCatalogView.cs','KitchenPremiumStyle.cs','Program.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),'Protected core changed: '+f
old=git('show',base+':KitchenCheckout.cs');new=git('show','HEAD:KitchenCheckout.cs')
assert new==old.replace('order.TableOnly||order.Status=="ENTREGUE"','order.TableOnly||ItemStatus(order,item)=="ENTREGUE"'),'Financial writes changed'
old=git('show',base+':KitchenOrder.cs');new=git('show','HEAD:KitchenOrder.cs')
assert new==old.replace('decimal? LineBalance=null);','decimal? LineBalance=null, [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? PreparationStatus=null);')
s=git('show','HEAD:KitchenForm.cs');assert 'store.ChangeItemStatus(id,position,order.Revision,status' in s
assert 'private Task SubmitDraft()=>SubmitConsumption();' in s and 'KitchenProductRouting.Confirm' not in s
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS per-item status only 003; approved destinations preserved; financial write path inventory ledger license updater and premium rendering unchanged')
