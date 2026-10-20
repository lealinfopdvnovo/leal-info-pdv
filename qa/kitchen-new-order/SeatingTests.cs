using LealInfoPDV;
using Microsoft.Data.Sqlite;

internal static class SeatingTests
{
    internal static void Run(KitchenStore store, Action<bool,string> check)
    {
        void Reject(Action action,string reason)
        {
            bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}catch(SqliteException){rejected=true;}
            check(rejected,reason);
        }
        check(store.LastSeatingBackup!=null&&File.Exists(store.LastSeatingBackup),"seating migration creates an actual pre-migration backup");
        using(var backup=new SqliteConnection("Data Source="+store.LastSeatingBackup+";Mode=ReadOnly;Pooling=False"))
        {
            backup.Open();using var q=backup.CreateCommand();q.CommandText="PRAGMA quick_check";check(Convert.ToString(q.ExecuteScalar())=="ok","seating backup integrity verified independently");
            q.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name='kitchen_tables_v2'";check(Convert.ToInt32(q.ExecuteScalar())==0,"backup precedes the new seating schema");
        }
        var initial=store.Seating();check(initial.Count==40,"new seating defaults to forty tables");
        var reserve=store.ReserveTable(10,0,true,"QA RESERVA","QA").Reservations.Single(r=>r.Number==10);
        check(new KitchenStore().Seating().Reservations.Single(r=>r.Number==10).Reserved,"reservation persists across store instances");
        Reject(()=>store.ReserveTable(10,0,false,"","QA"),"stale reservation revision cannot overwrite another terminal");
        int orderCount=store.List().Count;
        var blocked=new KitchenDraft(Guid.NewGuid(),"Mesa 10","","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("RESERVA QA",1,"")});
        Reject(()=>store.Submit(store.StoreId(),blocked),"reserved table rejects new order without losing its draft");
        check(store.List().Count==orderCount,"reserved-table rejection leaves existing orders intact");
        store.ReserveTable(10,reserve.Revision,false,"","QA");
        var expanded=store.ChangeTableCount(60,initial.Revision);
        check(new KitchenStore().Seating().Count==60,"table capacity persists across store instances");
        Reject(()=>store.ChangeTableCount(70,initial.Revision),"stale table configuration is rejected");
        var high=store.ReserveTable(55,0,true,"QA","QA");
        Reject(()=>store.ChangeTableCount(40,expanded.Revision),"capacity cannot hide reserved tables");
        store.ReserveTable(55,high.Reservations.Single(r=>r.Number==55).Revision,false,"","QA");
        var draft=new KitchenDraft(Guid.NewGuid(),"Mesa 39","","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("OCUPADA QA",1,"")});
        var order=store.Submit(store.StoreId(),draft);
        Reject(()=>store.ReserveTable(39,0,true,"","QA"),"occupied table cannot be reserved");
        Reject(()=>store.ChangeTableCount(10,store.Seating().Revision),"capacity cannot hide occupied tables");
        foreach(string status in new[]{"PREPARANDO","PRONTO","ENTREGUE"})order=store.ChangeStatus(order.Id,order.Revision,status,"QA",Guid.NewGuid());
        var repeated=store.ReserveTable(39,0,true,"","QA").Reservations.Single(r=>r.Number==39);
        check(store.Submit(store.StoreId(),draft).Id==order.Id,"confirmed order retry remains idempotent after a later reservation");
        store.ReserveTable(39,repeated.Revision,false,"","QA");
        store.ChangeTableCount(40,store.Seating().Revision);
        var before=System.Text.Json.JsonSerializer.Serialize(store.Seating());
        using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="CREATE TRIGGER qa_seating_abort BEFORE INSERT ON kitchen_tables_v2 WHEN NEW.number=29 BEGIN SELECT RAISE(ABORT,'QA seating rollback'); END";q.ExecuteNonQuery();}
        try{Reject(()=>store.ReserveTable(29,0,true,"","QA"),"real database failure rolls back reservation transaction");}
        finally{using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="DROP TRIGGER qa_seating_abort";q.ExecuteNonQuery();}
        check(System.Text.Json.JsonSerializer.Serialize(store.Seating())==before,"reservation transaction rollback preserves configuration and previous reservations");
        Reject(()=>store.ReserveTable(0,0,true,"","QA"),"invalid table number rejected");
    }
}
