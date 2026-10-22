using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
object? Call(string type,string method,params object?[] values)=>assembly.GetType("LealInfoPDV."+type,true)!.GetMethod(method,S)!.Invoke(null,values);
object Prop(object value,string name)=>value.GetType().GetProperty(name)!.GetValue(value)!;
var database=assembly.GetType("LealInfoPDV.Database",true)!;string db=(string)database.GetProperty("DbPath")!.GetValue(null)!;
if(args.Contains("--verify-rollback")){
 if(File.ReadAllText(Path.Combine(Path.GetDirectoryName(db)!,"MIGRATION-QA"))!=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"))throw new Exception("Wrong fixture");
 Call("CasaPetBootstrap","Initialize");
 var pet=assembly.GetType("LealInfoPDV.PetRuntime",true)!;
 if((string?)pet.GetProperty("CurrentVersion",S)!.GetValue(null)!="1.1")throw new Exception("Legacy profile did not load");
 var extension=pet.GetField("extension",S)!.GetValue(null)!;
 if(extension.GetType().Assembly.GetName().Name=="LealInfoPDV.Pet.Profile")throw new Exception("Legacy rollback loaded new profile");
 Console.WriteLine("PASS real legacy bootstrap loads its original embedded profile after rollback; new module is ignored");return;
}
if(File.Exists(db))throw new Exception("Fixture cannot overwrite existing database");
Call("Database","Initialize");File.WriteAllText(Path.Combine(Path.GetDirectoryName(db)!,"MIGRATION-QA"),Environment.GetEnvironmentVariable("GITHUB_RUN_ID"));
File.WriteAllText(Path.Combine(Path.GetDirectoryName(db)!,"angra-sync.identity"),"INVALID-OFFLINE-QA-IDENTITY"); // Deliberately invalid fixture prevents live gateway registration; no real identity is used.
Call("Auth","CreateUser","QA CASA","migrationqa","QA-migration-only!","ADMINISTRADOR","qa@example.invalid","",false);
using(var cn=new SqliteConnection("Data Source="+db)){cn.Open();using var q=cn.CreateCommand();q.CommandText="INSERT INTO products(id,name,price,stock,active) VALUES(901,'PRODUTO LEGADO',12.5,77,1);INSERT INTO customers(id,name) VALUES(901,'CLIENTE LEGADO');INSERT OR REPLACE INTO settings(key,value) VALUES('client_update_id','003'),('company_trade_name','EMPRESA LEGADA'),('smtp_host','PRESERVAR SMTP')";q.ExecuteNonQuery();}
var projectType=assembly.GetType("LealInfoPDV.GlassSavedProject",true)!;
string json=JsonSerializer.Serialize(new{FormatVersion=1,Model="BOX FRONTAL",Description="PROJETO LEGADO",WidthMm=1200,HeightMm=2000,WidthUnit="mm",HeightUnit="mm",Quantity=1,Quote=new{CustomerId=901,CustomerName="CLIENTE LEGADO",Service="INSTALAÇÃO",Price=100,ServiceValue=50,Discount=0,Addition=0},Materials=Array.Empty<object>(),Cut=(object?)null,Show3DMeasurements=true});
// Use the operational model constant and the exact record property names.
var model=(string)assembly.GetType("LealInfoPDV.GlassProject",true)!.GetField("ModelName",S)!.GetValue(null)!;
var node=System.Text.Json.Nodes.JsonNode.Parse(json)!;node["Model"]=model;
var project=JsonSerializer.Deserialize(node.ToJsonString(),projectType)!;
var saved=Call("GlassProjectStore","Save",project,null)!;string id=(string)Prop(saved,"Id");long revision=(long)Prop(saved,"Revision");
Call("GlassOperationsStore","EnsureReady");
var origin=JsonSerializer.Deserialize(JsonSerializer.Serialize(new{ProjectId=id,Revision=revision,CustomerId=901,OperationId=Guid.NewGuid().ToString("N")}),assembly.GetType("LealInfoPDV.GlassCashOrigin",true)!)!;
Call("CashFlow","RegisterForGlass","ENTRADA","SINAL LEGADO",100m,origin);
Call("GlassOperationsStore","Schedule",saved,"INSTALAÇÃO",DateTime.Now.AddDays(2),"AGENDA LEGADA");
var contract=Call("GlassDocumentStore","NewContract",saved)!;Call("GlassDocumentStore","SaveContract",saved,contract,0L,false);
Directory.CreateDirectory("evidence");File.WriteAllText("evidence/legacy-project.txt",id);
Console.WriteLine("PASS real verified legacy DLL seeded project, receipt, agenda and contract; no operational license used");
