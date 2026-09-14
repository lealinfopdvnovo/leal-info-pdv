using Microsoft.Data.Sqlite;
using LicAi.Models;

namespace LicAi.Memory;

public sealed class ConversationMemory
{
    private readonly string _connectionString;

    public ConversationMemory(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={databasePath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS conversation_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    role TEXT NOT NULL,
    content TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS long_term_memory (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    summary TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
""";
        command.ExecuteNonQuery();
    }

    public void Add(string role, string content)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO conversation_messages(role, content, created_at) VALUES($role,$content,$createdAt);";
        command.Parameters.AddWithValue("$role", role);
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public long CountMessages()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM conversation_messages;";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public IReadOnlyList<ChatMessage> GetRecent(int limit = 40)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, role, content, created_at FROM conversation_messages ORDER BY id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var items = new List<ChatMessage>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ChatMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3))));
        }
        items.Reverse();
        return items;
    }

    public string GetLongTermSummary()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT summary FROM long_term_memory WHERE id = 1;";
        return command.ExecuteScalar() as string ?? string.Empty;
    }

    public void SaveLongTermSummary(string summary)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO long_term_memory(id, summary, updated_at)
VALUES(1, $summary, $updatedAt)
ON CONFLICT(id) DO UPDATE SET summary = excluded.summary, updated_at = excluded.updated_at;
""";
        command.Parameters.AddWithValue("$summary", summary ?? string.Empty);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
