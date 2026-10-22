using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace Leal.Diagnostics;
internal static class Program
{
    [STAThread]static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Contains("--self-test"))return SelfTest.Run();
        Application.Run(new MainForm());return 0;
    }
}
internal static class SelfTest
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    internal static int Run()
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")return 2;
        Directory.CreateDirectory("diagnostics-proof");var checks=new List<string>();var skipped=new List<string>();string fixture=Path.Combine(Path.GetTempPath(),"leal-diagnostics-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
        using var timeout=new System.Threading.Timer(_=>Environment.Exit(93),null,60000,Timeout.Infinite);
        void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
        try
        {
            foreach(string name in new[]{"lealinfo.db","license.dat","settings.json","original.png","same-size.png"})File.WriteAllText(Path.Combine(fixture,name),name.EndsWith("png")?(name.StartsWith("original")?new string('a',40):new string('b',40)):"SENTINEL-SECRET-CONTENT");
            var before=Directory.GetFiles(fixture).ToDictionary(f=>f,f=>(Hash:Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))),Time:File.GetLastWriteTimeUtc(f)));
            string link=Path.Combine(fixture,"cycle");try{Directory.CreateSymbolicLink(link,fixture);}catch(Exception e)when(e is UnauthorizedAccessException or IOException or System.ComponentModel.Win32Exception){skipped.Add("Symlink unavailable on this runner");}
            var inventory=FileInventory.Scan(fixture,CancellationToken.None);
            Check(inventory.Files==5&&inventory.Complete,"inventory only observes original files and does not recurse through links");
            Check(inventory.SameSizeImages.Count==1,"same-size images are candidates only, not claimed as hash-confirmed duplicates");
            var partial=FileInventory.Scan(fixture,CancellationToken.None,maxEntries:1);Check(!partial.Complete,"file count budget marks inventory partial");
            using(var canceled=new CancellationTokenSource()){canceled.Cancel();Check(!FileInventory.Scan(fixture,canceled.Token).Complete,"canceled scan is marked partial");}
            var machine=Collector.Computer();Check(machine.TotalRam>0&&machine.LogicalProcessors>0,"real Windows hardware and RAM collected");
            using var own=Process.GetCurrentProcess();var collector=new Collector(own.Id);var one=collector.Take("Repouso");Thread.Sleep(1100);var two=collector.Take("Venda");
            Check(one.PdvCpu==null&&two.PdvCpu is >=0 and <=100,"first CPU sample is not fabricated; following sample is normalized and bounded");
            Check(two.ProcessAvailable&&two.WorkingSet>0&&two.PrivateBytes>0&&two.ReadBytes.HasValue,"real Windows process memory and I/O counters collected");
            Check(two.SystemCpu is >=0 and <=100,"real system CPU counter bounded");
            Check(two.TcpConnections.HasValue,"IPv4 and IPv6 owner-PID TCP tables queried without collecting endpoints");
            var gone=new Collector(int.MaxValue).Take("Repouso");Check(!gone.ProcessAvailable&&gone.Note!=null,"missing process is reported without invented zeros");
            var session=new Session{Computer=machine,Files=inventory,Samples=new(){one,two,gone}};
            Check(Report.Findings(session).Any(f=>f.Contains("insuficiente")),"short capture produces insufficient-evidence finding");
            var originalFiles=inventory.Largest.ToArray();inventory.Largest.Add(new("<script>alert('x')</script>",1,".png"));string html=Report.Html(session);
            Check(!html.Contains("<script>")&&html.Contains("&lt;script&gt;"),"report HTML escapes untrusted file names");
            Check(html.Contains("Não atribuído ao PDV/Firebase")&&!html.Contains("SENTINEL-SECRET-CONTENT"),"report separates network scope and never includes protected file contents");
            inventory.Largest.Clear();inventory.Largest.AddRange(originalFiles);
            bool refused=false;try{Report.Save(session,Path.Combine(fixture,"report.html"),fixture);}catch(IOException){refused=true;}Check(refused&&!File.Exists(Path.Combine(fixture,"report.html")),"report refuses writing inside monitored installation");
            string output=Path.GetFullPath("diagnostics-proof/relatorio-exemplo.html");Report.Save(session,output,fixture);
            var exported=JsonSerializer.Deserialize<Session>(File.ReadAllText(Path.ChangeExtension(output,"json")));
            Check(File.Exists(output)&&exported?.Samples.Count==3,"HTML and JSON export contain actual collected samples");
            foreach(var f in before)Check(File.Exists(f.Key)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f.Key)))==f.Value.Hash&&File.GetLastWriteTimeUtc(f.Key)==f.Value.Time,"protected fixture unchanged: "+Path.GetFileName(f.Key));
            using(var form=new MainForm())
            {
                form.Show();Application.DoEvents();
                var choices=Field<ComboBox>(form,"processes");choices.Items.Add(new ProcessChoice(own.Id,"QA isolated process"));choices.SelectedIndex=choices.Items.Count-1;Field<TextBox>(form,"folder").Text=fixture;Field<Button>(form,"start").PerformClick();
                var until=Stopwatch.StartNew();while((Field<Session?>(form,"session")?.Samples.Count??0)<3&&until.ElapsedMilliseconds<20000){Application.DoEvents();Thread.Sleep(25);}
                Check((Field<Session?>(form,"session")?.Samples.Count??0)>=3,"real start button collects process samples while UI remains responsive");
                Field<Button>(form,"stop").PerformClick();while(Field<bool>(form,"running")&&until.ElapsedMilliseconds<25000){Application.DoEvents();Thread.Sleep(25);}
                Check(!Field<bool>(form,"running")&&Field<Button>(form,"save").Enabled,"real stop button ends collection and enables report export");
                form.ClientSize=new Size(940,720);form.PerformLayout();Application.DoEvents();using(var bmp=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(bmp,form.ClientRectangle);bmp.Save("diagnostics-proof/diagnostico-windows.png");}
                form.Close();
            }
            Check(Application.OpenForms.Count==0,"all test windows close cleanly");
            foreach(var f in before)Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f.Key)))==f.Value.Hash,"GUI analysis preserves: "+Path.GetFileName(f.Key));
            File.WriteAllText("diagnostics-proof/results.json",JsonSerializer.Serialize(new{Success=true,Checks=checks,Skipped=skipped,Windows=Environment.OSVersion.ToString()},Report.JsonOptions));
            Directory.Delete(fixture,true);Environment.Exit(0);return 0;
        }
        catch(Exception e){File.WriteAllText("diagnostics-proof/failure.txt",e.ToString());Environment.Exit(1);return 1;}
    }
}
