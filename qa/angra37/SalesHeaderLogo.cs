using System.Drawing;using System.Drawing.Imaging;using System.Reflection;using System.Runtime.CompilerServices;using System.Runtime.InteropServices;using System.Windows.Forms;
namespace AngraQaIntegration;
/// <summary>Display-only treatment of QA005 sales header; never writes branding settings or source assets.</summary>
public static class SalesHeaderLogo {
 static bool watching;static readonly ConditionalWeakTable<PictureBox,State> states=new();
 static bool QaPetLoaded()=>AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType("ProfileExtension",false)!=null&&a.GetCustomAttributes<AssemblyMetadataAttribute>().Any(x=>x.Key=="PetIdentity"&&x.Value=="005"));
 public static void Observe(){if(watching||!QaPetLoaded())return;watching=true;Application.Idle+=Scan;Scan(null,EventArgs.Empty);}
 static IEnumerable<Control> All(Control c){yield return c;foreach(Control x in c.Controls)foreach(var y in All(x))yield return y;}
 static void Scan(object? s,EventArgs e){foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())foreach(var box in All(form).OfType<PictureBox>().Where(x=>x.Name=="sales_company_logo"))if(!states.TryGetValue(box,out _))states.Add(box,new State(box));}
 sealed class State {
  readonly PictureBox box;Image? seen;bool busy;
  public State(PictureBox box){this.box=box;box.Invalidated+=Changed;box.Disposed+=Disposed;Update();}
  void Changed(object? s,InvalidateEventArgs e)=>Update();
  void Update(){if(busy||box.IsDisposed||ReferenceEquals(seen,box.Image))return;busy=true;try{var old=box.Image;if(old==null){seen=null;return;}var next=ForHeader(old);seen=next;box.BackColor=Color.Transparent;box.SizeMode=PictureBoxSizeMode.Zoom;box.Image=next;old.Dispose();}finally{busy=false;}}
  void Disposed(object? s,EventArgs e){box.Invalidated-=Changed;box.Disposed-=Disposed;seen=null;}
 }
 public static Bitmap ForHeader(Image source){
  using var copy=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(copy)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImageUnscaled(source,0,0);}
  int w=copy.Width,h=copy.Height;var rect=new Rectangle(0,0,w,h);var data=copy.LockBits(rect,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);var bytes=new byte[data.Stride*h];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
  bool Dark(int i){int max=Math.Max(bytes[i],Math.Max(bytes[i+1],bytes[i+2])),min=Math.Min(bytes[i],Math.Min(bytes[i+1],bytes[i+2]));return bytes[i+3]>=250&&max<=36&&max-min<=18;}
  var edge=new List<int>();for(int x=0;x<w;x++){edge.Add(x);if(h>1)edge.Add((h-1)*w+x);}for(int y=1;y<h-1;y++){edge.Add(y*w);if(w>1)edge.Add(y*w+w-1);}
  // Only a predominantly neutral black perimeter qualifies. White/colored artwork is left intact.
  int dark=edge.Count(p=>Dark((p/w)*data.Stride+(p%w)*4));
  if(edge.Count>0&&dark>=edge.Count*.9){var visited=new bool[w*h];var queue=new Queue<int>();foreach(int p in edge)if(!visited[p]&&Dark((p/w)*data.Stride+(p%w)*4)){visited[p]=true;queue.Enqueue(p);}
   while(queue.Count>0){int p=queue.Dequeue(),x=p%w,y=p/w,i=y*data.Stride+x*4;bytes[i+3]=0;
    void Add(int n){if(!visited[n]&&Dark((n/w)*data.Stride+(n%w)*4)){visited[n]=true;queue.Enqueue(n);}}
    if(x>0)Add(p-1);if(x+1<w)Add(p+1);if(y>0)Add(p-w);if(y+1<h)Add(p+w);
   }
  }
  int left=w,top=h,right=-1,bottom=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(bytes[y*data.Stride+x*4+3]>8){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
  Marshal.Copy(bytes,0,data.Scan0,bytes.Length);copy.UnlockBits(data);
  if(right<left||bottom<top)return new Bitmap(source); // Fail safe for an all-black or empty image.
  left=Math.Max(0,left-4);top=Math.Max(0,top-4);right=Math.Min(w-1,right+4);bottom=Math.Min(h-1,bottom+4);
  return copy.Clone(new Rectangle(left,top,right-left+1,bottom-top+1),PixelFormat.Format32bppArgb);
 }
}
