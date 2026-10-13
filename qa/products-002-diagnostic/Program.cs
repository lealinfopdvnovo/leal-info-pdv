using LealInfoPDV;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;

static class Program
{
    const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.NonPublic;
    [DllImport("user32.dll")]static extern uint GetGuiResources(IntPtr process,uint flags);
    static readonly Type T=typeof(MainForm).Assembly.GetType("LealInfoPDV.ProductRegistrationForm",true)!;
    static readonly List<object> observations=new();
    static Form Create(long? id=null)=>(Form)Activator.CreateInstance(T,BindingFlags.Instance|BindingFlags.NonPublic,null,new object?[]{id},null)!;
    static object? Call(object instance,string name,params object[] args)=>T.GetMethod(name,I)!.Invoke(instance,args);
    static void Photo(Form f,string? path){T.GetField("_photoPath",I)!.SetValue(f,path);Call(f,"LoadPhoto");}
    static object Metrics(string action){using var p=Process.GetCurrentProcess();p.Refresh();return new{action,privateBytes=p.PrivateMemorySize64,workingSet=p.WorkingSet64,managed=GC.GetTotalMemory(false),handles=p.HandleCount,gdi=GetGuiResources(p.Handle,0),user=GetGuiResources(p.Handle,1)};}
    static void Record(string action){var m=Metrics(action);observations.Add(m);Console.WriteLine(JsonSerializer.Serialize(m));}
    static void ExpectedImageCase(string file,string name)
    {
        using var form=Create();
        try{Photo(form,file);observations.Add(new{test=name,result="loaded"});Console.WriteLine("IMAGE "+name+" carregada");}
        catch(TargetInvocationException e){var cause=e.InnerException!;observations.Add(new{test=name,type=cause.GetType().FullName,stack=cause.ToString()});Console.WriteLine("IMAGE "+name+" "+cause);}
    }
    [STAThread]static void Main()
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Exige Windows descartável");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
        Directory.CreateDirectory("produtos-002-evidencias");Database.Initialize();T.GetMethod("EnsureSchema",S)!.Invoke(null,null);
        string root=Path.GetFullPath("produtos-002-evidencias");
        string normal=Path.Combine(root,"foto-ficticia.png"),large=Path.Combine(root,"foto-grande-ficticia.png"),bad=Path.Combine(root,"foto-invalida-ficticia.jpg");
        using(var image=new Bitmap(1024,768)){using var g=Graphics.FromImage(image);g.Clear(Color.DarkBlue);image.Save(normal,ImageFormat.Png);}
        using(var image=new Bitmap(4096,2048)){using var g=Graphics.FromImage(image);g.Clear(Color.Cyan);image.Save(large,ImageFormat.Png);}
        File.WriteAllText(bad,"Arquivo fictício. Não é uma imagem.");ExpectedImageCase(normal,"normal");ExpectedImageCase(large,"grande");ExpectedImageCase(bad,"invalida");
        using(var f=Create()){Photo(f,null);Record("sem foto / fallback");}
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Record("baseline após GC");
        for(int i=0;i<40;i++)
        {
            var form=Create();Photo(form,normal);form.Show();Application.DoEvents();form.Close();form.Dispose();
            if(i%5==0)Record("abrir/fechar "+(i+1));
        }
        Record("40 aberturas antes GC");GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Record("40 aberturas após GC");
        using(var form=Create())
        {
            for(int i=0;i<80;i++){Photo(form,i%2==0?normal:large);if(i%20==0)Record("troca de foto "+i);}
            Call(form,"NewProduct");
        }
        Record("navegação/fotos concluída");
        var owned=Create();Photo(owned,normal);var box=(PictureBox)T.GetField("_photo",I)!.GetValue(owned)!;var retained=box.Image!;
        owned.Dispose();
        bool imageAlive;try{imageAlive=retained.Width==1024;}catch(ArgumentException){imageAlive=false;}
        observations.Add(new{test="image_lifetime_after_form_dispose",imageStillUsable=imageAlive});
        Console.WriteLine("DIAGNÓSTICO: imagem ainda utilizável após Dispose da janela = "+imageAlive);retained.Dispose();
        File.WriteAllText(Path.Combine(root,"DIAGNOSTICO_ISOLADO_002.json"),JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("DIAGNÓSTICO CONCLUÍDO. Evidências sintéticas NÃO comprovam causa do incidente do cliente.");
    }
}
