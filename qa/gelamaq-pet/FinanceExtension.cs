using System.Data;
using LealInfoPDV;
namespace GelamaqPet;
public sealed class FinanceExtension : IPetExtension, IFinancialMovementsPet
{
 public void Customize(System.Windows.Forms.Form form) { }
 public string PrepareSql(string sql)
 {
  const string old="COALESCE(m.sale_id,'') AS Venda";
  if(!sql.Contains(old,StringComparison.Ordinal))throw new InvalidOperationException("Consulta financeira incompatível com PET Gelamaq.");
  return sql.Replace(old,"m.sale_id AS Venda",StringComparison.Ordinal);
 }
 public DataTable ReadTable(IDataReader reader)
 {
  var table=new DataTable();
  for(int i=0;i<reader.FieldCount;i++)
   table.Columns.Add(reader.GetName(i),reader.GetName(i)=="Venda"?typeof(long):reader.GetFieldType(i)).AllowDBNull=true;
  while(reader.Read()) {
   var values=new object[reader.FieldCount];reader.GetValues(values);table.Rows.Add(values);
  }
  return table;
 }
}
