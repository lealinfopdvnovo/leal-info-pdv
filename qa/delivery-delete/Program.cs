using System.Reflection;
using LealInfoPDV;
internal static class Program
{
    [STAThread] private static void Main()
    {
        Database.Initialize();
        var type=typeof(Database).Assembly.GetType("LealInfoPDV.DeliveryManagementForm",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        type.GetMethod("EnsureTrackingSchema",flags)!.Invoke(null,null);
        var insert=type.GetMethod("InsertDeliveryWithTracking",flags)!;
        long Create()=>(long)insert.Invoke(null,new object[]{DateTime.Now.ToString("s"),"QA EXCLUSAO","","ENDEREÇO FICTÍCIO","","TESTE",0d,0d,"TESTE",DBNull.Value,"QA","QA"})!;
        string Code(long id){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText="SELECT code FROM delivery_tracking WHERE delivery_id=$id";q.Parameters.AddWithValue("$id",id);return Convert.ToString(q.ExecuteScalar())!;}
        bool Exists(string table,long id){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText="SELECT COUNT(*) FROM "+table+" WHERE "+(table=="deliveries"?"id":"delivery_id")+"=$id";q.Parameters.AddWithValue("$id",id);return Convert.ToInt64(q.ExecuteScalar())==1;}
        void Check(bool value){if(!value)throw new Exception("Exclusão afetou registro incorreto.");}
        var a=Create();var b=Create();var ca=Code(a);var cb=Code(b);var remove=type.GetMethod("DeleteLocalDelivery",flags)!;
        try{remove.Invoke(null,new object[]{a,cb});throw new Exception("Aceitou ID e código de pedidos diferentes.");}catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException){}
        Check(Exists("deliveries",a)&&Exists("deliveries",b)&&Exists("delivery_tracking",a)&&Exists("delivery_tracking",b));
        remove.Invoke(null,new object[]{a,ca});
        Check(!Exists("deliveries",a)&&!Exists("delivery_tracking",a)&&Exists("deliveries",b)&&Exists("delivery_tracking",b)&&Code(b)==cb);
        try{remove.Invoke(null,new object[]{a,ca});throw new Exception("Excluiu novamente.");}catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException){}
        Check(Exists("deliveries",b));
        Console.WriteLine("PASS: somente ID+código selecionados excluídos; outro pedido preservado; divergência e repetição bloqueadas.");
    }
}
