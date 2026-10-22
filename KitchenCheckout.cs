using LealInfoPDV.Network;
using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace LealInfoPDV;
internal sealed record KitchenChargeItem(long? ProductId,string Description,decimal Quantity,decimal UnitPrice,bool TrackStock,Guid OrderId=default,int Position=-1,bool Payable=true,decimal? TotalOverride=null)
{ internal decimal Total=>TotalOverride??decimal.Round(Quantity*UnitPrice,2,MidpointRounding.AwayFromZero); }
internal sealed record KitchenPayment(string Method,decimal Amount);
internal sealed record KitchenBill(string Reference,KitchenOrder[] Orders,KitchenChargeItem[] Items,Guid OperationId=default,bool FullPayment=true,string? AccountFingerprint=null)
{
    internal decimal Total=>Items.Sum(i=>i.Total);
    internal string Fingerprint=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{Reference,Orders=Orders.Select(o=>new{o.Id,o.Revision,o.Status}),Items=Items.Select(i=>new{i.OrderId,i.Position,i.ProductId,i.Description,i.Quantity,i.UnitPrice,i.TrackStock,i.Payable,i.Total})}))));
}
internal sealed partial class KitchenStore
{
    internal KitchenBill Quote(Guid selected)
    {
        using var db=open();using var tx=db.BeginTransaction();var bill=Quote(db,tx,selected);tx.Commit();return bill;
    }
    private static KitchenBill Quote(PdvConnection db,PdvTransaction tx,Guid selected)
    {
        var chosen=Read(db,tx,selected);
        if(chosen.Status=="CANCELADO")throw new InvalidOperationException("Pedido cancelado não pode ser recebido.");
        var ids=new List<Guid>();
        using(var query=Command(db,tx,"SELECT o.id FROM kitchen_orders_v1 o LEFT JOIN kitchen_paid_orders_v1 p ON p.order_id=o.id WHERE p.order_id IS NULL AND (o.id=$id OR ($mesa=1 AND o.reference=$ref AND o.status IN ('AGUARDANDO','PREPARANDO','PRONTO','ENTREGUE'))) ORDER BY o.received_utc,o.id",("$id",selected.ToString("D")),("$ref",chosen.Reference),("$mesa",KitchenTablesView.TableNumber(chosen.Reference).HasValue?1:0)))
        using(var r=query.ExecuteReader())while(r.Read())ids.Add(Guid.Parse(r.GetString(0)));
        if(!ids.Contains(selected))throw new InvalidOperationException("Este pedido já foi pago. Atualize as mesas.");
        var orders=ids.Select(id=>Read(db,tx,id)).ToArray();var items=new List<KitchenChargeItem>();
        foreach(var order in orders)foreach(var item in order.Items)
        {
            if(item.ProductId is not long productId){items.Add(new(null,item.Description,item.Quantity,item.UnitPrice??0,false,order.Id,item.Position??0,order.TableOnly||order.Status=="ENTREGUE",item.LineBalance));continue;}
            using var product=Command(db,tx,"SELECT price,track_stock FROM products WHERE id=$id AND active=1",("$id",productId));using var r=product.ExecuteReader();
            if(!r.Read()||Convert.ToDecimal(r.GetDouble(0))<=0)throw new InvalidOperationException("Informe um preço válido no cadastro de "+item.Description+" antes de receber.");
            items.Add(new(productId,item.Description,item.Quantity,item.UnitPrice??Convert.ToDecimal(r.GetDouble(0)),r.GetInt32(1)==1,order.Id,item.Position??0,order.TableOnly||order.Status=="ENTREGUE",item.LineBalance));
        }
        if(items.Count==0)throw new InvalidOperationException("Pedido sem itens para receber.");
        return new(chosen.Reference,orders,items.ToArray(),Guid.NewGuid());
    }
    internal long Receive(KitchenBill bill,IReadOnlyList<KitchenPayment> payments,string actor)
    {
        if(!Auth.HasPermission("sales"))throw new InvalidOperationException("Sem permissão para receber pagamentos.");
        if(string.IsNullOrWhiteSpace(actor)||actor.Length>160||bill.OperationId==Guid.Empty||bill.Orders.Length==0||bill.Items.Length==0||bill.Total<0||bill.Items.Any(i=>i.Quantity<=0||i.UnitPrice<=0||i.UnitPrice>1_000_000||decimal.Round(i.UnitPrice,2)!=i.UnitPrice)||payments.Any(p=>p.Amount<=0||decimal.Round(p.Amount,2)!=p.Amount||p.Method is not ("Dinheiro" or "PIX" or "Cartão" or "Cartão - Débito" or "Cartão - Crédito"))||payments.Sum(p=>p.Amount)!=bill.Total||bill.Total>0&&payments.Count==0)
            throw new InvalidOperationException("Confira os produtos, quantidades, formas de pagamento e total.");
        string request=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{bill.OperationId,bill.Fingerprint,bill.AccountFingerprint,bill.FullPayment,actor,Payments=payments}))));
        using var db=open();using var tx=db.BeginTransaction();
        using(var prior=Command(db,tx,"SELECT sale_id,request_hash FROM kitchen_payment_operations_v4 WHERE operation_id=$op",("$op",bill.OperationId.ToString("D"))))using(var r=prior.ExecuteReader())if(r.Read()){
            if(r.GetString(1)!=request)throw new InvalidOperationException("Esta cobrança já foi registrada com outros dados.");long done=r.GetInt64(0);tx.Commit();return done;
        }
        var current=Quote(db,tx,bill.Orders[0].Id);
        if(current.Fingerprint!=(bill.AccountFingerprint??bill.Fingerprint))throw new InvalidOperationException("A conta ou o preparo mudou. Nenhum lançamento foi feito. Atualize e confira novamente.");
        var priced=current.Items.Select(i=>{
            var supplied=bill.Items.SingleOrDefault(x=>x.OrderId==i.OrderId&&x.Position==i.Position);
            return !i.ProductId.HasValue&&i.UnitPrice<=0&&supplied!=null?i with{UnitPrice=supplied.UnitPrice,TotalOverride=null}:i;
        }).ToArray();
        var selected=SelectItems(current with{Items=priced},bill.Items.Select(i=>new KitchenPayQuantity(i.OrderId,i.Position,i.Quantity)).ToArray(),bill.FullPayment);
        if(selected.Items.Length!=bill.Items.Length||selected.Items.Where((item,index)=>item.ProductId!=bill.Items[index].ProductId||item.Description!=bill.Items[index].Description||item.Quantity!=bill.Items[index].Quantity||item.UnitPrice!=bill.Items[index].UnitPrice||item.TrackStock!=bill.Items[index].TrackStock||item.Payable!=bill.Items[index].Payable||item.Total!=bill.Items[index].Total).Any())throw new InvalidOperationException("Produtos, preços ou valores da seleção foram alterados. Confira novamente.");
        if(bill.Total==0&&bill.Items.Any(i=>priced.Single(x=>x.OrderId==i.OrderId&&x.Position==i.Position).Total!=0))throw new InvalidOperationException("Selecione uma quantidade com valor de cobrança válido.");
        foreach(var group in bill.Items.Where(i=>i.TrackStock).GroupBy(i=>i.ProductId))
        {
            using var update=Command(db,tx,"UPDATE products SET stock=stock-$qty WHERE id=$id AND stock>=$qty",("$id",group.Key),("$qty",group.Sum(i=>i.Quantity)));
            if(update.ExecuteNonQuery()!=1)throw new InvalidOperationException("Estoque insuficiente para "+group.First().Description+". Pagamento não registrado.");
        }
        string date=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),stamp=Stamp(DateTimeOffset.UtcNow);
        string description=payments.Count==0?"Ajuste final de centavos":payments.Count==1?payments[0].Method:"Múltiplo: "+string.Join(" + ",payments.Select(p=>p.Method));
        using var sale=Command(db,tx,"INSERT INTO sales(sold_at,payment,subtotal,discount,total,operator) VALUES($d,$p,$t,0,$t,$a); SELECT last_insert_rowid();",("$d",date),("$p",description),("$t",bill.Total),("$a",actor));
        long saleId=Convert.ToInt64(sale.ExecuteScalar());
        foreach(var item in bill.Items){using var add=Command(db,tx,"INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total) VALUES($s,$p,$d,$q,$u,$t)",("$s",saleId),("$p",item.ProductId),("$d",item.Description),("$q",item.Quantity),("$u",item.UnitPrice),("$t",item.Total));add.ExecuteNonQuery();}
        foreach(var payment in payments)
        {
            using var add=Command(db,tx,"INSERT INTO sale_payments(sale_id,method,amount) VALUES($s,$m,$v); INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES($d,'ENTRADA',$description,$v,$s)",("$s",saleId),("$m",payment.Method),("$v",payment.Amount),("$d",date),("$description","Venda #"+saleId+" - "+bill.Reference+" - "+payment.Method));add.ExecuteNonQuery();
        }
        using(var operation=Command(db,tx,"INSERT INTO kitchen_payment_operations_v4(operation_id,request_hash,sale_id,reference,operator,at_utc) VALUES($op,$hash,$sale,$reference,$actor,$now)",("$op",bill.OperationId.ToString("D")),("$hash",request),("$sale",saleId),("$reference",bill.Reference),("$actor",actor),("$now",stamp)))operation.ExecuteNonQuery();
        foreach(var item in bill.Items){
            var available=priced.Single(i=>i.OrderId==item.OrderId&&i.Position==item.Position);
            decimal original=InitialAmount(db,tx,available);
            using(var line=Command(db,tx,"INSERT INTO kitchen_item_payments_v4(operation_id,order_id,position,quantity,unit_price,total,original_total) VALUES($op,$id,$p,$q,$price,$total,$original)",("$op",bill.OperationId.ToString("D")),("$id",item.OrderId.ToString("D")),("$p",item.Position),("$q",item.Quantity.ToString(CultureInfo.InvariantCulture)),("$price",item.UnitPrice.ToString(CultureInfo.InvariantCulture)),("$total",item.Total.ToString(CultureInfo.InvariantCulture)),("$original",original.ToString(CultureInfo.InvariantCulture))))line.ExecuteNonQuery();
            decimal remaining=available.Quantity-item.Quantity;
            using(var update=Command(db,tx,"UPDATE kitchen_items_v1 SET quantity=$remaining WHERE order_id=$id AND position=$p",("$remaining",remaining.ToString("G29",CultureInfo.InvariantCulture)),("$id",item.OrderId.ToString("D")),("$p",item.Position)))if(update.ExecuteNonQuery()!=1)throw new InvalidOperationException("Item não encontrado para pagamento.");
            using(var price=Command(db,tx,"INSERT INTO kitchen_consumption_prices_v3(order_id,position,unit_price) VALUES($id,$p,$price) ON CONFLICT(order_id,position) DO UPDATE SET unit_price=excluded.unit_price",("$id",item.OrderId.ToString("D")),("$p",item.Position),("$price",item.UnitPrice.ToString(CultureInfo.InvariantCulture))))price.ExecuteNonQuery();
        }
        foreach(var order in bill.Orders.Where(o=>bill.Items.Any(i=>i.OrderId==o.Id))){
            bool complete=Read(db,tx,order.Id).Items.Length==0;
            using(var update=Command(db,tx,"UPDATE kitchen_orders_v1 SET status=CASE WHEN $complete=1 THEN 'ENTREGUE' ELSE status END,revision=revision+1,updated_utc=$now WHERE id=$id AND revision=$r",("$complete",complete?1:0),("$now",stamp),("$id",order.Id.ToString("D")),("$r",order.Revision)))if(update.ExecuteNonQuery()!=1)throw new InvalidOperationException("Conta alterada por outro terminal.");
            if(complete)using(var paid=Command(db,tx,"INSERT INTO kitchen_paid_orders_v1(order_id,sale_id) VALUES($id,$sale)",("$id",order.Id.ToString("D")),("$sale",saleId)))paid.ExecuteNonQuery();
            using var history=Command(db,tx,"INSERT INTO kitchen_events_v1(operation_id,order_id,status,at_utc,operator,request_hash) VALUES($op,$id,$status,$now,$actor,$hash)",("$op","payment:"+bill.OperationId.ToString("D")+":"+order.Id.ToString("D")),("$id",order.Id.ToString("D")),("$status",complete?"PAGO":"PAGAMENTO_PARCIAL"),("$now",stamp),("$actor",actor),("$hash",request));history.ExecuteNonQuery();
        }
        tx.Commit();return saleId;
    }
}
