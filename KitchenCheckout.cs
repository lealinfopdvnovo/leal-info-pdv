using LealInfoPDV.Network;
using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace LealInfoPDV;
internal sealed record KitchenChargeItem(long? ProductId,string Description,decimal Quantity,decimal UnitPrice,bool TrackStock)
{ internal decimal Total=>decimal.Round(Quantity*UnitPrice,2,MidpointRounding.AwayFromZero); }
internal sealed record KitchenPayment(string Method,decimal Amount);
internal sealed record KitchenBill(string Reference,KitchenOrder[] Orders,KitchenChargeItem[] Items)
{
    internal decimal Total=>Items.Sum(i=>i.Total);
    internal string Fingerprint=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{Reference,Orders=Orders.Select(o=>new{o.Id,o.Revision,o.Status}),Items=Items.Select(i=>new{i.ProductId,i.Description,i.Quantity,UnitPrice=i.ProductId.HasValue?i.UnitPrice:0,i.TrackStock})}))));
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
        foreach(var item in orders.SelectMany(o=>o.Items))
        {
            if(item.ProductId is not long productId){items.Add(new(null,item.Description,item.Quantity,0,false));continue;}
            using var product=Command(db,tx,"SELECT price,track_stock FROM products WHERE id=$id AND active=1",("$id",productId));using var r=product.ExecuteReader();
            if(!r.Read()||Convert.ToDecimal(r.GetDouble(0))<=0)throw new InvalidOperationException("Informe um preço válido no cadastro de "+item.Description+" antes de receber.");
            items.Add(new(productId,item.Description,item.Quantity,item.UnitPrice??Convert.ToDecimal(r.GetDouble(0)),r.GetInt32(1)==1));
        }
        if(items.Count==0)throw new InvalidOperationException("Pedido sem itens para receber.");
        return new(chosen.Reference,orders,items.ToArray());
    }
    internal long Receive(KitchenBill bill,IReadOnlyList<KitchenPayment> payments,string actor)
    {
        if(!Auth.HasPermission("sales"))throw new InvalidOperationException("Sem permissão para receber pagamentos.");
        if(string.IsNullOrWhiteSpace(actor)||bill.Orders.Length==0||bill.Total<=0||bill.Items.Any(i=>i.UnitPrice<=0||i.UnitPrice>1_000_000||decimal.Round(i.UnitPrice,2)!=i.UnitPrice)||payments.Count==0||payments.Any(p=>p.Amount<=0||decimal.Round(p.Amount,2)!=p.Amount||p.Method is not ("Dinheiro" or "PIX" or "Cartão" or "Cartão - Débito" or "Cartão - Crédito"))||payments.Sum(p=>p.Amount)!=bill.Total)
            throw new InvalidOperationException("Confira as formas de pagamento e o total da mesa.");
        using var db=open();using var tx=db.BeginTransaction();
        var paid=new List<long>();
        foreach(var order in bill.Orders){using var q=Command(db,tx,"SELECT sale_id FROM kitchen_paid_orders_v1 WHERE order_id=$id",("$id",order.Id.ToString("D")));var id=q.ExecuteScalar();if(id!=null&&id!=DBNull.Value)paid.Add(Convert.ToInt64(id));}
        if(paid.Count>0){if(paid.Count==bill.Orders.Length&&paid.Distinct().Count()==1){tx.Commit();return paid[0];}throw new InvalidOperationException("Pedido já recebido em outro fechamento. Atualize as mesas.");}
        var current=Quote(db,tx,bill.Orders[0].Id);
        if(current.Fingerprint!=bill.Fingerprint)throw new InvalidOperationException("A mesa ou os preços mudaram durante o pagamento. Nenhum lançamento foi feito. Confira e tente novamente.");
        foreach(var group in bill.Items.Where(i=>i.TrackStock).GroupBy(i=>i.ProductId))
        {
            using var update=Command(db,tx,"UPDATE products SET stock=stock-$qty WHERE id=$id AND stock>=$qty",("$id",group.Key),("$qty",group.Sum(i=>i.Quantity)));
            if(update.ExecuteNonQuery()!=1)throw new InvalidOperationException("Estoque insuficiente para "+group.First().Description+". Pagamento não registrado.");
        }
        string date=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),stamp=Stamp(DateTimeOffset.UtcNow);
        string description=payments.Count==1?payments[0].Method:"Múltiplo: "+string.Join(" + ",payments.Select(p=>p.Method));
        using var sale=Command(db,tx,"INSERT INTO sales(sold_at,payment,subtotal,discount,total,operator) VALUES($d,$p,$t,0,$t,$a); SELECT last_insert_rowid();",("$d",date),("$p",description),("$t",bill.Total),("$a",actor));
        long saleId=Convert.ToInt64(sale.ExecuteScalar());
        foreach(var item in bill.Items){using var add=Command(db,tx,"INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total) VALUES($s,$p,$d,$q,$u,$t)",("$s",saleId),("$p",item.ProductId),("$d",item.Description),("$q",item.Quantity),("$u",item.UnitPrice),("$t",item.Total));add.ExecuteNonQuery();}
        foreach(var payment in payments)
        {
            using var add=Command(db,tx,"INSERT INTO sale_payments(sale_id,method,amount) VALUES($s,$m,$v); INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES($d,'ENTRADA',$description,$v,$s)",("$s",saleId),("$m",payment.Method),("$v",payment.Amount),("$d",date),("$description","Venda #"+saleId+" - "+bill.Reference+" - "+payment.Method));add.ExecuteNonQuery();
        }
        foreach(var order in bill.Orders)
        {
            using var finish=Command(db,tx,"INSERT INTO kitchen_paid_orders_v1(order_id,sale_id) VALUES($id,$sale); UPDATE kitchen_orders_v1 SET status='ENTREGUE',revision=revision+1,updated_utc=$now WHERE id=$id AND revision=$revision; INSERT INTO kitchen_events_v1(operation_id,order_id,status,at_utc,operator,request_hash) VALUES($op,$id,'ENTREGUE',$now,$actor,$hash)",("$id",order.Id.ToString("D")),("$sale",saleId),("$now",stamp),("$revision",order.Revision),("$op","payment:"+order.Id.ToString("D")),("$actor",actor),("$hash",bill.Fingerprint));finish.ExecuteNonQuery();
        }
        tx.Commit();return saleId;
    }
}
