using LealInfoPDV;
using System.Reflection;
using System.Text.Json;
static class FinanceLayoutTests
{
 static IEnumerable<Control> Walk(Control root)=>root.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Walk(c)));
 [STAThread] static int Main(){
  Directory.CreateDirectory("evidence");using var guard=new System.Threading.Timer(_=>Environment.Exit(91),null,90000,Timeout.Infinite);
  var checks=new List<string>();void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
  try{
   Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&!File.Exists(Database.DbPath),"isolated Windows, no store database");
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Database.Initialize();
   Auth.CreateUser("QA","qafinance","QA-finance-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qafinance","QA-finance-123!");
   using(var db=Database.Open()){using var q=db.CreateCommand();q.CommandText="INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES(datetime('now'),'ENTRADA','DESCRIÇÃO COMPLETA DO MOVIMENTO FINANCEIRO DA LOJA',230,NULL)";q.ExecuteNonQuery();}
   string State(){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT COUNT(*)||'|'||SUM(amount)||'|'||(SELECT COUNT(*) FROM sales)||'|'||(SELECT COUNT(*) FROM products) FROM cash_movements";return Convert.ToString(q.ExecuteScalar())!;}
   var state=State();using var owner=new MainForm();
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",flags)!.GetValue(owner)!).Stop();
   Exception? failure=null;bool completed=false;using var timer=new System.Windows.Forms.Timer{Interval=200};
   timer.Tick+=(_,_)=>{
    var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="LEAL INFO PDV • RELATÓRIO FINANCEIRO");if(f==null)return;timer.Stop();
    try{
     var pet=new LojaTestExtension();pet.Customize(f);pet.Customize(f);
     f.WindowState=FormWindowState.Normal;
     foreach(var scale in new[]{1f,1.25f,1.5f}){
      if(scale!=1f)f.Scale(new SizeF(scale/(scale==1.25f?1f:1.25f),scale/(scale==1.25f?1f:1.25f)));
      foreach(var size in new[]{new Size(1360,680),new Size(1024,600)}){
       f.ClientSize=size;f.PerformLayout();
       var layout=f.Controls.OfType<TableLayoutPanel>().Single();var footer=(FlowLayoutPanel)layout.GetControlFromPosition(0,5)!;
       foreach(var b in footer.Controls.OfType<Button>())Check(footer.ClientRectangle.Contains(b.Bounds)&&f.ClientRectangle.Contains(f.RectangleToClient(b.RectangleToScreen(b.ClientRectangle))),$"complete {b.Text} at {size.Width}x{size.Height}, scale {scale}");
       var tabs=Walk(f).OfType<TabControl>().Single();Check(tabs.Height>100,"table retains usable height");
      }
     }
     var grid=Walk(f).OfType<TabControl>().Single().TabPages[0].Controls.OfType<DataGridView>().Single();
     Check(grid.Columns["Descrição"].Width>grid.Columns["Código"].Width*2,"description receives more space than identifier");
     var data=grid.DataSource;var total=grid.Rows.Cast<DataGridViewRow>().Sum(r=>Convert.ToDouble(r.Cells["Valor"].Value));
     Walk(f).OfType<Button>().Single(b=>b.Text=="BUSCAR").PerformClick();Check(total==grid.Rows.Cast<DataGridViewRow>().Sum(r=>Convert.ToDouble(r.Cells["Valor"].Value)),"search reload retains financial values");
     f.Scale(new SizeF(1/1.5f,1/1.5f));f.ClientSize=new Size(1360,680);f.PerformLayout();
     using(var image=new Bitmap(f.ClientSize.Width,f.ClientSize.Height)){f.Controls[0].DrawToBitmap(image,f.Controls[0].Bounds);image.Save("evidence/financeiro-corrigido.png");}
     Check(State()==state,"movements, sales and products unchanged");completed=true;
    }catch(Exception e){failure=e;}finally{f.Close();}
   };
   timer.Start();typeof(MainForm).GetMethod("OpenReports",flags)!.Invoke(owner,null);timer.Stop();if(failure!=null)throw failure;Check(completed,"real report opens and closes");
   File.WriteAllText("evidence/finance-results.json",JsonSerializer.Serialize(new{Success=true,checks,CustomerWindowsTested=false}));return 0;
  }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("evidence/finance-failure.txt",e.ToString());return 1;}
 }
}
