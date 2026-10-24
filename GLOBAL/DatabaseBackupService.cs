using Microsoft.Data.Sqlite;
using System.IO.Compression;

namespace LealInfoPDV;

public static class DatabaseBackupService
{
    private const long MaxRestoreBytes = 1_073_741_824;
    private static readonly SemaphoreSlim OperationGate = new(1, 1);

    private static async Task<string> CreateZipAsync(CancellationToken cancellationToken = default)
    {
        if (Network.NetworkConfiguration.Current.Mode == "terminal")
            throw new InvalidOperationException("Faça o backup no computador servidor, que contém os dados compartilhados.");
        Directory.CreateDirectory(Database.BackupFolder);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var snapshotPath = Path.Combine(Database.BackupFolder, $"LEAL_INFO_PDV_{stamp}.db");
        var zipPath = Path.Combine(Database.BackupFolder, $"LEAL_INFO_PDV_{stamp}.zip");

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = Database.Open();
            using var destination = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadWriteCreate;Pooling=False");
            destination.Open();
            source.BackupDatabase(destination);
        }, cancellationToken);

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(snapshotPath, "lealinfo.db", CompressionLevel.Optimal);
            }, cancellationToken);
            return zipPath;
        }
        catch
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            throw;
        }
        finally
        {
            if (File.Exists(snapshotPath)) File.Delete(snapshotPath);
        }
    }

    public static async Task<string> CreateAndSendAsync(CancellationToken cancellationToken = default)
    {
        await OperationGate.WaitAsync(cancellationToken);
        try
        {
            var zipPath = await CreateZipAsync(cancellationToken);
            await EmailRecovery.SendBackupAsync(zipPath, cancellationToken);
            return zipPath;
        }
        finally { OperationGate.Release(); }
    }

    // Preparation never changes the operational database.
    public static async Task<string> PrepareAutomaticRestoreAsync(string selectedPath, CancellationToken cancellationToken = default)
    {
        if (Network.NetworkConfiguration.Current.Mode == "terminal")
            throw new InvalidOperationException("Faça a restauração no servidor.");
        await OperationGate.WaitAsync(cancellationToken);
        try { return await Task.Run(() => new RestoreEngine(Database.AppFolder).Prepare(selectedPath, cancellationToken), cancellationToken); }
        finally { OperationGate.Release(); }
    }

    public static async Task RestoreAsync(string selectedPath, CancellationToken cancellationToken = default)
    {
        if (Network.NetworkConfiguration.Current.Mode == "terminal" || Network.NetworkDatabaseServer.Current != null)
            throw new InvalidOperationException("O servidor e os terminais precisam estar fechados para restaurar.");
        await OperationGate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() => new RestoreEngine(Database.AppFolder).RestorePrepared(selectedPath, cancellationToken), cancellationToken);
        }
        finally { OperationGate.Release(); }
    }
}
