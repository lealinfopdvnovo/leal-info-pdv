using LealInfoPDV;
using LealInfoPDV.Network;
using System.Reflection;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;

static class Program
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic;
    const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Assembly A=typeof(MainForm).Assembly;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
    static void Exec(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;q.ExecuteNonQuery();}
    static string Snapshot(){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText="SELECT (SELECT COUNT(*) FROM users)||':'||(SELECT COUNT(*) FROM products)||':'||(SELECT COUNT(*) FROM customers)||':'||(SELECT COUNT(*) FROM sales)||':'||(SELECT COUNT(*) FROM settings)||':'||(SELECT COUNT(*) FROM cash_movements)";return Convert.ToString(q.ExecuteScalar())!;}
    static string Manifest(string version,string id="")=>JsonSerializer.Serialize(new{Version=version,TargetClientCode=id,Sha256=new string('a',64),PackageUrl=id==""?$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v{version}/UPDATE.zip":$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-{id}-v{version}/UPDATE.zip"});
    sealed class Feed(string client):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken t)
        {
            bool global=request.RequestUri!.AbsolutePath.EndsWith("/main/version.json");
            var body=global?Manifest("10.377"):client is "001" or "002" or "003"?Manifest("10.377",client):null;
            return Task.FromResult(new HttpResponseMessage(body==null?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new StringContent(body??"not found")});
        }
    }
    static void Selection()
    {
        var updater=A.GetType("LealInfoPDV.UpdateManager",true)!;
        var method=updater.GetMethod("LoadManifestAsync",S,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
        foreach(var id in new[]{"001","002","003","004"})
        {
            using var http=new HttpClient(new Feed(id));
            var m=Task.Run(async()=>{var task=(Task)method.Invoke(null,new object[]{http,id,"10.376"})!;await task;return task.GetType().GetProperty("Result")!.GetValue(task)!;}).GetAwaiter().GetResult();
            Check((string)m.GetType().GetProperty("Version")!.GetValue(m)! =="10.377","Versão incorreta");
            Check((string)m.GetType().GetProperty("TargetClientCode")!.GetValue(m)! ==(id is "001" or "002" or "003"?id:""),"002 não recebeu pacote próprio");
            Console.WriteLine($"PASS detecção {id}: 10.376 -> 10.377; alvo correto");
        }
    }
    static void Modal(MainForm main,Control caption,bool escape,string name)
    {
        bool seen=false;Exception? failure=null;
        using var timer=new System.Windows.Forms.Timer{Interval=80};
        timer.Tick+=(_,_)=>
        {
            var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="COZINHA");if(f==null||seen)return;seen=true;timer.Stop();
            try
            {
                Check(f.Modal&&f.Owner==main,"Modal não pertence ao PDV");
                Check(All(f).OfType<Label>().Any(x=>x.Text=="Em desenvolvimento para próxima atualização."),"Mensagem incorreta");
                var close=All(f).OfType<Button>().Single(x=>x.Text=="FECHAR");
                Check(close.Visible&&f.CancelButton==close,"FECHAR/ESC indisponíveis");
                var owner=main.Bounds;var area=Screen.FromControl(main).WorkingArea;int center=Math.Clamp(owner.Left+owner.Width/2,area.Left+f.Width/2,area.Right-f.Width/2);Check(Math.Abs((f.Left+f.Width/2)-center)<=4,"Modal fora do centro/área útil");
                using var bmp=new Bitmap(f.Width,f.Height);f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save($"kitchen-evidencias/{name}.png",ImageFormat.Png);
                if(escape){var key=typeof(Form).GetMethod("ProcessDialogKey",I)!;Check((bool)key.Invoke(f,new object[]{Keys.Escape})!,"ESC não processado");}
                else close.PerformClick();
            }
            catch(Exception ex){failure=ex;f.Close();}
        };
        timer.Start();typeof(Control).GetMethod("OnClick",I)!.Invoke(caption,new object[]{EventArgs.Empty});
        Check(seen,"COZINHA não abriu");if(failure!=null)throw failure;Check(!Application.OpenForms.Cast<Form>().Any(x=>x.Text=="COZINHA"),"Modal não fechou");
    }
    static void ValidateLive()
    {
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(40)};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LealInfoPDV-QA-Cozinha/10.377");
        var updater=A.GetType("LealInfoPDV.UpdateManager",true)!;
        var load=updater.GetMethod("LoadManifestAsync",S,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
        var downloaded=new HashSet<string>();
        foreach(var id in new[]{"001","002","003","004"})
        {
            var task=(Task)load.Invoke(null,new object[]{http,id,"10.376"})!;task.GetAwaiter().GetResult();
            var m=task.GetType().GetProperty("Result")!.GetValue(task);Check(m!=null,"Feed real sem atualização para "+id);
            string Read(string key)=>(string)m!.GetType().GetProperty(key)!.GetValue(m)!;
            Check(Read("Version")=="10.377"&&Read("TargetClientCode")==(id is "001" or "002" or "003"?id:""),"Seleção real incorreta: "+id);
            var url=Read("PackageUrl");
            if(downloaded.Add(url))
            {
                var data=http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                Check(Convert.ToHexString(SHA256.HashData(data)).Equals(Read("Sha256"),StringComparison.OrdinalIgnoreCase),"SHA remoto incorreto");
                using var bytes=new MemoryStream(data);using var z=new ZipArchive(bytes);
                Check(z.Entries.Select(x=>x.FullName).Order().SequenceEqual(new[]{"Assets/kitchen.png","LealInfoPDV.deps.json","LealInfoPDV.dll","LealInfoPDV.exe"}),"Conteúdo remoto inesperado");
            }
            Console.WriteLine("PASS updater REAL com feed REAL: "+id+" -> "+Read("Version")+" alvo="+Read("TargetClientCode")+"; SHA remoto validado");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Somente ambiente de teste descartável.");
        if(args.Contains("--live")){ValidateLive();return;}
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();
        Exec("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1'),('company','EMPRESA FICTICIA QA COZINHA')");
        Auth.CreateUser("QA FICTICIO","qa_kitchen","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");
        Check(Auth.Login("qa_kitchen","errada")==null,"Senha incorreta aceita");
        using(var login=new LoginForm())
        {
            login.Show();Pump(6000);var text=All(login).OfType<TextBox>().ToArray();text.Single(x=>!x.UseSystemPasswordChar).Text="qa_kitchen";text.Single(x=>x.UseSystemPasswordChar).Text="Senha_Ficticia_123";
            All(login).OfType<Button>().Single(x=>x.Text=="ENTRAR").PerformClick();Check(login.DialogResult==DialogResult.OK,"Login real falhou");
        }
        Console.WriteLine("PASS login real WinForms com credenciais fictícias");
        A.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!.GetProperty("Current",S)!.SetValue(null,new LicenseTerms("QA",2,"unico",null,"",1,"QA","standard","TESTE"));
        Directory.CreateDirectory("kitchen-evidencias");
        var baseline=Snapshot();
        using(var main=new MainForm())
        {
            main.Show();Pump(200);typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main)!.GetType().GetMethod("Stop")!.Invoke(typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main),null);
            main.WindowState=FormWindowState.Normal;typeof(MainForm).GetField("automaticBackupCompleted",I)!.SetValue(main,true);
            var kitchen=All(main).OfType<Label>().Single(x=>x.Text=="COZINHA");var bar=(FlowLayoutPanel)kitchen.Parent!.Parent!;
            var service=All(main).OfType<Label>().Single(x=>x.Text=="SERVIÇOS");
            var chef=kitchen.Parent!.Controls.OfType<PictureBox>().Single();
            var gear=service.Parent!.Controls.OfType<PictureBox>().Single();
            Check(chef.Size==gear.Size&&chef.Location==gear.Location&&chef.SizeMode==gear.SizeMode,"Geometria do icone mudou");
            Check(chef.Size==new Size(58,58),"Area do icone mudou");
            var chefFile=Path.Combine(AppContext.BaseDirectory,"Assets","kitchen.png");
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(chefFile))).Equals("bf07fc592ead270d8440b5a75b9b18ee62dbc48e2e0338fd580776be70ea9ad3",StringComparison.OrdinalIgnoreCase),"Icone errado no build");
            Check(chef.Image!.Width!=gear.Image!.Width||!File.ReadAllBytes(chefFile).SequenceEqual(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Assets","services.png"))),"Icones iguais");
            using(var iconCapture=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(iconCapture,new Rectangle(Point.Empty,iconCapture.Size));iconCapture.Save("kitchen-evidencias/barra-icones.png",ImageFormat.Png);}
            Modal(main,chef,false,"modal-clique-icone");
            Check(Snapshot()==baseline,"Clique no icone alterou dados");
            Console.WriteLine("PASS icone proprio transparente; mesma caixa 58x58, posicao e modo Zoom; clique no icone abre modal; SERVICOS preservado");
            var existing=bar.Controls.Cast<Control>().Where(x=>x!=kitchen.Parent).Select(x=>x.Controls.OfType<Label>().Single().Text).ToArray();
            Check(existing.Contains("PRODUTOS")&&existing.Contains("SAIR"),"Atalhos anteriores ausentes");
            float appliedScale=1f;
            foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
            foreach(float scale in new[]{1f,1.25f,1.5f})
            {
                main.Scale(new SizeF(scale/appliedScale,scale/appliedScale));appliedScale=scale;main.ClientSize=new Size((int)(size.Width*scale),(int)(size.Height*scale));main.PerformLayout();Pump(50);
                Check(kitchen.Parent!.Right<=bar.ClientSize.Width&&kitchen.Visible,"COZINHA cortada/oculta");
                var sibling=bar.Controls[0];Check(kitchen.Parent.Height==sibling.Height&&kitchen.Parent.Margin==sibling.Margin,"Padrão do card diferente");
                foreach(var label in bar.Controls.Cast<Control>().SelectMany(x=>x.Controls.OfType<Label>()))
                {
                    Check(label.Font.Size<=8.21f,"Fonte grande permaneceu");
                    var room=new Size(label.ClientSize.Width-label.Padding.Horizontal,label.ClientSize.Height-label.Padding.Vertical);
                    var measured=TextRenderer.MeasureText(label.Text,label.Font,room,TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);
                    Check(measured.Width<=room.Width&&measured.Height<=room.Height,"Texto cortado: "+label.Text+" "+measured+" / "+room);
                    Check(label.TextAlign==ContentAlignment.MiddleCenter,"Texto nao centralizado");
                    var font=label.Font.Size;typeof(Control).GetMethod("OnMouseEnter",I)!.Invoke(label,new object[]{EventArgs.Empty});Check(label.Font.Size==font,"Hover aumentou fonte");
                }
                using(var capture=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(capture,new Rectangle(Point.Empty,capture.Size));capture.Save($"kitchen-evidencias/textos-{size.Width}-{scale*100:0}.png",ImageFormat.Png);}
                Console.WriteLine($"PASS tipografia: todos captions centralizados, fonte ajustada, sem cortes, hover estavel; {size.Width}x{size.Height} escala {scale}");
                var name=$"modal-{size.Width}x{size.Height}-dimensao-{scale*100:0}";
                Modal(main,kitchen,false,name);Modal(main,kitchen,true,name+"-esc");
                Check(Snapshot()==baseline,"Modal alterou banco/configuração");
                Console.WriteLine($"PASS {size.Width}x{size.Height} fator de dimensão {scale}: card padrão visível, modal central, FECHAR, ESC, sem negócio novo.");
            }
            bool is002=args.Contains("--002");Check((A.GetType("LealInfoPDV.PartsWithdrawal")!=null)==is002,"Retirada de Peças misturada/removida");Check((A.GetType("LealInfoPDV.ServiceNote")!=null)==is002,"Nota de Serviço misturada/removida");
            Console.WriteLine(is002?"PASS módulos exclusivos 002 continuam presentes":"PASS universal sem módulos exclusivos do 002");
            main.Close();Pump(100);
        }
        Selection();Console.WriteLine("PASS abertura PDV, login, cozinha e detecção; DPI físico ainda depende de monitores reais.");
    }
}
