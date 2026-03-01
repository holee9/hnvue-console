namespace HnVue.Workflow.Journal;

using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using HnVue.Workflow.StateMachine;

/// <summary>
/// SQLite-backed workflow journal implementation.
///
/// SPEC-WORKFLOW-001 NFR-WF-01: Atomic, Logged State Transitions
/// SPEC-WORKFLOW-001 NFR-WF-02: Crash Recovery
///
/// Provides durable, write-ahead logging for all state transitions.
/// Journal entries are stored in JSON format for extensibility.
/// </summary>
// @MX:ANCHOR: SQLite journal persistence for crash recovery
// @MX:REASON: High fan_in - all state transitions are journaled. Critical for crash recovery and audit trail.
public class SqliteWorkflowJournal : IWorkflowJournal, System.IAsyncDisposable
{
    private readonly string _journalPath;
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _lock;
    private bool _disposed;

    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS journal_entries (
            transition_id TEXT PRIMARY KEY,
            timestamp TEXT NOT NULL,
            from_state INTEGER NOT NULL,
            to_state INTEGER NOT NULL,
            trigger TEXT NOT NULL,
            guard_results TEXT NOT NULL,
            operator_id TEXT NOT NULL,
            study_instance_uid TEXT,
            metadata TEXT NOT NULL,
            category INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_journal_timestamp ON journal_entries(timestamp);
        CREATE INDEX IF NOT EXISTS idx_journal_study ON journal_entries(study_instance_uid);
        """;

    public SqliteWorkflowJournal(string journalPath)
    {
        _journalPath = journalPath ?? throw new ArgumentNullException(nameof(journalPath));
        _lock = new SemaphoreSlim(1, 1);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = journalPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        // Initialize database is called in constructor
        InitializeDatabase();
    }

    /// <summary>
    /// Initializes the journal database and tables.
    /// This method is called implicitly by the constructor.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Database is already initialized in constructor
        // This method exists for interface compatibility
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a journal entry atomically.
    /// </summary>
    public async Task WriteEntryAsync(WorkflowJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string insertSql = """
                INSERT INTO journal_entries (
                    transition_id, timestamp, from_state, to_state, trigger,
                    guard_results, operator_id, study_instance_uid, metadata, category
                ) VALUES (
                    @transition_id, @timestamp, @from_state, @to_state, @trigger,
                    @guard_results, @operator_id, @study_instance_uid, @metadata, @category
                )
                """;

            await using var command = _connection.CreateCommand();
            command.CommandText = insertSql;

            command.Parameters.AddWithValue("@transition_id", entry.TransitionId.ToString("D"));
            command.Parameters.AddWithValue("@timestamp", entry.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@from_state", (int)entry.FromState);
            command.Parameters.AddWithValue("@to_state", (int)entry.ToState);
            command.Parameters.AddWithValue("@trigger", entry.Trigger);
            command.Parameters.AddWithValue("@guard_results", JsonSerializer.Serialize(entry.GuardResults));
            command.Parameters.AddWithValue("@operator_id", entry.OperatorId);
            command.Parameters.AddWithValue("@study_instance_uid", entry.StudyInstanceUID ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(entry.Metadata));
            command.Parameters.AddWithValue("@category", (int)entry.Category);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reads all journal entries ordered by timestamp.
    /// </summary>
    public async Task<WorkflowJournalEntry[]> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string selectSql = """
                SELECT transition_id, timestamp, from_state, to_state, trigger,
                       guard_results, operator_id, study_instance_uid, metadata, category
                FROM journal_entries
                ORDER BY timestamp ASC
                """;

            var entries = new List<WorkflowJournalEntry>();

            await using var command = _connection.CreateCommand();
            command.CommandText = selectSql;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapReaderToEntry(reader));
            }

            return entries.ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reads the most recent journal entry.
    /// </summary>
    public async Task<WorkflowJournalEntry?> ReadLastEntryAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string selectSql = """
                SELECT transition_id, timestamp, from_state, to_state, trigger,
                       guard_results, operator_id, study_instance_uid, metadata, category
                FROM journal_entries
                ORDER BY timestamp DESC
                LIMIT 1
                """;

            await using var command = _connection.CreateCommand();
            command.CommandText = selectSql;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapReaderToEntry(reader);
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Alias for ReadLastEntryAsync for backward compatibility.
    /// </summary>
    public Task<WorkflowJournalEntry?> GetLastEntryAsync(CancellationToken cancellationToken = default)
    {
        return ReadLastEntryAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if the journal has any entries.
    /// </summary>
    public async Task<bool> HasEntriesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string countSql = "SELECT COUNT(*) FROM journal_entries";

            await using var command = _connection.CreateCommand();
            command.CommandText = countSql;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears all journal entries.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string deleteSql = "DELETE FROM journal_entries";

            await using var command = _connection.CreateCommand();
            command.CommandText = deleteSql;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void InitializeDatabase()
    {
        _connection.ExecuteNonQuery(CreateTablesSql);
    }

    private static WorkflowJournalEntry MapReaderToEntry(IDataRecord reader)
    {
        var guardResultsJson = reader.GetString(reader.GetOrdinal("guard_results"));
        var guardResults = string.IsNullOrEmpty(guardResultsJson)
            ? Array.Empty<GuardResult>()
            : JsonSerializer.Deserialize<GuardResult[]>(guardResultsJson)
              ?? Array.Empty<GuardResult>();

        var metadataJson = reader.GetString(reader.GetOrdinal("metadata"));
        var metadata = string.IsNullOrEmpty(metadataJson)
            ? new Dictionary<string, object>()
            : DeserializeMetadata(metadataJson);

        return new WorkflowJournalEntry
        {
            TransitionId = Guid.Parse(reader.GetString(reader.GetOrdinal("transition_id"))),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp")), System.Globalization.CultureInfo.InvariantCulture),
            FromState = (WorkflowState)reader.GetInt32(reader.GetOrdinal("from_state")),
            ToState = (WorkflowState)reader.GetInt32(reader.GetOrdinal("to_state")),
            Trigger = reader.GetString(reader.GetOrdinal("trigger")),
            GuardResults = guardResults,
            OperatorId = reader.GetString(reader.GetOrdinal("operator_id")),
            StudyInstanceUID = reader.IsDBNull(reader.GetOrdinal("study_instance_uid"))
                ? null
                : reader.GetString(reader.GetOrdinal("study_instance_uid")),
            Metadata = metadata,
            Category = (LogCategory)reader.GetInt32(reader.GetOrdinal("category"))
        };
    }

    private static Dictionary<string, object> DeserializeMetadata(string json)
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new MetadataConverter() }
        };
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, options) ?? new();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SqliteWorkflowJournal));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            await _connection.DisposeAsync();
            _disposed = true;
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}

/// <summary>
/// JSON converter for metadata that properly handles primitive types.
/// </summary>
file class MetadataConverter : System.Text.Json.Serialization.JsonConverter<object>
{
    public override object? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            System.Text.Json.JsonTokenType.String => reader.GetString()!,
            System.Text.Json.JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
            System.Text.Json.JsonTokenType.True => true,
            System.Text.Json.JsonTokenType.False => false,
            System.Text.Json.JsonTokenType.Null => null,
            _ => throw new System.Text.Json.JsonException($"Unexpected token type: {reader.TokenType}")
        };
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}

file static class SqliteExtensions
{
    public static void ExecuteNonQuery(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
