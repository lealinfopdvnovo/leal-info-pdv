using LealInfoPDV;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Data.Sqlite;
static class Tests
{
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static string State(){using var db=Database.Open();var tables=new List<string>();using(var q=db.CreateCommand()){q.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";using var r=q.ExecuteReader();while(r.Read())tables.Add(r.GetString(0));}var data=new Dictionary<string,List<object?[]>>();foreach(string t in tables){using var q=db.CreateCommand();q.CommandText="SELECT * FROM \""+t.Replace("\"","\"\"")+"\" ORDER BY rowid";using var r=q.ExecuteReader();var rows=new List<object?[]>();while(r.Read()){var values=new object?[r.FieldCount];for(int i=0;i<values.Length;i++)values[i]=r.IsDBNull(i)?null:r.GetValue(i);rows.Add(values);}data[t]=rows;}return JsonSerializer.Serialize(data);}
 static IEnumerable<Control> Walk(Control root)=>root.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 [STAThread]static int Main(string[] args){
  try{
   Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","isolated Windows only");
   string root=Path.GetFullPath(args[0]);
   if(args.Contains("--crash")){PetBootstrap.EnsurePresent(root,"003",_=>Process.GetCurrentProcess().Kill());return 90;}
   Check(File.ReadAllText(Path.Combine(Database.AppFolder,"MIGRATION-QA"))==Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),"database was created by verified legacy DLL in this run");
   Directory.CreateDirectory("evidence");string before=State();
   string Fixture(string name){string p=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"migration-"+name+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);File.WriteAllText(Path.Combine(p,"QA-ISOLATED"),"fixture");File.Copy(Path.Combine(root,"LealInfoPDV.exe"),Path.Combine(p,"LealInfoPDV.exe"));return p;}
   void Reject(Action action,string name){bool blocked=false;try{action();}catch(InvalidDataException){blocked=true;}Check(blocked,name);}
   string wrong=Fixture("wrong");Reject(()=>PetBootstrap.EnsurePresent(wrong,"002"),"wrong identity rejected before creating profile");Check(!Directory.Exists(Path.Combine(wrong,"Extensions")),"wrong identity creates no profile directory");
   string corrupt=Fixture("corrupt");var bytes=File.ReadAllBytes(Path.Combine(corrupt,"LealInfoPDV.exe"));bytes[^60]^=0x01;File.WriteAllBytes(Path.Combine(corrupt,"LealInfoPDV.exe"),bytes);Reject(()=>PetBootstrap.EnsurePresent(corrupt,"003"),"corrupt capsule rejected before writing profile");
   string orphan=Fixture("orphan");Directory.CreateDirectory(Path.Combine(orphan,"Extensions","PET"));string sentinel=Path.Combine(orphan,"Extensions","PET","preserve.txt");File.WriteAllText(sentinel,"PRESERVE");Reject(()=>PetBootstrap.EnsurePresent(orphan,"003"),"existing incomplete profile is never overwritten");Check(File.ReadAllText(sentinel)=="PRESERVE","existing files retained byte for byte");
   string crash=Fixture("crash");var start=new ProcessStartInfo("dotnet"){UseShellExecute=false};foreach(string arg in new[]{Assembly.GetExecutingAssembly().Location,crash,"--crash"})start.ArgumentList.Add(arg);using(var child=Process.Start(start)!){Check(child.WaitForExit(15000)&&child.ExitCode!=0,"hard process interruption before profile activation");}
   Check(!Directory.Exists(Path.Combine(crash,"Extensions","PET")),"interrupted staging cannot expose partial active profile");Check(PetBootstrap.EnsurePresent(crash,"003"),"next start recovers from incomplete staging");
   Check(PetBootstrap.EnsurePresent(root,"003"),"first migration atomically installs separate PET from protected apphost");
   var installed=Directory.GetFiles(Path.Combine(root,"Extensions","PET")).ToDictionary(x=>x,x=>File.ReadAllBytes(x));
   Check(!PetBootstrap.EnsurePresent(root,"003")&&installed.All(x=>File.ReadAllBytes(x.Key).SequenceEqual(x.Value)),"repeat startup preserves already installed independent PET");
   Database.Initialize();Check(State()==before,"new common initialization preserves every populated legacy table row");
   PetRuntime.Load(Path.Combine(root,"Extensions","PET"),"003");Check(PetRuntime.CurrentVersion=="1.1","actual independently installed PET V1.1 loads through common API");
   string projectId=File.ReadAllText("evidence/legacy-project.txt");var saved=GlassProjectStore.Load(projectId);
   Check(saved.Data.WidthMm==1200&&saved.Data.HeightMm==2000&&saved.Data.Quote.CustomerId==901,"legacy project dimensions and customer preserved across assembly separation");
   Check(GlassOperationsStore.Read(saved).Received==100m&&GlassDocumentStore.Receipts(saved).Count==1,"legacy payment and receipt retained without duplicate charge");
   Check(GlassOperationsStore.Appointments().Single().ProjectId==projectId&&GlassDocumentStore.Contracts(projectId).Count==1,"legacy agenda and hashed contract retained");
   Application.EnableVisualStyles();Check(Auth.Login("migrationqa","QA-migration-only!")!=null,"legacy user credentials retained");
   using(var main=new MainForm()){
    const BindingFlags p=BindingFlags.Instance|BindingFlags.NonPublic;
    ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",p)!.GetValue(main)!).Stop();typeof(MainForm).GetField("automaticBackupCompleted",p)!.SetValue(main,true);
    var handlers=(EventHandlerList)typeof(Component).GetProperty("Events",p)!.GetValue(main)!;var key=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;var shown=handlers[key];handlers.RemoveHandler(key,shown);main.Show();Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
    Check(Walk(main).Count(x=>x.Text=="VIDRAÇARIA")==1&&main.Text.Contains("CASA LEAL INFO"),"actual native main form retains Casa profile and Glass entry");
    using var workshop=new GlassWorkshopForm(main.PetTheme);workshop.LoadSavedProject(projectId);Check(workshop.CaptureSavedProject().Description=="PROJETO LEGADO","actual workshop reopens project created by legacy binary");main.Close();
   }
   Check(State()==before,"profile installation, startup and project reopening leave all legacy records unchanged");
   using(var cn=new SqliteConnection(Database.ConnectionString))using(var backup=new SqliteConnection("Data Source="+Path.GetFullPath("evidence/legacy-restorable.db")+";Pooling=False")){cn.Open();backup.Open();cn.BackupDatabase(backup);}
   RestoreEngine.Validate(Path.GetFullPath("evidence/legacy-restorable.db"));Check(true,"populated legacy database has validated restorable online backup");
   File.WriteAllText("evidence/migration-results.json",JsonSerializer.Serialize(new{Success=true,checks,Published=false,InstalledOnCustomer=false,RealActivatedStartupQualified=false}));return 0;
  }catch(Exception e){Console.Error.WriteLine(e);Directory.CreateDirectory("evidence");File.WriteAllText("evidence/migration-failure.txt",e.ToString());return 1;}
 }
}
