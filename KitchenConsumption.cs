using LealInfoPDV.Network;
using Microsoft.Data.Sqlite;
using System.Globalization;
namespace LealInfoPDV;
internal sealed record KitchenTableLine(Guid OrderId,int Position,string Status,bool TableOnly,KitchenItem Item);
internal sealed partial class KitchenStore
{
    internal string? LastConsumptionBackup {get;private set;}
    private static bool HasConsumption(PdvConnection db,PdvTransaction? tx=null){using var q=Command(db,tx,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_consumption_orders_v3','kitchen_consumption_prices_v3','kitchen_item_cancellations_v3')");return Convert.ToInt32(q.ExecuteScalar())==3;}
    private void EnsureConsumptionReady(){
        lock(schemaGate){using var db=open();if(HasConsumption(db))return;
            using(var count=Command(db,null,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_consumption_orders_v3','kitchen_consumption_prices_v3','kitchen_item_cancellations_v3')"))if(Convert.ToInt32(count.ExecuteScalar())!=0)throw new InvalidDataException("Estrutura de consumo incompleta. Dados preservados.");
            if(!canMigrate)throw new InvalidOperationException("Abra a Cozinha no servidor para preparar o consumo das mesas com backup.");
            using(var check=Command(db,null,"PRAGMA quick_check"))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new InvalidDataException("Integridade não confirmada.");
            Directory.CreateDirectory(Database.BackupFolder);string path=Path.Combine(Database.BackupFolder,"ANTES_CONSUMO_MESAS_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".db");
            using(var backup=new SqliteConnection("Data Source="+path+";Pooling=False")){backup.Open();db.BackupDatabase(backup);using var check=backup.CreateCommand();check.CommandText="PRAGMA quick_check";if(Convert.ToString(check.ExecuteScalar())!="ok")throw new InvalidDataException("Backup de consumo não validado.");}
            using var tx=db.BeginTransaction();using var create=Command(db,tx,"""
                CREATE TABLE kitchen_consumption_orders_v3(order_id TEXT PRIMARY KEY,table_only INTEGER NOT NULL CHECK(table_only IN (0,1)),FOREIGN KEY(order_id) REFERENCES kitchen_orders_v1(id));
                CREATE TABLE kitchen_consumption_prices_v3(order_id TEXT NOT NULL,position INTEGER NOT NULL,unit_price TEXT NOT NULL,PRIMARY KEY(order_id,position),FOREIGN KEY(order_id,position) REFERENCES kitchen_items_v1(order_id,position));
                CREATE TABLE kitchen_item_cancellations_v3(operation_id TEXT PRIMARY KEY,order_id TEXT NOT NULL,position INTEGER NOT NULL,at_utc TEXT NOT NULL,operator TEXT NOT NULL,request_hash TEXT NOT NULL,reason TEXT NOT NULL,UNIQUE(order_id,position),FOREIGN KEY(order_id,position) REFERENCES kitchen_items_v1(order_id,position));
                """);create.ExecuteNonQuery();tx.Commit();LastConsumptionBackup=path;
        }
    }
    private static void CaptureConsumption(PdvConnection db,PdvTransaction tx,KitchenDraft draft){
        using(var q=Command(db,tx,"INSERT INTO kitchen_consumption_orders_v3(order_id,table_only) VALUES($id,$mode)",("$id",draft.Id.ToString("D")),("$mode",draft.TableOnly?1:0)))q.ExecuteNonQuery();
        for(int p=0;p<draft.Items.Length;p++){
            var item=draft.Items[p];decimal price=0;
            if(item.ProductId is long product){using var q=Command(db,tx,"SELECT price FROM products WHERE id=$id AND active=1",("$id",product));var value=q.ExecuteScalar();if(value==null||value==DBNull.Value)throw new InvalidOperationException("Produto indisponível: "+item.Description);price=Convert.ToDecimal(value,CultureInfo.InvariantCulture);if(price<=0)throw new InvalidOperationException("Preço inválido: "+item.Description);}
            using var add=Command(db,tx,"INSERT INTO kitchen_consumption_prices_v3(order_id,position,unit_price) VALUES($id,$p,$price)",("$id",draft.Id.ToString("D")),("$p",p),("$price",price.ToString(CultureInfo.InvariantCulture)));add.ExecuteNonQuery();
        }
    }
    internal KitchenOrder CancelItem(Guid id,int position,long expectedRevision,string actor,Guid operationId,string reason="Cancelamento solicitado pelo operador"){
        if(!Auth.HasPermission("sales")||string.IsNullOrWhiteSpace(actor)||actor.Length>160||operationId==Guid.Empty||position<0||string.IsNullOrWhiteSpace(reason)||reason.Length>1000)throw new InvalidOperationException("Cancelamento inválido ou sem permissão.");
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new{id,position,expectedRevision,actor,reason}))));
        using var db=open();using var tx=db.BeginTransaction();
        using(var q=Command(db,tx,"SELECT request_hash FROM kitchen_item_cancellations_v3 WHERE operation_id=$op",("$op",operationId.ToString("D")))){var prior=Convert.ToString(q.ExecuteScalar());if(!string.IsNullOrEmpty(prior)){if(prior!=hash)throw new InvalidOperationException("Operação reutilizada com dados diferentes.");var done=Read(db,tx,id);tx.Commit();return done;}}
        var order=Read(db,tx,id);if(order.Paid||order.Status=="CANCELADO"||order.Revision!=expectedRevision||!order.Items.Any(i=>i.Position==position))throw new InvalidOperationException("Item alterado, cancelado ou já pago. Atualize a mesa; nenhum pagamento foi alterado.");
        string now=Stamp(DateTimeOffset.UtcNow);using(var q=Command(db,tx,"INSERT INTO kitchen_item_cancellations_v3(operation_id,order_id,position,at_utc,operator,request_hash,reason) VALUES($op,$id,$p,$now,$actor,$hash,$reason)",("$op",operationId.ToString("D")),("$id",id.ToString("D")),("$p",position),("$now",now),("$actor",actor),("$hash",hash),("$reason",reason)))q.ExecuteNonQuery();
        string status=order.Items.Length==1?"CANCELADO":order.Status;
        using(var q=Command(db,tx,"UPDATE kitchen_orders_v1 SET status=$status,revision=revision+1,updated_utc=$now WHERE id=$id AND revision=$r",("$status",status),("$now",now),("$id",id.ToString("D")),("$r",expectedRevision)))if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Pedido alterado em outro terminal.");
        using(var q=Command(db,tx,"INSERT INTO kitchen_events_v1(operation_id,order_id,status,at_utc,operator,request_hash) VALUES($op,$id,$status,$now,$actor,$hash)",("$op","item:"+operationId.ToString("D")),("$id",id.ToString("D")),("$status",order.Items.Length==1?"CANCELADO":"ITEM_CANCELADO"),("$now",now),("$actor",actor),("$hash",hash)))q.ExecuteNonQuery();
        var result=Read(db,tx,id);tx.Commit();return result;
    }
}
