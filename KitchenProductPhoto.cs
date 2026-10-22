using System.Drawing.Drawing2D;
namespace LealInfoPDV;
internal sealed class KitchenProductPhoto : PictureBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Image==null||Width<1||Height<1)return;
        float scale=Math.Max((float)Width/Image.Width,(float)Height/Image.Height);
        float width=Width/scale,height=Height/scale;
        e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(Image,ClientRectangle,new RectangleF((Image.Width-width)/2,(Image.Height-height)/2,width,height),GraphicsUnit.Pixel);
    }
}
