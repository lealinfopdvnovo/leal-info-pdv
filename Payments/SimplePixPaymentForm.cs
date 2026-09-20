using System.Drawing.Drawing2D;
using QRCoder;

namespace LealInfoPDV;

public sealed class SimplePixPaymentForm : Form
{
    private readonly string payload;
    public SimplePixPaymentForm(decimal value,string key,string keyType,string merchantName,string city)
    {
        payload=PixEmv.BuildPayload(key,keyType,value,merchantName,city);
        Text="LEAL INFO PDV • PIX POR CHAVE"; FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.CenterParent; ClientSize=new Size(460,590);
        BackColor=Color.FromArgb(8,20,35); TopMost=true;
        var title=new Label{Text=$"PIX • {value:C2}",ForeColor=Color.White,Font=new Font("Segoe UI",18,FontStyle.Bold),AutoSize=true,Location=new Point(135,20)};
        var info=new Label{Text="Escaneie o QR Code ou use PIX Copia e Cola.",ForeColor=Color.White,Font=new Font("Segoe UI",10),TextAlign=ContentAlignment.MiddleCenter,Location=new Point(35,58),Size=new Size(390,34)};
        var qr=new PictureBox{Location=new Point(70,98),Size=new Size(320,320),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,Image=BuildQr(payload)};
        var keyLabel=new Label{Text=$"Chave: {key}",ForeColor=Color.White,Font=new Font("Segoe UI",9,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Location=new Point(30,425),Size=new Size(400,34)};
        var copy=new Button{Text="COPIAR PIX",Location=new Point(55,480),Size=new Size(160,45),BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold)};
        var confirm=new Button{Text="PAGAMENTO RECEBIDO",Location=new Point(225,480),Size=new Size(180,45),BackColor=Color.FromArgb(0,145,85),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",9.5f,FontStyle.Bold)};
        var cancel=new Button{Text="CANCELAR",Location=new Point(150,535),Size=new Size(160,36),BackColor=Color.FromArgb(90,100,110),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        copy.FlatAppearance.BorderSize=0;confirm.FlatAppearance.BorderSize=0;cancel.FlatAppearance.BorderSize=0;
        copy.Click+=(_,_)=>{Clipboard.SetText(payload);copy.Text="PIX COPIADO ✓";};
        confirm.Click+=(_,_)=>{if(MessageBox.Show(this,"Você conferiu no banco e o PIX foi recebido?","Confirmar recebimento",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){DialogResult=DialogResult.OK;Close();}};
        cancel.Click+=(_,_)=>{DialogResult=DialogResult.Cancel;Close();};
        Controls.AddRange(new Control[]{title,info,qr,keyLabel,copy,confirm,cancel});
        Resize+=(_,_)=>Round(); Round();
    }
    private static Image BuildQr(string value)
    {
        using var gen=new QRCodeGenerator(); using var data=gen.CreateQrCode(value,QRCodeGenerator.ECCLevel.M);
        var png=new PngByteQRCode(data).GetGraphic(8);
        using var ms=new MemoryStream(png); using var source=Image.FromStream(ms); return new Bitmap(source);
    }
    private void Round(){using var p=new GraphicsPath();const int r=28;var x=ClientRectangle;x.Width--;x.Height--;p.AddArc(x.Left,x.Top,r,r,180,90);p.AddArc(x.Right-r,x.Top,r,r,270,90);p.AddArc(x.Right-r,x.Bottom-r,r,r,0,90);p.AddArc(x.Left,x.Bottom-r,r,r,90,90);p.CloseFigure();Region=new Region(p);}
}
