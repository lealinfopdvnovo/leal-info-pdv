using System.Reflection;
using System.Text.Json;
using LealInfoPDV;
using LealInfo.Components;
class LifecycleProbe
{
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 static readonly Assembly App=typeof(IPetExtension).Assembly;
 static readonly List<string> Checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Checks.Add(text);Console.WriteLine("PASS "+text);}
 static void Reject(Action call,string text){try{call();}catch(Exception e){var cause=e is TargetInvocationException?e.InnerException!:e;if(cause is InvalidDataException&&cause.Message.Contains("inativo")){Check(true,text);return;}throw;}throw new Exception("Inactive operation accepted: "+text);}
 [STAThread]static int Main(){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")return 91;
  var root=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"inactive-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   File.WriteAllText(Path.Combine(root,"keep.txt"),"PRESERVE-HISTORY");
   Reject(()=>App.GetType("LealInfoPDV.PetBootstrap")!.GetMethod("EnsurePresent",Flags)!.Invoke(null,new object?[]{root,"004",null}),"inactive bootstrap rejected before any copy or backup");
   Check(Directory.GetFiles(root).Length==1&&File.ReadAllText(Path.Combine(root,"keep.txt"))=="PRESERVE-HISTORY","inactive history retained byte for byte");
   Reject(()=>((Task)App.GetType("LealInfoPDV.PetTransactionalUpdate")!.GetMethod("BeginAsync",Flags)!.Invoke(null,new object[]{"missing.zip","invalid","1.2","PET","004"})!).GetAwaiter().GetResult(),"direct PET entry rejects inactive before package access");
   ((Task)App.GetType("LealInfoPDV.ComponentUpdateCenter")!.GetMethod("ShowAsync",Flags)!.Invoke(null,new object?[]{null,"004","10.465",true})!).GetAwaiter().GetResult();
   Check(true,"silent update center returns for inactive without opening a dialog");
   Database.Initialize();string? previous;
   using(var cn=Database.Open())using(var q=cn.CreateCommand()){
    q.CommandText="SELECT value FROM settings WHERE key='client_update_id'";previous=Convert.ToString(q.ExecuteScalar());
    q.CommandText="INSERT INTO settings(key,value) VALUES('client_update_id','004') ON CONFLICT(key) DO UPDATE SET value='004'";q.ExecuteNonQuery();
   }
   try{Reject(()=>((Task)App.GetType("LealInfoPDV.TransactionalUpdate")!.GetMethod("BeginAsync",Flags)!.Invoke(null,new object[]{"missing.zip","invalid","10.470","GLOBAL"})!).GetAwaiter().GetResult(),"direct GLOBAL entry rejects inactive before package or PET manifest access");}
   finally{using var cn=Database.Open();using var q=cn.CreateCommand();q.CommandText="UPDATE settings SET value=$v WHERE key='client_update_id'";q.Parameters.AddWithValue("$v",previous??"");q.ExecuteNonQuery();}
   Check(ClientPolicy.Default.WithActivity("004",true).IsActive("004")&&!ClientPolicy.Default.IsActive("004"),"reactivation requires explicit candidate policy and does not mutate default");
   Directory.CreateDirectory("pair-evidence");File.WriteAllText("pair-evidence/inactive-client.json",JsonSerializer.Serialize(new{Pass=true,Checks,OperationalMigrationIncluded=false,Published=false}));return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}
 }
}
