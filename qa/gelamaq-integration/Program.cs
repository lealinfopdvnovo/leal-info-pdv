using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using Microsoft.Data.Sqlite;
class Tests {
 [STAThread] static int Main(string[] args) {
 var evidence=new List<object>(); Directory.CreateDirectory("evidence");
 try {
 var pet=new GelamaqPet.FinanceExtension();
 const string original="SELECT m.id AS Código, datetime(m.occurred_at) AS Data, CASE WHEN upper(m.type) LIKE '%SAÍDA%' THEN 'SAÍDA' ELSE 'ENTRADA' END AS Tipo, m.description AS Descrição, m.amount AS Valor, COALESCE(m.sale_id,'') AS Venda FROM cash_movements m ORDER BY m.id";
 foreach(var scenario in new[]{"empty","linked","manual","mixed","null-amount","empty-amount","invalid-amount"}) {
 using var cn=new SqliteConnection("Data Source=:memory:");cn.Open();
 using var cmd=cn.CreateCommand(); cmd.CommandText="CREATE TABLE cash_movements(id INTEGER,occurred_at TEXT,type TEXT,description TEXT,amount REAL,sale_id INTEGER)";cmd.ExecuteNonQuery();
 string rows=scenario switch {
 "empty"=>"", "linked"=>"(1,'2026-10-24','ENTRADA','Venda',12.5,7)",
 "manual"=>"(1,'2026-10-24','ENTRADA','Manual',8.5,NULL)",
 "mixed"=>"(1,'2026-10-24','ENTRADA','Venda',12.5,7),(2,'2026-10-24','SAÍDA','Manual',8.5,NULL)",
 "null-amount"=>"(1,'2026-10-24','ENTRADA','Nulo',NULL,NULL)",
 "empty-amount"=>"(1,'2026-10-24','ENTRADA','Vazio','',NULL)",
 _=>"(1,'2026-10-24','ENTRADA','Inválido','abc',NULL)" };
 if(rows!=""){cmd.CommandText="INSERT INTO cash_movements VALUES "+rows;cmd.ExecuteNonQuery();}
 cmd.CommandText=pet.PrepareSql(original);
 bool invalid=scenario is "empty-amount" or "invalid-amount"; bool rejected=false; DataTable dt;
 using(var reader=cmd.ExecuteReader())dt=pet.ReadTable(reader);
 if(invalid && Convert.ToString(dt.Rows[0]["Valor"])!=(scenario=="empty-amount"?"":"abc"))throw new Exception("Invalid monetary data changed");
 if(!invalid){
 if(dt.Columns["Venda"]!.DataType!=typeof(long))throw new Exception("Wrong sale type");
 if(scenario=="mixed"&&(dt.Rows.Count!=2||!dt.Rows[1].IsNull("Venda")||Convert.ToDouble(dt.Rows[0]["Valor"])+Convert.ToDouble(dt.Rows[1]["Valor"])!=21))throw new Exception("Rows or money changed");
 if(scenario=="manual"&&!dt.Rows[0].IsNull("Venda"))throw new Exception("Missing sale not DBNull");
 if(scenario=="null-amount"&&!dt.Rows[0].IsNull("Valor"))throw new Exception("Null money replaced");
 }
 evidence.Add(new{test=scenario,passed=true,invalidAmountRejected=rejected});
 }
 // The exact production SQL changes only its sale-column expression.
 if(pet.PrepareSql(original)!=original.Replace("COALESCE(m.sale_id,'') AS Venda","m.sale_id AS Venda"))throw new Exception("Unexpected SQL changes");
 var rt=typeof(IPetExtension).Assembly.GetType("LealInfoPDV.PetRuntime")!;
 var identity=rt.GetField("loadedIdentity",BindingFlags.NonPublic|BindingFlags.Static)!;
 var ext=rt.GetField("extension",BindingFlags.NonPublic|BindingFlags.Static)!;
 var property=rt.GetProperty("FinancialMovements",BindingFlags.NonPublic|BindingFlags.Static)!;
 ext.SetValue(null,pet);
 foreach(var client in new[]{"001","002","003","004","GLOBAL"}){
 identity.SetValue(null,client);
 if((property.GetValue(null)!=null)!=(client=="002"))throw new Exception("Cross-client hook");
 evidence.Add(new{test="client-hook-"+client,passed=true});
 }
 ext.SetValue(null,null);identity.SetValue(null,null);
 if(property.GetValue(null)!=null)throw new Exception("Default GLOBAL hook active");
 // Actual manifest/hash loader, isolated folder. No customer DB or license accessed.
 string folder=Path.Combine(Path.GetTempPath(),"gelamaq-pet-"+Guid.NewGuid());Directory.CreateDirectory(folder);
 try {
 string dll=typeof(GelamaqPet.FinanceExtension).Assembly.Location;
 File.Copy(dll,Path.Combine(folder,Path.GetFileName(dll)));
 var manifest=new{Identity="002",Version="1.3",ApiVersion=1,Assembly=Path.GetFileName(dll),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)))};
 File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonSerializer.Serialize(manifest));
 var load=rt.GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Static)!;
 bool wrong=false;try{load.Invoke(null,new object[]{folder,"003"});}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){wrong=true;}
 if(!wrong)throw new Exception("Wrong-client package accepted");
 File.AppendAllText(Path.Combine(folder,manifest.Assembly),"tamper");
 bool tamper=false;try{load.Invoke(null,new object[]{folder,"002"});}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){tamper=true;}
 if(!tamper)throw new Exception("Tampered assembly accepted");
 File.Copy(dll,Path.Combine(folder,manifest.Assembly),true);
 load.Invoke(null,new object[]{folder,"002"});
 if(property.GetValue(null)==null)throw new Exception("PET did not load");
 evidence.Add(new{test="real-loader-identity-hash-valid-load",passed=true});
 } finally{Directory.Delete(folder,true);}
 File.WriteAllText("evidence/integration.json",JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
 return 0;
 }catch(Exception ex){File.WriteAllText("evidence/failure.txt",ex.ToString());Console.Error.WriteLine(ex);return 1;}
 }
}
