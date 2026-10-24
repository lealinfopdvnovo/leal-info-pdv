using System.Data;
using LealInfoPDV;
namespace GelamaqPet;
public sealed class FinanceExtension : IPetExtension, IFinancialMovementsPet
{
 public void Customize(System.Windows.Forms.Form form) { }
 public string PrepareSql(string sql)
 {
  const string old = "COALESCE(m.sale_id,'') AS Venda";
  if(!sql.Contains(old,StringComparison.Ordinal))throw new InvalidOperationException("Consulta financeira incompatível com PET Gelamaq.");
  return sql.Replace(old,"m.sale_id AS Venda",StringComparison.Ordinal);
 }
 public DataTable CreateTable()
 {
  var table=new DataTable();
  table.Columns.Add("Código",typeof(long));
  table.Columns.Add("Data",typeof(string));
  table.Columns.Add("Tipo",typeof(string));
  table.Columns.Add("Descrição",typeof(string));
  table.Columns.Add("Valor",typeof(double));
  table.Columns.Add("Venda",typeof(long)).AllowDBNull=true;
  return table;
 }
}
