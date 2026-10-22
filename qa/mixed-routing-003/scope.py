import re,subprocess
base='c802a5f1d565371e4bb22abe6fa9c9b71322fc70'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenDraftDestinations.cs','KitchenOutbox.cs','KitchenForm.cs','KitchenOrderPanel.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/cozinha_destino_por_item_003.yml'}|{'qa/mixed-routing-003/'+f for f in ['Program.cs','Tests.csproj','scope.py','rollback.ps1','publish.py']}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 norm=lambda s:re.sub(r'10\.(437|438)','VERSION',s)
 assert norm(git('show','HEAD:'+f))==norm(git('show',base+':'+f)),f
for f in ['KitchenCheckout.cs','KitchenPartialPayments.cs','KitchenPartialPaymentForm.cs','KitchenConsumption.cs','KitchenStore.cs','KitchenOrder.cs','Database.cs','MainForm.cs','ProductRegistrationForm.cs','KitchenProductRouting.cs','KitchenRoutingReviewForm.cs','KitchenCatalogView.cs','KitchenPremiumStyle.cs','Program.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1']:
 assert git('rev-parse','HEAD:'+f)==git('rev-parse',base+':'+f),'Protected core changed: '+f
old=git('show',base+':KitchenOrderPanel.cs');new=git('show','HEAD:KitchenOrderPanel.cs')
assert new==old.replace('new("DashboardSend","ENVIAR PARA COZINHA","CONFIRMAR PEDIDO"','new("DashboardSend","CONFIRMAR PEDIDO","DESTINOS POR ITEM"')
old=git('show',base+':KitchenOutbox.cs');new=git('show','HEAD:KitchenOutbox.cs')
a='    internal void Queue(';b='    private static KitchenPending Read('
assert old[old.index(a):old.index(b)]==new[new.index(a):new.index('    internal void QueueBatch(')]
assert 'DistributionChannel="003"' in git('show','HEAD:UpdateManager.cs')
print('PASS only per-item draft destination 003; exact payments stock persistence classification license startup updater and premium rendering preserved; legacy queue write unchanged')
