using LealInfoPDV;
using System.Drawing.Drawing2D;
public sealed class LojaTestExtension : IPetExtension
{
 public void Customize(Form form)
 {
  if(form.GetType().Name=="MainForm"){
   const string old=" — LOJA LEAL INFO • PET V1.1",marker=" — LOJA LEAL INFO • PET V1.2";
   if(form.Text.EndsWith(old,StringComparison.Ordinal))form.Text=form.Text[..^old.Length];
   if(!form.Text.EndsWith(marker,StringComparison.Ordinal))form.Text+=marker;
   foreach(var strip in Walk(form).OfType<StatusStrip>())
    foreach(var label in strip.Items.OfType<ToolStripStatusLabel>().Where(l=>l.Text=="V"+UpdateManager.CurrentVersion||l.Name=="loja_pet_version")){
     label.Name="loja_pet_version";
     label.Text="PDV "+UpdateManager.CurrentVersion+" | LOJA LEAL INFO • PET V1.2";
     label.AutoSize=true;label.Spring=false;label.Overflow=ToolStripItemOverflow.Never;
    }
  }
  if(!form.Text.Contains("TELA DE VENDA",StringComparison.OrdinalIgnoreCase))return;
  var logo=Walk(form).OfType<PictureBox>().FirstOrDefault(p=>p.Name=="sales_company_logo");
  if(logo==null||Equals(logo.Tag,"LOJA-PET-1.2-LOGO"))return;
  logo.Tag="LOJA-PET-1.2-LOGO";
  Image? previous=null;Rectangle crop=Rectangle.Empty;
  logo.Paint+=(_,e)=>{
   var source=logo.Image;if(source==null)return;
   if(!ReferenceEquals(previous,source)){crop=ContentBounds(source);previous=source;}
   var parent=logo.Parent;while(parent!=null&&parent.BackColor.A==0)parent=parent.Parent;
   using(var bg=new SolidBrush(parent?.BackColor??Color.FromArgb(7,24,43)))e.Graphics.FillRectangle(bg,logo.ClientRectangle);
   var area=Rectangle.Inflate(logo.ClientRectangle,-5,-5);if(area.Width<=0||area.Height<=0)return;
   float scale=Math.Min((float)area.Width/crop.Width,(float)area.Height/crop.Height);
   var target=new RectangleF(area.X+(area.Width-crop.Width*scale)/2,area.Y+(area.Height-crop.Height*scale)/2,crop.Width*scale,crop.Height*scale);
   e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
   e.Graphics.DrawImage(source,target,crop,GraphicsUnit.Pixel);
  };
  logo.Invalidate();
 }
 // Remove only empty, transparent or uniform margins when displaying the logo.
 // The registered image and the database remain unchanged.
 internal static Rectangle ContentBounds(Image image)
 {
  using var b=new Bitmap(image);var bg=b.GetPixel(0,0);
  int Diff(Color a,Color z)=>Math.Max(Math.Abs(a.R-z.R),Math.Max(Math.Abs(a.G-z.G),Math.Abs(a.B-z.B)));
  bool uniform=new[]{b.GetPixel(b.Width-1,0),b.GetPixel(0,b.Height-1),b.GetPixel(b.Width-1,b.Height-1)}.All(c=>Diff(c,bg)<12);
  int left=b.Width,top=b.Height,right=-1,bottom=-1;
  for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){
   var c=b.GetPixel(x,y);if(c.A<24||uniform&&bg.A>240&&Diff(c,bg)<32)continue;
   left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
  }
  if(right<left||bottom<top)return new Rectangle(0,0,b.Width,b.Height);
  return Rectangle.FromLTRB(Math.Max(0,left-3),Math.Max(0,top-3),Math.Min(b.Width,right+4),Math.Min(b.Height,bottom+4));
 }
 static IEnumerable<Control> Walk(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
}
