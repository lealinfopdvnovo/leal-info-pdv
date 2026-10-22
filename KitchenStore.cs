using LealInfoPDV.Network;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;

namespace LealInfoPDV;

// Only kitchen_* tables are written. Production sales, payments and stock are never modified here.
internal sealed partial class KitchenStore
{
    private static readonly object schemaGate = new();
    private readonly Func<PdvConnection> open;
    private readonly bool canMigrate;
    internal KitchenStore(Func<PdvConnection>? connection = null, bool? allowMigration = null)
    {
        open = connection ?? Database.Open;
        canMigrate = allowMigration ?? NetworkConfiguration.Current.Mode != "terminal";
    }
    internal string? LastBackup { get; private set; }
    private static string Stamp(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset Time(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
    private static PdvCommand Command(PdvConnection db, PdvTransaction? tx, string sql, params (string, object?)[] values)
    {
        var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    private static string Identity(PdvConnection db, PdvTransaction? tx = null)
    {
        using var cmd = Command(db, tx, "SELECT store_id FROM kitchen_schema_v1 WHERE id=1 AND version=1");
        var id = Convert.ToString(cmd.ExecuteScalar());
        if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty) throw new InvalidDataException("Base da cozinha não validada. Abra a cozinha no servidor.");
        return id!;
    }
    internal string StoreId() { using var db = open(); return Identity(db); }
    internal string EnsureReady()
    {
        lock (schemaGate)
        {
            using var db = open();
            using var exists = Command(db, null, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='kitchen_schema_v1'");
            if (Convert.ToInt32(exists.ExecuteScalar()) == 1)
            {
                string id = Identity(db);
                using var tables = Command(db, null, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_orders_v1','kitchen_items_v1','kitchen_events_v1')");
                if (Convert.ToInt32(tables.ExecuteScalar()) != 3) throw new InvalidDataException("Estrutura da cozinha incompleta; nenhum reparo automático foi aplicado.");
                EnsureConsumptionReady();EnsurePaymentsReady();EnsureItemStatusesReady();return id;
            }
            if (!canMigrate) throw new InvalidOperationException("Abra COZINHA primeiro no computador servidor para preparar a base com backup validado.");
            using (var check = Command(db, null, "PRAGMA quick_check"))
                if (Convert.ToString(check.ExecuteScalar()) != "ok") throw new InvalidDataException("Integridade do banco não confirmada; preparação cancelada.");
            Directory.CreateDirectory(Database.BackupFolder);
            string backup = Path.Combine(Database.BackupFolder, "ANTES_COZINHA_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N") + ".db");
            using (var snapshot = new SqliteConnection("Data Source=" + backup + ";Pooling=False"))
            {
                snapshot.Open(); db.BackupDatabase(snapshot);
                using var verify = snapshot.CreateCommand(); verify.CommandText = "PRAGMA quick_check";
                if (Convert.ToString(verify.ExecuteScalar()) != "ok") throw new InvalidDataException("Backup não validado; preparação cancelada.");
            }
            LastBackup = backup;
            using var tx = db.BeginTransaction();
            using var create = Command(db, tx, """
                CREATE TABLE kitchen_schema_v1(id INTEGER PRIMARY KEY CHECK(id=1),version INTEGER NOT NULL,store_id TEXT NOT NULL UNIQUE);
                CREATE TABLE kitchen_orders_v1(
                    id TEXT PRIMARY KEY,reference TEXT NOT NULL,notes TEXT NOT NULL,operator TEXT NOT NULL,
                    status TEXT NOT NULL CHECK(status IN ('AGUARDANDO','PREPARANDO','PRONTO','ENTREGUE','CANCELADO')),
                    revision INTEGER NOT NULL,created_utc TEXT NOT NULL,received_utc TEXT NOT NULL,updated_utc TEXT NOT NULL,
                    payload TEXT NOT NULL,payload_hash TEXT NOT NULL);
                CREATE TABLE kitchen_items_v1(
                    order_id TEXT NOT NULL,position INTEGER NOT NULL,description TEXT NOT NULL,quantity TEXT NOT NULL,notes TEXT NOT NULL,product_id INTEGER,
                    PRIMARY KEY(order_id,position),FOREIGN KEY(order_id) REFERENCES kitchen_orders_v1(id));
                CREATE TABLE kitchen_events_v1(operation_id TEXT PRIMARY KEY,order_id TEXT NOT NULL,status TEXT NOT NULL,at_utc TEXT NOT NULL,operator TEXT NOT NULL,
                    request_hash TEXT NOT NULL,FOREIGN KEY(order_id) REFERENCES kitchen_orders_v1(id));
                CREATE INDEX kitchen_orders_status_v1 ON kitchen_orders_v1(status,received_utc);
                INSERT INTO kitchen_schema_v1(id,version,store_id) VALUES(1,1,$id);
                """, ("$id", Guid.NewGuid().ToString("D")));
            create.ExecuteNonQuery(); tx.Commit();EnsureConsumptionReady();EnsurePaymentsReady();EnsureItemStatusesReady();return Identity(db);
        }
    }
    internal KitchenOrder Submit(string storeId, KitchenDraft draft)
    {
        draft.Validate(); if(draft.TableOnly&&!KitchenTablesView.TableNumber(draft.Reference).HasValue)throw new InvalidOperationException("Selecione uma mesa para adicionar consumo."); using var db = open(); using var tx = db.BeginTransaction();
        if (Identity(db, tx) != storeId) throw new InvalidOperationException("Pedido pertence a outra base. Pendência preservada.");
        string id = draft.Id.ToString("D");
        using (var prior = Command(db, tx, "SELECT payload_hash FROM kitchen_orders_v1 WHERE id=$id", ("$id", id)))
        {
            var hash = Convert.ToString(prior.ExecuteScalar());
            if (!string.IsNullOrEmpty(hash))
            {
                if (hash != draft.Hash) throw new InvalidOperationException("Identificador já usado com conteúdo diferente. Pedido anterior preservado.");
                var existing = Read(db, tx, draft.Id); tx.Commit(); return existing;
            }
        }
        if (KitchenTablesView.TableNumber(draft.Reference) is int tableNumber)
        {
            using var schema = Command(db, tx, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_tables_v2','kitchen_table_settings_v2')");
            if (Convert.ToInt32(schema.ExecuteScalar()) == 2)
            {
                using var reservation = Command(db, tx, "SELECT reserved FROM kitchen_tables_v2 WHERE number=$n", ("$n", tableNumber));
                if (Convert.ToInt32(reservation.ExecuteScalar() ?? 0) == 1)
                    throw new InvalidOperationException("Mesa reservada. Libere a reserva antes de enviar; a pendência permanece guardada.");
                using var capacity = Command(db, tx, "UPDATE kitchen_table_settings_v2 SET table_count=$n,revision=revision+1 WHERE id=1 AND table_count<$n", ("$n", tableNumber));
                capacity.ExecuteNonQuery();
            }
        }
        string now = Stamp(DateTimeOffset.UtcNow);
        using (var add = Command(db, tx, """
            INSERT INTO kitchen_orders_v1(id,reference,notes,operator,status,revision,created_utc,received_utc,updated_utc,payload,payload_hash)
            VALUES($id,$reference,$notes,$operator,$status,1,$created,$now,$now,$payload,$hash)
            """, ("$id", id), ("$reference", draft.Reference), ("$notes", draft.Notes), ("$operator", draft.Operator),
            ("$status",draft.TableOnly?"ENTREGUE":"AGUARDANDO"),("$created", Stamp(draft.CreatedUtc)), ("$now", now), ("$payload", draft.Json), ("$hash", draft.Hash))) add.ExecuteNonQuery();
        for (int i = 0; i < draft.Items.Length; i++)
        {
            var item = draft.Items[i];
            using var add = Command(db, tx, "INSERT INTO kitchen_items_v1(order_id,position,description,quantity,notes,product_id) VALUES($id,$p,$d,$q,$n,$product)",
                ("$id", id), ("$p", i), ("$d", item.Description), ("$q", item.Quantity.ToString("G29", CultureInfo.InvariantCulture)), ("$n", item.Notes), ("$product", item.ProductId));
            add.ExecuteNonQuery();
        }
        using (var record = Command(db, tx, "INSERT INTO kitchen_events_v1(operation_id,order_id,status,at_utc,operator,request_hash) VALUES($op,$id,$status,$now,$operator,$hash)",
            ("$status",draft.TableOnly?"ADICIONADO_MESA":"AGUARDANDO"),("$op", "submit:" + id), ("$id", id), ("$now", now), ("$operator", draft.Operator), ("$hash", draft.Hash))) record.ExecuteNonQuery();
        CaptureConsumption(db,tx,draft);tx.Commit(); return Get(draft.Id);
    }
    private static KitchenOrder Read(PdvConnection db, PdvTransaction? tx, Guid id)
    {
        KitchenOrder result;
        using (var cmd = Command(db, tx, "SELECT reference,notes,operator,status,revision,created_utc,received_utc,updated_utc FROM kitchen_orders_v1 WHERE id=$id", ("$id", id.ToString("D"))))
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read()) throw new InvalidOperationException("Pedido não encontrado.");
            result = new(id, r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), Time(r.GetString(5)), Time(r.GetString(6)), Time(r.GetString(7)), Array.Empty<KitchenItem>());
        }
        bool enhanced=HasConsumption(db,tx);bool tableOnly=false,paid=false;
        if(enhanced){using var mode=Command(db,tx,"SELECT table_only FROM kitchen_consumption_orders_v3 WHERE order_id=$id",("$id",id.ToString("D")));tableOnly=Convert.ToInt32(mode.ExecuteScalar()??0)==1;}
        using(var payment=Command(db,tx,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='kitchen_paid_orders_v1'"))if(Convert.ToInt32(payment.ExecuteScalar())==1){using var q=Command(db,tx,"SELECT COUNT(*) FROM kitchen_paid_orders_v1 WHERE order_id=$id",("$id",id.ToString("D")));paid=Convert.ToInt32(q.ExecuteScalar())>0;}
        var lineStates=new Dictionary<int,string>();
        if(HasItemStatuses(db,tx)){using var q=Command(db,tx,"SELECT position,status FROM kitchen_item_status_v5 WHERE order_id=$id",("$id",id.ToString("D")));using var r=q.ExecuteReader();while(r.Read())lineStates[r.GetInt32(0)]=r.GetString(1);}
        var items=new List<KitchenItem>();
        string sql=enhanced?"SELECT i.description,i.quantity,i.notes,i.product_id,i.position,p.unit_price FROM kitchen_items_v1 i LEFT JOIN kitchen_consumption_prices_v3 p ON p.order_id=i.order_id AND p.position=i.position WHERE i.order_id=$id AND CAST(i.quantity AS NUMERIC)>0 AND NOT EXISTS(SELECT 1 FROM kitchen_item_cancellations_v3 c WHERE c.order_id=i.order_id AND c.position=i.position) ORDER BY i.position":"SELECT description,quantity,notes,product_id,position,NULL FROM kitchen_items_v1 WHERE order_id=$id AND CAST(quantity AS NUMERIC)>0 ORDER BY position";
        using var itemCmd=Command(db,tx,sql,("$id",id.ToString("D")));using var rows=itemCmd.ExecuteReader();
        while(rows.Read())items.Add(new(rows.GetString(0),decimal.Parse(rows.GetString(1),CultureInfo.InvariantCulture),rows.GetString(2),rows.IsDBNull(3)?null:rows.GetInt64(3),rows.IsDBNull(5)?null:decimal.Parse(rows.GetString(5),CultureInfo.InvariantCulture),rows.GetInt32(4)));
        var active=items.Select(i=>i with{LineBalance=RemainingAmount(db,tx,id,i.Position??0),PreparationStatus=tableOnly?"ENTREGUE":lineStates.GetValueOrDefault(i.Position??0,result.Status)}).ToArray();
        var prepared=result with{Items=active,TableOnly=tableOnly,Paid=paid};return prepared with{Status=AggregateItemStatus(prepared,active)};
    }

    internal KitchenOrder Get(Guid id) { using var db = open(); return Read(db, null, id); }
    internal IReadOnlyList<KitchenOrder> List()
    {
        using var db = open(); var ids = new List<Guid>();
        using (var cmd = Command(db, null, "SELECT id FROM kitchen_orders_v1 ORDER BY received_utc DESC,id LIMIT 500"))
        using (var rows = cmd.ExecuteReader()) while (rows.Read()) ids.Add(Guid.Parse(rows.GetString(0)));
        return ids.Select(id => Read(db, null, id)).ToArray();
    }
    // Table occupancy must include every active order, even after 500 newer completed orders.
    internal IReadOnlyList<KitchenOrder> ListActive()
    {
        using var db = open(); var ids = new List<Guid>();
        using (var cmd = Command(db, null, "SELECT o.id FROM kitchen_orders_v1 o LEFT JOIN kitchen_paid_orders_v1 p ON p.order_id=o.id WHERE o.status!='CANCELADO' AND p.order_id IS NULL ORDER BY o.received_utc,o.id"))
        using (var rows = cmd.ExecuteReader()) while (rows.Read()) ids.Add(Guid.Parse(rows.GetString(0)));
        return ids.Select(id => Read(db, null, id)).ToArray();
    }
    internal IReadOnlyList<KitchenEvent> History(Guid id)
    {
        using var db = open(); using var cmd = Command(db, null, "SELECT operation_id,status,at_utc,operator FROM kitchen_events_v1 WHERE order_id=$id ORDER BY rowid", ("$id", id.ToString("D")));
        using var rows = cmd.ExecuteReader(); var result = new List<KitchenEvent>();
        while (rows.Read()) result.Add(new(rows.GetString(0), rows.GetString(1), Time(rows.GetString(2)), rows.GetString(3)));
        return result;
    }
    internal KitchenOrder ChangeStatus(Guid id, long expectedRevision, string status, string actor, Guid operationId)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(actor) || actor.Length > 160) throw new InvalidDataException("Operador ou operação inválidos.");
        string request = JsonSerializer.Serialize(new { id, expectedRevision, status, actor });
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request)));
        using var db = open(); using var tx = db.BeginTransaction();
        using (var prior = Command(db, tx, "SELECT request_hash FROM kitchen_events_v1 WHERE operation_id=$op", ("$op", operationId.ToString("D"))))
        {
            var found = Convert.ToString(prior.ExecuteScalar());
            if (!string.IsNullOrEmpty(found))
            {
                if (found != hash) throw new InvalidOperationException("Operação reutilizada com dados diferentes.");
                var previous = Read(db, tx, id); tx.Commit(); return previous;
            }
        }
        var order = Read(db, tx, id);
        if(order.Paid||order.TableOnly)throw new InvalidOperationException("Pedido já pago ou consumo sem preparo. Use cancelar item para consumo ativo.");
        bool allowed = (order.Status, status) is ("AGUARDANDO", "PREPARANDO") or ("PREPARANDO", "PRONTO") or ("PRONTO", "ENTREGUE") or
            ("AGUARDANDO", "CANCELADO") or ("PREPARANDO", "CANCELADO") or ("PRONTO", "CANCELADO");
        if (order.Revision != expectedRevision || !allowed) throw new InvalidOperationException("Pedido mudou ou transição inválida. Atualize a lista antes de tentar novamente.");
        string now = Stamp(DateTimeOffset.UtcNow);
        SyncOrderItemStatuses(db,tx,order,status,now);
        using (var change = Command(db, tx, "UPDATE kitchen_orders_v1 SET status=$s,revision=revision+1,updated_utc=$now WHERE id=$id AND revision=$r", ("$s", status), ("$now", now), ("$id", id.ToString("D")), ("$r", expectedRevision)))
            if (change.ExecuteNonQuery() != 1) throw new InvalidOperationException("Pedido atualizado por outro terminal.");
        using (var record = Command(db, tx, "INSERT INTO kitchen_events_v1(operation_id,order_id,status,at_utc,operator,request_hash) VALUES($op,$id,$s,$now,$actor,$hash)",
            ("$op", operationId.ToString("D")), ("$id", id.ToString("D")), ("$s", status), ("$now", now), ("$actor", actor), ("$hash", hash))) record.ExecuteNonQuery();
        tx.Commit(); return Get(id);
    }
}
