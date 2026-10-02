namespace LealInfoPDV.Licensing;
internal sealed class BrandSplashForm : Form
{
    private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    private readonly System.Diagnostics.Stopwatch elapsed=new();
    internal BrandSplashForm()
    {
        FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;BackColor=Color.Black;Opacity=0;ShowInTaskbar=false;
        var logo=new PictureBox {Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Padding=new Padding(100),Image=CompanyBranding.LoadLogo()};Controls.Add(logo);
        if(logo.Image==null)Controls.Add(new Label {Text="LEAL INFO PDV",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Gold,Font=new Font("Segoe UI",32,FontStyle.Bold)});
        Shown+=(_,_)=>{elapsed.Start();timer.Start();};
        timer.Tick+=(_,_)=>{double ms=elapsed.Elapsed.TotalMilliseconds;Opacity=ms<450?ms/450:ms<1100?1:Math.Max(0,1-(ms-1100)/450);if(ms>=1550){timer.Stop();DialogResult=DialogResult.OK;Close();}};
        FormClosed+=(_,_)=>{timer.Dispose();logo.Image?.Dispose();};
    }
}
