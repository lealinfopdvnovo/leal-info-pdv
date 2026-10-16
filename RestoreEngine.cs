using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoPDV;

// The UI and the Windows fault tests use this same engine. Paths supplied by the
// application always point to its private data directory, never the program files.
public sealed class RestoreEngine
{
    private readonly string folder;
    private readonly Action<string>? checkpoint;
    private readonly Func<long>? freeBytes;
    private string Live => Path.Combine(folder, "lealinfo.db");
    private string Journal => Path.Combine(folder, "restore-journal.json");
    private const long Limit = 1_073_741_824;
    private sealed record State(string Safety, string Hash, bool Committed);

    public RestoreEngine(string dataFolder, Action<string>? checkpoint = null, Func<long>? freeBytes = null)
    {
        folder = Path.GetFullPath(dataFolder);
        this.checkpoint = checkpoint;
        this.freeBytes = freeBytes;
    }

    public IDisposable AcquireOwnership()
    {
        Directory.CreateDirectory(folder);
        try { return new FileStream(Path.Combine(folder, "pdv-process.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException("Outro PDV ou processo de manutenção está aberto neste computador. Feche-o antes de continuar.", ex); }
    }

    private void RequireSpace(long source)
    {
        var live = File.Exists(Live) ? new FileInfo(Live).Length : 0;
        var wal = File.Exists(Live + "-wal") ? new FileInfo(Live + "-wal").Length : 0;
        var available = freeBytes?.Invoke() ?? new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace;
        if (available < checked(4 * source + 3 * live + 2 * wal + 64L * 1024 * 1024))
            throw new IOException("Espaço livre insuficiente para restaurar e preservar a recuperação. Nenhum dado foi substituído.");
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 3 }.ToString());
        try { c.Open(); return c; } catch { c.Dispose(); throw; }
    }

    public static void Validate(string path)
    {
        using var c = Open(path, SqliteOpenMode.ReadOnly);
        using var cmd = c.CreateCommand(); cmd.CommandTimeout = 15;
        cmd.CommandText = "PRAGMA integrity_check";
        using (var rows = cmd.ExecuteReader())
        {
            if (!rows.Read() || rows.GetString(0) != "ok" || rows.Read())
                throw new InvalidDataException("O banco de dados está corrompido.");
        }
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var rows = cmd.ExecuteReader())
            if (rows.Read()) throw new InvalidDataException("O banco contém vínculos de dados inválidos.");
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('settings','products','sales','sale_items','users')";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 5)
            throw new InvalidDataException("O arquivo não é um backup completo do LEAL INFO PDV.");
        cmd.CommandText = "SELECT key,value FROM settings LIMIT 0"; cmd.ExecuteNonQuery();
    }

    private static void Flush(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        file.Flush(true);
    }

    private static void Snapshot(string source, string target)
    {
        using (var input = Open(source, SqliteOpenMode.ReadOnly))
        using (var output = Open(target, SqliteOpenMode.ReadWriteCreate))
        {
            input.BackupDatabase(output);
            using var cmd = output.CreateCommand(); cmd.CommandText = "PRAGMA journal_mode=DELETE"; cmd.ExecuteScalar();
        }
        Flush(target);
    }

