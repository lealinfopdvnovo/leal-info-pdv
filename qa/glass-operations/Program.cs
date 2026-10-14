using LealInfoPDV;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
static class Program
{
    const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly Assembly A=typeof(MainForm).Assembly;static int checks;
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
    static void Check(bool b,string name){if(!b)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static object Call(string cls,string method,params object[] args)=>A.GetType("LealInfoPDV."+cls)!.GetMethods(S).Single(x=>x.Name==method && x.GetParameters().Length==args.Length).Invoke(null,args)!;
    static object? Prop(object x,string name)=>x.GetType().GetProperty(name,I)!.GetValue(x);
    static void Invalid(Action action,string name){try{action();throw new Exception("FAIL aceita "+name);}catch(TargetInvocationException e)when(e.InnerException is InvalidOperationException or ArgumentException){Check(true,"Bloqueado "+name);}}
    static object Origin(GlassStoredProject p,string? op=null,long? client=null)=>Activator.CreateInstance(A.GetType("LealInfoPDV.GlassCashOrigin")!,I,null,new object[]{p.Id,p.Revision,client??p.Data.Quote.CustomerId,op??Guid.NewGuid().ToString("N")},CultureInfo.InvariantCulture)!;
    static object? Sql(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static long Count(string table)=>Convert.ToInt64(Sql("SELECT COUNT(*) FROM "+table));
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name)where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
    static Button Button(Form f,string text)=>All(f).OfType<Button>().Single(x=>x.Text==text);
    static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
    static GlassStoredProject Save(GlassWorkshopForm f)=>(GlassStoredProject)typeof(GlassWorkshopForm).GetMethod("SaveCurrentProject",I)!.Invoke(f,null)!;
    static void Load(GlassWorkshopForm f,string id)=>typeof(GlassWorkshopForm).GetMethod("LoadSavedProject",I)!.Invoke(f,new object[]{id});
    static void State(GlassStoredProject p,decimal paid,decimal spent,string status)
    {var state=Call("GlassOperationsStore","Read",p);Check((decimal)Prop(state,"Received")! ==paid && (decimal)Prop(state,"Expenses")! ==spent && (string)Prop(state,"Status")! ==status,"Financeiro real "+status+" recebido"+paid+" saidas"+spent);}
    static void Movement(GlassWorkshopForm f,decimal amount,string type)
    {
        int stage=0;bool confirmed=false;var watch=Stopwatch.StartNew();using var timer=new System.Windows.Forms.Timer{Interval=60};
        timer.Tick+=(_,_)=>{
            if(watch.ElapsedMilliseconds>15000)throw new Exception("Timeout fluxo financeiro real");
            var finance=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="FLUXO DE CAIXA — VIDRAÇARIA");var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Nova movimentação manual");
            if(stage==0 && finance!=null){Check(Find<Label>(finance,"GlassFinanceOverview").Text.Contains("Orçamento não é pagamento"),"Mesmo Fluxo de Caixa explica operacao real");stage=1;Button(finance,"NOVO").PerformClick();}
            else if(stage==1 && editor!=null){var fields=(List<TextBox>)editor.Tag!;fields[2].Text=amount.ToString("0.00",CultureInfo.GetCultureInfo("pt-BR"));All(editor).OfType<ComboBox>().Single().SelectedItem=type;stage=2;Button(editor,"SALVAR").PerformClick();}
            else if(stage==2){var confirm=FindWindow("#32770","Confirmar movimentação");if(confirm!=IntPtr.Zero){confirmed=true;stage=3;PostMessage(confirm,0x111,(IntPtr)6,IntPtr.Zero);}}
            else if(stage==3 && finance!=null && editor==null){timer.Stop();Check(All(finance).OfType<DataGridView>().Single().Rows.Count>0,"Lancamento real aparece no historico existente");finance.Close();}
        };
        timer.Start();Find<Button>(f,"OpenGlassFinance").PerformClick();Check(confirmed && stage==3,"Confirmacao financeira NATIVA explicita "+type+" "+amount);
    }
    static void Agenda(GlassWorkshopForm f,string kind,string? expectedProject=null)
    {
        int stage=0;bool seen=false;Task? nativeConfirmation=null;var watch=Stopwatch.StartNew();using var timer=new System.Windows.Forms.Timer{Interval=60};
        timer.Tick+=(_,_)=>{
            if(watch.ElapsedMilliseconds>15000)throw new Exception("Timeout agenda real");
            var agenda=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="GlassAgenda");var editor=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="GlassAppointmentEditor");
            if(stage==0 && agenda!=null){stage=1;if(expectedProject==null)Find<Button>(agenda,"NewGlassAppointment").PerformClick();}
            else if(stage==1 && editor!=null){Find<ComboBox>(editor,"GlassAppointmentKind").SelectedItem=kind;Find<DateTimePicker>(editor,"GlassAppointmentDate").Value=DateTime.Today.AddDays(3);Find<DateTimePicker>(editor,"GlassAppointmentTime").Value=DateTime.Today.AddHours(kind=="MEDIÇÃO"?9:14);Find<TextBox>(editor,"GlassAppointmentNotes").Text="OBSERVACAO QA "+kind;stage=2;Find<Button>(editor,"SaveGlassAppointment").PerformClick();}
            else if((stage==2 || expectedProject!=null) && agenda!=null && editor==null)
            {
                var grid=Find<DataGridView>(agenda,"GlassAgendaGrid");var row=grid.Rows.Cast<DataGridViewRow>().First(x=>(string)Prop(x.DataBoundItem!,"Kind")! ==kind && (expectedProject==null || (string)Prop(x.DataBoundItem!,"ProjectId")! ==expectedProject));
                Check((string)Prop(row.DataBoundItem!,"Phone")! =="TEL QA" && (string)Prop(row.DataBoundItem!,"Address")! =="END QA","Agenda consulta contato ORIGINAL sem duplicar");seen=true;timer.Stop();
                if(expectedProject!=null){grid.CurrentCell=row.Cells["Kind"];
                    bool fresh=typeof(GlassWorkshopForm).GetField("stored",I)!.GetValue(f)==null;
                    nativeConfirmation=Task.Run(async()=>{if(!fresh)return;for(int i=0;i<100;i++){var h=FindWindow("#32770","Agenda");if(h!=IntPtr.Zero){PostMessage(h,0x111,(IntPtr)6,IntPtr.Zero);return;}await Task.Delay(50);}throw new Exception("Confirmacao de abrir projeto nao encontrada");});
                    Find<Button>(agenda,"OpenGlassAgendaProject").PerformClick();}else agenda.Close();
            }
        };timer.Start();Find<Button>(f,"OpenGlassAgenda").PerformClick();nativeConfirmation?.GetAwaiter().GetResult();Check(seen,"Agenda REAL localizar "+kind);
    }
    [STAThread]static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Windows descartavel");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();Directory.CreateDirectory("glass-operations-evidencias");
        if(args.Length>0){var reopenedProject=GlassProjectStore.Load(args[0]);State(reopenedProject,132,27,"PAGO");var items=(System.Collections.IEnumerable)Call("GlassOperationsStore","Appointments",true);var list=items.Cast<object>().ToList();Check(list.Count==2 && list.All(x=>(string)Prop(x,"ProjectId")! ==reopenedProject.Id),"MEDICAO/INSTALACAO persistem em NOVO PROCESSO");using var reopened=new GlassWorkshopForm();reopened.Show();Load(reopened,reopenedProject.Id);Check(Find<Label>(reopened,"GlassOperationsStatus").Text.Contains("PAGO"),"Financeiro na Central reaberta");reopened.Close();return;}
        Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1');INSERT INTO customers(id,name,phone,address) VALUES(994,'CLIENTE OPS QA','TEL QA','END QA'); SELECT 1");Auth.CreateUser("QA OPS","qa_ops","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");Auth.Login("qa_ops","Senha_Ficticia_123");
        using var main=new MainForm();main.Show();Pump();((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main)!).Stop();typeof(MainForm).GetField("automaticBackupCompleted",I)!.SetValue(main,true);
        using var f=new GlassWorkshopForm(dialog=>typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(main,new object[]{dialog}));f.Show(main);Pump();Find<Button>(f,"LoadQuoteCustomers").PerformClick();Find<ComboBox>(f,"QuoteCustomer").SelectedIndex=1;Find<NumericUpDown>(f,"QuotePrice").Value=100;Find<NumericUpDown>(f,"QuoteAddition").Value=12;Find<TextBox>(f,"Description").Text="PROJETO OPS QA";
        long sales=Count("sales"),cash=Count("cash_movements"),products=Count("products"),clients=Count("customers");var p=Save(f);
        var quote=Call("GlassOperationsStore","Quote",p.Data);var snap=new GlassCommercialSnapshot((GlassQuote)quote,994,"CLIENTE OPS QA","","",DateTime.Now,Array.Empty<GlassMaterialLink>());File.WriteAllBytes("glass-operations-evidencias/orcamento-sem-receita.pdf",GlassCommercialDocument.PdfBytes(snap));State(p,0,0,"PENDENTE");Check(Count("sales")==sales && Count("cash_movements")==cash && Count("products")==products,"Orcamento/PDF/salvar NAO geram receita, venda ou estoque");
        int beforeBackup=Directory.GetFiles(Database.BackupFolder,"ANTES_VIDRA_FIN_AGENDA_V1_*.db").Length;Call("GlassOperationsStore","EnsureReady");Check(Directory.GetFiles(Database.BackupFolder,"ANTES_VIDRA_FIN_AGENDA_V1_*.db").Length==beforeBackup+1,"Backup SQLite real ANTES das duas tabelas aditivas");Call("GlassOperationsStore","EnsureReady");Check(Directory.GetFiles(Database.BackupFolder,"ANTES_VIDRA_FIN_AGENDA_V1_*.db").Length==beforeBackup+1,"Migracao idempotente");
        Movement(f,50,"ENTRADA");State(p,50,0,"PARCIALMENTE PAGO");Movement(f,82,"ENTRADA");State(p,132,0,"PAGO");Movement(f,20,"SAÍDA");State(p,132,20,"PAGO");
        var origin=Origin(p);long id=(long)Call("CashFlow","RegisterForGlass","SAÍDA","QA SAIDA",7m,origin);long same=(long)Call("CashFlow","RegisterForGlass","SAÍDA","QA SAIDA",7m,origin);Check(id==same && Count("cash_movements")==cash+4,"Repetir MESMA operacao nao duplica movimento");Invalid(()=>Call("CashFlow","RegisterForGlass","SAÍDA","QA ALTERADO",8m,origin),"token reutilizado com valor diferente");Invalid(()=>Call("CashFlow","RegisterForGlass","ENTRADA","QA EXCESSO",1m,Origin(p)),"receber acima do saldo");Invalid(()=>Call("CashFlow","RegisterForGlass","ENTRADA","QA CLIENTE",1m,Origin(p,client:999)),"cliente incompatível");Check(Count("cash_movements")==cash+4,"Falhas atomicas sem movimentos orfaos");
        Invalid(()=>{var wrong=p.Data with{Quote=p.Data.Quote with{CustomerId=0}};try{GlassProjectStore.Save(wrong,p);}catch(InvalidOperationException e){throw new TargetInvocationException(e);}},"trocar cliente de projeto com movimentacao");
        Agenda(f,"MEDIÇÃO");Agenda(f,"INSTALAÇÃO");var other=GlassProjectStore.Save(p.Data with{Description="OUTRO PROJETO QA"});Load(f,other.Id);Agenda(f,"INSTALAÇÃO",p.Id);Check((GlassStoredProject)typeof(GlassWorkshopForm).GetField("stored",I)!.GetValue(f)! is var selected && selected.Id==p.Id,"ABRIR PROJETO pela agenda abre ID correto");
        typeof(GlassWorkshopForm).GetMethod("ResetSavedProject",I)!.Invoke(f,null);Agenda(f,"MEDIÇÃO",p.Id);Check(typeof(GlassWorkshopForm).GetField("stored",I)!.GetValue(f)!=null,"Agenda acessivel na Central nova e abre projeto salvo");
        var updated=GlassProjectStore.Save(p.Data with{Description="PROJETO OPS QA EDITADO"},p);Invalid(()=>Call("CashFlow","RegisterForGlass","SAÍDA","QA STALE",1m,Origin(p)),"revisao obsoleta");Load(f,updated.Id);State(updated,132,27,"PAGO");
        Check(Count("sales")==sales && Count("products")==products && Count("customers")==clients,"Financeiro/agenda nao criam vendas, estoque ou clientes duplicados");Check(Count("glass_cash_links_v1")==4 && Count("glass_appointments_v1")==2,"Somente referencias e dois compromissos persistidos");
        f.Close();main.Close();var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};psi.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);psi.ArgumentList.Add(updated.Id);using(var child=Process.Start(psi)!){var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();Check(child.WaitForExit(60000),"Novo processo encerrou");Console.Write(output.GetAwaiter().GetResult());Console.Write(error.GetAwaiter().GetResult());Check(child.ExitCode==0,"Financeiro e agenda sobrevivem a fechamento/reabertura");}Console.WriteLine("PASS PASSOS05/06: "+checks+" verificacoes Windows");
    }
}
