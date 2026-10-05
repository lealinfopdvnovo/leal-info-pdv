using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using LealInfoPDV;

internal static class Program
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic;
    const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Type T=typeof(DeliveryManagementForm);
    static bool baseline;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static TextBox Field(Form f,string label)=>All(f).OfType<Label>().Single(l=>l.Text==label).Parent!.Controls.OfType<TextBox>().Single();
    static Button Button(Form f,string text)=>All(f).OfType<Button>().Single(b=>b.Text==text);
    static object? Invoke(string method,object? owner,params object?[] args)=>T.GetMethod(method,owner==null?S:I)!.Invoke(owner,args);
    static object? Scalar(string sql){using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText=sql;return cmd.ExecuteScalar();}
    static void Exec(string sql){using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();}
    static void Dialog(DeliveryManagementForm central,string method,long? id,string title,Action<Form> inspect,bool save)
    {
        Exception? failure=null;bool visited=false;
        using var timer=new System.Windows.Forms.Timer{Interval=80};
        timer.Tick+=(_,_)=>
        {
            var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text==title);
            if(f==null||visited)return;
            visited=true;timer.Stop();
            try{inspect(f);if(save)f.BeginInvoke(new Action(()=>Button(f,method=="EditDriver"?"SALVAR MOTOBOY":"SALVAR ENTREGA").PerformClick()));else f.Close();}
            catch(Exception ex){failure=ex;f.Close();}
        };
        timer.Start();Invoke(method,central,id);Check(visited,"Dialog não abriu: "+title);if(failure!=null)throw failure;
    }
    static long? ChoiceId(ComboBox c)=>c.SelectedItem?.GetType().GetProperty("Value")?.GetValue(c.SelectedItem) as long?;
    static void Select(ComboBox c,long id){for(int i=0;i<c.Items.Count;i++)if((long?)c.Items[i]!.GetType().GetProperty("Value")!.GetValue(c.Items[i])==id){c.SelectedIndex=i;return;}throw new Exception("Motoboy não encontrado no combo: "+id);}
    sealed class FakeFirebase:HttpMessageHandler
    {
        public int Requests;
        public bool StopAtSend;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token)
        {
            Requests++;
            if(StopAtSend)throw new InvalidOperationException("QA_FLUXO_ENVIO_ALCANCADO");
            Check(req.Method==HttpMethod.Get,"Teste tentou gravar Firebase");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{pdvUid="Lme0kXlHalc9s6av0I9iJIoWxgJ2",destino="Rua QA, 125, Centro, Angra dos Reis - RJ"}))});
        }
    }
    static readonly FakeFirebase fake=new();
    static void InstallFake()
    {
        var type=typeof(Database).Assembly.GetType("LealInfoPDV.FirebaseDeliverySync",true)!;
        type.GetField("_idToken",S)!.SetValue(null,"QA_LOCAL_ONLY");type.GetField("_expires",S)!.SetValue(null,DateTime.UtcNow.AddHours(1));
        var http=(HttpClient)type.GetField("Http",S)!.GetValue(null)!;
        typeof(HttpMessageInvoker).GetField("_handler",I)!.SetValue(http,fake);
    }
    static void ExpectMessage(DeliveryManagementForm central,string expected,bool expectNetwork)
    {
        bool seen=false;int before=fake.Requests;Exception? failure=null;
        using var timer=new System.Windows.Forms.Timer{Interval=80};
        timer.Tick+=(_,_)=>
        {
            var hwnd=GetForegroundWindow();var text=new System.Text.StringBuilder(2048);GetWindowText(hwnd,text,text.Capacity);
            if(text.ToString() is not ("Telefone do motoboy" or "Envio ao SpeedFood"))return;
            var messages=new List<string>();EnumChildWindows(hwnd,(child,_)=>{var b=new System.Text.StringBuilder(4096);GetWindowText(child,b,b.Capacity);messages.Add(b.ToString());return true;},IntPtr.Zero);
            seen=true;timer.Stop();if(!messages.Any(x=>x.Contains(expected)))failure=new Exception("Mensagem incorreta: "+string.Join("|",messages));
            SendMessage(hwnd,0x111,(IntPtr)1,IntPtr.Zero);
        };
        timer.Start();Button(central,"ENVIAR CÓDIGO AO MOTOBOY").PerformClick();
        while(!seen){Application.DoEvents();Thread.Sleep(10);}
        if(failure!=null)throw failure;Check((fake.Requests>before)==expectNetwork,"Fluxo de envio/rejeição incorreto");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int GetWindowText(IntPtr h,System.Text.StringBuilder b,int n);
    delegate bool EnumProc(IntPtr h,IntPtr p);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumProc cb,IntPtr p);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [STAThread] static void Main(string[] args)
    {
        baseline=args.Contains("--baseline");
        _=Task.Run(async()=>{await Task.Delay(90000);Environment.FailFast("Timeout driver QA");});
        Application.ThreadException+=(_,e)=>{Console.Error.WriteLine(e.Exception);Environment.Exit(1);};
        Application.EnableVisualStyles();Database.Initialize();Invoke("EnsureTrackingSchema",null);InstallFake();
        var before=Convert.ToString(Scalar("SELECT (SELECT COUNT(*) FROM sales)||':'||(SELECT COUNT(*) FROM cash_movements)||':'||(SELECT SUM(stock) FROM products)"));
        using var central=new DeliveryManagementForm();central.Show();Application.DoEvents();
        Dialog(central,"EditDriver",null,"Novo Motoboy",f=>{Field(f,"Nome").Text="QA JOAO";Field(f,"Telefone").Text="(24) 99999-9999";},true);
        var driver=Convert.ToInt64(Scalar("SELECT id FROM delivery_drivers WHERE name='QA JOAO' ORDER BY id DESC LIMIT 1"));
        Check(Convert.ToString(Scalar($"SELECT phone FROM delivery_drivers WHERE id={driver}"))=="(24) 99999-9999","Telefone não persistiu");
        Console.WriteLine("PASS: cadastro real salvou telefone em delivery_drivers.phone; id="+driver);
        var delivery=(long)Invoke("InsertDeliveryWithTracking",null,DateTime.Now.ToString("s"),"QA CLIENTE","","Rua QA, 125, Centro, Angra dos Reis - RJ","","QA",0d,0d,"",driver,"","QA")!;
        var code=Convert.ToString(Scalar($"SELECT code FROM delivery_tracking WHERE delivery_id={delivery}"));
        bool restored=false;
        Dialog(central,"EditDelivery",delivery,"Editar Entrega",f=>
        {
            var combo=All(f).OfType<ComboBox>().Single();restored=ChoiceId(combo)==driver;
            Console.WriteLine($"REOPEN expected={driver}, actual={ChoiceId(combo)}, SelectedValue={combo.SelectedValue}, SelectedValueType={combo.SelectedValue?.GetType().FullName}");
            if(!baseline)Check(restored,"Vínculo não restaurado ao abrir entrega");
            Select(combo,driver);
        },true);
        Check(Convert.ToInt64(Scalar($"SELECT driver_id FROM deliveries WHERE id={delivery}"))==driver,"ID não persistiu ao salvar pela UI");
        if(baseline){Console.WriteLine("BASELINE_REOPEN_RESTORED="+restored);return;}
        central.Close();using var reopened=new DeliveryManagementForm();reopened.Show();Application.DoEvents();
        Dialog(reopened,"EditDelivery",delivery,"Editar Entrega",f=>Check(ChoiceId(All(f).OfType<ComboBox>().Single())==driver,"Perdeu vínculo após fechar/reabrir Central"),false);
        Console.WriteLine("PASS: ID real salvo e restaurado após reabrir entrega e Central");
        var data=Invoke("ReadDeliveryShare",null,delivery)!;Check((string)data.GetType().GetProperty("DriverPhone")!.GetValue(data)!=="(24) 99999-9999","Telefone incorreto recuperado");
        var normalized=(string)Invoke("BuildWhatsAppUrl",null,"(24) 99999-9999","QA TESTE")!;Check(normalized.StartsWith("https://wa.me/5524999999999?text="),"Normalização incorreta");
        fake.StopAtSend=true;ExpectMessage(reopened,"QA_FLUXO_ENVIO_ALCANCADO",true);
        Console.WriteLine("PASS: botão recuperou telefone e continuou fluxo existente; sem rede real/WhatsApp");
        Exec($"UPDATE deliveries SET driver_id=NULL WHERE id={delivery}");ExpectMessage(reopened,"Selecione um motoboy para esta entrega.",false);
        Exec($"UPDATE deliveries SET driver_id={driver} WHERE id={delivery};UPDATE delivery_drivers SET phone='' WHERE id={driver}");ExpectMessage(reopened,"O motoboy selecionado não possui telefone cadastrado.",false);
        Exec($"UPDATE delivery_drivers SET phone='24999999999',active=0 WHERE id={driver}");
        Dialog(reopened,"EditDelivery",delivery,"Editar Entrega",f=>Check(ChoiceId(All(f).OfType<ComboBox>().Single())==driver,"Perdeu motoboy inativo já vinculado"),false);
        Check(Convert.ToString(Scalar($"SELECT code FROM delivery_tracking WHERE delivery_id={delivery}"))==code,"Código alterado");
        Check(Convert.ToString(Scalar("SELECT (SELECT COUNT(*) FROM sales)||':'||(SELECT COUNT(*) FROM cash_movements)||':'||(SELECT SUM(stock) FROM products)"))==before,"Venda/caixa/estoque modificados");
        Console.WriteLine("PASS: sem motoboy, sem telefone, inativo, código preservado e sem movimentações externas");
        reopened.Close();
    }
}
