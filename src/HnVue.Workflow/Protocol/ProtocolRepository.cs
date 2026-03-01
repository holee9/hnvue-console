namespace HnVue.Workflow.Protocol;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQLite-backed protocol repository implementation.
/// SPEC-WORKFLOW-001 FR-WF-08: Protocol selection and validation
/// SPEC-WORKFLOW-001 FR-WF-09: Safety limit enforcement on protocol save
/// SPEC-WORKFLOW-001 NFR-WF-03: 50ms or better lookup performance for 500+ protocols
/// IEC 62304 Class C - Safety-critical protocol storage
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Protocol repository implementation - SQLite-backed protocol storage
/// @MX:REASON: High fan_in from all protocol access points. Safety-critical parameter enforcement.
/// </remarks>
public sealed class ProtocolRepository : IProtocolRepository, IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _lock;
    private readonly ILogger<ProtocolRepository> _logger;
    private readonly DeviceSafetyLimits _safetyLimits;
    private bool _disposed;

    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS protocols (
            protocol_id TEXT PRIMARY KEY,
            body_part TEXT NOT NULL,
            projection TEXT NOT NULL,
            kv REAL NOT NULL,
            ma REAL NOT NULL,
            exposure_time_ms INTEGER NOT NULL,
            aec_mode INTEGER NOT NULL,
            aec_chambers INTEGER NOT NULL,
            focus_size INTEGER NOT NULL,
            grid_used INTEGER NOT NULL,
            device_model TEXT NOT NULL,
            is_active INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS protocol_procedure_codes (
            protocol_id TEXT NOT NULL,
            procedure_code TEXT NOT NULL,
            PRIMARY KEY (protocol_id, procedure_code),
            FOREIGN KEY (protocol_id) REFERENCES protocols(protocol_id) ON DELETE CASCADE
        );

        -- Drop old non-unique index if exists, create unique constraint with case-insensitive collation
        DROP INDEX IF EXISTS idx_protocols_composite;
        CREATE UNIQUE INDEX IF NOT EXISTS idx_protocols_composite_unique ON protocols(body_part COLLATE NOCASE, projection COLLATE NOCASE, device_model COLLATE NOCASE);
        CREATE INDEX IF NOT EXISTS idx_protocols_body_part ON protocols(body_part);
        CREATE INDEX IF NOT EXISTS idx_protocols_active ON protocols(is_active);
        """;

    /// <summary>
    /// Initializes a new instance of the ProtocolRepository.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file.</param>
    /// <param name="safetyLimits">Device safety limits for protocol validation.</param>
    /// <param name="logger">Logger instance.</param>
    public ProtocolRepository(string databasePath, DeviceSafetyLimits safetyLimits, ILogger<ProtocolRepository> logger)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _safetyLimits = safetyLimits ?? throw new ArgumentNullException(nameof(safetyLimits));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lock = new SemaphoreSlim(1, 1);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        InitializeDatabase();
    }

    /// <summary>
    /// Gets a protocol by its identifier.
    /// </summary>
    public async Task<Protocol?> GetProtocolAsync(Guid protocolId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                SELECT protocol_id, body_part, projection, kv, ma, exposure_time_ms,
                       aec_mode, aec_chambers, focus_size, grid_used, device_model,
                       is_active, created_at, updated_at
                FROM protocols
                WHERE protocol_id = @protocol_id AND is_active = 1
                """;

            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@protocol_id", protocolId.ToString("D"));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var protocol = MapReaderToProtocol(reader);
                await LoadProcedureCodesAsync(_connection, protocol, cancellationToken);
                return protocol;
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets all active protocols.
    /// </summary>
    public async Task<Protocol[]> GetAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                SELECT protocol_id, body_part, projection, kv, ma, exposure_time_ms,
                       aec_mode, aec_chambers, focus_size, grid_used, device_model,
                       is_active, created_at, updated_at
                FROM protocols
                WHERE is_active = 1
                ORDER BY body_part, projection, device_model
                """;

            return await QueryProtocolsAsync(sql, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Creates a new protocol with safety limit validation.
    /// SPEC-WORKFLOW-001 FR-WF-09: Enforce safety limits on protocol save
    /// </summary>
    public async Task<bool> CreateAsync(Protocol protocol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Validate against safety limits first
        var validationResult = _safetyLimits.Validate(protocol);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Protocol creation failed safety validation: {Errors}",
                string.Join("; ", validationResult.Errors));
            return false;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string insertSql = """
                INSERT INTO protocols (
                    protocol_id, body_part, projection, kv, ma, exposure_time_ms,
                    aec_mode, aec_chambers, focus_size, grid_used, device_model,
                    is_active, created_at, updated_at
                ) VALUES (
                    @protocol_id, @body_part, @projection, @kv, @ma, @exposure_time_ms,
                    @aec_mode, @aec_chambers, @focus_size, @grid_used, @device_model,
                    @is_active, @created_at, @updated_at
                )
                """;

            protocol.CreatedAt = DateTime.UtcNow;
            protocol.UpdatedAt = DateTime.UtcNow;

            await using var transaction = _connection.BeginTransaction();
            try
            {
                await using var command = _connection.CreateCommand();
                command.CommandText = insertSql;
                command.Transaction = transaction;

                AddProtocolParameters(command, protocol);

                await command.ExecuteNonQueryAsync(cancellationToken);
                await SaveProcedureCodesAsync(_connection, protocol.ProtocolId, protocol.ProcedureCodes, transaction, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Protocol {ProtocolId} created successfully", protocol.ProtocolId);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            _logger.LogWarning("Protocol creation failed: duplicate composite key {CompositeKey}", protocol.CompositeKey);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Updates an existing protocol with safety limit validation.
    /// SPEC-WORKFLOW-001 FR-WF-09: Enforce safety limits on protocol save
    /// </summary>
    public async Task<bool> UpdateAsync(Protocol protocol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Validate against safety limits first
        var validationResult = _safetyLimits.Validate(protocol);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Protocol update failed safety validation: {Errors}",
                string.Join("; ", validationResult.Errors));
            return false;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string updateSql = """
                UPDATE protocols
                SET body_part = @body_part, projection = @projection, kv = @kv, ma = @ma,
                    exposure_time_ms = @exposure_time_ms, aec_mode = @aec_mode,
                    aec_chambers = @aec_chambers, focus_size = @focus_size, grid_used = @grid_used,
                    device_model = @device_model, is_active = @is_active, updated_at = @updated_at
                WHERE protocol_id = @protocol_id
                """;

            protocol.UpdatedAt = DateTime.UtcNow;

            await using var transaction = _connection.BeginTransaction();
            try
            {
                await using var command = _connection.CreateCommand();
                command.CommandText = updateSql;
                command.Transaction = transaction;

                AddProtocolParameters(command, protocol);

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }

                // Update procedure codes
                await using var deleteCmd = _connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM protocol_procedure_codes WHERE protocol_id = @protocol_id";
                deleteCmd.Transaction = transaction;
                deleteCmd.Parameters.AddWithValue("@protocol_id", protocol.ProtocolId.ToString("D"));
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

                await SaveProcedureCodesAsync(_connection, protocol.ProtocolId, protocol.ProcedureCodes, transaction, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Protocol {ProtocolId} updated successfully", protocol.ProtocolId);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes a protocol (soft delete by setting IsActive = false).
    /// </summary>
    public async Task DeleteAsync(Guid protocolId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = "UPDATE protocols SET is_active = 0, updated_at = @updated_at WHERE protocol_id = @protocol_id";

            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@protocol_id", protocolId.ToString("D"));
            command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Protocol {ProtocolId} soft deleted", protocolId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets protocols by body part.
    /// </summary>
    public async Task<Protocol[]> GetProtocolsByBodyPartAsync(string bodyPart, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                SELECT protocol_id, body_part, projection, kv, ma, exposure_time_ms,
                       aec_mode, aec_chambers, focus_size, grid_used, device_model,
                       is_active, created_at, updated_at
                FROM protocols
                WHERE body_part = @body_part COLLATE NOCASE AND is_active = 1
                ORDER BY projection, device_model
                """;

            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@body_part", bodyPart.ToUpperInvariant());

            return await QueryProtocolsAsync(command, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets a protocol by composite key (BodyPart, Projection, DeviceModel).
    /// SPEC-WORKFLOW-001 FR-WF-08: Protocol lookup by composite key
    /// Target: 50ms or better lookup performance for 500+ protocols
    /// </summary>
    public async Task<Protocol?> GetByCompositeKeyAsync(string bodyPart, string projection, string deviceModel, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var sw = Stopwatch.StartNew();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                SELECT protocol_id, body_part, projection, kv, ma, exposure_time_ms,
                       aec_mode, aec_chambers, focus_size, grid_used, device_model,
                       is_active, created_at, updated_at
                FROM protocols
                WHERE body_part = @body_part COLLATE NOCASE
                  AND projection = @projection COLLATE NOCASE
                  AND device_model = @device_model COLLATE NOCASE
                  AND is_active = 1
                LIMIT 1
                """;

            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@body_part", bodyPart.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("@projection", projection.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("@device_model", deviceModel.Trim().ToUpperInvariant());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var protocol = MapReaderToProtocol(reader);
                await LoadProcedureCodesAsync(_connection, protocol, cancellationToken);

                sw.Stop();
                if (sw.ElapsedMilliseconds > 50)
                {
                    _logger.LogWarning(
                        "Composite key lookup took {ElapsedMs}ms (target: 50ms) for key: {BodyPart}|{Projection}|{DeviceModel}",
                        sw.ElapsedMilliseconds, bodyPart, projection, deviceModel);
                }

                return protocol;
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets protocols by procedure code.
    /// SPEC-WORKFLOW-001 FR-WF-08: N-to-1 procedure code to protocol mapping
    /// </summary>
    public async Task<Protocol[]> GetByProcedureCodeAsync(string procedureCode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                SELECT p.protocol_id, p.body_part, p.projection, p.kv, p.ma, p.exposure_time_ms,
                       p.aec_mode, p.aec_chambers, p.focus_size, p.grid_used, p.device_model,
                       p.is_active, p.created_at, p.updated_at
                FROM protocols p
                INNER JOIN protocol_procedure_codes pc ON p.protocol_id = pc.protocol_id
                WHERE pc.procedure_code = @procedure_code COLLATE NOCASE AND p.is_active = 1
                ORDER BY p.body_part, p.projection, p.device_model
                """;

            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@procedure_code", procedureCode.Trim().ToUpperInvariant());

            return await QueryProtocolsAsync(command, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void InitializeDatabase()
    {
        _connection.ExecuteNonQuery(CreateTablesSql);

        // Enable WAL mode for better concurrent access
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();

        _logger.LogInformation("Protocol repository database initialized at {Path}", _databasePath);
    }

    private async Task<Protocol[]> QueryProtocolsAsync(string sql, CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return await QueryProtocolsAsync(command, cancellationToken);
    }

    private async Task<Protocol[]> QueryProtocolsAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var protocols = new List<Protocol>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var protocol = MapReaderToProtocol(reader);
            protocols.Add(protocol);
        }

        // Load procedure codes for all protocols
        foreach (var protocol in protocols)
        {
            await LoadProcedureCodesAsync(_connection, protocol, cancellationToken);
        }

        return protocols.ToArray();
    }

    private static Protocol MapReaderToProtocol(IDataRecord reader)
    {
        return new Protocol
        {
            ProtocolId = Guid.Parse(reader.GetString(reader.GetOrdinal("protocol_id"))),
            BodyPart = reader.GetString(reader.GetOrdinal("body_part")),
            Projection = reader.GetString(reader.GetOrdinal("projection")),
            Kv = reader.GetDecimal(reader.GetOrdinal("kv")),
            Ma = reader.GetDecimal(reader.GetOrdinal("ma")),
            ExposureTimeMs = reader.GetInt32(reader.GetOrdinal("exposure_time_ms")),
            AecMode = (AecMode)reader.GetInt32(reader.GetOrdinal("aec_mode")),
            AecChambers = (byte)reader.GetInt32(reader.GetOrdinal("aec_chambers")),
            FocusSize = (FocusSize)reader.GetInt32(reader.GetOrdinal("focus_size")),
            GridUsed = reader.GetBoolean(reader.GetOrdinal("grid_used")),
            DeviceModel = reader.GetString(reader.GetOrdinal("device_model")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")), System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at")), System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static async Task LoadProcedureCodesAsync(SqliteConnection connection, Protocol protocol, CancellationToken cancellationToken)
    {
        const string sql = "SELECT procedure_code FROM protocol_procedure_codes WHERE protocol_id = @protocol_id";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@protocol_id", protocol.ProtocolId.ToString("D"));

        var codes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            codes.Add(reader.GetString(0));
        }

        protocol.ProcedureCodes = codes.ToArray();
    }

    private static void AddProtocolParameters(SqliteCommand command, Protocol protocol)
    {
        command.Parameters.AddWithValue("@protocol_id", protocol.ProtocolId.ToString("D"));
        command.Parameters.AddWithValue("@body_part", protocol.BodyPart);
        command.Parameters.AddWithValue("@projection", protocol.Projection);
        command.Parameters.AddWithValue("@kv", protocol.Kv);
        command.Parameters.AddWithValue("@ma", protocol.Ma);
        command.Parameters.AddWithValue("@exposure_time_ms", protocol.ExposureTimeMs);
        command.Parameters.AddWithValue("@aec_mode", (int)protocol.AecMode);
        command.Parameters.AddWithValue("@aec_chambers", protocol.AecChambers);
        command.Parameters.AddWithValue("@focus_size", (int)protocol.FocusSize);
        command.Parameters.AddWithValue("@grid_used", protocol.GridUsed ? 1 : 0);
        command.Parameters.AddWithValue("@device_model", protocol.DeviceModel);
        command.Parameters.AddWithValue("@is_active", protocol.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@created_at", protocol.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@updated_at", protocol.UpdatedAt.ToString("o"));
    }

    private static async Task SaveProcedureCodesAsync(
        SqliteConnection connection,
        Guid protocolId,
        string[] procedureCodes,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (procedureCodes.Length == 0)
        {
            return;
        }

        const string sql = "INSERT INTO protocol_procedure_codes (protocol_id, procedure_code) VALUES (@protocol_id, @procedure_code)";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.Parameters.AddWithValue("@protocol_id", protocolId.ToString("D"));

        var codeParam = command.CreateParameter();
        codeParam.ParameterName = "@procedure_code";
        command.Parameters.Add(codeParam);

        foreach (var code in procedureCodes)
        {
            codeParam.Value = code.Trim().ToUpperInvariant();
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProtocolRepository));
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

file static class SqliteExtensions
{
    public static void ExecuteNonQuery(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
