using LealInfoPDV;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]
    static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static void Pump(int ms){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
    static Rectangle Absolute(Control c)=>new(c.PointToScreen(Point.Empty),c.Size);
    [STAThread] static void Main()
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Exige runner descartavel; nao executar contra producao.");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
        Database.Initialize();
        Auth.CreateUser("TESTE CRISTAL","qa_crystal","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");
        Check(Auth.Login("qa_crystal","errada")==null,"Senha incorreta aceita");
        Directory.CreateDirectory("crystal-evidencias");
        using(var actual=new LoginForm())
        {
            actual.Show();Pump(2400);var fields=All(actual).OfType<TextBox>().ToArray();
            fields.Single(x=>!x.UseSystemPasswordChar).Text="qa_crystal";fields.Single(x=>x.UseSystemPasswordChar).Text="Senha_Ficticia_123";
            All(actual).OfType<Button>().Single(x=>x.Text=="ENTRAR").PerformClick();Check(actual.DialogResult==DialogResult.OK,"Login nativo falhou");
            Console.WriteLine("PASS login nativo top-level no desktop Windows real do runner");
        }
        foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
        foreach(var scale in new[]{1f,1.25f,1.5f})
        {
            using var host=new Form();using var f=new LoginForm();f.WindowState=FormWindowState.Normal;f.TopLevel=false;host.Controls.Add(f);host.Show();f.Show();Pump(2400);f.WindowState=FormWindowState.Normal;
            f.AutoScaleMode=AutoScaleMode.None;f.MaximumSize=new Size(4000,3000);f.ClientSize=size;
            Check(SetWindowPos(f.Handle,IntPtr.Zero,0,0,size.Width,size.Height,0x0006),"Surface native sizing failed");Pump(100);
            f.Font=new Font("Segoe UI",10f*scale);
            var labels=All(f).OfType<Label>().ToArray();
            var brand=labels.Single(x=>x.Text=="LEAL INFO");var connected=labels.Single(x=>x.Text=="CONECTADO");
            brand.Font=new Font("Segoe UI",38*scale,FontStyle.Bold);connected.Font=new Font("Segoe UI",38*scale,FontStyle.Bold);
            typeof(Control).GetMethod("OnResize",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(f,new object[]{EventArgs.Empty});Pump(100);
            Check(brand.GetType().Name=="CrystalTitleLabel"&&connected.GetType().Name=="CrystalTitleLabel","Marca sem cristal");
            var header=labels.Single(x=>x.Text=="BEM-VINDO DE VOLTA");var card=header.Parent!.Parent!;
            foreach(var title in new[]{brand,connected})
            {
                var r=Absolute(title);var screen=Absolute(f);
                Check(Math.Abs((r.Left+r.Width/2)-(screen.Left+f.ClientSize.Width/2))<=1,"Marca descentralizada");
                Console.WriteLine($"GEOMETRIA {size} fonte={scale} marca={r} formulario={screen} cliente={f.ClientSize} painel={Absolute(card)}");
                Check(r.Top>=screen.Top&&r.Right<=screen.Right&&r.Left>=screen.Left,"Marca cortada");
                Check(r.Bottom<Absolute(card).Top,"Marca sobrepoe painel");
                using var image=new Bitmap(title.Width,title.Height);title.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                int lit=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var p=image.GetPixel(x,y);if(p.B>150&&p.G>90)lit++;}
                Check(lit>100,"Cristal nao renderizado");
            }
            Check(Absolute(brand).Bottom<=Absolute(connected).Top,"Linhas sobrepostas");
            var product=labels.Single(x=>x.Text=="PDV PRO");var tagline=labels.Single(x=>x.Text=="TECNOLOGIA QUE CONECTA");
            Check(!product.Visible||Absolute(product).Top>=Absolute(connected).Bottom,"Produto sobrepoe marca");
            Check(!tagline.Visible||Absolute(tagline).Bottom<Absolute(card).Top,"Subtitulo encoberto pelo painel");
            using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save($"crystal-evidencias/login-{size.Width}x{size.Height}-fonte-{scale*100:0}.png",ImageFormat.Png);}
            var boxes=All(f).OfType<TextBox>().ToArray();boxes.Single(x=>!x.UseSystemPasswordChar).Text="qa_crystal";boxes.Single(x=>x.UseSystemPasswordChar).Text="Senha_Ficticia_123";
            All(f).OfType<Button>().Single(x=>x.Text=="ENTRAR").PerformClick();Check(f.DialogResult==DialogResult.OK,"Login real falhou");
            Console.WriteLine($"PASS {size.Width}x{size.Height} fontes {scale*100:0}%: cristal, centro, limites, sem sobreposicao, login real");
        }
        var identity=typeof(LoginForm).Assembly.GetType("LealInfoPDV.ClientUpdateIdentity",true)!;
        var matches=identity.GetMethod("ManifestMatches",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(var id in new[]{"001","003"})
        {
            Check((bool)matches.Invoke(null,new object[]{id,id})!,"Canal correto recusado");
            foreach(var other in new[]{"002",id=="001"?"003":"001"})Check(!(bool)matches.Invoke(null,new object[]{other,id})!,"Canal incorreto aceito");
        }
        Console.WriteLine("PASS isolamento de ID pelo mecanismo existente; updater nao modificado");
    }
}
