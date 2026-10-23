using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;


class Probe
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool IsWindowEnabled(IntPtr hwnd);
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--route"){TestRoutes(args[1]);return;}
        ApplicationConfiguration.Initialize();
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        if (assembly.Location != Path.GetFullPath(args[0])) throw new Exception("Wrong delivered assembly");
        var type=assembly.GetType("LicAi.ApiKeyDialog")!;
        Console.WriteLine("PASS: delivered LIA assembly loaded from PDV LIC-AI path");
        using var owner = new Form { TopMost = true };
        owner.Show();
        string dummy = "sk-isolated-dummy-not-a-real-key-123456789";
        foreach (var action in new[] { "Save", "Cancel", "Close" })
        {
            using var dialog = (Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "Teste", "Chave" }, null)!;
            Exception? failure = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    if (dialog.Owner != owner || IsWindowEnabled(owner.Handle) || !dialog.Visible) throw new Exception("Modal owner failed");
                    var key = (TextBox)dialog.Controls.Find("ApiKey", true).Single();
                    if (!key.UseSystemPasswordChar) throw new Exception("Unmasked key");
                    key.Text = dummy;
                    if (action == "Close") dialog.Close();
                    else ((Button)dialog.Controls.Find(action, true).Single()).PerformClick();
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            };
            timer.Start();
            var result = dialog.ShowDialog(owner);
            if (failure != null) throw failure;
            if (!IsWindowEnabled(owner.Handle)) throw new Exception("Owner remains disabled");
            if (action == "Save" && (result != DialogResult.OK || (string)type.GetProperty("ApiKey", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)! != dummy)) throw new Exception("Save failed");
            if (action != "Save" && result == DialogResult.OK) throw new Exception("Cancel failed");
            Console.WriteLine("PASS: modal owned, masked, responsive: " + action);
        }
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".bin");
        try
        {
            var secretType=assembly.GetType("LicAi.Security.LocalSecretStore")!;
            var store=Activator.CreateInstance(secretType, path)!;
            secretType.GetMethod("SaveApiKey")!.Invoke(store,new object[]{dummy});
            if (System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(dummy)) throw new Exception("Plaintext secret");
            if ((string?)secretType.GetMethod("GetApiKey")!.Invoke(Activator.CreateInstance(secretType,path),null) != dummy) throw new Exception("DPAPI persistence failed");
            Console.WriteLine("PASS: DPAPI CurrentUser persistence without plaintext");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        owner.Close();
    }

    sealed class FeedHandler : System.Net.Http.HttpMessageHandler
    {
        public int ComponentRequests;
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken token)
        {
            string path=request.RequestUri!.AbsolutePath;
            bool component=path.Contains("/lia-mobile/");
            if(component)ComponentRequests++;
            string version=component?"10.457":"10.456";
            string target=path.Contains("/clients/")?"001":"";
            string json=System.Text.Json.JsonSerializer.Serialize(new {Version=version,TargetClientCode=target,Sha256=new string('a',64),PackageUrl="https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-001-v"+version+"/LEAL_INFO_PDV_001_UPDATE.zip"});
            return System.Threading.Tasks.Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent(json)});
        }
    }
    static void TestRoutes(string file)
    {
        var assembly=Assembly.LoadFrom(Path.GetFullPath(file));
        var type=assembly.GetType("LealInfoPDV.UpdateManager")!;
        var method=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static).Single(x=>x.Name=="LoadManifestAsync"&&x.GetParameters().Length==3);
        foreach(var (id,current,expected,count) in new[]{("001","10.453","10.456",0),("001","10.456","10.457",1),("001","10.457","",1),("002","10.456","",0),("003","10.456","",0),("004","10.456","",0)})
        {
            var handler=new FeedHandler();
            using var http=new System.Net.Http.HttpClient(handler);
            var task=(System.Threading.Tasks.Task)method.Invoke(null,new object[]{http,id,current})!;
            task.GetAwaiter().GetResult();
            var result=task.GetType().GetProperty("Result")!.GetValue(task);
            var version=result==null?"":(string)result.GetType().GetProperty("Version")!.GetValue(result)!;
            if(version!=expected||handler.ComponentRequests!=count)throw new Exception("Incorrect bridge feed eligibility");
            Console.WriteLine("PASS: updater route "+id+" "+current+" -> "+expected+" component calls="+count);
        }
    }
}
