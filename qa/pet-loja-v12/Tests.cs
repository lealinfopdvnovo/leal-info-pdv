using System.Text.Json;
static class Tests{
 [STAThread]static int Main(){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
  Application.EnableVisualStyles();Directory.CreateDirectory("evidence");var checks=new List<string>();
  void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks.Add(msg);Console.WriteLine("PASS "+msg);}
  try{
   var ext=new LojaTestExtension();
   using var other=new Form{Text="PRODUTOS"};ext.Customize(other);Check(other.Controls.Count==0,"unrelated windows unchanged");
   foreach(int width in new[]{1024,1366,1920}){
    using var f=new Form{Text="LEAL INFO CONECTADO • TELA DE VENDA",ClientSize=new Size(width,650),BackColor=Color.FromArgb(16,29,45)};
    var grid=new TableLayoutPanel{Dock=DockStyle.Top,Height=110,ColumnCount=3,RowCount=1};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));f.Controls.Add(grid);
    grid.Controls.Add(new Label{Text="CAIXA LIVRE",Dock=DockStyle.Fill,ForeColor=Color.White,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",24,FontStyle.Bold)},1,0);
    var panel=new TableLayoutPanel{Dock=DockStyle.Right,Width=160,RowCount=2,ColumnCount=1};panel.RowStyles.Add(new RowStyle(SizeType.Percent,50));panel.RowStyles.Add(new RowStyle(SizeType.Percent,50));
    var style=new Button{Text="ESTILO",Dock=DockStyle.Fill};var close=new Button{Text="SAIR",Dock=DockStyle.Fill};int styleClicks=0;style.Click+=(_,_)=>styleClicks++;
    panel.Controls.Add(style,0,0);panel.Controls.Add(close,0,1);grid.Controls.Add(panel,2,0);f.Show();Application.DoEvents();
    if(width==1366)Shot(f,"evidence/antes-v11.png");
    ext.Customize(f);ext.Customize(f);Application.DoEvents();
    var found=f.Controls.Find("pet001_test_button",true);Check(found.Length==1,"exactly one test button at "+width);
    var b=(Button)found[0];Check(b.Right<=style.Left&&b.Top==style.Top&&b.Width>100,"test button sits left of style without overlap at "+width);
    style.PerformClick();Check(styleClicks==1,"existing style handler retained at "+width);
    bool opened=false;using var timer=new System.Windows.Forms.Timer{Interval=100};timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="pet001_test_dialog");if(dialog!=null){opened=true;dialog.Close();}};timer.Start();b.PerformClick();timer.Stop();Check(opened,"left click opens floating test dialog at "+width);
    if(width==1366)Shot(f,"evidence/depois-v12.png");f.Close();
   }
   File.WriteAllText("evidence/results.json",JsonSerializer.Serialize(new{Success=true,Pet="001",Version="1.2",checks,FixtureLayoutOnly=true,RealSalesNotTested=true,GlobalModified=false,Published=false,Installed=false}));return 0;
  }catch(Exception e){File.WriteAllText("evidence/failure.txt",e.ToString());Console.Error.WriteLine(e);return 1;}
 }
 static void Shot(Form f,string path){using var b=new Bitmap(f.Width,f.Height);f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}
}
