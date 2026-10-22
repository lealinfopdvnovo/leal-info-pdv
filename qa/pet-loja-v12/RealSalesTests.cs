using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
static class RealSalesTests{
 const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
 static IEnumerable<Control> Walk(Control p){foreach(Control c in p.Controls){yield return c;foreach(var n in Walk(c))yield return n;}}
 static string Sha(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
 [STAThread]static int Main(){
 Directory.CreateDirectory("evidence");using var guard=new System.Threading.Timer(_=>Environment.Exit(91),null,90000,Timeout.Infinite);
 var checks=new List<string>();void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks.Add(msg);Console.WriteLine("PASS "+msg);}
 try{
 Check(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!File.Exists(Database.DbPath),"isolated Windows without store database");
 Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
 Database.Initialize();Auth.CreateUser("QA","qapet","QA-pet-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qapet","QA-pet-123!");
 object? Sql(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
 Sql("INSERT INTO products(barcode,name,price,stock,active) VALUES('PETQA','PRODUTO QA PET',10,77,1); INSERT OR REPLACE INTO settings(key,value) VALUES('sales_theme','Futurista Azul'),('client_update_id','001'),('qa_keep','PRESERVAR')");
 string State()=>Convert.ToString(Sql("SELECT (SELECT COUNT(*) FROM sales)||'|'||(SELECT stock FROM products WHERE barcode='PETQA')||'|'||(SELECT value FROM settings WHERE key='qa_keep')"))!;
 string before=State(),global=typeof(MainForm).Assembly.Location,hash=Sha(global);
 using var owner=new MainForm();((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",P)!.GetValue(owner)!).Stop();
 var runtime=typeof(MainForm).Assembly.GetType("LealInfoPDV.PetRuntime")!;
 string folder=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"pet-real-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
 var dll=typeof(LojaTestExtension).Assembly.Location;File.Copy(dll,Path.Combine(folder,"Loja.dll"));File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonSerializer.Serialize(new{Identity="001",Version="1.2",ApiVersion=1,Assembly="Loja.dll",Sha256=Sha(dll)}));
 int stage=0;Exception? failure=null;bool completed=false;
 using var timer=new System.Windows.Forms.Timer{Interval=250};
 timer.Tick+=(_,_)=>{
 try{
 var sales=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text.Contains("TELA DE VENDA"));if(sales==null)return;
 if(stage==0){stage=1;Shot(sales,"evidence/real-antes.png");runtime.GetMethod("Load",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{folder,"001"});Application.RaiseIdle(EventArgs.Empty);return;}
 if(stage==1){stage=2;Application.RaiseIdle(EventArgs.Empty);
 var button=Walk(sales).OfType<Button>().Single(b=>b.Name=="pet001_test_button");var style=Walk(sales).OfType<Button>().Single(b=>b.Text=="ESTILO");
 Check(button.Parent==style.Parent,"actual sales window receives PET button beside ESTILO");Check(runtime.GetProperty("CurrentVersion",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)?.ToString()=="1.2","actual runtime reports PET V1.2");
 foreach(var width in new[]{1024,1366,1920}){sales.WindowState=FormWindowState.Normal;sales.ClientSize=new Size(width,768);sales.PerformLayout();Check(button.Visible&&button.Width>50&&button.Right<=style.Left,"real responsive layout no overlap at "+width);}
 Shot(sales,"evidence/real-depois.png");button.PerformClick();return;}
 if(stage==2){var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="pet001_test_dialog");if(dialog==null)return;Check(true,"real sales button opens floating dialog");Shot(dialog,"evidence/real-janela.png");stage=3;dialog.Close();return;}
 if(stage==3){stage=4;Walk(sales).OfType<Button>().Single(b=>b.Text=="ESTILO").PerformClick();return;}
 if(stage==4){var chooser=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Estilo da Tela de Vendas");if(chooser==null)return;Check(true,"original ESTILO still opens its actual chooser");stage=5;chooser.Close();return;}
 if(stage==5){timer.Stop();Check(State()==before,"test product stock sales and settings preserved");Check(Sha(global)==hash,"GLOBAL binary unchanged by PET customization");Check(Convert.ToString(Sql("PRAGMA quick_check"))=="ok","test database integrity valid");completed=true;sales.Close();}
 }catch(Exception e){failure=e;timer.Stop();foreach(var f in Application.OpenForms.Cast<Form>().Reverse().ToArray())f.Close();}
 };
 timer.Start();typeof(MainForm).GetMethod("OpenSales",P)!.Invoke(owner,null);timer.Stop();if(failure!=null)throw failure;Check(completed,"actual OpenSales completed all checks");
 File.WriteAllText("evidence/real-results.json",JsonSerializer.Serialize(new{Success=true,checks,ActualSalesForm=true,CustomerWindowsTested=false,Published=false,PetUpdaterValidated=false,GlobalSha256=hash}));return 0;
 }catch(Exception e){File.WriteAllText("evidence/real-failure.txt",e.ToString());Console.Error.WriteLine(e);return 1;}
 }
 static void Shot(Form f,string path){using var b=new Bitmap(f.Width,f.Height);f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}
}
