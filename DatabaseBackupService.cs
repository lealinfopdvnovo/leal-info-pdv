using Microsoft.Data.Sqlite;
using System.IO.Compression;

namespace LealInfoPDV;

public static class DatabaseBackupService
{
    private const long MaxRestoreBytes = 1_073_741_824;
    private static readonly SemaphoreSlim OperationGate = new(1, 1);

    private static async Task<string> CreateZipAsync(CancellationToken cancellationToken = default)
    {
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

    public static async Task RestoreAsync(string selectedPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(selectedPath)) throw new FileNotFoundException("Arquivo de backup não encontrado.", selectedPath);

        await OperationGate.WaitAsync(cancellationToken);
        Directory.CreateDirectory(Database.BackupFolder);
        var stagePath = Path.Combine(Database.AppFolder, $"restore_{Guid.NewGuid():N}.db");
        try
        {
            if (Path.GetExtension(selectedPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                await ExtractDatabaseAsync(selectedPath, stagePath, cancellationToken);
            else
                await CopyCheckedAsync(selectedPath, stagePath, cancellationToken);

            await ValidateDatabaseAsync(stagePath, cancellationToken);

            await CheckpointCurrentDatabaseAsync(cancellationToken);
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            DeleteWalSidecars();

            var safetyCopy = Path.Combine(Database.BackupFolder, $"ANTES_RESTAURACAO_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(Database.DbPath))
                File.Replace(stagePath, Database.DbPath, safetyCopy, true);
            else
                File.Move(stagePath, Database.DbPath);
        }
        finally
        {
            if (File.Exists(stagePath)) File.Delete(stagePath);
            OperationGate.Release();
        }
    }

    private static async Task CheckpointCurrentDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Database.DbPath)) return;
        await using var connection = Database.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExtractDatabaseAsync(string zipPath, string destination, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var candidates = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name) &&
                (Path.GetExtension(e.Name).Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(e.Name).Equals(".sqlite", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (candidates.Count != 1)
            throw new InvalidDataException("O ZIP deve conter exatamente um arquivo de banco de dados.");
        if (candidates[0].Length <= 0 || candidates[0].Length > MaxRestoreBytes)
            throw new InvalidDataException("O tamanho do banco de dados é inválido para restauração.");

        await using var source = candidates[0].Open();
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await source.CopyToAsync(target, cancellationToken);
        await target.FlushAsync(cancellationToken);
    }

    private static async Task CopyCheckedAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var info = new FileInfo(source);
        if (info.Length <= 0 || info.Length > MaxRestoreBytes)
            throw new InvalidDataException("O tamanho do banco de dados é inválido para restauração.");
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);

        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O banco selecionado está corrompido: " + result);
        }

        await using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('settings','products','sales','sale_items','users')";
        if (Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken)) != 5)
            throw new InvalidDataException("O arquivo não pertence ao LEAL INFO PDV ou está incompleto.");
    }

    private static void DeleteWalSidecars()
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = Database.DbPath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
