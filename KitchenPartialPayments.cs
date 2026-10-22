using LealInfoPDV.Network;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LealInfoPDV;
internal sealed record KitchenPayQuantity(Guid OrderId,int Position,decimal Quantity);
internal sealed record KitchenPaymentPending(KitchenBill Bill,KitchenPayment[] Payments,string Actor);
internal static class KitchenPaymentJournal
{
    private static string PathFor(string store,string reference)=>Path.Combine(Database.AppFolder,"KitchenPaymentPending",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(store+":"+reference)))+".pending");
    internal static KitchenPaymentPending? Read(string store,string reference){string path=PathFor(store,reference);return File.Exists(path)?ProtectedFile.Read<KitchenPaymentPending>(path):null;}
    internal static void Save(string store,KitchenPaymentPending pending){string path=PathFor(store,pending.Bill.Reference);Directory.CreateDirectory(Path.GetDirectoryName(path)!);if(File.Exists(path))throw new InvalidOperationException("Confira a cobrança pendente desta mesa antes de iniciar outra.");ProtectedFile.Write(path,pending);}
    internal static void Complete(string store,string reference)=>File.Delete(PathFor(store,reference));
}
internal sealed partial class KitchenStore
{
    internal string? LastPaymentBackup {get;private set;}
    private static bool HasPartialPayments(PdvConnection db,PdvTransaction? tx=null){using var q=Command(db,tx,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_payment_operations_v4','kitchen_item_payments_v4')");return Convert.ToInt32(q.ExecuteScalar())==2;}
    private void EnsurePaymentsReady(){lock(schemaGate){using var db=open();if(HasPartialPayments(db))return;
        using(var q=Command(db,null,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_payment_operations_v4','kitchen_item_payments_v4')"))if(Convert.ToInt32(q.ExecuteScalar())!=0)throw new InvalidDataException("Estrutura de pagamentos incompleta. Dados preservados.");
        if(!canMigrate)throw new InvalidOperationException("Abra a Cozinha no servidor para preparar os pagamentos com backup.");
        using(var q=Command(db,null,"PRAGMA quick_check"))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new InvalidDataException("Integridade não confirmada.");
        Directory.CreateDirectory(Database.BackupFolder);string path=Path.Combine(Database.BackupFolder,"ANTES_PAGAMENTO_PARCIAL_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".db");
        using(var snapshot=new SqliteConnection("Data Source="+path+";Pooling=False")){snapshot.Open();db.BackupDatabase(snapshot);using var q=snapshot.CreateCommand();q.CommandText="PRAGMA quick_check";if(Convert.ToString(q.ExecuteScalar())!="ok")throw new InvalidDataException("Backup financeiro não validado.");}
        using var tx=db.BeginTransaction();using var create=Command(db,tx,"""
            CREATE TABLE kitchen_payment_operations_v4(operation_id TEXT PRIMARY KEY,request_hash TEXT NOT NULL,sale_id INTEGER NOT NULL,reference TEXT NOT NULL,operator TEXT NOT NULL,at_utc TEXT NOT NULL,FOREIGN KEY(sale_id) REFERENCES sales(id));
            CREATE TABLE kitchen_item_payments_v4(operation_id TEXT NOT NULL,order_id TEXT NOT NULL,position INTEGER NOT NULL,quantity TEXT NOT NULL,unit_price TEXT NOT NULL,total TEXT NOT NULL,original_total TEXT NOT NULL,PRIMARY KEY(operation_id,order_id,position),FOREIGN KEY(operation_id) REFERENCES kitchen_payment_operations_v4(operation_id),FOREIGN KEY(order_id,position) REFERENCES kitchen_items_v1(order_id,position));
            CREATE INDEX kitchen_item_payments_position_v4 ON kitchen_item_payments_v4(order_id,position);
            """);create.ExecuteNonQuery();tx.Commit();LastPaymentBackup=path;
    }}
    private static decimal? RemainingAmount(PdvConnection db,PdvTransaction? tx,Guid id,int position){
        if(!HasPartialPayments(db,tx))return null;using var q=Command(db,tx,"SELECT original_total,total FROM kitchen_item_payments_v4 WHERE order_id=$id AND position=$p ORDER BY rowid",("$id",id.ToString("D")),("$p",position));using var r=q.ExecuteReader();decimal? original=null;decimal paid=0;while(r.Read()){original??=decimal.Parse(r.GetString(0),CultureInfo.InvariantCulture);paid+=decimal.Parse(r.GetString(1),CultureInfo.InvariantCulture);}return original.HasValue?original.Value-paid:null;
    }
    private static decimal InitialAmount(PdvConnection db,PdvTransaction tx,KitchenChargeItem item){using var q=Command(db,tx,"SELECT original_total FROM kitchen_item_payments_v4 WHERE order_id=$id AND position=$p ORDER BY rowid LIMIT 1",("$id",item.OrderId.ToString("D")),("$p",item.Position));var value=q.ExecuteScalar();return value==null?item.Total:decimal.Parse(Convert.ToString(value)!,CultureInfo.InvariantCulture);}
    internal long? PaymentResult(Guid operation){using var db=open();using var q=Command(db,null,"SELECT sale_id FROM kitchen_payment_operations_v4 WHERE operation_id=$op",("$op",operation.ToString("D")));var value=q.ExecuteScalar();return value==null?null:Convert.ToInt64(value);}
    internal static KitchenBill SelectItems(KitchenBill bill,IReadOnlyList<KitchenPayQuantity> selection,bool fullPayment=false){
        if(selection.Count==0||selection.Select(x=>(x.OrderId,x.Position)).Distinct().Count()!=selection.Count)throw new InvalidOperationException("Selecione produtos e quantidades para receber.");
        if(fullPayment&&bill.Items.Any(i=>!i.Payable))throw new InvalidOperationException("Pagamento total bloqueado: existem produtos aguardando preparo, preparando ou prontos para entrega.");
        var chosen=new List<KitchenChargeItem>();
        foreach(var s in selection){var item=bill.Items.SingleOrDefault(i=>i.OrderId==s.OrderId&&i.Position==s.Position)??throw new InvalidOperationException("Produto fora desta conta.");
            if(!item.Payable||s.Quantity<=0||s.Quantity>item.Quantity||decimal.Round(s.Quantity,3)!=s.Quantity)throw new InvalidOperationException("Pague somente quantidades disponíveis de produtos entregues ou consumo direto.");
            decimal amount=s.Quantity==item.Quantity?item.Total:Math.Min(item.Total,decimal.Round(s.Quantity*item.UnitPrice,2,MidpointRounding.AwayFromZero));
            chosen.Add(item with{Quantity=s.Quantity,TotalOverride=amount});
        }
        if(fullPayment&&(chosen.Count!=bill.Items.Length||chosen.Any(i=>bill.Items.Single(x=>x.OrderId==i.OrderId&&x.Position==i.Position).Quantity!=i.Quantity)))throw new InvalidOperationException("Pagamento total exige todos os produtos e quantidades.");
        return bill with{Items=chosen.ToArray(),FullPayment=fullPayment,AccountFingerprint=bill.AccountFingerprint??bill.Fingerprint};
    }
}
