using LealInfoPDV;
using LealInfoPDV.Network;
using System.Reflection;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;

static class Program
{
    const BindingFlags I=BindingFlags.NonPublic|BindingFlags.Instance,S=BindingFlags.NonPublic|BindingFlags.Static;
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static void Pump(){Application.DoEvents();Thread.Sleep(100);Application.DoEvents();}
    static void Sql(string text){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=text;q.ExecuteNonQuery();}
    static string Data(){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT (SELECT COUNT(*) FROM products)||':'||(SELECT COUNT(*) FROM customers)||':'||(SELECT COUNT(*) FROM sales)||':'||(SELECT COUNT(*) FROM settings)";return Convert.ToString(q.ExecuteScalar())!;}
    static string Hash(Control c){using var b=new Bitmap(c.Width,c.Height);c.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));using var s=new MemoryStream();b.Save(s,ImageFormat.Png);return Convert.ToHexString(SHA256.HashData(s.ToArray()));}
    [STAThread]static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Windows descartável");
        bool before=args.Contains("--before");string root=Environment.GetEnvironmentVariable("PORT001_EVIDENCE")!;Directory.CreateDirectory(root);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Database.Initialize();
        if(before)
        {
            Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1'),('company','EMPRESA FICTICIA PORT001')");
            Sql("INSERT INTO customers(name) VALUES('CLIENTE FICTICIO PORT001')");
            Auth.CreateUser("QA FICTICIO","qa_port001","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");
        }
        Check(Auth.Login("qa_port001","errada")==null,"Senha inválida recusada");
        using(var login=new LoginForm())
        {
            login.Show();var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<6000)Pump();
            var inputs=All(login).OfType<TextBox>().ToArray();inputs.Single(t=>!t.UseSystemPasswordChar).Text="qa_port001";inputs.Single(t=>t.UseSystemPasswordChar).Text="Senha_Ficticia_123";
            using var picture=new Bitmap(login.Width,login.Height);login.DrawToBitmap(picture,new Rectangle(Point.Empty,picture.Size));picture.Save(Path.Combine(root,before?"login-antes.png":"login-depois.png"));
            All(login).OfType<Button>().Single(b=>b.Text=="ENTRAR").PerformClick();Check(login.DialogResult==DialogResult.OK,"Login real "+(before?"ANTES":"DEPOIS"));
        }
        var license=typeof(MainForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
        license.GetProperty("Current",S)!.SetValue(null,new LicenseTerms("QA",2,"unico",null,"",1,"QA","standard","001"));
        string initial=Data();
        using var main=new MainForm();main.Show();Pump();main.WindowState=FormWindowState.Normal;
        ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main)!).Stop();
        typeof(MainForm).GetField("automaticBackupCompleted",I)!.SetValue(main,true);
        var products=All(main).OfType<Label>().Single(c=>c.Text=="PRODUTOS");var bar=(FlowLayoutPanel)products.Parent!.Parent!;
        var records=new Dictionary<string,string>();var legibility=new Dictionary<string,bool>();float applied=1;
        foreach(var scale in new[]{1f,1.25f,1.5f})foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
        {
            main.Size=size;if(scale!=applied){main.Scale(new SizeF(scale/applied,scale/applied));applied=scale;}Pump();
            foreach(Control card in bar.Controls)
            {
                var caption=card.Controls.OfType<Label>().Single();
                if(caption.Text=="VIDRAÇARIA"){Hash(card);Check(!before,"Somente versão integrada possui Vidraçaria");Check((bool)caption.GetType().GetProperty("CompleteTextFits")!.GetValue(caption)!,"Legenda Vidraçaria completa");continue;}
                Hash(card); // Executa a medição real do renderer antes de verificar legibilidade.
                var fits=caption.GetType().GetProperty("CompleteTextFits");
                legibility[$"{size.Width}-{scale}-{caption.Text}"]=fits==null||(bool)fits.GetValue(caption)!;
                if(!legibility[$"{size.Width}-{scale}-{caption.Text}"])Console.WriteLine("OBSERVAÇÃO "+(before?"BASELINE":"CANDIDATA")+" legenda limitada "+caption.Text);
                var icon=card.Controls.OfType<PictureBox>().SingleOrDefault();
                string iconHash="";
                if(icon?.Image!=null){using var bytes=new MemoryStream();icon.Image.Save(bytes,ImageFormat.Png);iconHash=Convert.ToHexString(SHA256.HashData(bytes.ToArray()));}
                records[$"{size.Width}-{scale}-{caption.Text}"]=$"{caption.Text}|{caption.Font.FontFamily.Name}|{caption.ForeColor.ToArgb()}|{caption.Dock}|{caption.AutoEllipsis}|{card.Cursor==Cursors.Hand}|{icon?.SizeMode}|{iconHash}";
                if(!before){bar.ScrollControlIntoView(card);Pump();Check(bar.RectangleToScreen(bar.ClientRectangle).IntersectsWith(card.RectangleToScreen(card.ClientRectangle)),"Atalho acessível "+caption.Text);}
            }
            using var image=new Bitmap(bar.Width,bar.Height);bar.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,$"barra-{(before?"antes":"depois")}-{size.Width}-{scale}.png"),ImageFormat.Png);
        }
        var snapshot=new{Data=initial,Buttons=records,Legibility=legibility};
        if(before)File.WriteAllText(Path.Combine(root,"antes.json"),JsonSerializer.Serialize(snapshot));
        else
        {
            var previous=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"antes.json")));
            Check(previous.RootElement.GetProperty("Data").GetString()==initial,"Dados fictícios preservados ANTES/DEPOIS");
            foreach(var property in previous.RootElement.GetProperty("Buttons").EnumerateObject())Check(records.TryGetValue(property.Name,out var hash)&&hash==property.Value.GetString(),"Ícone, texto, tipografia e comportamento preservados "+property.Name);
            foreach(var property in previous.RootElement.GetProperty("Legibility").EnumerateObject())Check(!property.Value.GetBoolean()||legibility[property.Name],"Sem regressão de legibilidade "+property.Name);
            Check(All(main).OfType<Label>().Any(l=>l.Text=="VIDRAÇARIA"),"Entrada Vidraçaria presente no 001");
            File.WriteAllText(Path.Combine(root,"depois.json"),JsonSerializer.Serialize(snapshot));
        }
        Check(Data()==initial,"Tela principal não modificou dados");
        main.Close();Console.WriteLine($"PASS PORT001 {(before?"ANTES":"DEPOIS")}: {checks} verificações");
    }
}
