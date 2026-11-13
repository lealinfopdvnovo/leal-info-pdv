using Microsoft.Data.Sqlite;
var directory=Path.GetFullPath(args[0]);Directory.CreateDirectory(directory);
using(var db=new SqliteConnection("Data Source="+Path.Combine(directory,"customer.db"))){db.Open();using var q=db.CreateCommand();q.CommandText="CREATE TABLE qa_stock(id INTEGER PRIMARY KEY,quantity INTEGER);INSERT INTO qa_stock VALUES(1,77);CREATE TABLE qa_cash(id INTEGER PRIMARY KEY,amount INTEGER);INSERT INTO qa_cash VALUES(1,1250);";q.ExecuteNonQuery();}
File.WriteAllText(Path.Combine(directory,"license.protected"),"QA-OPAQUE-PRESERVATION-MARKER-NOT-A-LICENSE");
File.WriteAllText(Path.Combine(directory,"network.config"),"QA-SYNTHETIC-NONOPERATIONAL");
Console.WriteLine("PASS synthetic SQLite stock/cash dataset created; no customer data or activation license");
