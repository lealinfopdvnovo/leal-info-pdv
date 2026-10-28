using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

internal static class Probe
{
    static IEnumerable<Control> Walk(Control c) => c.Controls.Cast<Control>()
        .SelectMany(x => new[] { x }.Concat(Walk(x)));
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
                throw new Exception("Disposable Windows CI only");
            ApplicationConfiguration.Initialize();
            var a = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var db = a.GetType("LealInfoPDV.Database", true)!;
            db.GetMethod("Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
            var mainType = a.GetType("LealInfoPDV.MainForm", true)!;
            using var main = (Form)Activator.CreateInstance(mainType)!;
            bool removed = args[1] == "after";
            var buttons = Walk(main).Where(x => x.Name == "btnAssistenteAI").ToArray();
            bool baselineAbsent=Environment.GetEnvironmentVariable("CHANNEL")=="GLOBAL" && Environment.GetEnvironmentVariable("LEAL_LIA_BASELINE_ABSENT")=="1";
            int expectedCount=removed || baselineAbsent ? 0 : 1;
            if (buttons.Length != expectedCount) throw new Exception("Unexpected LIA button count");
            var labels = Walk(main).Select(x => x.GetType().FullName + ":" + x.Name + ":" + x.Text)
                .Where(x => !x.Contains("btnAssistenteAI")).Order().ToArray();
            var methods = mainType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => !x.Name.Contains('<') && !x.Name.Contains("btnAssistenteAI"))
                .Select(x => x.Name+"("+string.Join(',',x.GetParameters().Select(p=>p.ParameterType.FullName))+")")
                .Order().ToArray();
            foreach (var size in new[] { new System.Drawing.Size(1366,768), new System.Drawing.Size(1600,900), new System.Drawing.Size(1920,1080) })
            {
                main.WindowState=FormWindowState.Normal; main.ClientSize=size; main.PerformLayout();
                if (Walk(main).Count(x=>x.Name=="btnAssistenteAI") != expectedCount) throw new Exception("Resize restored LIA button");
            }
            using var login = (Form)Activator.CreateInstance(a.GetType("LealInfoPDV.LoginForm",true)!)!;
            using var splash = (Form)Activator.CreateInstance(a.GetType("LealInfoPDV.SplashForm",true)!)!;
            var result = new { Pass=true, Removed=removed, OperationalControls=labels,
                Methods=methods, LoginType=login.GetType().FullName, SplashType=splash.GetType().FullName,
                AnimationPixelTested=false, SalesEndToEndTested=false, Published=false };
            Directory.CreateDirectory("evidence");
            var path = "evidence/windows-"+args[1]+".json";
            if(removed)
            {
                using var old=JsonDocument.Parse(File.ReadAllText("evidence/windows-before.json"));
                if(!old.RootElement.GetProperty("OperationalControls").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(labels))
                    throw new Exception("Operational controls changed");
                if(!old.RootElement.GetProperty("Methods").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(methods))
                    throw new Exception("Operational methods changed");
            }
            File.WriteAllText(path, JsonSerializer.Serialize(result));
            Console.WriteLine("PASS Windows "+args[1]+": button count, layout, operational controls/methods, login and splash constructors");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
