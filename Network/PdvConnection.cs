using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace LealInfoPDV.Network;
// A mesma API mantém o modo local; em terminais as operações são executadas
// no servidor, com uma conexão SQLite por sessão e transações reais.
public sealed class PdvConnection : IDisposable, IAsyncDisposable
{
    internal SqliteConnection? Local { get; }
    private NetworkDatabaseClient? remote;
    public PdvConnection(string connectionString, bool localOnly = false)
    {
        if (localOnly || NetworkConfiguration.Current.Mode != "terminal") Local = new(connectionString);
    }
    public void Open()
    {
        if (Local != null) Local.Open(); else remote = new(NetworkConfiguration.Current);
    }
    public PdvCommand CreateCommand() => new(this);
    internal NetworkResponse Send(NetworkRequest request) => (remote ?? throw new InvalidOperationException("Conexão não está aberta.")).Send(request);
    public PdvTransaction BeginTransaction() => new(this);
    public void BackupDatabase(SqliteConnection destination)
    {
        if (Local == null) throw new InvalidOperationException("Faça o backup no computador servidor, que contém os dados compartilhados.");
        Local.BackupDatabase(destination);
    }
    public void Dispose() { remote?.Dispose(); Local?.Dispose(); }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
public sealed class PdvTransaction : IDisposable
{
    internal SqliteTransaction? Local { get; }
    private readonly PdvConnection connection;
    private bool ended;
    internal PdvTransaction(PdvConnection connection)
    {
        this.connection = connection;
        if (connection.Local != null) Local = connection.Local.BeginTransaction(); else connection.Send(new() { Operation = "begin" });
    }
    public void Commit()
    {
        if (ended) throw new InvalidOperationException("Transação encerrada.");
        if (Local != null) Local.Commit(); else connection.Send(new() { Operation = "commit" });
        ended = true;
    }
    public void Rollback()
    {
        if (ended) return;
        if (Local != null) Local.Rollback(); else connection.Send(new() { Operation = "rollback" });
        ended = true;
    }
    public void Dispose()
    {
        if (Local != null) Local.Dispose();
        else if (!ended) { try { connection.Send(new() { Operation = "rollback" }); } catch (IOException) { } }
        ended = true;
    }
}
public sealed class PdvCommand : IDisposable, IAsyncDisposable
{
    private readonly PdvConnection connection;
    private readonly SqliteCommand command;
    public PdvCommand(PdvConnection connection) { this.connection = connection; command = connection.Local?.CreateCommand() ?? new SqliteCommand(); }
    public string CommandText { get => command.CommandText; set => command.CommandText = value; }
    public SqliteParameterCollection Parameters => command.Parameters;
    private PdvTransaction? transaction;
    public PdvTransaction? Transaction { get => transaction; set { transaction = value; command.Transaction = value?.Local; } }
    private NetworkRequest Request(string operation) => new() { Operation = operation, Sql = CommandText,
        Parameters = Parameters.Cast<SqliteParameter>().ToDictionary(p => p.ParameterName, p => WireValue.From(p.Value)) };
    public int ExecuteNonQuery() => connection.Local != null ? command.ExecuteNonQuery() : connection.Send(Request("nonquery")).Affected;
    public object? ExecuteScalar() => connection.Local != null ? command.ExecuteScalar() : connection.Send(Request("scalar")).Scalar?.ToObject();
    public DbDataReader ExecuteReader() => connection.Local != null ? command.ExecuteReader() : new NetworkDataReader(connection.Send(Request("reader")));
    public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return connection.Local != null ? command.ExecuteNonQueryAsync(cancellationToken) : Task.FromResult(ExecuteNonQuery()); }
    public void Dispose() => command.Dispose();
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
