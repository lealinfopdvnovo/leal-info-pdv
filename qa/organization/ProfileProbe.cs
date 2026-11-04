using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
static class ProfileProbe
{
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Assembly App=typeof(IPetExtension).Assembly;
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control root)=>root.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 static string State(){
  using var db=Database.Open();var tables=new List<string>();
  using(var command=db.CreateCommand()){command.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";using var reader=command.ExecuteReader();while(reader.Read())tables.Add(reader.GetString(0));}
  var data=new Dictionary<string,List<object?[]>>();
  foreach(var table in tables){using var command=db.CreateCommand();command.CommandText="SELECT * FROM \""+table.Replace("\"","\"\"")+"\" ORDER BY rowid";using var reader=command.ExecuteReader();var rows=new List<object?[]>();while(reader.Read()){var values=new object?[reader.FieldCount];for(int i=0;i<values.Length;i++)values[i]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(values);}data[table]=rows;}
  return JsonSerializer.Serialize(data);
 }
 [STAThread]static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=3)return 91;
  var identity=args[0];var version=args[1];var module=Path.GetFullPath(args[2]);
  try{
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   Check(Auth.Login("qa","qa-test-123!")!=null,"QA login retained");
   var folder=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"profile-probe-"+identity+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   File.Copy(module,Path.Combine(folder,"Pet.dll"));var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module)));
   File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonSerializer.Serialize(new{Identity=identity,Version=version,ApiVersion=1,Assembly="Pet.dll",Sha256=hash}));
   var before=State();
   App.GetType("LealInfoPDV.PetRuntime",true)!.GetMethod("Load",Flags)!.Invoke(null,new object[]{folder,identity});
   Check((string?)App.GetType("LealInfoPDV.PetRuntime",true)!.GetProperty("CurrentVersion",Flags)!.GetValue(null)==version,"actual profile "+identity+" version "+version+" loads against current common build");
   using(var main=new MainForm()){
    const BindingFlags p=BindingFlags.Instance|BindingFlags.NonPublic;
    ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",p)!.GetValue(main)!).Stop();
    typeof(MainForm).GetField("automaticBackupCompleted",p)!.SetValue(main,true);
    var handlers=(EventHandlerList)typeof(Component).GetProperty("Events",p)!.GetValue(main)!;
    var key=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
    var shown=handlers[key];handlers.RemoveHandler(key,shown);
    main.Show();Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
    Check(main.Text.Contains("PET V"+version),"native main form shows independent profile version for "+identity);
    if(identity is "001" or "003")Check(Walk(main).Count(x=>x.Text=="VIDRAÇARIA")==1,"approved business extension remains available for "+identity);
    Directory.CreateDirectory("pair-evidence");
    using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("pair-evidence/profile-"+identity+"-Windows.png");}
    main.Close();
   }
   Check(State()==before,"all QA database rows preserved by profile load and native main form");
   File.WriteAllText("pair-evidence/profile-"+identity+".json",JsonSerializer.Serialize(new{Pass=true,Identity=identity,Version=version,ModuleSha256=hash,ActualProfileSource=true,OperationalCustomer=false,Published=false}));
   return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
