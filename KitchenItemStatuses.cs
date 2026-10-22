using LealInfoPDV.Network;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;
namespace LealInfoPDV;

internal sealed partial class KitchenStore
{
    internal string? LastItemStatusBackup{get;private set;}
    private static bool HasItemStatuses(PdvConnection db,PdvTransaction? tx=null){using var q=Command(db,tx,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='kitchen_item_status_v5'");return Convert.ToInt32(q.ExecuteScalar())==1;}
    private void EnsureItemStatusesReady()
    {
        lock(schemaGate){using var db=open();if(HasItemStatuses(db))return;
            if(!canMigrate)throw new InvalidOperationException("Atualize e abra a Cozinha no servidor para preparar os status por produto.");
            using(var q=Command(db,null,"PRAGMA quick_check"))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new InvalidDataException("Integridade não confirmada.");
            Directory.CreateDirectory(Database.BackupFolder);string path=Path.Combine(Database.BackupFolder,"ANTES_STATUS_POR_ITEM_"+Guid.NewGuid().ToString("N")+".db");
            using(var snapshot=new SqliteConnection("Data Source="+path+";Pooling=False")){snapshot.Open();db.BackupDatabase(snapshot);using var q=snapshot.CreateCommand();q.CommandText="PRAGMA quick_check";if(Convert.ToString(q.ExecuteScalar())!="ok")throw new InvalidDataException("Backup dos status não validado.");}
            using var tx=db.BeginTransaction();using var create=Command(db,tx,"CREATE TABLE kitchen_item_status_v5(order_id TEXT NOT NULL,position INTEGER NOT NULL,status TEXT NOT NULL CHECK(status IN ('AGUARDANDO','PREPARANDO','PRONTO','ENTREGUE')),updated_utc TEXT NOT NULL,PRIMARY KEY(order_id,position),FOREIGN KEY(order_id,position) REFERENCES kitchen_items_v1(order_id,position))");create.ExecuteNonQuery();tx.Commit();LastItemStatusBackup=path;
        }
    }
    internal static string ItemStatus(KitchenOrder order,KitchenItem item)=>order.TableOnly?"ENTREGUE":item.PreparationStatus??order.Status;
    private static bool CanAdvanceItem(string from,string target)=>(from,target) is ("AGUARDANDO","PREPARANDO") or ("AGUARDANDO","PRONTO") or ("PREPARANDO","PRONTO") or ("PRONTO","ENTREGUE");
    private static string AggregateItemStatus(KitchenOrder order,KitchenItem[] items)
    {
        if(order.Status=="CANCELADO"||order.Paid||order.TableOnly||items.Length==0)return order.Status;
        var states=items.Select(i=>i.PreparationStatus??order.Status).ToArray();
        return states.Contains("AGUARDANDO")?"AGUARDANDO":states.Contains("PREPARANDO")?"PREPARANDO":states.Contains("PRONTO")?"PRONTO":"ENTREGUE";
    }
    private static void PutItemStatus(PdvConnection db,PdvTransaction tx,Guid id,int position,string status,string now,bool initializeOnly=false)
    {
        string conflict=initializeOnly?"DO NOTHING":"DO UPDATE SET status=excluded.status,updated_utc=excluded.updated_utc";
        using var q=Command(db,tx,"INSERT INTO kitchen_item_status_v5(order_id,position,status,updated_utc) VALUES($id,$p,$s,$now) ON CONFLICT(order_id,position) "+conflict,("$id",id.ToString("D")),("$p",position),("$s",status),("$now",now));q.ExecuteNonQuery();
    }
    // Explicit whole-order API remains compatible; completed siblings never regress.
    private static void SyncOrderItemStatuses(PdvConnection db,PdvTransaction tx,KitchenOrder order,string target,string now)
    {
        if(target=="CANCELADO"||!HasItemStatuses(db,tx))return;
        foreach(var item in order.Items){string from=ItemStatus(order,item);if(CanAdvanceItem(from,target))PutItemStatus(db,tx,order.Id,item.Position??0,target,now);}
    }
    internal KitchenOrder ChangeItemStatus(Guid id,int position,long expectedRevision,string status,string actor,Guid operationId)
    {
        if(!Auth.HasPermission("sales")||operationId==Guid.Empty||position<0||string.IsNullOrWhiteSpace(actor)||actor.Length>160)throw new InvalidOperationException("Operação de status inválida ou sem permissão.");
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{id,position,expectedRevision,status,actor}))));string operation="item-status:"+operationId.ToString("D");
        using var db=open();using var tx=db.BeginTransaction();if(!HasItemStatuses(db,tx))throw new InvalidOperationException("Abra a Cozinha atualizada no servidor.");
        using(var q=Command(db,tx,"SELECT request_hash FROM kitchen_events_v1 WHERE operation_id=$op",("$op",operation))){var old=Convert.ToString(q.ExecuteScalar());if(!string.IsNullOrEmpty(old)){if(old!=hash)throw new InvalidOperationException("Operação reutilizada com outros dados.");var existing=Read(db,tx,id);tx.Commit();return existing;}}
        var order=Read(db,tx,id);var item=order.Items.SingleOrDefault(i=>i.Position==position)??throw new InvalidOperationException("Produto já pago, cancelado ou removido.");
        if(order.Paid||order.TableOnly||order.Status=="CANCELADO"||order.Revision!=expectedRevision||!CanAdvanceItem(ItemStatus(order,item),status))throw new InvalidOperationException("Produto alterado ou transição inválida. Atualize a mesa.");
        string now=Stamp(DateTimeOffset.UtcNow);
        // Freeze every sibling's current state before changing the aggregate fallback.
        foreach(var sibling in order.Items)PutItemStatus(db,tx,id,sibling.Position??0,ItemStatus(order,sibling),now,true);
        PutItemStatus(db,tx,id,position,status,now);
        var result=Read(db,tx,id);
        using(var q=Command(db,tx,"UPDATE kitchen_orders_v1 SET status=$s,revision=revision+1,updated_utc=$now WHERE id=$id AND revision=$r",("$s",result.Status),("$now",now),("$id",id.ToString("D")),("$r",expectedRevision)))if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Pedido alterado por outro terminal.");
        using(var q=Command(db,tx,"INSERT INTO kitchen_events_v1(operation_id,order_id,status,at_utc,operator,request_hash) VALUES($op,$id,$s,$now,$actor,$hash)",("$op",operation),("$id",id.ToString("D")),("$s","ITEM_"+status+":"+position.ToString(CultureInfo.InvariantCulture)),("$now",now),("$actor",actor),("$hash",hash)))q.ExecuteNonQuery();
        result=Read(db,tx,id);tx.Commit();return result;
    }
}
