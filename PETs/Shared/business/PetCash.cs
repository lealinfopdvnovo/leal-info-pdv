using System.Globalization;
namespace LealInfoPDV;
internal static class PetCash
{
    internal static long RegisterForGlass(string type,string description,decimal amount,GlassCashOrigin origin)=>RegisterCore(type,description,amount,origin);
    private static long RegisterCore(string type,string description,decimal amount,GlassCashOrigin? origin)
    {
        if(type!="ENTRADA"&&type!="SAÍDA")throw new ArgumentException("Selecione ENTRADA ou SAÍDA.");
        if(string.IsNullOrWhiteSpace(description))throw new ArgumentException("Informe a descrição/motivo.");
        if(amount<=0||amount>999999999m||decimal.Round(amount,2)!=amount)throw new ArgumentException("Valor inválido.");
        if(origin!=null)GlassOperationsStore.EnsureReady();
        using var cn=Database.Open();using var tx=cn.BeginTransaction();
        if(origin!=null){var previous=GlassOperationsStore.ValidateCash(cn,tx,origin,type,amount);if(previous.HasValue){tx.Commit();return previous.Value;}}
        using var cmd=cn.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES($date,$type,$description,$amount,NULL); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.Parameters.AddWithValue("$type",type);
        cmd.Parameters.AddWithValue("$description",description.Trim());cmd.Parameters.AddWithValue("$amount",(double)amount);
        long id=Convert.ToInt64(cmd.ExecuteScalar());
        if(origin!=null)GlassOperationsStore.LinkCash(cn,tx,origin,id);
        tx.Commit();return id;
    }
}
