using System.Drawing.Drawing2D;
namespace LealInfoPDV;
// Scoped to kitchen-owned controls; no global theme or business-state changes.
internal static class KitchenLighting
{
    internal static void Surface(Graphics g,Rectangle rect,Color accent,bool strong=true)
    {
        if(rect.Width<4||rect.Height<4)return;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=KitchenPremiumStyle.Round(rect,10);
        for(int i=12;i>=2;i-=2){using var halo=new Pen(Color.FromArgb(strong?22:10,accent),i);g.DrawPath(halo,shape);}
        using var fill=new LinearGradientBrush(rect,Color.FromArgb(strong?Math.Max(20,accent.R/3):25,strong?Math.Max(25,accent.G/3):35,strong?Math.Max(30,accent.B/3):43),Color.FromArgb(2,6,10),90f);
        g.FillPath(fill,shape);
        var state=g.Save();g.SetClip(shape);
        using(var ambient=new GraphicsPath())
        {ambient.AddEllipse(rect.Left-rect.Width/4,rect.Bottom-rect.Height/2,rect.Width*3/2,rect.Height);using var glow=new PathGradientBrush(ambient){CenterColor=Color.FromArgb(strong?135:48,accent),SurroundColors=new[]{Color.Transparent}};g.FillPath(glow,ambient);}
        var shine=new Rectangle(rect.Left,rect.Top,rect.Width,Math.Max(2,rect.Height/2));
        using(var reflection=new LinearGradientBrush(shine,Color.FromArgb(strong?85:35,Color.White),Color.Transparent,90f))g.FillRectangle(reflection,shine);
        g.Restore(state);
        using(var border=new Pen(Color.FromArgb(240,accent),1.5f))g.DrawPath(border,shape);
        var inner=Rectangle.Inflate(rect,-2,-2);using var bevel=KitchenPremiumStyle.Round(inner,8);using var edge=new Pen(Color.FromArgb(75,Color.White));g.DrawPath(edge,bevel);
        using var highlight=new Pen(Color.FromArgb(170,Color.White));g.DrawLine(highlight,rect.Left+12,rect.Top+2,rect.Right-12,rect.Top+2);
    }
    internal static void Activity(Graphics g,Rectangle rect,Color accent)
    {
        var track=new Rectangle(rect.Left+10,rect.Bottom-17,Math.Max(1,rect.Width-20),9);
        using(var shape=KitchenPremiumStyle.Round(track,4))using(var fill=new SolidBrush(Color.FromArgb(35,47,59)))g.FillPath(fill,shape);
        // State indicator only: no invented preparation percentage or completion estimate.
        var bar=new Rectangle(track.Left,track.Top,Math.Max(1,track.Width/2),track.Height);
        using var lit=KitchenPremiumStyle.Round(bar,4);using var color=new LinearGradientBrush(bar,Color.FromArgb(225,accent),Color.FromArgb(75,accent),90);g.FillPath(color,lit);
        using var pen=new Pen(Color.FromArgb(110,accent),4);g.DrawPath(pen,lit);
    }
}
internal sealed class KitchenProductCard : TableLayoutPanel
{
    internal KitchenProductCard(){DoubleBuffered=true;ResizeRedraw=true;}
    protected override void OnPaintBackground(PaintEventArgs e)
    {e.Graphics.Clear(KitchenPremiumStyle.Background);KitchenLighting.Surface(e.Graphics,new Rectangle(5,5,Math.Max(1,Width-11),Math.Max(1,Height-11)),KitchenPremiumStyle.Gold,false);}
}
