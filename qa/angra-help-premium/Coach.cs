using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using LealInfoPDV;
namespace AngraHelpPremium;
public static class Coach {
 static bool installed;
 static readonly ConditionalWeakTable<MainForm,Session> Sessions=new();
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
 static bool Allowed(){var t=typeof(MainForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;var current=t.GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(null);return QaIsolation.Isolated&&current!=null&&(string?)current.GetType().GetProperty("ClientCode")!.GetValue(current)=="005";}
 public static void Apply(Form? form){
  if(!Allowed())return;
  if(form==null){if(installed)return;installed=true;Application.Idle+=Observe;Observe(null,EventArgs.Empty);return;}
  if(form is MainForm main&&!main.IsDisposed)Sessions.GetValue(main,owner=>new Session(owner));
 }
 static void Observe(object? s,EventArgs e){foreach(var main in Application.OpenForms.OfType<MainForm>().ToArray())Apply(main);}
 public static string Evidence(Form form)=>form is MainForm main&&Sessions.TryGetValue(main,out var session)?session.Evidence():"{}";
 static ToolStripItem? Help(MainForm owner)=>Walk(owner).OfType<MenuStrip>().Where(m=>m.Visible).SelectMany(m=>m.Items.Cast<ToolStripItem>()).FirstOrDefault(i=>i.Visible&&i.Available&&i.Enabled&&i.Placement==ToolStripItemPlacement.Main&&i.Text.Replace("&","").Trim().Equals("AJUDA",StringComparison.OrdinalIgnoreCase));
 static Rectangle ScreenBounds(ToolStripItem item)=>item.Owner!.RectangleToScreen(item.Bounds);
 sealed class Session {
  readonly MainForm owner;readonly System.Windows.Forms.Timer tick=new(){Interval=16};readonly Stopwatch clock=new();
  Overlay? overlay;Rectangle lastTarget;DateTime readySince;bool started,finished;int plays,frames;
  double circleAt=-1,arrowAt=-1,bubbleAt=-1,fadeAt=-1,closedAt=-1;Rectangle target,bubble,canvas;string? error;
  public Session(MainForm owner){this.owner=owner;tick.Tick+=Tick;owner.FormClosed+=(_,_)=>Finish();tick.Start();}
  public string Evidence()=>JsonSerializer.Serialize(new{Identity="005",Version="1.10",Started=started,Finished=finished,Plays=plays,Frames=frames,Elapsed=clock.Elapsed.TotalSeconds,CircleCompletedAt=circleAt,ArrowStartedAt=arrowAt,BubbleStartedAt=bubbleAt,FadeStartedAt=fadeAt,ClosedAt=closedAt,Target=Rect(target),Bubble=Rect(bubble),Canvas=Rect(canvas),Error=error});
  static object Rect(Rectangle r)=>new{r.X,r.Y,r.Width,r.Height};
  void Tick(object? s,EventArgs e){
   if(finished)return;
   try{
    if(owner.IsDisposed||owner.Disposing){Finish();return;}
    var help=Help(owner);
    if(!started){
     if(!owner.Visible||!owner.Enabled||owner.WindowState==FormWindowState.Minimized||Form.ActiveForm!=owner||help==null){readySince=default;return;}
     var candidate=ScreenBounds(help);
     if(candidate.IsEmpty||!owner.RectangleToScreen(owner.ClientRectangle).Contains(candidate)||!Screen.FromRectangle(candidate).WorkingArea.Contains(candidate)){readySince=default;return;}
     if(candidate!=lastTarget||readySince==default){lastTarget=candidate;readySince=DateTime.UtcNow;return;}
     if((DateTime.UtcNow-readySince).TotalMilliseconds<200)return;
     started=true;plays++;overlay=new Overlay();clock.Start();Draw(help,0);overlay.Show(owner);return;
    }
    double seconds=clock.Elapsed.TotalSeconds;
    if(seconds>=3){Finish();return;}
    if(!owner.Visible||owner.WindowState==FormWindowState.Minimized||help==null){Finish();return;}
    Draw(help,seconds);
   }catch(Exception ex){error=ex.GetType().Name;Finish();} // purely visual; never interrupt the PDV
  }
  void Draw(ToolStripItem help,double seconds){
   float scale=Math.Max(0.75f,owner.DeviceDpi/96f);target=ScreenBounds(help);
   if(!owner.RectangleToScreen(owner.ClientRectangle).Contains(target)||!Screen.FromRectangle(target).WorkingArea.Contains(target)){Finish();return;}
   var area=Rectangle.Intersect(owner.RectangleToScreen(owner.ClientRectangle),Screen.FromRectangle(target).WorkingArea);
   if(area.Width<100||area.Height<100){Finish();return;}
   int S(float value)=>(int)Math.Round(value*scale);
   var ring=target;ring.Inflate(S(9),S(7));
   int topBars=Walk(owner).Where(c=>c.Visible&&c.Dock==DockStyle.Top&&(c is ToolStrip||c is FlowLayoutPanel)).Select(c=>c.RectangleToScreen(c.ClientRectangle).Bottom).DefaultIfEmpty(target.Bottom).Max();
   int width=Math.Min(S(254),Math.Max(160,area.Width-S(20))),height=S(68);
   int bx=Math.Clamp(target.Left+target.Width/2-width/2,area.Left+S(10),Math.Max(area.Left+S(10),area.Right-width-S(10)));
   int by=Math.Clamp(Math.Max(target.Bottom+S(86),topBars+S(14)),area.Top+S(10),Math.Max(area.Top+S(10),area.Bottom-height-S(10)));
   bubble=new Rectangle(bx,by,width,height);
   var tip=new PointF(target.Left+target.Width/2f,ring.Bottom+S(3));
   var tail=new PointF(Math.Clamp(tip.X+S(62),area.Left+S(12),area.Right-S(12)),bubble.Top-S(10));
   var arrowBounds=Rectangle.FromLTRB((int)Math.Min(tip.X,tail.X)-S(22),(int)Math.Min(tip.Y,tail.Y)-S(22),(int)Math.Max(tip.X,tail.X)+S(22),(int)Math.Max(tip.Y,tail.Y)+S(22));
   canvas=Rectangle.Union(Rectangle.Union(ring,bubble),arrowBounds);canvas.Inflate(S(12),S(12));canvas=Rectangle.Intersect(canvas,Screen.FromRectangle(target).Bounds);
   if(canvas.Width<1||canvas.Height<1){Finish();return;}
   if(seconds>=.8&&circleAt<0)circleAt=seconds;
   if(seconds>=.8&&arrowAt<0)arrowAt=seconds;
   if(seconds>=1.3&&bubbleAt<0)bubbleAt=seconds;
   if(seconds>=2.5&&fadeAt<0)fadeAt=seconds;
   overlay!.Render(canvas,g=>{
    g.TranslateTransform(-canvas.Left,-canvas.Top);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    float fade=seconds<2.5?1:1-Ease((float)((seconds-2.5)/.5));
    float progress=Ease((float)Math.Clamp(seconds/.8,0,1));
    if(progress>0){
     using var path=new GraphicsPath();var points=new List<PointF>();int count=Math.Max(2,(int)Math.Ceiling(120*progress));
     for(int i=0;i<=count;i++){double angle=(-100+360*progress*i/count)*Math.PI/180;float wobble=1+(float)(Math.Sin(angle*3)*.012);points.Add(new PointF(ring.Left+ring.Width/2f+(float)Math.Cos(angle)*ring.Width/2*wobble,ring.Top+ring.Height/2f+(float)Math.Sin(angle)*ring.Height/2*wobble));}
     path.AddLines(points.ToArray());Glow(g,path,Color.FromArgb(255,62,70),3f*scale,fade);
    }
    if(seconds>=.8){
     float alpha=fade*Ease((float)Math.Clamp((seconds-.8)/.18,0,1))*(.87f+.13f*(float)Math.Sin(seconds*8));
     using var path=new GraphicsPath();path.AddBezier(tail,new PointF(tail.X,tip.Y+S(40)),new PointF(tip.X+S(18),tip.Y+S(28)),tip);
     path.StartFigure();path.AddLine(new PointF(tip.X-S(9),tip.Y+S(13)),tip);path.AddLine(tip,new PointF(tip.X+S(13),tip.Y+S(8)));
     Glow(g,path,Color.FromArgb(0,218,255),2.8f*scale,alpha);
    }
    if(seconds>=1.3){
     float alpha=fade*Ease((float)Math.Clamp((seconds-1.3)/.16,0,1));
     using var path=Rounded(bubble,S(13));Glow(g,path,Color.FromArgb(0,200,255),1.7f*scale,alpha);
     using var fill=new LinearGradientBrush(bubble,Color.FromArgb((int)(240*alpha),8,43,72),Color.FromArgb((int)(240*alpha),2,18,36),LinearGradientMode.Vertical);g.FillPath(fill,path);
     using var outline=new Pen(Color.FromArgb((int)(220*alpha),0,220,255),1.5f*scale);g.DrawPath(outline,path);
     using var title=new Font("Segoe UI",13.5f*scale,FontStyle.Bold,GraphicsUnit.Pixel);using var body=new Font("Segoe UI",12f*scale,FontStyle.Regular,GraphicsUnit.Pixel);
     using var cyan=new SolidBrush(Color.FromArgb((int)(255*alpha),82,235,255));using var white=new SolidBrush(Color.FromArgb((int)(255*alpha),237,248,255));
     using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
     g.DrawString("PRECISA DE AJUDA?",title,cyan,new RectangleF(bubble.X+S(9),bubble.Y+S(9),bubble.Width-S(18),S(22)),format);
     g.DrawString("Conheça nossos tutoriais!",body,white,new RectangleF(bubble.X+S(9),bubble.Y+S(32),bubble.Width-S(18),S(23)),format);
    }
   });frames++;
  }
  void Finish(){if(finished)return;finished=true;closedAt=started?clock.Elapsed.TotalSeconds:-1;clock.Stop();tick.Stop();tick.Tick-=Tick;tick.Dispose();if(overlay!=null){overlay.Close();overlay.Dispose();overlay=null;}}
 }
 static float Ease(float t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
 static void Glow(Graphics g,GraphicsPath path,Color color,float width,float alpha){
  foreach(var layer in new[]{(width*4,24),(width*2.4f,55),(width,245)}){using var pen=new Pen(Color.FromArgb(Math.Clamp((int)(layer.Item2*alpha),0,255),color),layer.Item1){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};g.DrawPath(pen,path);}
 }
 static GraphicsPath Rounded(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 sealed class Overlay:Form {
  Bitmap? buffer;
  public Overlay(){Name="AngraHelpCoach";Text="ANGRA QA — AJUDA";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;}
  protected override bool ShowWithoutActivation=>true;
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x00080000|0x20|0x08000000|0x80;return p;}}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){}
  protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
  public void Render(Rectangle bounds,Action<Graphics> paint){
   if(buffer==null||buffer.Size!=bounds.Size){buffer?.Dispose();buffer=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppPArgb);}
   using(var g=Graphics.FromImage(buffer)){g.Clear(Color.Transparent);paint(g);}
   Bounds=bounds;
   var screen=GetDC(IntPtr.Zero);var dc=CreateCompatibleDC(screen);var bitmap=buffer.GetHbitmap(Color.FromArgb(0));var previous=SelectObject(dc,bitmap);
   try{var position=new PointNative(bounds.Left,bounds.Top);var origin=new PointNative(0,0);var size=new SizeNative(bounds.Width,bounds.Height);var blend=new Blend{Operation=0,Flags=0,Alpha=255,Format=1};if(!UpdateLayeredWindow(Handle,screen,ref position,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
   finally{SelectObject(dc,previous);DeleteObject(bitmap);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}
  }
  protected override void Dispose(bool disposing){if(disposing){buffer?.Dispose();buffer=null;}base.Dispose(disposing);}
 }
 [StructLayout(LayoutKind.Sequential)]struct PointNative{public int X,Y;public PointNative(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)]struct SizeNative{public int Width,Height;public SizeNative(int w,int h){Width=w;Height=h;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Operation,Flags,Alpha,Format;}
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr h);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr h,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dc,ref PointNative dest,ref SizeNative size,IntPtr source,ref PointNative origin,uint key,ref Blend blend,uint flags);
}
