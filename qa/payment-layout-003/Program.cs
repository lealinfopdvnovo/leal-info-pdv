using LealInfoPDV;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
static class Tests
{
 const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> All(Control c)=>new[]{c}.Concat(c.Controls.Cast<Control>().SelectMany(All));
 static object? Sql(string text){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=text;return q.ExecuteScalar();}
 [STAThread]static int Main(){Directory.CreateDirectory("corrections-evidence");using var watchdog=new System.Threading.Timer(_=>Environment.Exit(91),null,120000,Timeout.Infinite);
 try{
 Check(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!File.Exists(Database.DbPath),"isolated native Windows profile without customer data");
 Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Database.Initialize();Auth.CreateUser("QA LAYOUT","qalayout","QA-layout-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qalayout","QA-layout-123!");
 InternalWorkspace.Install();PremiumGlobalTheme.Install();using var main=new MainForm();((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",P)!.GetValue(main)!).Stop();var events=(EventHandlerList)typeof(Component).GetProperty("Events",P)!.GetValue(main)!;var key=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(f=>f.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;var shown=events[key];events.RemoveHandler(key,shown);main.Show();Application.DoEvents();events.AddHandler(key,shown);
 string before=Convert.ToString(Sql("SELECT (SELECT COUNT(*) FROM sales)||':'||(SELECT COUNT(*) FROM sale_payments)||':'||(SELECT COUNT(*) FROM cash_movements)"))!;
 object? Run(int method,bool confirm)
 {
  Exception? failure=null;bool exercised=false;using var timer=new System.Windows.Forms.Timer{Interval=200};timer.Tick+=(_,_)=>{
   var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="RECEBER / PAGAMENTO — MESA 01");if(f==null||exercised)return;exercised=true;
   try{
    var tabs=All(f).OfType<TabControl>().Single();Check(tabs.SelectedIndex==method,"requested payment method preserved "+method);
    foreach(float factor in new[]{1f,1.25f,1.2f})
    {
     if(factor!=1)f.Scale(new SizeF(factor,factor));
     for(int i=0;i<4;i++)
     {
      tabs.SelectedIndex=i;f.PerformLayout();Application.DoEvents();var page=tabs.TabPages[i];var b=All(page).OfType<Button>().Single();
      var fields=page.Controls.OfType<Panel>().Single(p=>p.Dock==DockStyle.Fill);
      Check(!fields.Bounds.IntersectsWith(b.Parent!.Bounds),"fields reserve button space without overlap "+i+" at scale "+f.Width);
      var outerFooter=f.Controls.OfType<Panel>().Single(p=>p.Dock==DockStyle.Bottom);
      Check(!tabs.Bounds.IntersectsWith(outerFooter.Bounds),"tabs reserve cancel footer space "+i+" at scale "+f.Width);
      Check(Math.Abs(2*b.Left+b.Width-b.Parent!.ClientSize.Width)<=1,"confirmation centered on actual page "+i+" at scale "+f.Width);
      Check(b.Parent!.ClientRectangle.Contains(b.Bounds) && page.RectangleToScreen(page.ClientRectangle).Contains(b.Parent.RectangleToScreen(b.Bounds)),"confirmation fully visible without clipping "+i+" at scale "+f.Width);
      Check(All(page).OfType<Label>().Where(l=>l.TextAlign==ContentAlignment.MiddleCenter).All(l=>Math.Abs(2*l.Left+l.Width-l.Parent!.ClientSize.Width)<=1),"payment headings and amount share center "+i+" at scale "+f.Width);
      using(var shot=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("corrections-evidence/page-"+method+"-"+i+"-"+f.Width+".png");}
      if(i==0){var amount=All(page).OfType<NumericUpDown>().Single();amount.Value=25m;Check(All(page).OfType<Label>().Any(l=>l.Text.Contains("5,00")&&l.Text.StartsWith("TROCO")),"change still calculated after entering received amount");amount.Value=20m;}
     }
     var cancel=All(f).OfType<Button>().Single(b=>b.Text=="CANCELAR");Check(cancel.Parent!.ClientRectangle.Contains(cancel.Bounds),"cancel button remains contained at scale "+f.Width);
     using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("corrections-evidence/payment-"+method+"-"+f.Width+".png");
    }
    tabs.SelectedIndex=method;
    if(confirm)All(tabs.TabPages[0]).OfType<Button>().Single().PerformClick();else All(f).OfType<Button>().Single(b=>b.Text=="CANCELAR").PerformClick();
   }catch(Exception e){failure=e;f.Close();}
  };timer.Start();var result=typeof(MainForm).GetMethod("SelectPayment",P)!.Invoke(main,new object?[]{20d,method,main,"RECEBER / PAGAMENTO — MESA 01"});timer.Stop();if(failure!=null)throw failure;Check(exercised,"actual modal payment dialog exercised");return result;
 }
 Check(Run(0,true)!=null,"cash confirmation returns selected tender normally");Check(Run(3,false)==null,"cancel returns no charge after switching payment methods");
 Check(Convert.ToString(Sql("SELECT (SELECT COUNT(*) FROM sales)||':'||(SELECT COUNT(*) FROM sale_payments)||':'||(SELECT COUNT(*) FROM cash_movements)"))==before,"layout and tender selection write no sales payments or cash movements");
 Check(Convert.ToString(Sql("PRAGMA quick_check"))=="ok","database integrity unchanged");
 main.Close();File.WriteAllText("corrections-evidence/results.json",JsonSerializer.Serialize(new{success=true,channel="003",source=Environment.GetEnvironmentVariable("APPROVED_SOURCE"),checks,nativeWindows=true,published=false}));Console.WriteLine("PASS PAYMENT LAYOUT 003 ONLY");Environment.Exit(0);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("corrections-evidence/failure.txt",e.ToString());return 1;}
 }
}