    public string Prepare(string selected, CancellationToken token = default)
    {
        Directory.CreateDirectory(folder);
        selected = Path.GetFullPath(selected);
        if (selected.Equals(Live, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selecione um backup, não o banco operacional.");
        if (!File.Exists(selected)) throw new FileNotFoundException("Backup não encontrado.");
        var output = Path.Combine(folder, $"restore_pending_{Guid.NewGuid():N}.db");
        var extracted = output + ".extract";
        try
        {
            token.ThrowIfCancellationRequested();
            string source = selected;
            long size;
            if (Path.GetExtension(selected).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(selected);
                var entries = zip.Entries.Where(x => x.Name.Length > 0).ToArray();
                if (entries.Any(x => x.Name.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) || x.Name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("O ZIP contém arquivos SQLite abertos. Gere um backup pelo PDV.");
                var databases = entries.Where(x => Path.GetExtension(x.Name).ToLowerInvariant() is ".db" or ".sqlite").ToArray();
                if (databases.Length != 1) throw new InvalidDataException("O ZIP deve conter exatamente um banco de dados.");
                size = databases[0].Length;
                if (size <= 0 || size > Limit) throw new InvalidDataException("Tamanho do backup inválido.");
                RequireSpace(size);
                using var input = databases[0].Open();
                using (var target = new FileStream(extracted, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920]; long written = 0; int count;
                    while ((count = input.Read(buffer)) > 0)
                    {
                        token.ThrowIfCancellationRequested(); written += count;
                        if (written > size || written > Limit) throw new InvalidDataException("Tamanho descompactado inválido.");
                        target.Write(buffer, 0, count);
                    }
                    if (written != size) throw new InvalidDataException("Backup incompleto.");
                    target.Flush(true);
                }
                source = extracted;
            }
            else
            {
                size = new FileInfo(selected).Length;
                if (size <= 0 || size > Limit) throw new InvalidDataException("Tamanho do backup inválido.");
                RequireSpace(size);
            }
            Validate(source);
            Snapshot(source, output); // SQLite backup API includes committed WAL pages.
            if (new FileInfo(output).Length > Limit) throw new InvalidDataException("O banco excede o limite seguro de restauração.");
            token.ThrowIfCancellationRequested();
            Validate(output);
            checkpoint?.Invoke("staged");
            return output;
        }
        catch { Delete(output); throw; }
        finally { Delete(extracted); }
    }

    private static void Delete(string path) { if (File.Exists(path)) File.Delete(path); }
    private static string Hash(string path)
    { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private void Save(State state)
    {
        var temp = Journal + ".tmp";
        using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(f, state); f.Flush(true); }
        File.Move(temp, Journal, true);
    }

    // Call only while holding ownership and with the network listener stopped.
    public string RestorePrepared(string staged, CancellationToken token = default)
    {
        staged = Path.GetFullPath(staged);
        if (Path.GetDirectoryName(staged) != folder || !Path.GetFileName(staged).StartsWith("restore_pending_"))
            throw new InvalidDataException("Solicitação de restauração inválida.");
        Recover();
        Validate(staged);
        RequireSpace(new FileInfo(staged).Length);
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke("before-checkpoint");
        // BEGIN EXCLUSIVE in DELETE mode detects open readers as well as writers.
        // Query the checkpoint result: ExecuteNonQuery alone ignores SQLITE_BUSY.
        using (var c = Open(Live, SqliteOpenMode.ReadWrite))
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            using (var r = cmd.ExecuteReader())
                if (!r.Read() || r.GetInt32(0) != 0) throw new IOException("Há conexões SQLite em uso. Feche os terminais e tente novamente.");
            cmd.CommandText = "PRAGMA journal_mode=DELETE";
            if (Convert.ToString(cmd.ExecuteScalar())?.ToLowerInvariant() != "delete")
                throw new IOException("Não foi possível encerrar o modo WAL com segurança.");
            cmd.CommandText = "BEGIN EXCLUSIVE; ROLLBACK;"; cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        // Sharing excludes any SQLite reader/writer while the safety copy is made.
        using (new FileStream(Live, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = Live + suffix;
            if (!File.Exists(path)) continue;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            if (suffix == "-wal" && new FileInfo(path).Length > 0) throw new IOException("WAL ainda contém dados. Restauração cancelada.");
            File.Delete(path);
        }
        var backups = Path.Combine(folder, "Backups"); Directory.CreateDirectory(backups);
        var safety = Path.Combine(backups, $"ANTES_RESTAURACAO_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db");
        Snapshot(Live, safety); Validate(safety);
        // Keep current installation settings, including identity and SMTP. Financial
        // tables are restored exclusively from the selected backup.
        using (var original = Open(safety, SqliteOpenMode.ReadOnly))
        using (var target = Open(staged, SqliteOpenMode.ReadWrite))
        using (var transaction = target.BeginTransaction())
        {
            using var read = original.CreateCommand(); read.CommandText = "SELECT key,value FROM settings";
            using var rows = read.ExecuteReader();
            using var write = target.CreateCommand(); write.Transaction = transaction;
            write.CommandText = "DELETE FROM settings"; write.ExecuteNonQuery();
            write.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            while (rows.Read())
            {
                write.Parameters.Clear(); write.Parameters.AddWithValue("$k", rows.GetValue(0)); write.Parameters.AddWithValue("$v", rows.GetValue(1)); write.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        Validate(staged); Flush(staged);
        var state = new State(safety, Hash(safety), false);
        Save(state);
        try
        {
            checkpoint?.Invoke("before-replace"); token.ThrowIfCancellationRequested();
            using (new FileStream(Live, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                File.Replace(staged, Live, null, true);
            checkpoint?.Invoke("after-replace");
            Validate(Live); Flush(Live);
            checkpoint?.Invoke("validated");
            Save(state with { Committed = true });
            Delete(Journal);
            return safety;
        }
        catch { Recover(); throw; }
    }

    public bool Recover()
    {
        if (!File.Exists(Journal)) return false;
        var state = JsonSerializer.Deserialize<State>(File.ReadAllText(Journal)) ?? throw new InvalidDataException("Registro de recuperação inválido.");
        var safety = Path.GetFullPath(state.Safety);
        if (Path.GetDirectoryName(safety) != Path.Combine(folder, "Backups") || !Path.GetFileName(safety).StartsWith("ANTES_RESTAURACAO_") || Hash(safety) != state.Hash)
            throw new InvalidDataException("A cópia de recuperação não pôde ser verificada. O PDV permanecerá fechado.");
        if (state.Committed) { Validate(Live); Delete(Journal); return false; }
        Validate(safety);
        var recovery = Path.Combine(folder, "restore_recovery.db");
        File.Copy(safety, recovery, true); Flush(recovery);
        // Sidecars cannot be discarded when another process holds a connection.
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var p = Live + suffix;
            if (File.Exists(p)) { using (new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } File.Delete(p); }
        }
        if (File.Exists(Live))
        {
            using (new FileStream(Live, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete)) File.Replace(recovery, Live, null, true);
        }
        else File.Move(recovery, Live);
        Validate(Live); Delete(Journal);
        checkpoint?.Invoke("recovered");
        return true;
    }
}
