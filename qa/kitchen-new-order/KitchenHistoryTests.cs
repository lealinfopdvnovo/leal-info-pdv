using LealInfoPDV;
using System.Reflection;

internal static class KitchenHistoryTests
{
    internal static void Run(KitchenForm form,Action<bool,string> check,Action<Func<bool>> pump)
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Disposable QA profile only");
        var store=new KitchenStore();
        var draft=new KitchenDraft(Guid.NewGuid(),"QA PENDENTE ANTIGO","","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("PEDIDO A PRESERVAR",1,"")});
        var pending=store.Submit(store.StoreId(),draft);
        using(var db=Database.Open())
        using(var tx=db.BeginTransaction())
        {
            using(var old=db.CreateCommand()){old.Transaction=tx;old.CommandText="UPDATE kitchen_orders_v1 SET received_utc=$time WHERE id=$id";old.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));old.Parameters.AddWithValue("$id",pending.Id.ToString("D"));old.ExecuteNonQuery();}
            // Synthetic completed archive rows; complete item payloads, written atomically only in CI.
            for(int i=0;i<501;i++)
            {
                var record=new KitchenDraft(Guid.NewGuid(),"QA ARQUIVO "+i,"","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem("HISTÓRICO SINTÉTICO",1,"")});
                using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="""
                    INSERT INTO kitchen_orders_v1(id,reference,notes,operator,status,revision,created_utc,received_utc,updated_utc,payload,payload_hash)
                    VALUES($id,$ref,'','QA','ENTREGUE',4,$time,$time,$time,$payload,$hash);
                    INSERT INTO kitchen_items_v1(order_id,position,description,quantity,notes,product_id) VALUES($id,0,'HISTÓRICO SINTÉTICO','1','',NULL);
                    """;
                q.Parameters.AddWithValue("$id",record.Id.ToString("D"));q.Parameters.AddWithValue("$ref",record.Reference);q.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O"));q.Parameters.AddWithValue("$payload",record.Json);q.Parameters.AddWithValue("$hash",record.Hash);q.ExecuteNonQuery();
            }
            tx.Commit();
        }
        check(!store.List().Any(o=>o.Id==pending.Id)&&store.ListActive().Any(o=>o.Id==pending.Id),"old pending order survives beyond the 500-row history window");
        var refresh=(Task)typeof(KitchenForm).GetMethod("RefreshOrders",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,null)!;
        pump(()=>refresh.IsCompleted);refresh.GetAwaiter().GetResult();
        var cards=(FlowLayoutPanel)typeof(KitchenForm).GetField("orderCards",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;
        check(cards.Controls.OfType<Panel>().Any(c=>Equals(c.Tag,pending.Id)),"old pending order remains visible and actionable in premium cards");
        foreach(string status in new[]{"PREPARANDO","PRONTO","ENTREGUE"})pending=store.ChangeStatus(pending.Id,pending.Revision,status,"QA",Guid.NewGuid());
    }
}
