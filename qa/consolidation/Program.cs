using LealInfoPDV;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
static partial class Tests
{
 const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control root)=>root.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 static object? Sql(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
 [STAThread]static int Main(string[] args){
  string id=args[0],version=args[1],folder=Path.GetFullPath(args[2]);Directory.CreateDirectory("evidence/"+id);
  if(args.Contains("--reopen"))return ReopenChecks(id,version,folder,args[^1]);
  using var guard=new System.Threading.Timer(_=>Environment.Exit(91),null,120000,Timeout.Infinite);
  try{
   Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!File.Exists(Database.DbPath),"isolated Windows; no customer database");
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Database.Initialize();File.WriteAllText(Path.Combine(Database.AppFolder,"ISOLATED-CONSOLIDATION"),Environment.GetEnvironmentVariable("GITHUB_RUN_ID"));
   Auth.CreateUser("QA","qaglobal","QA-global-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qaglobal","QA-global-123!");
   Sql("INSERT INTO products(id,name,price,stock,active) VALUES(901,'PRODUTO EXISTENTE',12.5,77,1);INSERT INTO customers(id,name) VALUES(901,'CLIENTE EXISTENTE');INSERT OR REPLACE INTO settings(key,value) VALUES('qa_keep','PRESERVAR')");
   var common=typeof(MainForm).Assembly;
   Check(!common.GetTypes().Any(x=>JsonSerializer.Deserialize<string[]>(File.ReadAllText("qa/consolidation/legacy-domain-types.json"))!.Contains(x.Name)),"GLOBAL contains no Glass or Angra domain implementation");
   using var main=new MainForm();main.Text="LEAL INFO PDV PRO V"+UpdateManager.CurrentVersion;((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",P)!.GetValue(main)!).Stop();
   var events=(EventHandlerList)typeof(Component).GetProperty("Events",P)!.GetValue(main)!;var shownKey=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;var shown=events[shownKey];events.RemoveHandler(shownKey,shown);main.Show();Application.DoEvents();events.AddHandler(shownKey,shown);
   string State()=>Convert.ToString(Sql("SELECT (SELECT stock FROM products WHERE id=901)||'|'||(SELECT price FROM products WHERE id=901)||'|'||(SELECT COUNT(*) FROM sales)||'|'||(SELECT value FROM settings WHERE key='qa_keep')"))!;
   string before=State();var coreHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(common.Location)));
   ManifestChecks(folder,id,version);PetRuntime.Load(folder,id);Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();Application.RaiseIdle(EventArgs.Empty);
   Check(PetRuntime.CurrentVersion==version,"PET version retained "+id+" V"+version);
   string name=id switch{"001"=>"LOJA LEAL INFO","002"=>"GELAMAC","003"=>"CASA LEAL INFO",_=>"CASA GENILSON"};
   Check(main.Text.Contains(name)&&main.Text.Contains("PET V"+version),"correct client name and independent version on real main window");
   Check(State()==before,"profile startup preserves stock price sales and configuration");
   Check(coreHash==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(common.Location))),"profile loading leaves shared binary unchanged");
   if(id is "001" or "003"){BusinessChecks(main,id);ReportChecks(main,id);LoginChecks(id);}
   else Check(!Walk(main).Any(x=>x.Text=="VIDRAÇARIA"),"identification profile does not acquire Glass functionality");
   using(var shot=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("evidence/"+id+"/principal.png");}
   if(id is "001" or "003")ReopenInNewProcess(id,version,folder);
   DatabaseChecks(id);
   Check(Convert.ToString(Sql("PRAGMA quick_check"))=="ok","fixture database integrity valid");
   File.WriteAllText("evidence/"+id+"/results.json",JsonSerializer.Serialize(new{Success=true,Identity=id,PetVersion=version,checks,Published=false,InstalledOnCustomer=false,ReadyForLegacyMigration=false,PetSpecificUpdaterValidated=false}));main.Close();return 0;
  }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("evidence/"+id+"/failure.txt",e.ToString());return 1;}
 }
 static void BusinessChecks(MainForm main,string id){
  var assembly=typeof(GlassWorkshopForm).Assembly;
  var legacy=JsonSerializer.Deserialize<string[]>(File.ReadAllText("qa/consolidation/legacy-domain-types.json"))!;
  var available=assembly.GetTypes().Select(x=>x.Name).Concat(typeof(MainForm).Assembly.GetTypes().Select(x=>x.Name)).ToHashSet();
  Check(legacy.All(available.Contains),"all published Glass and Angra types available in PET plus GLOBAL");
  Check(Walk(main).Count(x=>x.Text=="VIDRAÇARIA")==1,"Glass entry added exactly once to actual toolbar");
  var data=new GlassSavedProject(1,GlassProject.ModelName,"PROJETO PRESERVADO",1200,2000,"mm","mm",1,new GlassSavedQuote(901,"CLIENTE EXISTENTE","INSTALAÇÃO",100,50,0,0),Array.Empty<GlassMaterialLink>(),null,true);
  var saved=GlassProjectStore.Save(data);var restored=GlassProjectStore.Load(saved.Id);
  Check(restored.Data.WidthMm==1200&&restored.Data.HeightMm==2000&&restored.Data.Quote.CustomerId==901,"project save and reload preserve physical measures and customer link");
  GlassOperationsStore.EnsureReady();var origin=new GlassCashOrigin(saved.Id,saved.Revision,901,Guid.NewGuid().ToString("N"));
  long payment=PetCash.RegisterForGlass("ENTRADA","QA SINAL",100m,origin);
  Check(PetCash.RegisterForGlass("ENTRADA","QA SINAL",100m,origin)==payment,"domain payment retry cannot duplicate receipt");
  Check(GlassOperationsStore.Read(saved).Received==100m&&Convert.ToDouble(Sql("SELECT stock FROM products WHERE id=901"))==77,"domain receipt and project balance preserve product stock");
  string financialBefore=Convert.ToString(Sql("SELECT COUNT(*)||'|'||SUM(amount) FROM cash_movements"))!;
  var appointment=GlassOperationsStore.Schedule(saved,"INSTALAÇÃO",DateTime.Now.AddDays(2),"PRESERVAR AGENDA");
  Check(GlassOperationsStore.Appointments().Any(x=>x.Id==appointment&&x.CustomerId==901&&x.ProjectId==saved.Id),"agenda retains project and customer after module extraction");
  var receipt=GlassDocumentStore.Receipt(saved,payment);
  Check(receipt.Amount==100m&&GlassDocumentStore.Receipts(saved).Count==1,"receipt corresponds to exactly one real payment");
  var receiptPdf=GlassLinkedDocument.PdfBytes(GlassLinkedDocument.Receipt(receipt));
  Check(System.Text.Encoding.ASCII.GetString(receiptPdf.Take(5).ToArray())=="%PDF-","real A4 receipt remains renderable after extraction");
  File.WriteAllBytes("evidence/"+id+"/recibo.pdf",receiptPdf);
  var contract=GlassDocumentStore.SaveContract(saved,GlassDocumentStore.NewContract(saved),0,false);
  Check(GlassDocumentStore.Contracts(saved.Id).Single().ContentHash==contract.ContentHash,"contract persists with validated content hash");
  File.WriteAllBytes("evidence/"+id+"/contrato.pdf",GlassLinkedDocument.PdfBytes(GlassLinkedDocument.Contract(contract)));
  Check(financialBefore==Convert.ToString(Sql("SELECT COUNT(*)||'|'||SUM(amount) FROM cash_movements")),"agenda receipts and contracts create no additional cash movement");
  using(var form=new GlassWorkshopForm(main.PetTheme)){form.Show(main);form.LoadSavedProject(saved.Id);Application.DoEvents();Check(JsonSerializer.Serialize(form.CaptureSavedProject())==JsonSerializer.Serialize(saved.Data),"saved project reopens through actual workshop controls");Check(Walk(form).OfType<TabControl>().First().TabPages.Count>=7,"actual Glass workshop opens with its existing sections");using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("evidence/"+id+"/vidracaria.png");form.Close();}
 }
}
