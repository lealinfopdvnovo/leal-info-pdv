using LealInfoPDV;
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
class Tests
{
 static readonly Assembly Host=typeof(MainForm).Assembly;
 static readonly List<string> Checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Checks.Add(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 static object? Property(string name)=>Host.GetType("LealInfoPDV.PetRuntime")!.GetProperty(name,BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null);
 static void Bootstrap(string identity)=>Host.GetType("LealInfoPDV.GelamaqPetBootstrap")!.GetMethod("Initialize",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(string)},null)!.Invoke(null,new object[]{identity});
 static string Snapshot()
 {
  using var cn=Database.Open();var all=new List<string>();
  foreach(var table in new[]{"products","sales","sale_items","sale_payments","cash_movements","settings"}){
   using var cmd=cn.CreateCommand();cmd.CommandText="SELECT * FROM "+table+" ORDER BY rowid";using var rd=cmd.ExecuteReader();
   while(rd.Read()){var row=new object[rd.FieldCount];rd.GetValues(row);all.Add(table+":"+JsonSerializer.Serialize(row));}
  }
  return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",all))));
 }
 [STAThread]static int Main(string[] args)
 {
  try{
   if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
   Directory.CreateDirectory("evidence");ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Check(Host.GetName().Version!.Minor==457,"GLOBAL assembly version remains 10.457");
   var update=Host.GetType("LealInfoPDV.UpdateManager")!;
   Check((string)update.GetField("CurrentVersion",BindingFlags.Public|BindingFlags.Static)!.GetRawConstantValue()! == "10.457","GLOBAL display remains 10.457");
   Check((string)update.GetField("InstalledUpdateVersion",BindingFlags.NonPublic|BindingFlags.Static)!.GetRawConstantValue()! == "10.458","002 delivery revision separated from GLOBAL");
   foreach(var id in new[]{"001","003","004","GLOBAL"}){Bootstrap(id);Check(Property("FinancialMovements")==null,"002 bootstrap inactive for "+id);}
   Bootstrap("002");
   Check(Property("CurrentVersion") as string=="1.1" && Property("FinancialMovements") is IFinancialMovementsPet,"actual embedded Gelamaq PET V1.1 loaded without standalone customer files");
   var rt=Host.GetType("LealInfoPDV.PetRuntime")!;var identity=rt.GetField("loadedIdentity",BindingFlags.NonPublic|BindingFlags.Static)!;
   foreach(var id in new[]{"001","003","004","GLOBAL"}){identity.SetValue(null,id);Check(Property("FinancialMovements")==null,"financial hook isolated from "+id);}identity.SetValue(null,"002");
   // Confirm installed updater chooses delivery revision and refuses other identities.
   var manifest=update.GetNestedType("UpdateManifest",BindingFlags.NonPublic)!;
   object Make(string client,string version){var m=Activator.CreateInstance(manifest)!;foreach(var pair in new[]{("TargetClientCode",client),("Version",version),("Sha256",new string('a',64)),("PackageUrl","https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-"+client+"-v"+version+"/update.zip")})manifest.GetProperty(pair.Item1)!.SetValue(m,pair.Item2);return m;}
   var select=update.GetMethod("SelectManifest",BindingFlags.NonPublic|BindingFlags.Static)!;
   Check(select.Invoke(null,new object?[]{"002","10.457",null,Make("002","10.458")})!=null,"legacy installed 10.457 accepts targeted PET bridge delivery");
   Check(select.Invoke(null,new object?[]{"002","10.458",null,Make("002","10.458")})==null,"installed PET delivery is not repeatedly offered");
   foreach(var id in new[]{"001","003","004"})Check(select.Invoke(null,new object?[]{id,"10.457",null,Make("002","10.458")})==null,"002 delivery refused by other identity "+id);
   if(args.Contains("--payload-check"))return 0;
   if(File.Exists(Database.DbPath))throw new Exception("Fresh disposable profile required");
   Database.Initialize();Auth.CreateUser("QA Gelamaq","qa-gelamaq","qa-finance-test!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qa-gelamaq","qa-finance-test!");
   using(var cn=Database.Open())using(var q=cn.CreateCommand()){
    q.CommandText="INSERT INTO settings(key,value) VALUES('client_update_id','002') ON CONFLICT(key) DO UPDATE SET value='002'; INSERT INTO sales(id,sold_at,payment,subtotal,discount,total,operator) VALUES(700,datetime('now','localtime'),'PIX',12.5,0,12.5,'QA'); INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total) VALUES(700,NULL,'Fixture',1,12.5,12.5); INSERT INTO sale_payments(sale_id,method,amount) VALUES(700,'PIX',12.5);";q.ExecuteNonQuery();
   }
   using var main=new MainForm();
   foreach(var scenario in new[]{"empty","sale-only","manual-only","mixed"}){
    using(var cn=Database.Open())using(var q=cn.CreateCommand()){
     q.CommandText="DELETE FROM cash_movements;";
     if(scenario is "sale-only" or "mixed")q.CommandText+="INSERT INTO cash_movements(id,occurred_at,type,description,amount,sale_id) VALUES(2,datetime('now','localtime'),'ENTRADA','Venda QA',12.5,700);";
     if(scenario is "manual-only" or "mixed")q.CommandText+="INSERT INTO cash_movements(id,occurred_at,type,description,amount,sale_id) VALUES(1,datetime('now','localtime','-1 minute'),'SAÍDA','Manual QA',8.5,NULL);";
     q.ExecuteNonQuery();
    }
    var before=Snapshot();Exception? failure=null;bool observed=false;
    using var timer=new System.Windows.Forms.Timer{Interval=1200};
    timer.Tick+=(_,_)=>{
     var form=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text.Contains("RELATÓRIO FINANCEIRO"));if(form==null)return;
     timer.Stop();try{
      var grid=Walk(form).OfType<DataGridView>().Single(g=>g.DataSource is DataTable t&&t.Columns.Contains("Código")&&t.Columns.Contains("Venda"));
      var table=(DataTable)grid.DataSource!;
      int expected=scenario=="empty"?0:scenario=="mixed"?2:1;
      Check(table.Rows.Count==expected,"real financial report row count "+scenario);
      Check(table.Columns["Venda"]!.DataType==typeof(long),"real report numeric nullable sale "+scenario);
      if(scenario is "mixed" or "manual-only")Check(table.Rows.Cast<DataRow>().Single(r=>Convert.ToInt64(r["Código"])==1).IsNull("Venda"),"manual movement keeps DBNull "+scenario);
      if(scenario is "mixed" or "sale-only")Check(Convert.ToInt64(table.Rows.Cast<DataRow>().Single(r=>Convert.ToInt64(r["Código"])==2)["Venda"])==700,"linked sale preserved "+scenario);
      if(scenario=="mixed")Check(table.Rows.Cast<DataRow>().Sum(r=>Convert.ToDouble(r["Valor"]))==21,"financial amounts preserved exactly");
      using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,form.ClientRectangle);image.Save("evidence/report-"+scenario+".png");
      observed=true;
     }catch(Exception ex){failure=ex;}finally{form.Close();}
    };
    timer.Start();Host.GetType("LealInfoPDV.MainForm")!.GetMethod("OpenReports",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,null);
    if(failure!=null)throw failure;Check(observed,"report opened and closed normally "+scenario);
    Check(Snapshot()==before,"report preserves database financial stock settings state "+scenario);
   }
   var extension=(IFinancialMovementsPet)Property("FinancialMovements")!;
   using(var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")){
    cn.Open();using var q=cn.CreateCommand();q.CommandText="CREATE TABLE t(id INTEGER,amount REAL,sale_id INTEGER); INSERT INTO t VALUES(1,12.5,700),(2,'',NULL),(3,'abc',NULL),(4,NULL,NULL);";q.ExecuteNonQuery();
    q.CommandText="SELECT id AS Código,amount AS Valor,sale_id AS Venda FROM t ORDER BY id";using var reader=q.ExecuteReader();var dt=extension.ReadTable(reader);
    Check(dt.Rows.Count==4&&dt.Rows[1]["Valor"].ToString()==""&&dt.Rows[2]["Valor"].ToString()=="abc"&&dt.Rows[3].IsNull("Valor"),"invalid empty and null monetary source values are never converted to zero or rewritten");
   }
   File.WriteAllText("evidence/gelamaq-finance.json",JsonSerializer.Serialize(new{Success=true,Checks,GlobalVersion="10.457",PetVersion="1.1",DeliveryRevision="10.458",RealCustomerTest=false},new JsonSerializerOptions{WriteIndented=true}));
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);File.WriteAllText("evidence/gelamaq-failure.txt",ex.ToString());return 1;}
 }
}
