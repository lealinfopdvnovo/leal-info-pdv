using LealInfoPDV;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
static class Tests
{
 const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static string Sha(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
 static object? Sql(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
 static string[] Types(string path){using var input=File.OpenRead(path);using var pe=new PEReader(input);var md=pe.GetMetadataReader();return md.TypeDefinitions.Select(h=>md.GetString(md.GetTypeDefinition(h).Name)).ToArray();}
 static void Reject(Action action,string text){bool ok=false;try{action();}catch(InvalidDataException){ok=true;}Check(ok,text);}
 [STAThread]static int Main(string[] args)
 {
  Directory.CreateDirectory("evidence");using var watchdog=new System.Threading.Timer(_=>Environment.Exit(91),null,150000,Timeout.Infinite);
  try{
   Check(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!File.Exists(Database.DbPath),"disposable Windows without customer database");
   string extension=Path.GetFullPath(args[0]),baseline=Path.GetFullPath(args[1]);
   string globalPath=typeof(Database).Assembly.Location;string commonHash=Sha(globalPath),petHash=Sha(extension);
   Check(commonHash==args[2],"actual GLOBAL DLL exactly matches immutable approved 10.443 artifact");
   var dependency=JsonDocument.Parse(File.ReadAllText("evidence/runtime-compatibility.json"));bool dependenciesCompatible=dependency.RootElement.GetProperty("IncrementalCompatible").GetBoolean();
   var legacy=Types(baseline).Where(n=>n.StartsWith("Glass",StringComparison.Ordinal)).Distinct().Order().ToArray();
   var candidate=Types(globalPath).Concat(Types(extension)).ToHashSet();var missing=legacy.Where(n=>!candidate.Contains(n)).ToArray();
   Check(legacy.Contains("GlassWorkshopForm")&&legacy.Contains("GlassProjectStore")&&legacy.Contains("GlassReceiptForm"),"exact published LOJA binary contains existing Glass functionality");
   Console.WriteLine("MIGRATION BLOCKER: existing Glass types absent from GLOBAL plus identification PET: "+string.Join(", ",missing));
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Database.Initialize();Auth.CreateUser("QA LOJA","qaloja","QA-loja-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qaloja","QA-loja-123!");
   Sql("INSERT INTO products(id,name,price,cost,stock,active) VALUES(901,'PRODUTO EXISTENTE LOJA',12.5,4,77,1)");Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_name','EMPRESA DE TESTE ISOLADO'),('qa_existing_customization','PRESERVAR-REGRA-LOCAL'),('client_update_id','001')");
   string State()=>Convert.ToString(Sql("SELECT (SELECT stock FROM products WHERE id=901)||'|'||(SELECT price FROM products WHERE id=901)||'|'||(SELECT COUNT(*) FROM sales)||'|'||(SELECT value FROM settings WHERE key='qa_existing_customization')||'|'||(SELECT value FROM settings WHERE key='client_update_id')"))!;
   using var main=new MainForm();typeof(MainForm).GetField("licenseTimer",P)!.GetValue(main).AsTimer().Stop();
   var events=(EventHandlerList)typeof(Component).GetProperty("Events",P)!.GetValue(main)!;var shownKey=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(f=>f.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;var shown=events[shownKey];events.RemoveHandler(shownKey,shown);main.Show();Application.DoEvents();events.AddHandler(shownKey,shown);main.Text="LEAL INFO PDV PRO — GLOBAL 10.443";
   string beforeState=State();
   string folder=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"LOJA-PET-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);File.Copy(extension,Path.Combine(folder,"Loja.dll"));
   string manifest=Path.Combine(folder,"manifest.json");
   void Manifest(string id="001",string version="1.1",string? sha=null)=>File.WriteAllText(manifest,JsonSerializer.Serialize(new{Identity=id,DisplayName="LOJA LEAL INFO",Version=version,ApiVersion=1,Assembly="Loja.dll",Sha256=sha??petHash,CandidateOnly=true,MigrationApplied=false}));
   Manifest("002");Reject(()=>PetRuntime.Load(folder,"001"),"another PET identity rejected by actual GLOBAL loader");Manifest(sha:new string('0',64));Reject(()=>PetRuntime.Load(folder,"001"),"corrupt PET rejected before loading");Manifest(version:"10.443");Reject(()=>PetRuntime.Load(folder,"001"),"common version cannot replace local PET version");Manifest();
   string manifestHash=Sha(manifest);
   using var before=new Bitmap(main.Width,main.Height);main.DrawToBitmap(before,new Rectangle(Point.Empty,before.Size));
   PetRuntime.Load(folder,"001");Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();Application.RaiseIdle(EventArgs.Empty);
   Check(main.Text=="LEAL INFO PDV PRO — GLOBAL 10.443 — LOJA LEAL INFO • PET V1.1","real GLOBAL MainForm loads LOJA name and V1.1 through PetRuntime exactly once");
   Check(PetRuntime.CurrentVersion=="1.1"&&UpdateManager.CurrentVersion=="10.443","GLOBAL and PET report independent versions");
   Check(State()==beforeState,"existing product stock price settings identity and sales unchanged by PET loading");
   Check(Sha(globalPath)==commonHash&&Sha(Path.Combine(folder,"Loja.dll"))==petHash&&Sha(manifest)==manifestHash,"PET loading modifies neither common binary nor local library and metadata");
   Check(Convert.ToString(Sql("PRAGMA quick_check"))=="ok","disposable database integrity remains valid");
   using var after=new Bitmap(main.Width,main.Height);main.DrawToBitmap(after,new Rectangle(Point.Empty,after.Size));
   // The existing GLOBAL update policy deliberately has no PET installation route.
   string zip=Path.Combine(folder,"pet-update.zip");using(var z=System.IO.Compression.ZipFile.Open(zip,System.IO.Compression.ZipArchiveMode.Create)){using(var w=new StreamWriter(z.CreateEntry("Extensions/PET/Loja.dll").Open()))w.Write("PET");}
   bool petUpdater=false;try{LealInfoPDV.Licensing.UpdatePayloadPolicy.Validate(zip);petUpdater=true;}catch(InvalidDataException){}
   var blockers=new List<string>();if(!dependenciesCompatible)blockers.Add("As dependências da candidata GLOBAL diferem da base publicada; pacote incremental de três arquivos não aprovado para essa transição.");if(missing.Length>0)blockers.Add("Personalizações de Vidraçaria da versão publicada não estão no GLOBAL nem no PET de identificação.");if(!petUpdater)blockers.Add("Não existe nesta candidata um aplicador transacional específico de atualização PET; a política GLOBAL recusa esse pacote.");
   main.Hide();using var result=new Form{Text="LOJA LEAL INFO — TESTE ISOLADO — NÃO PUBLICADO",ClientSize=new Size(1120,780),BackColor=Color.FromArgb(5,17,30),ForeColor=Color.White,Font=new Font("Segoe UI",11),StartPosition=FormStartPosition.CenterScreen};
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(18)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.Absolute,105));root.RowStyles.Add(new RowStyle(SizeType.Absolute,65));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,220));
   var title=new Label{Text="LOJA LEAL INFO — PET V1.1\nGLOBAL 10.443 • TESTE EM WINDOWS ISOLADO\nPRÉVIA: NÃO INSTALADO NA LOJA",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Gold,Font=new Font("Segoe UI",16,FontStyle.Bold)};root.Controls.Add(title,0,0);root.SetColumnSpan(title,2);
   foreach(var (text,image,index) in new[]{("ANTES: base GLOBAL sem PET",before,0),("DEPOIS: LOJA LEAL INFO • PET V1.1",after,1)}){root.Controls.Add(new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Cyan,Font=new Font("Segoe UI",12,FontStyle.Bold)},index,1);root.Controls.Add(new PictureBox{Image=image,Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom},index,2);}
   var summary=new Label{Text="APROVADO: carregamento, nome, V1.1 e preservação dos dados de teste.\n\nPENDENTE: funções de Vidraçaria não migradas.\nPENDENTE: atualização e rollback específicos do PET.\nDependências incrementais: "+(dependenciesCompatible?"compatíveis":"incompatíveis")+".\n\nNÃO LIBERADO PARA MIGRAR OU PUBLICAR.\nRecuperação de arquivos comuns e atualização PET são escopos distintos.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(255,185,80),Font=new Font("Segoe UI",12,FontStyle.Bold)};root.Controls.Add(summary,0,3);root.SetColumnSpan(summary,2);result.Controls.Add(root);result.Show();Application.DoEvents();using(var shot=new Bitmap(result.Width,result.Height)){result.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("evidence/loja-antes-depois.png");}result.Close();main.Close();
   File.WriteAllText("evidence/results.json",JsonSerializer.Serialize(new{ExecutionSuccess=true,ReadyForStoreMigration=false,Published=false,InstalledOnStore=false,Source=Environment.GetEnvironmentVariable("GITHUB_SHA"),checks,Blockers=blockers,LegacyGlassTypes=legacy,MissingLegacyTypes=missing,GlobalSha256=commonHash,PetSha256=petHash,ActualCustomerDatabaseTested=false,PetSpecificUpdaterValidated=false,IncrementalDependenciesCompatible=dependenciesCompatible,ExistingProfilePreservation="NOT_APPROVED"}));Console.WriteLine("TEST EXECUTED; STORE MIGRATION BLOCKED; NO PUBLICATION");Environment.Exit(0);return 0;
  }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("evidence/failure.txt",e.ToString());return 1;}
 }
 static System.Windows.Forms.Timer AsTimer(this object? value)=>(System.Windows.Forms.Timer)value!;
}
