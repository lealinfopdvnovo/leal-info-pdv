using LealInfoPDV;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
static class Program
{
    const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool b,string label){if(!b)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name)where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
    static Button Button(Form f,string text)=>All(f).OfType<Button>().Single(x=>x.Text==text);
    static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
    static object? Sql(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static GlassSavedProject Data(GlassWorkshopForm f)=>(GlassSavedProject)typeof(GlassWorkshopForm).GetMethod("CaptureSavedProject",I)!.Invoke(f,null)!;
    static GlassStoredProject Save(GlassWorkshopForm f)=>(GlassStoredProject)typeof(GlassWorkshopForm).GetMethod("SaveCurrentProject",I)!.Invoke(f,null)!;
    static void Load(GlassWorkshopForm f,string id)=>typeof(GlassWorkshopForm).GetMethod("LoadSavedProject",I)!.Invoke(f,new object[]{id});
    static GlassCommercialSnapshot Snapshot(GlassWorkshopForm f){var p=typeof(GlassWorkshopForm).GetField("project",I)!.GetValue(f)!;return (GlassCommercialSnapshot)p.GetType().GetMethod("CommercialSnapshot",I)!.Invoke(p,new object[]{Array.Empty<GlassMaterialLink>()})!;}
    static void Document(GlassWorkshopForm f,long id,string name,string evidence)
    {
        var s=Snapshot(f);Check(s.CustomerId==id && s.CustomerName==name,"Documento recebe ID/nome do cliente vinculado "+evidence);
        Check(s.Quote.Total==132m && Find<TextBox>(f,"QuoteSummary").Text.Contains("TOTAL: R$ 132,00"),"Calculo/tela132 preservados "+evidence);
        Check(GlassCommercialDocument.Lines(s).Any(x=>x.Contains(name)),"A4 reconhece cliente correto "+evidence);
        Find<TabControl>(f,"GlassSections").SelectedIndex=6;Pump();bool opened=false;
        using var timer=new System.Windows.Forms.Timer{Interval=60};
        timer.Tick+=(_,_)=>{var doc=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="ORÇAMENTO COMERCIAL — VIDRAÇARIA");if(doc==null)return;timer.Stop();var real=(GlassCommercialSnapshot)doc.Tag!;Check(real.CustomerId==id && real.CustomerName==name,"PREVIA REAL reconhece cliente "+evidence);Check(All(doc).OfType<Button>().Any(x=>x.Text=="GERAR PDF"),"Geracao PDF disponivel");opened=true;doc.Close();};
        timer.Start();Find<Button>(f,"CommercialPreview").PerformClick();Check(opened,"A4 abre sem aviso Selecione um cliente "+evidence);
        File.WriteAllBytes("glass-client-link-evidencias/"+evidence+".pdf",GlassCommercialDocument.PdfBytes(s));
        Find<TabControl>(f,"GlassSections").SelectedIndex=0;Pump();Check(Find<Label>(f,"SelectedGlassCustomer").Text.Contains(name+" (ID "+id+")"),"Feedback visual corresponde ao ID "+evidence);
    }
    [STAThread]static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Runner Windows descartavel");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();Directory.CreateDirectory("glass-client-link-evidencias");
        if(args.Length>0)
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
            using var reopened=new GlassWorkshopForm();reopened.Show();Load(reopened,args[0]);
            long id=long.Parse(args[1]);string name=args[2];Check(Data(reopened).Quote.CustomerId==id,"Mesmo ID restaurado em NOVO PROCESSO");Document(reopened,id,name,args[3]);reopened.Close();return;
        }
        Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1'); SELECT 1");
        Auth.CreateUser("QA VINCULO","qa_vinculo","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qa_vinculo","Senha_Ficticia_123");
        Sql("INSERT INTO customers(id,name,document,phone,email,address) VALUES(995,'CLIENTE EXISTENTE QA','DOC QA','TEL QA','qa@example.invalid','END QA'); SELECT 1");
        long initial=Convert.ToInt64(Sql("SELECT COUNT(*) FROM customers"));
        using var main=new MainForm();main.Show();Pump();((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main)!).Stop();typeof(MainForm).GetField("automaticBackupCompleted",I)!.SetValue(main,true);
        var f=new GlassWorkshopForm(dialog=>typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(main,new object[]{dialog}));f.Show(main);Pump();Find<Button>(f,"LoadQuoteCustomers").PerformClick();Check(Data(f).Quote.CustomerId==0,"CARREGAR lista nao vincula registro automaticamente");
        Find<NumericUpDown>(f,"QuotePrice").Value=100;Find<NumericUpDown>(f,"QuoteAddition").Value=12;
        foreach(bool create in new[]{false,true})
        {
            string name=create?"CLIENTE RECEM CADASTRADO QA":"CLIENTE EXISTENTE QA";int stage=0;long chosen=0;var watch=Stopwatch.StartNew();
            using(var timer=new System.Windows.Forms.Timer{Interval=60})
            {
                timer.Tick+=(_,_)=>{
                    if(watch.ElapsedMilliseconds>20000)throw new Exception("Timeout fluxo original CLIENTES");
                    var crud=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="CLIENTES");var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="CLIENTE");
                    if(stage==0 && crud!=null)
                    {
                        Check(crud.Modal && All(crud).OfType<DataGridView>().Count()==1 && !All(crud).OfType<ListBox>().Any(),"MESMO cadastro original; sem segunda lista");
                        Check(Find<Button>(crud,"SelectCustomerForGlass").Visible,"SELECIONAR CLIENTE visivel junto ao cadastro real");stage=1;Button(crud,create?"NOVO":"EDITAR").PerformClick();
                    }
                    else if(stage==1 && editor!=null)
                    {
                        var values=(List<TextBox>)editor.Tag!;
                        if(create){values[0].Text=name;values[1].Text="DOC NOVO";stage=2;Button(editor,"SALVAR").PerformClick();}
                        else{Check(values[0].Text==name && values[1].Text=="DOC QA","Dados carregados nos campos do editor original");Check(Data(f).Quote.CustomerId==0,"Exibir dados NAO significa cliente vinculado");stage=2;Button(editor,"CANCELAR").PerformClick();}
                    }
                    else if(stage==2 && crud!=null)
                    {
                        var grid=All(crud).OfType<DataGridView>().Single();var row=grid.Rows.Cast<DataGridViewRow>().Single(x=>Convert.ToString(x.Cells["Nome"].Value)==name);chosen=Convert.ToInt64(row.Cells["ID"].Value);grid.CurrentCell=row.Cells["Nome"];row.Selected=true;
                        Check(Data(f).Quote.CustomerId==(create?995:0),"Escolher linha ainda exige acao explicita; vinculo anterior preservado");timer.Stop();Find<Button>(crud,"SelectCustomerForGlass").PerformClick();
                    }
                };
                timer.Start();Find<Button>(f,create?"RegisterGlassCustomer":"SelectGlassCustomer").PerformClick();
            }
            Check(chosen>0 && Data(f).Quote.CustomerId==chosen && Data(f).Quote.CustomerName==name,"Callback do cadastro vincula ID legitimo ao projeto");
            Check(Find<ComboBox>(f,"QuoteCustomer").Text==name,"Lista existente atualizada imediatamente sem reiniciar PDV");
            Check(Convert.ToInt64(Sql("SELECT COUNT(*) FROM customers"))==initial+(create?1:0),"Sem cadastro paralelo/duplicacao");
            string evidence=create?"novo":"existente";Document(f,chosen,name,evidence);var saved=Save(f);Check(GlassProjectStore.Load(saved.Id).Data.Quote.CustomerId==chosen,"ID salvo pela persistencia EXISTENTE");
            f.Close();var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{Assembly.GetExecutingAssembly().Location,saved.Id,chosen.ToString(),name,evidence+"-reaberto"})psi.ArgumentList.Add(arg);
            using(var child=Process.Start(psi)!){var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();Check(child.WaitForExit(60000),"Reabertura em processo separado terminou");Console.Write(output.GetAwaiter().GetResult());Console.Write(error.GetAwaiter().GetResult());Check(child.ExitCode==0,"Vinculo/A4/PDF preservados em novo processo "+evidence);}
            // Reabre a mesma central com projeto existente para testar troca por cadastro novo.
            if(!create){f=new GlassWorkshopForm(dialog=>typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(main,new object[]{dialog}));f.Show(main);Load(f,saved.Id);Pump();}
        }
        main.Close();Console.WriteLine("PASS VINCULO CLIENTE EXISTENTE/NOVO: "+checks+" verificacoes Windows");
    }
}
