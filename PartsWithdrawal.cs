using System.Data;
namespace LealInfoPDV;
internal static class PartsWithdrawal
{
    internal const string HistorySql="SELECT id AS ID,substr(occurred_at,1,10) AS Data,substr(occurred_at,12,8) AS Hora,CASE WHEN customer_name='' THEN 'RETIRADA AVULSA' ELSE customer_name END AS Cliente,collected_by AS 'Retirado por',description AS Descrição FROM parts_withdrawals ORDER BY occurred_at DESC,id DESC";
    internal static DataTable History()=>Query(HistorySql,"");
    internal static DataTable FindCustomers(string term)=>Query("SELECT id AS ID,name AS Nome,COALESCE(document,'') AS Documento,COALESCE(phone,'') AS Telefone FROM customers WHERE name LIKE $q OR document LIKE $q OR phone LIKE $q OR CAST(id AS TEXT) LIKE $q ORDER BY name LIMIT 200",term);
    private static DataTable Query(string sql,string term)
    {
        using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText=sql;
        if(sql.Contains("$q"))cmd.Parameters.AddWithValue("$q","%"+term.Trim()+"%");
        using var rd=cmd.ExecuteReader();var table=new DataTable();table.Load(rd);return table;
    }
    internal static long Register(string description,long? customerId,string collectedBy,DateTime occurredAt)
    {
        if(string.IsNullOrWhiteSpace(description)||string.IsNullOrWhiteSpace(collectedBy))throw new ArgumentException("Informe a descrição e o nome de quem retirou.");
        using var cn=Database.Open();using var tx=cn.BeginTransaction();var customerName="";
        if(customerId.HasValue)
        {
            using var read=cn.CreateCommand();read.Transaction=tx;read.CommandText="SELECT name FROM customers WHERE id=$id";read.Parameters.AddWithValue("$id",customerId.Value);
            var value=read.ExecuteScalar();if(value==null||value==DBNull.Value)throw new ArgumentException("Cliente não encontrado. Busque novamente.");customerName=Convert.ToString(value)??"";
        }
        using var cmd=cn.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="INSERT INTO parts_withdrawals(occurred_at,description,customer_id,customer_name,collected_by,created_at) VALUES($at,$description,$customer,$name,$person,$created); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$at",occurredAt.ToString("yyyy-MM-dd HH:mm:ss"));cmd.Parameters.AddWithValue("$description",description.Trim());cmd.Parameters.AddWithValue("$customer",customerId.HasValue?(object)customerId.Value:DBNull.Value);cmd.Parameters.AddWithValue("$name",customerName);cmd.Parameters.AddWithValue("$person",collectedBy.Trim());cmd.Parameters.AddWithValue("$created",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        var id=Convert.ToInt64(cmd.ExecuteScalar());tx.Commit();return id;
    }
}
