using System.Globalization;
using Microsoft.Data.Sqlite;
namespace LealInfoPDV;
internal static class CashFlow
{
    internal static readonly CultureInfo Brazilian=CultureInfo.GetCultureInfo("pt-BR");
    internal const string HistorySql="SELECT id AS ID,substr(occurred_at,1,10) AS Data,substr(occurred_at,12,8) AS Hora,type AS Tipo,description AS Descrição,printf('R$ %.2f',amount) AS Valor FROM cash_movements ORDER BY id DESC";
    internal static decimal ParseAmount(string text)
    {
        if(!decimal.TryParse(text.Trim().Replace("R$","").Trim(),NumberStyles.Number,Brazilian,out var amount)||amount<=0||amount>999999999m||decimal.Round(amount,2)!=amount)
            throw new ArgumentException("Informe um valor positivo, maior que zero, com no máximo duas casas decimais.");
        return amount;
    }
    internal static long Register(string type,string description,decimal amount)
    {
        if(type!="ENTRADA"&&type!="SAÍDA")throw new ArgumentException("Selecione ENTRADA ou SAÍDA.");
        if(string.IsNullOrWhiteSpace(description))throw new ArgumentException("Informe a descrição/motivo.");
        if(amount<=0||amount>999999999m||decimal.Round(amount,2)!=amount)throw new ArgumentException("Valor inválido.");
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES($date,$type,$description,$amount,NULL); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.Parameters.AddWithValue("$type",type);
        cmd.Parameters.AddWithValue("$description",description.Trim());cmd.Parameters.AddWithValue("$amount",(double)amount);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
    internal sealed record Summary(decimal Opening,decimal Receipts,decimal ManualEntries,decimal ManualExits,decimal OtherExits)
    {
        public decimal Final=>Opening+Receipts+ManualEntries-ManualExits-OtherExits;
        public string Display=>$"Saldo inicial: {Opening.ToString("C2",Brazilian)}\nVendas/recebimentos: + {Receipts.ToString("C2",Brazilian)}\nEntradas manuais: + {ManualEntries.ToString("C2",Brazilian)}\nSaídas manuais: - {ManualExits.ToString("C2",Brazilian)}\n"+(OtherExits==0?"":$"Outras saídas: - {OtherExits.ToString("C2",Brazilian)}\n")+$"\nSALDO FINAL: {Final.ToString("C2",Brazilian)}";
    }
    internal static Summary ReadSummary(DateTime start,DateTime endExclusive)
    {
        using var cn=Database.Open();using var cmd=cn.CreateCommand();
        cmd.CommandText="""
            WITH ledger AS (
              SELECT occurred_at,amount,sale_id,
                CASE WHEN upper(replace(replace(type,'í','I'),'Í','I')) LIKE '%SAIDA%' THEN 1 ELSE 0 END AS outgoing
              FROM cash_movements WHERE occurred_at < $to
            )
            SELECT
              COALESCE(SUM(CASE WHEN occurred_at<$from THEN CASE WHEN outgoing=1 THEN -amount ELSE amount END ELSE 0 END),0),
              COALESCE(SUM(CASE WHEN occurred_at>=$from AND outgoing=0 AND sale_id IS NOT NULL THEN amount ELSE 0 END),0),
              COALESCE(SUM(CASE WHEN occurred_at>=$from AND outgoing=0 AND sale_id IS NULL THEN amount ELSE 0 END),0),
              COALESCE(SUM(CASE WHEN occurred_at>=$from AND outgoing=1 AND sale_id IS NULL THEN amount ELSE 0 END),0),
              COALESCE(SUM(CASE WHEN occurred_at>=$from AND outgoing=1 AND sale_id IS NOT NULL THEN amount ELSE 0 END),0)
            FROM ledger
            """;
        cmd.Parameters.AddWithValue("$from",start.ToString("yyyy-MM-dd HH:mm:ss"));cmd.Parameters.AddWithValue("$to",endExclusive.ToString("yyyy-MM-dd HH:mm:ss"));
        using var rd=cmd.ExecuteReader();rd.Read();
        decimal Value(int i)=>decimal.Round(Convert.ToDecimal(rd.GetValue(i),CultureInfo.InvariantCulture),2);
        return new(Value(0),Value(1),Value(2),Value(3),Value(4));
    }
}
