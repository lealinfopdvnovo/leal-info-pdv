using System.Reflection;
using LealInfoPDV;
namespace AngraTextIdentity;
public static class Identity
{
 static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PictureBox,Image> Decorated=new();
 static void HomeFooter(MainForm form,string version){
  var picture=(PictureBox?)typeof(MainForm).GetField("mainScreenPicture",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(form);
  if(picture?.Image==null||Decorated.TryGetValue(picture,out _))return;
  var path=Path.Combine(AppContext.BaseDirectory,"Assets","tela_principal.png");
  if(!File.Exists(path)||Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()!="027179684aab68e76da9d1d81ae20b523bf07ae3a401e8eae658c58b481704da")return;
  using var source=new Bitmap(path);
  if(picture.Image.Size!=source.Size)return;
  static byte[] Encode(Image image){using var m=new MemoryStream();image.Save(m,System.Drawing.Imaging.ImageFormat.Png);return System.Security.Cryptography.SHA256.HashData(m.ToArray());}
  if(!Encode(picture.Image).SequenceEqual(Encode(source)))return; // retain any owner-selected custom artwork
  var edited=new Bitmap(source);
  using(var g=Graphics.FromImage(edited)){
   // Only the baked establishment text is replaced; retain monitor icon, dividers, central manufacturer artwork and navigation.
   g.DrawImage(source,new Rectangle(70,903,350,32),new Rectangle(330,903,80,32),GraphicsUnit.Pixel);
   using var font=new Font("Segoe UI",17,FontStyle.Regular,GraphicsUnit.Pixel);
   using var brush=new SolidBrush(Color.White);
   g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   g.DrawString("ANGRA VIDROS • PET V"+version,font,brush,new PointF(75,905));
  }
  var previous=picture.Image;picture.Image=edited;Decorated.Add(picture,edited);previous.Dispose();
 }
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
 public static void Apply(Form form,string version){
  var t=typeof(LoginForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
  var current=t.GetProperty("Current",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null);
  if(!QaIsolation.Isolated||current==null||(string?)current.GetType().GetProperty("ClientCode")!.GetValue(current)!="005")return;
  if(form is LoginForm){
   var plan=((string?)current.GetType().GetProperty("Plan")!.GetValue(current))?.Trim().ToLowerInvariant();
   var name=plan switch{"standard"=>"STANDARD","plus"=>"PLUS","pro"=>"PRO",_=>"NÃO ATIVADO"};
   foreach(var label in Walk(form).OfType<Label>())if(label.Text is "PDV PRO" or "PDV STANDARD" or "PDV PLUS" or "PDV NÃO ATIVADO")label.Text="PDV "+name;
  }
  if(form is MainForm main){
   HomeFooter(main,version);
   // Window title identifies the establishment; retain the manufacturer's original name.
   var title=System.Text.RegularExpressions.Regex.Replace(form.Text,@"(?:\s*[—|]\s*ANGRA VIDROS\s*•\s*PET V[\d.]+)+\s*$","");
   if(!title.StartsWith("ANGRA VIDROS",StringComparison.Ordinal))title="ANGRA VIDROS — "+title;
   form.Text=title+" — ANGRA VIDROS • PET V"+version;
   foreach(var strip in Walk(form).OfType<StatusStrip>())foreach(ToolStripItem item in strip.Items){
    if(item.Text=="LEAL INFO CONECTADO")item.Text="ANGRA VIDROS";
    if(item.Name=="loja_pet_version")item.Text="ANGRA VIDROS • PET V"+version;
   }
  }
 }
}
