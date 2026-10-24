using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
var results=new List<object>();
foreach(var scenario in new[]{"empty","sale-only","manual-only","mixed-sale-first","mixed-manual-first","invalid-amount","null-amount","empty-amount"}){
 using var c=new SqliteConnection("Data Source=:memory:");c.Open();
 using(var setup=c.CreateCommand()){setup.CommandText="CREATE TABLE cash_movements(id INTEGER,amount REAL,sale_id INTEGER);";setup.ExecuteNonQuery();}
 var rows=scenario switch{
 "sale-only"=>new[]{"(1,12.5,7)"},
 "manual-only"=>new[]{"(1,12.5,NULL)"},
 "mixed-sale-first"=>new[]{"(1,12.5,7)","(2,10,NULL)"},
 "mixed-manual-first"=>new[]{"(1,10,NULL)","(2,12.5,7)"},
 "invalid-amount"=>new[]{"(1,'abc',7)","(2,10,NULL)"},
 "empty-amount"=>new[]{"(1,'',7)","(2,10,NULL)"},
 "null-amount"=>new[]{"(1,NULL,7)","(2,10,NULL)"},
 _=>Array.Empty<string>()};
 foreach(var row in rows){using var insert=c.CreateCommand();insert.CommandText="INSERT INTO cash_movements VALUES"+row;insert.ExecuteNonQuery();}
 foreach(var variant in new[]{"original","text-candidate","null-candidate"}){
  var sql=variant=="null-candidate"?"SELECT id AS Código,amount AS Valor,sale_id AS Venda FROM cash_movements ORDER BY id":variant=="original"?"SELECT id AS Código,amount AS Valor,COALESCE(sale_id,'') AS Venda FROM cash_movements ORDER BY id":"SELECT id AS Código,amount AS Valor,COALESCE(CAST(sale_id AS TEXT),'') AS Venda FROM cash_movements ORDER BY id";
  try{using var q=c.CreateCommand();q.CommandText=sql;using var rd=q.ExecuteReader();var dt=new DataTable();if(variant=="null-candidate"){dt.Columns.Add("Código",typeof(long));dt.Columns.Add("Valor",rd.GetFieldType(1));dt.Columns.Add("Venda",typeof(long));}dt.Load(rd);if(variant=="null-candidate"){foreach(DataRow row in dt.Rows)if(row["Venda"]!=DBNull.Value && Convert.ToInt64(row["Venda"])!=7)throw new Exception("Sale ID changed");if(dt.Rows.Count!=rows.Length)throw new Exception("Row loss");}results.Add(new{scenario,variant,success=true,columns=dt.Columns.Cast<DataColumn>().Select(x=>x.ColumnName+":"+x.DataType.Name).ToArray(),rows=dt.Rows.Count,dbNullSales=dt.Rows.Cast<DataRow>().Count(x=>x.IsNull("Venda"))});}
  catch(Exception e){results.Add(new{scenario,variant,success=false,type=e.GetType().FullName,error=e.Message,stack=e.StackTrace});}
 }
 using var sum=c.CreateCommand();sum.CommandText="SELECT COALESCE(SUM(amount),0) FROM cash_movements";
 var value=sum.ExecuteScalar();results.Add(new{scenario,variant="QueryValue",success=true,value=Convert.ToDouble(value??0,System.Globalization.CultureInfo.InvariantCulture)});
}
Directory.CreateDirectory("evidence");File.WriteAllText("evidence/results.json",JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(File.ReadAllText("evidence/results.json"));
