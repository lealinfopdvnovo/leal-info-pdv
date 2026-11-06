using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;

static class Database
{
    public static string BackupFolder = Path.Combine(Path.GetTempPath(), "LealLogonQA-" + Guid.NewGuid());
}
static class DatabaseBackupService
{
    public static TaskCompletionSource Gate = new();
    public static int Calls;
    public static Task CreateAndSendAsync(CancellationToken token) { Calls++; return Gate.Task; }
    public static void Reset() { Gate = new(); Calls = 0; }
}
partial class CloseProbe
{
    public int ClosedCount;
    public void Prepare() { Text = "LOGON Loja - teste isolado"; Show(); FormClosed += (_,_) => ClosedCount++; }
}
static class Program
{
    static readonly List<string> Passed = new();
    static readonly List<string> Errors = new();
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Passed.Add(label);
    }
    static void Pump(Func<bool> until)
    {
        var watch = Stopwatch.StartNew();
        while (!until() && watch.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(5); }
        if (!until()) throw new TimeoutException("UI did not finish");
    }
    static CloseProbe Start()
    {
        DatabaseBackupService.Reset();
        var form = new CloseProbe();
        form.Prepare();
        form.Close();
        Pump(() => DatabaseBackupService.Calls == 1);
        return form;
    }
    [STAThread] static int Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_,e) => Errors.Add(e.Exception.ToString());
        try
        {
            using(var f = Start())
            {
                Check(!f.IsDisposed && !f.Enabled && f.UseWaitCursor, "First click waits visibly for backup");
                f.Close(); f.Close();
                Application.DoEvents();
                Check(!f.IsDisposed && f.ClosedCount == 0 && DatabaseBackupService.Calls == 1, "Repeated close stays cancelled and no duplicate backup");
                DatabaseBackupService.Gate.SetResult();
                Pump(() => f.IsDisposed);
                Check(f.ClosedCount == 1, "Single original click closes automatically after backup");
            }
            DatabaseBackupService.Reset();
            DatabaseBackupService.Gate.SetResult();
            using(var f = new CloseProbe())
            {
                f.Prepare(); f.Close(); Pump(() => f.IsDisposed);
                Check(f.ClosedCount == 1 && DatabaseBackupService.Calls == 1, "Synchronous backup completes without recursive initial close");
            }
            using(var f = Start())
            {
                DatabaseBackupService.Gate.SetException(new IOException("simulated backup failure"));
                Pump(() => f.IsDisposed);
                Check(f.ClosedCount == 1 && File.ReadAllText(Path.Combine(Database.BackupFolder,"backup-errors.log")).Contains("simulated backup failure"), "Backup failure is logged and closes safely");
            }
            using(var f = Start())
            {
                f.Dispose();
                DatabaseBackupService.Gate.SetResult();
                for(int i=0;i<30;i++) { Application.DoEvents(); Thread.Sleep(5); }
                Check(f.IsDisposed, "External disposal before backup completion does not invoke destroyed handle");
            }
            for(int i=0;i<3;i++)
            {
                using var f = Start();
                DatabaseBackupService.Gate.SetResult();
                Pump(() => f.IsDisposed);
                Check(f.ClosedCount == 1 && DatabaseBackupService.Calls == 1, "Fresh login session " + (i+1));
            }
            Check(Errors.Count == 0, "No unhandled UI exception");
            File.WriteAllText("evidence/logon-windows.json", JsonSerializer.Serialize(new {Pass=true, Passed, Errors, Scope="Exact reconstructed production closing handler; fake backup I/O; real Windows Forms loop", RealCustomerLoginTested=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS " + Passed.Count + " Windows LOGON lifecycle checks");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { if(Directory.Exists(Database.BackupFolder))Directory.Delete(Database.BackupFolder,true); }
    }
}
