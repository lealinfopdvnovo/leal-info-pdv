using System.IO.Compression;
using System.Reflection;
using LealInfoPDV;
using Microsoft.Data.Sqlite;

internal static class FirstAccessProbe
{
    [STAThread]
    private static void Main()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Directory.Exists(Database.AppFolder))
            throw new InvalidOperationException("Use um perfil descartável e vazio do GitHub Actions.");

        using var ownership = new RestoreEngine(Database.AppFolder).AcquireOwnership();
        Database.Initialize();
        Check(Auth.UserCount() == 0, "Empty database");
        var screen = typeof(Database).Assembly.GetType("LealInfoPDV.FirstAccessSetup", true)!;
        using (var form = (Form)Activator.CreateInstance(screen, nonPublic: true)!)
        {
            var labels = form.Controls[0].Controls.OfType<FlowLayoutPanel>().Single()
                .Controls.OfType<Button>().Select(button => button.Text).ToArray();
            Check(labels.SequenceEqual(new[] { "NOVO CADASTRO", "RECUPERAR BACKUP" }), "First access choices");
        }
        var restore = screen.GetMethod("RestoreLocalBackupAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        void Restore(string path) => ((Task)restore.Invoke(null, new object[] { path })!).GetAwaiter().GetResult();

        var temp = Directory.CreateTempSubdirectory("leal-first-access-");
        try
        {
            var invalid = Path.Combine(temp.FullName, "invalid.zip");
            File.WriteAllText(invalid, "invalid backup");
            try { Restore(invalid); throw new Exception("Invalid ZIP accepted"); }
            catch (InvalidDataException) { }
            Check(Auth.UserCount() == 0, "Invalid backup leaves the current database intact");

            var fixture = Path.Combine(temp.FullName, "fixture.db");
            using (var source = Database.Open())
            using (var destination = new SqliteConnection($"Data Source={fixture};Pooling=False"))
            {
                destination.Open();
                source.BackupDatabase(destination);
                using var command = destination.CreateCommand();
                command.CommandText = "INSERT INTO customers (name) VALUES ('CLIENTE DE HOMOLOGACAO')";
                command.ExecuteNonQuery();
            }
            var zipPath = Path.Combine(temp.FullName, "backup.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(fixture, "lealinfo.db");
            Restore(zipPath);
            Database.Initialize();
            using (var current = Database.Open())
            using (var command = current.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM customers WHERE name='CLIENTE DE HOMOLOGACAO'";
                Check(Convert.ToInt32(command.ExecuteScalar()) == 1, "ZIP restored and reopened");
            }
            Auth.CreateUser("Administrador de homologação", "qaadmin", "Teste123!", "ADMINISTRADOR", "", "");
            try { Restore(zipPath); throw new Exception("Unauthenticated restore allowed after registration"); }
            catch (InvalidOperationException) { }
            Check(Auth.UserCount() == 1, "Existing installation requires administrator restore");
        }
        finally { temp.Delete(true); }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Console.WriteLine("PASS " + label);
    }
}
