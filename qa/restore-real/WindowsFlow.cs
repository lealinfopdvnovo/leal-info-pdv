using LealInfoPDV;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Forms;

internal static class WindowsFlow
{
    public static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private static string Hash(string p) { using var f = File.OpenRead(p); return Convert.ToHexString(SHA256.HashData(f)); }
    private static Process Start(string executable, params string[] args)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }
    private static void Wait(Func<bool> condition, int seconds, string why)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(seconds)) { if (condition()) return; Thread.Sleep(100); }
        throw new Exception(why);
    }
    public static void AutomaticRestart()
    {
        var executable = Path.GetFullPath("bin/Release/net8.0-windows10.0.19041.0/LealInfoPDV.exe");
        Check(File.Exists(executable), "Real PDV executable not found");
        var licensePath = Path.Combine(Database.AppFolder, "network.license");
        Check(!File.Exists(licensePath), "Disposable runner must not contain an operational license");
        // A deliberately invalid activation fixture stops normal startup at its
        // real activation dialog without using or changing a customer license.
        File.WriteAllText(licensePath, "QA-IMMUTABLE-LICENSE");
        var licenseHash = Hash(licensePath);
        var started = DateTime.UtcNow;
        Process? previous = null; Process? maintenance = null;
        var source = Path.Combine(Database.AppFolder, "qa-selected.db");
        try
        {
            using (var input = new Microsoft.Data.Sqlite.SqliteConnection(Database.ConnectionString + ";Pooling=False"))
            using (var output = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={source};Pooling=False"))
            { input.Open(); output.Open(); input.BackupDatabase(output); }
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={source};Pooling=False"))
            {
                c.Open(); using var cmd = c.CreateCommand();
                cmd.CommandText = "INSERT INTO products(id,name,price) VALUES(900,'REAL-RESTART',20);"; cmd.ExecuteNonQuery();
            }
            var stage = DatabaseBackupService.PrepareAutomaticRestoreAsync(source).GetAwaiter().GetResult();
            previous = Start(executable);
            Wait(() => { previous.Refresh(); return previous.MainWindowHandle != IntPtr.Zero; }, 20, "Previous real PDV did not show its activation fixture");
            var before = Hash(Database.DbPath);
            maintenance = Start(executable, "--restaurar-automatico", stage, previous.Id.ToString());
            Thread.Sleep(500);
            Check(Hash(Database.DbPath) == before && !maintenance.HasExited, "Maintenance did not wait for previous PDV");
            Wait(() =>
            {
                if (previous.HasExited) return true;
                previous.Refresh();
                previous.CloseMainWindow(); return false;
            }, 20, "Previous real PDV did not close cleanly");
            Check(maintenance.WaitForExit(30000), "Maintenance real process did not complete");
            Wait(() => Process.GetProcessesByName("LealInfoPDV").Any(p =>
            {
                using (p) return p.Id != previous.Id && p.Id != maintenance.Id && p.StartTime.ToUniversalTime() >= started && p.MainModule!.FileName == executable;
            }), 15, "Real PDV was not automatically restarted");
            using var check = new Microsoft.Data.Sqlite.SqliteConnection(Database.ConnectionString); check.Open();
            using var query = check.CreateCommand(); query.CommandText = "SELECT name FROM products WHERE id=900";
            Check(Convert.ToString(query.ExecuteScalar()) == "REAL-RESTART", "Real process did not restore selected data");
            Check(Hash(licensePath) == licenseHash, "Real restart modified license fixture");
            Console.WriteLine("PASS real-maintenance-wait-restore-and-automatic-restart (activation UI fixture; not activated-store qualification)");
        }
        finally
        {
            foreach (var p in Process.GetProcessesByName("LealInfoPDV"))
            using (p)
                if (p.StartTime.ToUniversalTime() >= started && p.MainModule!.FileName == executable && !p.HasExited) { p.Kill(true); p.WaitForExit(); }
            previous?.Dispose(); maintenance?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(licensePath)) File.Delete(licensePath);
            if (File.Exists(source)) File.Delete(source);
        }
    }
    public static void MainFormAfterRestore()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Auth.CreateUser("QA", "restore-qa", "qa-only-local", "ADMINISTRADOR", "", "");
                Check(Auth.Login("restore-qa", "qa-only-local") != null, "Authentication after restore failed");
                using var main = new MainForm();
                main.CreateControl();
                Check(main.Controls.Count > 0, "Real PDV controls were not constructed");
                using var timer = new System.Windows.Forms.Timer { Interval = 1500 };
                var salesOpened = false;
                timer.Tick += (_, _) =>
                {
                    var sales = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text.Contains("TELA DE VENDAS"));
                    if (sales == null) return;
                    salesOpened = true;
                    using var bitmap = new System.Drawing.Bitmap(sales.Width, sales.Height);
                    sales.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, sales.Width, sales.Height));
                    Directory.CreateDirectory("restore-evidence"); bitmap.Save("restore-evidence/pdv-sales-after-restore.png");
                    timer.Stop(); sales.Close();
                };
                timer.Start();
                typeof(MainForm).GetMethod("OpenSales", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(main, null);
                Check(salesOpened, "Real sales UI did not open");
                Console.WriteLine("PASS real-mainform-and-sales-ui-after-restore");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Check(thread.Join(TimeSpan.FromSeconds(30)), "PDV UI test timed out");
        if (failure != null) throw failure;
    }
}
