using System.Drawing.Drawing2D;
namespace LealInfoPDV;
internal sealed class KitchenCounterBadge : Label
{
    internal KitchenCounterBadge(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(KitchenPremiumStyle.Background);var box=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));using var shape=KitchenPremiumStyle.Round(box,7);
        KitchenLighting.Surface(g,new Rectangle(5,5,Math.Max(1,Width-11),Math.Max(1,Height-11)),ForeColor,false);
        var lines=Text.Split('\n');KitchenPremiumStyle.DrawFittedText(g,lines.ElementAtOrDefault(0)??"",new Rectangle(5,4,Width-10,Math.Max(1,Height*55/100)),ForeColor,Math.Min(30,Height*.38f));
        KitchenPremiumStyle.DrawFittedText(g,lines.ElementAtOrDefault(1)??"",new Rectangle(5,Height*57/100,Width-10,Math.Max(1,Height*35/100)),ForeColor,12*g.DpiY/96f);
    }
}
internal static class KitchenGold
{
    internal static void DrawTitle(Graphics g,string text,Rectangle box,float pixels)
    {
        using var font=new Font("Segoe UI",pixels,FontStyle.Bold,GraphicsUnit.Pixel);using var path=new GraphicsPath();path.AddString(text,font.FontFamily,(int)FontStyle.Bold,pixels,Point.Empty,StringFormat.GenericTypographic);
        var bounds=path.GetBounds();float scale=Math.Min(1,Math.Min((box.Width-4)/Math.Max(1,bounds.Width),(box.Height-3)/Math.Max(1,bounds.Height)));
        using var transform=new Matrix();transform.Translate(box.Left+(box.Width-bounds.Width*scale)/2-bounds.X*scale,box.Top+(box.Height-bounds.Height*scale)/2-bounds.Y*scale);transform.Scale(scale,scale);path.Transform(transform);
        for(int i=8;i>=2;i-=2){using var glow=new Pen(Color.FromArgb(22,KitchenPremiumStyle.Gold),i);g.DrawPath(glow,path);}
        using(var shadow=new Pen(Color.FromArgb(90,45,3),3))g.DrawPath(shadow,path);
        using var gold=new LinearGradientBrush(box,Color.White,KitchenPremiumStyle.Gold,90);gold.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(255,254,219),Color.FromArgb(255,221,106),Color.FromArgb(133,75,8),Color.FromArgb(255,235,159)},Positions=new[]{0f,.38f,.55f,1f}};g.FillPath(gold,path);
        using var edge=new Pen(Color.FromArgb(180,255,244,192),.7f);g.DrawPath(edge,path);
    }
    internal static void Icon(Graphics g,Rectangle box,bool chef)
    {
        using var edge=new Pen(KitchenPremiumStyle.Gold,2);using var glow=new Pen(Color.FromArgb(50,KitchenPremiumStyle.Gold),5);
        using var path=new GraphicsPath(FillMode.Winding);
        if(chef){path.AddEllipse(box.Left+2,box.Top+5,15,17);path.AddEllipse(box.Left+12,box.Top,18,21);path.AddEllipse(box.Left+24,box.Top+6,13,16);path.AddRectangle(new Rectangle(box.Left+9,box.Top+18,22,17));}
        else {for(int i=0;i<3;i++){int y=box.Top+5+i*10;Point[] shape={new(box.Left+2,y+8),new(box.Left+18,y),new(box.Right-2,y+8),new(box.Left+18,y+16)};path.AddPolygon(shape);}}
        for(int i=10;i>=2;i-=2){using var bloom=new Pen(Color.FromArgb(25,KitchenPremiumStyle.Gold),i);g.DrawPath(bloom,path);}g.DrawPath(glow,path);using var fill=new LinearGradientBrush(box,Color.FromArgb(255,233,139),Color.FromArgb(98,57,5),90);g.FillPath(fill,path);g.DrawPath(edge,path);
    }
}
