using System.Drawing.Drawing2D;
using QRCoder;

namespace LealInfoPDV;

public sealed class MercadoPagoPixForm:Form
{
    private readonly MercadoPagoPixService service; private readonly decimal value; private readonly string email; private readonly CancellationTokenSource cts=new();
    private readonly PictureBox qr=new(){Location=new Point(70,90),Size=new Size(320,320),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};
    private readonly Label status=new(){Location=new Point(35,420),Size=new Size(390,40),TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold)};
    private readonly Button copy=new(){Text="COPIAR PIX",Location=new Point(55,480),Size=new Size(160,45),Enabled=false};
    private string payload="";
    public MercadoPagoPixForm(decimal value,string email,MercadoPagoPixService service)
    {
        this.value=value;this.email=email;this.service=service;Text="PIX AUTOMÁTICO • MERCADO PAGO";FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(460,555);BackColor=Color.FromArgb(8,20,35);TopMost=true;
        var title=new Label{Text=$"MERCADO PAGO • {value:C2}",Location=new Point(80,20),Size=new Size(300,45),TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",16,FontStyle.Bold)};
        var cancel=new Button{Text="CANCELAR",Location=new Point(225,480),Size=new Size(180,45)};
        foreach(var b in new[]{copy,cancel}){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.ForeColor=Color.White;b.BackColor=Color.FromArgb(0,120,170);}
        copy.Click+=(_,_)=>{if(payload.Length>0)Clipboard.SetText(payload);};
        cancel.Click+=(_,_)=>{cts.Cancel();DialogResult=DialogResult.Cancel;Close();};
        Controls.AddRange(new Control[]{title,qr,status,copy,cancel});Shown+=async(_,_)=>await StartAsync();FormClosed+=(_,_)=>cts.Dispose();Resize+=(_,_)=>Round();Round();
    }
    private async Task StartAsync(){try{status.Text="Gerando PIX no Mercado Pago...";var ch=await service.GerarAsync(value,email,cts.Token);payload=ch.QrCode;qr.Image=BuildQr(payload);copy.Enabled=true;status.Text="Aguardando confirmação automática...";if(await service.AguardarPagamentoAsync(ch.OrderId,cts.Token)){status.Text="PAGAMENTO CONFIRMADO ✓";DialogResult=DialogResult.OK;await Task.Delay(600);Close();}}catch(OperationCanceledException){}catch(Exception ex){MessageBox.Show(this,ex.Message,"Mercado Pago PIX",MessageBoxButtons.OK,MessageBoxIcon.Error);DialogResult=DialogResult.Abort;Close();}}
    private static Image BuildQr(string value){using var gen=new QRCodeGenerator();using var data=gen.CreateQrCode(value,QRCodeGenerator.ECCLevel.M);var png=new PngByteQRCode(data).GetGraphic(8);using var ms=new MemoryStream(png);using var src=Image.FromStream(ms);return new Bitmap(src);}
    private void Round(){using var p=new GraphicsPath();const int r=28;var x=ClientRectangle;x.Width--;x.Height--;p.AddArc(x.Left,x.Top,r,r,180,90);p.AddArc(x.Right-r,x.Top,r,r,270,90);p.AddArc(x.Right-r,x.Bottom-r,r,r,0,90);p.AddArc(x.Left,x.Bottom-r,r,r,90,90);p.CloseFigure();Region=new Region(p);}
}
