using System.Drawing;using System.Drawing.Imaging;using System.Reflection;using System.Windows.Forms;using AngraQaIntegration;
static class Program {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Bitmap Fixture(Color bg){var b=new Bitmap(420,420);using var g=Graphics.FromImage(b);g.Clear(bg);using var f=new Font("Segoe UI",40,FontStyle.Bold);g.DrawString("ANGRA",f,Brushes.Silver,60,145);g.DrawString("VIDROS",new Font("Segoe UI",24,FontStyle.Bold),Brushes.DeepSkyBlue,100,210);g.FillRectangle(Brushes.Black,180,167,5,5);return b;}
 static void Pump(){for(int i=0;i<8;i++){Application.DoEvents();Thread.Sleep(15);}}
 [STAThread]static void Main(string[] args){
  Directory.CreateDirectory("integration-evidence");Application.EnableVisualStyles();
  using var source=Fixture(Color.Black);source.Save("integration-evidence/logo-input-fixture.png");using var output=SalesHeaderLogo.ForHeader(source);Check(output.GetPixel(0,0).A==0,"Black border not transparent");Check(output.Width>output.Height,"Empty margins not cropped");Check(source.GetPixel(0,0).A==255,"Source modified");Check(output.GetPixel(184-GetLeft(source),169-GetTop(source)).A==255,"Interior black artwork removed");
  output.Save("integration-evidence/logo-transparent-fixture.png");
  using var white=Fixture(Color.White);using var whiteResult=SalesHeaderLogo.ForHeader(white);Check(whiteResult.Size==white.Size&&whiteResult.GetPixel(0,0).A==255,"White background changed");
  using var blue=Fixture(Color.DarkBlue);using var blueResult=SalesHeaderLogo.ForHeader(blue);Check(blueResult.Size==blue.Size&&blueResult.GetPixel(0,0).A==255,"Colored background changed");
  using var alpha=new Bitmap(100,100);using(var g=Graphics.FromImage(alpha)){g.Clear(Color.Transparent);g.FillRectangle(Brushes.Silver,20,40,60,20);}using var alphaOut=SalesHeaderLogo.ForHeader(alpha);Check(alphaOut.Width==68&&alphaOut.Height==28,"Existing alpha crop failed");
  using var blank=new Bitmap(12,12);using(var g=Graphics.FromImage(blank))g.Clear(Color.Black);using var safe=SalesHeaderLogo.ForHeader(blank);Check(safe.Size==blank.Size,"Empty artwork lost");
  using var form=new Form{Size=new Size(1100,550),BackColor=Color.FromArgb(7,31,54),Text="QA005 — fixture de validação do cabeçalho"};
  var grid=new TableLayoutPanel{Dock=DockStyle.Top,Height=108,ColumnCount=3,BackColor=Color.FromArgb(8,44,72)};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));
  var header=new PictureBox{Name="sales_company_logo",Dock=DockStyle.Fill,Image=new Bitmap(source),SizeMode=PictureBoxSizeMode.Zoom};grid.Controls.Add(header,0,0);grid.Controls.Add(new Label{Text="CAIXA LIVRE",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Cyan,Font=new Font("Segoe UI",24,FontStyle.Bold)},1,0);
  var product=new PictureBox{Name="sales_product_picture",Location=new Point(20,150),Size=new Size(280,300),Image=new Bitmap(source),SizeMode=PictureBoxSizeMode.Zoom};var originalProduct=product.Image;form.Controls.Add(grid);form.Controls.Add(product);form.Show();Pump();
  // Without the real QA PET loaded, the observer must not affect any form.
  SalesHeaderLogo.Observe();Pump();Check(((Bitmap)header.Image!).GetPixel(0,0).A==255,"Ungated installation modified");
  Assembly.LoadFrom(Path.GetFullPath(args[0]));SalesHeaderLogo.Observe();Pump();Check(((Bitmap)header.Image!).GetPixel(0,0).A==0,"QA005 header not treated");Check(ReferenceEquals(product.Image,originalProduct),"Large image changed");Check(((Bitmap)product.Image!).GetPixel(0,0).A==255,"Large image pixels changed");
  var bounds=header.Bounds;header.Image=new Bitmap(source);Pump();Check(((Bitmap)header.Image!).GetPixel(0,0).A==0&&header.Bounds==bounds,"Refresh lost treatment or changed layout");
  foreach(int width in new[]{850,1366,1920}){form.Size=new Size(width,550);Pump();Check(header.Width>100&&header.Height>40,"Header clipped");}
  form.Size=new Size(1100,550);Pump();using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("integration-evidence/header-only-fixture.png");}
  form.Close();header.Image?.Dispose();product.Image?.Dispose();Console.WriteLine("PASS QA005 header-only transparency, crop, source preservation, refresh, three widths and no application without QA PET");
 }
 static int GetLeft(Bitmap b){int v=b.Width;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);if(p.R>36||p.G>36||p.B>36)v=Math.Min(v,x);}return Math.Max(0,v-4);}
 static int GetTop(Bitmap b){int v=b.Height;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);if(p.R>36||p.G>36||p.B>36)v=Math.Min(v,y);}return Math.Max(0,v-4);}
}
