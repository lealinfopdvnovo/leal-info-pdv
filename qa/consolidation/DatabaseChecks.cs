using LealInfoPDV;
using System.Text.Json;
static partial class Tests
{
 static string DatabaseState(){
  using var db=Database.Open();var tables=new List<string>();
  using(var q=db.CreateCommand()){q.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";using var r=q.ExecuteReader();while(r.Read())tables.Add(r.GetString(0));}
  var content=new Dictionary<string,List<object?[]>>();
  foreach(var table in tables){using var q=db.CreateCommand();q.CommandText="SELECT * FROM \""+table.Replace("\"","\"\"")+"\" ORDER BY rowid";using var r=q.ExecuteReader();var rows=new List<object?[]>();while(r.Read()){var row=new object?[r.FieldCount];for(int i=0;i<r.FieldCount;i++)row[i]=r.IsDBNull(i)?null:r.GetValue(i);rows.Add(row);}content[table]=rows;}
  return JsonSerializer.Serialize(content);
 }
 static void DatabaseChecks(string id){
  var state=DatabaseState();Database.Initialize();Check(DatabaseState()==state,"reinitialization preserves every existing table record in populated fixture");
  string backup=Path.GetFullPath("evidence/"+id+"/fixture-backup.db");
  using(var source=Database.Open())using(var copy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+backup+";Pooling=False")){copy.Open();source.BackupDatabase(copy);}
  using(var copy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+backup+";Mode=ReadOnly;Pooling=False")){copy.Open();using var q=copy.CreateCommand();q.CommandText="PRAGMA quick_check";Check(Convert.ToString(q.ExecuteScalar())=="ok","SQLite online backup integrity valid with populated business tables");q.CommandText="SELECT stock FROM products WHERE id=901";Check(Convert.ToDouble(q.ExecuteScalar())==77,"restorable online backup retains existing inventory");}
  Check(DatabaseState()==state,"backup does not modify current data or credentials");
  Sql("PRAGMA wal_checkpoint(TRUNCATE)");
 }
}
