using LealInfoPDV;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
static class Program
{
    const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool b,string label){if(!b)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name)where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
    static void Call(object x,string name)=>x.GetType().GetMethod(name,I)!.Invoke(x,null);
    static object? Scalar(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static void Sql(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;q.ExecuteNonQuery();}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
    static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
    [STAThread]static void Main()
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Somente runner Windows descartavel");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Database.Initialize();
        Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1')");
        Auth.CreateUser("QA CADASTRO","qa_cliente","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qa_cliente","Senha_Ficticia_123");
        Sql("INSERT INTO customers(name) VALUES('CLIENTE ANTERIOR QA')");
        long initial=Convert.ToInt64(Scalar("SELECT COUNT(*) FROM customers"));
        using var main=new MainForm();main.Show();Pump();
        ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main)!).Stop();
        typeof(MainForm).GetField("automaticBackupCompleted",I)!.SetValue(main,true);
        Directory.CreateDirectory("glass-customer-evidencias");
        using var f=new GlassWorkshopForm(dialog=>typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(main,new object[]{dialog}));f.Show(main);Pump();
        Find<Button>(f,"LoadQuoteCustomers").PerformClick();var clients=Find<ComboBox>(f,"QuoteCustomer");clients.SelectedIndex=1;
        Find<TextBox>(f,"Description").Text="PROJETO NAO PODE MUDAR";Find<NumericUpDown>(f,"QuotePrice").Value=87.35m;
        Find<NumericUpDown>(f,"WidthMm").Value=1234;Find<ComboBox>(f,"WidthMmUnit").SelectedItem="cm";
        var save=f.GetType().GetMethod("SaveCurrentProject",I)!;var stored=(GlassStoredProject)save.Invoke(f,null)!;
        string persisted=JsonSerializer.Serialize(GlassProjectStore.Load(stored.Id));
        int stage=0;bool crudSeen=false,editorSeen=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=60})
        {
            var watch=Stopwatch.StartNew();
            timer.Tick+=(_,_)=>{
                if(watch.ElapsedMilliseconds>20000)throw new Exception("Timeout do cadastro real");
                var crud=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="CLIENTES");
                var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="CLIENTE");
                if(stage==0 && crud!=null){stage=1;crudSeen=true;Check(crud.Modal,"Mesmo cadastro CLIENTES modal do PDV");All(crud).OfType<Button>().Single(x=>x.Text=="NOVO").PerformClick();}
                else if(stage==1 && editor!=null){stage=2;editorSeen=true;var inputs=(List<TextBox>)editor.Tag!;inputs[0].Text="CLIENTE NOVO VIDRACARIA QA";inputs[1].Text="DOCUMENTO QA";inputs[2].Text="TELEFONE QA";All(editor).OfType<Button>().Single(x=>x.Text=="SALVAR").PerformClick();}
                else if(stage==2 && crud!=null){timer.Stop();crud.Close();}
            };
            timer.Start();Find<Button>(f,"RegisterGlassCustomer").PerformClick();
        }
        Check(crudSeen && editorSeen,"Botao abre cadastro e editor reais");
        Check(Convert.ToInt64(Scalar("SELECT COUNT(*) FROM customers"))==initial+1,"Somente um cliente criado na tabela existente");
        Check(Convert.ToInt64(Scalar("SELECT COUNT(*) FROM customers WHERE name='CLIENTE NOVO VIDRACARIA QA' AND document='DOCUMENTO QA' AND phone='TELEFONE QA'"))==1,"Campos gravados pela rotina original");
        int added=Enumerable.Range(0,clients.Items.Count).Single(x=>clients.Items[x]!.ToString()=="CLIENTE NOVO VIDRACARIA QA");
        Check(added>0 && clients.SelectedIndex==1,"Lista atualizada ao retornar; selecao anterior preservada");
        Check(Find<TextBox>(f,"Description").Text=="PROJETO NAO PODE MUDAR" && Find<NumericUpDown>(f,"QuotePrice").Value==87.35m && Find<NumericUpDown>(f,"WidthMm").Value==1234 && Find<ComboBox>(f,"WidthMmUnit").Text=="cm","Projeto e orçamento em andamento intactos");
        Check(JsonSerializer.Serialize(GlassProjectStore.Load(stored.Id))==persisted,"Cadastro nao altera projeto persistido nem revisao");
        clients.SelectedIndex=added;var saved=(GlassStoredProject)save.Invoke(f,null)!;
        Check(GlassProjectStore.Load(saved.Id).Data.Quote.CustomerName=="CLIENTE NOVO VIDRACARIA QA","Novo cliente imediatamente selecionavel e salvo pelo mecanismo homologado");
        using(var timer=new System.Windows.Forms.Timer{Interval=60})
        {timer.Tick+=(_,_)=>{var crud=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="CLIENTES");if(crud!=null){timer.Stop();crud.Close();}};timer.Start();Find<Button>(f,"RegisterGlassCustomer").PerformClick();}
        Check(Convert.ToInt64(Scalar("SELECT COUNT(*) FROM customers"))==initial+1 && clients.Text=="CLIENTE NOVO VIDRACARIA QA","Fechar sem cadastrar nao duplica nem perde selecao");
        var denied=Auth.CreateUser("QA SEM ACESSO","qa_negado","Senha_Ficticia_123","OPERADOR","qa@example.invalid","");Sql($"INSERT INTO user_permissions(user_id,permission_key,allowed) VALUES({denied},'customers',0)");Auth.Login("qa_negado","Senha_Ficticia_123");Check(!Auth.HasPermission("customers"),"Permissao de clientes negada no mecanismo real");
        var dismiss=Task.Run(async ()=>{
            for(int attempt=0;attempt<150;attempt++){
                var dialog=FindWindow("#32770","Acesso não permitido");
                if(dialog!=IntPtr.Zero){Console.WriteLine("PASS aviso nativo de permissao localizado");PostMessage(dialog,0x0010,IntPtr.Zero,IntPtr.Zero);return true;}
                await Task.Delay(100);
            }
            return false;
        });
        Find<Button>(f,"RegisterGlassCustomer").PerformClick();
        Check(dismiss.GetAwaiter().GetResult(),"Aviso real de acesso negado exibido e fechado");
        Check(!Application.OpenForms.Cast<Form>().Any(x=>x.Text=="CLIENTES") && Convert.ToInt64(Scalar("SELECT COUNT(*) FROM customers"))==initial+1,"Botao respeita bloqueio sem abrir cadastro ou gravar");Auth.Login("qa_cliente","Senha_Ficticia_123");
        f.Close();
        foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})foreach(float factor in new[]{1f,1.25f,1.5f})
        {
            using var layout=new GlassWorkshopForm(dialog=>typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(main,new object[]{dialog}));layout.Show(main);layout.Scale(new SizeF(factor,factor));layout.Size=new(Math.Min(layout.Width,size.Width),Math.Min(layout.Height,size.Height-60));Pump();
            var panel=All(layout).OfType<Panel>().Single(x=>x.Name=="FieldsProjetos");
            foreach(var name in new[]{"LoadQuoteCustomers","RegisterGlassCustomer"}){var b=Find<Button>(layout,name);panel.ScrollControlIntoView(b);Pump();Check(b.Visible && b.Width>100 && b.Height>=30 && panel.RectangleToScreen(panel.ClientRectangle).IntersectsWith(b.RectangleToScreen(b.ClientRectangle)),"Botao visivel "+name+" "+size+" "+factor);}
            using var image=new Bitmap(layout.Width,layout.Height);layout.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save($"glass-customer-evidencias/botoes-{size.Width}-{factor}.png");layout.Close();
        }
        int modalStage=0;bool actualModal=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=60})
        {
            var watch=Stopwatch.StartNew();
            timer.Tick+=(_,_)=>{
                if(watch.ElapsedMilliseconds>15000)throw new Exception("Timeout da Central modal real");
                var central=Application.OpenForms.Cast<Form>().OfType<GlassWorkshopForm>().FirstOrDefault();
                var crud=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="CLIENTES");
                if(modalStage==0 && central!=null){modalStage=1;Check(central.Modal && central.Owner==main,"Central aberta pela entrada real do PDV");Find<Button>(central,"RegisterGlassCustomer").PerformClick();}
                else if(modalStage==1 && crud!=null){modalStage=2;actualModal=true;crud.Close();}
                else if(modalStage==2 && central!=null){timer.Stop();Check(Find<ComboBox>(central,"QuoteCustomer").Items.Cast<object>().Any(x=>x.ToString()=="CLIENTE NOVO VIDRACARIA QA"),"Retorno funcional pela Central modal real");central.Close();}
            };
            timer.Start();Call(main,"OpenGlassWorkshop");
        }
        Check(actualModal,"Cadastro existente acessivel com tela principal desabilitada pela Central modal");
        main.Close();Console.WriteLine("PASS CADASTRO REAL VIDRACARIA: "+checks+" verificacoes Windows");
    }
}
