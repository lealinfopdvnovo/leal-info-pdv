using System.Drawing.Drawing2D;
namespace LealInfoPDV;
internal sealed class KitchenCategoryButton : Button
{
    internal KitchenCategoryButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Cursor=Cursors.Hand;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(KitchenPremiumStyle.Background);
        bool selected=AccessibleDescription=="selecionado";var box=new Rectangle(2,2,Math.Max(1,Width-5),Math.Max(1,Height-5));using var shape=KitchenPremiumStyle.Round(box,8);
        using var fill=new LinearGradientBrush(box,selected?Color.FromArgb(255,221,105):Color.FromArgb(35,47,56),selected?Color.FromArgb(153,103,9):Color.FromArgb(6,12,17),90);g.FillPath(fill,shape);
        using(var glow=new Pen(Color.FromArgb(selected?90:25,KitchenPremiumStyle.Gold),5))g.DrawPath(glow,shape);
        using(var edge=new Pen(selected?KitchenPremiumStyle.Gold:Color.FromArgb(95,111,122)))g.DrawPath(edge,shape);
        KitchenLighting.Surface(g,box,selected?KitchenPremiumStyle.Gold:Color.FromArgb(95,120,138),selected);
        string text=Text.Replace("\n"," ");string symbol=text.Contains("PIZ")?"🍕":text.Contains("BEB")?"🥤":text.Contains("LANCH")?"🍔":text.Contains("POR")?"🍟":text.Contains("SOBREM")?"🍰":text.Contains("REFEI")?"♨":text=="TODOS"?"▦":"●";
        bool stacked=!Text.Contains("\n")&&text.Length>10;
        using var font=new Font("Segoe UI Emoji",Math.Min(25,Height*.36f),GraphicsUnit.Pixel);using var ink=new SolidBrush(selected?Color.FromArgb(45,29,3):KitchenPremiumStyle.Gold);g.DrawString(symbol,font,ink,new PointF(stacked?(Width-30)/2:6,stacked?4:(Height-font.GetHeight(g))/2));
        KitchenPremiumStyle.DrawFittedText(g,Text,stacked?new Rectangle(5,35,Math.Max(1,Width-10),Math.Max(1,Height-39)):new Rectangle(35,5,Math.Max(1,Width-40),Math.Max(1,Height-10)),selected?Color.FromArgb(27,20,3):Color.White,13*g.DpiY/96f);
    }
}
