using LealInfoPDV;
using System.Reflection;
static partial class Tests
{
 static void ReportChecks(MainForm main,string id){
 string State()=>Convert.ToString(Sql("SELECT COUNT(*)||'|'||COALESCE(SUM(amount),0)||'|'||(SELECT COUNT(*) FROM sales)||'|'||(SELECT stock FROM products WHERE id=901) FROM cash_movements"))!;
 Sql("INSERT INTO sales(id,sold_at,payment,subtotal,total,operator) VALUES(901,datetime('now'),'PIX',25,25,'QA');INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES(datetime('now'),'ENTRADA','VENDA COM REFERÊNCIA NUMÉRICA',25,901)");
 var state=State();
   Exception? failure=null;bool completed=false;using var timer=new System.Windows.Forms.Timer{Interval=200};
   timer.Tick+=(_,_)=>{
    var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="LEAL INFO PDV • RELATÓRIO FINANCEIRO");if(f==null)return;timer.Stop();
    try{
     typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{f,"Futurista Azul",null});
     Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();Application.RaiseIdle(EventArgs.Empty);
     var indicators=(TableLayoutPanel)f.Controls.OfType<TableLayoutPanel>().Single().GetControlFromPosition(0,2)!;
     var expected=new[]{Color.FromArgb(0,125,185),Color.FromArgb(94,102,110),Color.FromArgb(0,145,92),Color.FromArgb(190,58,55),Color.FromArgb(95,72,175),Color.FromArgb(222,126,20)};
     void Colors(){for(int i=0;i<6;i++)Check(indicators.GetControlFromPosition(i,0)!.BackColor==expected[i],"indicator color preserved "+i);}
     Colors();typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{f,"Futurista Azul",null});Colors();
     f.WindowState=FormWindowState.Normal;f.MinimumSize=Size.Empty;
     foreach(var scale in new[]{1f,1.15f,1.5f}){
      if(scale!=1f)f.Scale(new SizeF(scale/(scale==1.15f?1f:1.15f),scale/(scale==1.15f?1f:1.15f)));
      foreach(var size in new[]{new Size(1360,680),new Size(1024,600)}){
       f.ClientSize=size;f.PerformLayout();Check(f.ClientSize==size,"requested client viewport applied");
       var layout=f.Controls.OfType<TableLayoutPanel>().Single();var footer=(FlowLayoutPanel)layout.GetControlFromPosition(0,5)!;
       foreach(var b in footer.Controls.OfType<Button>())Check(footer.ClientRectangle.Contains(b.Bounds)&&f.ClientRectangle.Contains(f.RectangleToClient(b.RectangleToScreen(b.ClientRectangle))),$"complete {b.Text} at {size.Width}x{size.Height}, scale {scale}");
       var tabs=Walk(f).OfType<TabControl>().Single();Check(tabs.Height>100,"table retains usable height");
      }
     }
     var grid=Walk(f).OfType<TabControl>().Single().TabPages[0].Controls.OfType<DataGridView>().Single();
     Check(grid.Columns["Descrição"].Width>grid.Columns["Código"].Width*2,"description receives more space than identifier");
     Check(grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToString(r.Cells["Venda"].Value)=="901")&&grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToString(r.Cells["Venda"].Value)==""),"manual and numeric sale references coexist without loading error");
     var data=grid.DataSource;var total=grid.Rows.Cast<DataGridViewRow>().Sum(r=>Convert.ToDouble(r.Cells["Valor"].Value));
     Walk(f).OfType<Button>().Single(b=>b.Text=="BUSCAR").PerformClick();Check(total==grid.Rows.Cast<DataGridViewRow>().Sum(r=>Convert.ToDouble(r.Cells["Valor"].Value)),"search reload retains financial values");
     f.Scale(new SizeF(1/1.5f,1/1.5f));f.ClientSize=new Size(1360,680);f.PerformLayout();
     using(var image=new Bitmap(f.ClientSize.Width,f.ClientSize.Height)){f.Controls[0].DrawToBitmap(image,f.Controls[0].Bounds);image.Save("evidence/"+id+"/financeiro.png");}
     Colors();
     Check(State()==state,"movements, sales and products unchanged");completed=true;
    }catch(Exception e){Console.Error.WriteLine(e);failure=e;}finally{f.Close();}
   };
   timer.Start();typeof(MainForm).GetMethod("OpenReports",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);timer.Stop();if(failure!=null)throw failure;Check(completed,"real report opens and closes");

 }
}
