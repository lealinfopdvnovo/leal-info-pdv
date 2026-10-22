using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
static class RealSalesTests{
 const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
 static IEnumerable<Control> Walk(Control p){foreach(Control c in p.Controls){yield return c;foreach(var n in Walk(c))yield return n;}}
 static string Sha(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
 [STAThread]static int Main(string[] args){
 Directory.CreateDirectory("evidence");using var guard=new System.Threading.Timer(_=>Environment.Exit(91),null,90000,Timeout.Infinite);
 var checks=new List<string>();void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks.Add(msg);Console.WriteLine("PASS "+msg);}
 try{
 Check(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!File.Exists(Database.DbPath),"isolated Windows without store database");
 using(var padded=new Bitmap(1000,1000)){using(var g=Graphics.FromImage(padded)){g.Clear(Color.Transparent);g.FillRectangle(Brushes.Cyan,350,450,300,100);}var bounds=(Rectangle)typeof(LojaTestExtension).GetMethod("ContentBounds",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{padded})!;Check(bounds.Contains(new Rectangle(350,450,300,100))&&bounds.Width<320&&bounds.Height<120,"transparent logo margins removed without clipping visible content");}
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
 if(stage==0){stage=1;Shot(sales,"evidence/real-antes.png");if(args.Contains("--bootstrap")){typeof(MainForm).Assembly.GetType("LealInfoPDV.LojaPetBootstrap")!.GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);Check(true,"published 001 binary extracts and loads its embedded PET V1.2");}else runtime.GetMethod("Load",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{folder,"001"});Application.RaiseIdle(EventArgs.Empty);return;}
 if(stage==1){stage=2;Application.RaiseIdle(EventArgs.Empty);
 var loaded=(IPetExtension)runtime.GetField("extension",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
 string[] serialBefore=Walk(owner).OfType<StatusStrip>().SelectMany(x=>x.Items.OfType<ToolStripStatusLabel>()).Where(x=>x.Text.StartsWith("Serial:")).Select(x=>x.Text).ToArray();
 loaded.Customize(owner);loaded.Customize(owner);
 var versionLabels=Walk(owner).OfType<StatusStrip>().SelectMany(x=>x.Items.OfType<ToolStripStatusLabel>()).Where(x=>x.Name=="loja_pet_version").ToArray();
 Check(versionLabels.Length==1&&versionLabels[0].Text=="PDV "+UpdateManager.CurrentVersion+" | LOJA LEAL INFO • PET V1.2","actual main footer shows PDV and PET versions side by side exactly once");
 Check(serialBefore.SequenceEqual(Walk(owner).OfType<StatusStrip>().SelectMany(x=>x.Items.OfType<ToolStripStatusLabel>()).Where(x=>x.Text.StartsWith("Serial:")).Select(x=>x.Text)),"footer keeps existing serial information");

 var logo=Walk(sales).OfType<PictureBox>().Single(p=>p.Name=="sales_company_logo");var style=Walk(sales).OfType<Button>().Single(b=>b.Text=="ESTILO");
 Check(!Walk(sales).OfType<Button>().Any(b=>b.Name=="pet001_test_button"||b.Text=="EM FASE DE TESTE"),"test button removed from actual sales window");Check(Equals(logo.Tag,"LOJA-PET-1.2-LOGO"),"larger logo customization applied only to header");Check(runtime.GetProperty("CurrentVersion",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)?.ToString()=="1.2","actual runtime reports PET V1.2");
 foreach(var width in new[]{1024,1366,1920}){sales.WindowState=FormWindowState.Normal;sales.ClientSize=new Size(width,768);sales.PerformLayout();Check(logo.Visible&&logo.Width>100&&logo.Height>40&&style.Visible,"header logo and ESTILO remain visible at "+width);}
 Shot(sales,"evidence/real-depois.png");stage=3;return;}
 if(stage==3){stage=4;Walk(sales).OfType<Button>().Single(b=>b.Text=="ESTILO").PerformClick();return;}
 if(stage==4){var chooser=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Estilo da Tela de Vendas");if(chooser==null)return;Check(true,"original ESTILO still opens its actual chooser");stage=5;chooser.Close();return;}
 if(stage==5){timer.Stop();Check(State()==before,"test product stock sales and settings preserved");Check(Sha(global)==hash,"GLOBAL binary unchanged by PET customization");Check(Convert.ToString(Sql("PRAGMA quick_check"))=="ok","test database integrity valid");completed=true;sales.Close();}
 }catch(Exception e){failure=e;timer.Stop();foreach(var f in Application.OpenForms.Cast<Form>().Reverse().ToArray())f.Close();}
 };
 timer.Start();typeof(MainForm).GetMethod("OpenSales",P)!.Invoke(owner,null);timer.Stop();if(failure!=null)throw failure;Check(completed,"actual OpenSales completed all checks");
 File.WriteAllText("evidence/real-results.json",JsonSerializer.Serialize(new{Success=true,checks,ActualSalesForm=true,CustomerWindowsTested=false,Published=false,PetUpdaterValidated=false,GlobalSha256=hash}));return 0;
 }catch(Exception e){File.WriteAllText("evidence/real-failure.txt",e.ToString());Console.Error.WriteLine(e);return 1;}
 }
 static void Shot(Form f,string path){var roots=f.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();int w=Math.Max(f.ClientSize.Width,roots.Select(c=>c.Right).DefaultIfEmpty(1).Max());int h=Math.Max(f.ClientSize.Height,roots.Select(c=>c.Bottom).DefaultIfEmpty(1).Max());using var b=new Bitmap(w,h);using(var g=Graphics.FromImage(b))g.Clear(f.BackColor);foreach(var root in roots)if(root.Width>0&&root.Height>0)root.DrawToBitmap(b,root.Bounds);b.Save(path);}
}
