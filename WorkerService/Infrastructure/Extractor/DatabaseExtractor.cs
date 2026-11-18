using DataOpinionsExtractor.Application.Interfaces;
using DataOpinionsExtractor.Application.Options;
using DataOpinionsExtractor.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace DataOpinionsExtractor.Infrastructure.Extractor;

public class DatabaseExtractor : IExtractor
{
    private readonly IConfiguration _configuration;
    private readonly ExtractionOptions _options;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(
        IOptions<ExtractionOptions> options,
        IConfiguration configuration,
        ILogger<DatabaseExtractor> logger)
    {
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "Database";

    public int MaxParallelism => _options.DatabaseMaxParallelism;

    public async Task<IReadOnlyList<ExtractedDataset>> GetDatasetsAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("SourceDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Falta ConnectionStrings:SourceDatabase (use user-secrets, appsettings.Development.json o una variable de entorno).");

        List<(string Schema, string Name)> existing;

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogInformation("[Database] Conexión abierta a {Database}", connection.Database);
            existing = await GetExistingTablesAsync(connection, cancellationToken);
        }

        var tables = ResolveTables(existing);

        IReadOnlyList<ExtractedDataset> datasets = tables
            .Select(table => new ExtractedDataset(
                $"{table.Schema}.{table.Name}",
                ReadTableAsync(connectionString, table, _options.DatabaseCommandTimeoutSeconds, cancellationToken)))
            .ToList();

        return datasets;
    }

    private static async Task<List<(string Schema, string Name)>> GetExistingTablesAsync(
        SqlConnection connection, CancellationToken ct)
    {
        var tables = new List<(string Schema, string Name)>();

        const string sql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND TABLE_NAME <> 'sysdiagrams'
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            tables.Add((reader.GetString(0), reader.GetString(1)));

        return tables;
    }

    private List<(string Schema, string Name)> ResolveTables(List<(string Schema, string Name)> existing)
    {
        var configuredTables = _options.DatabaseTables;

        if (configuredTables.Count == 0)
        {
            _logger.LogWarning("[Database] No hay tablas configuradas: se extraen todas ({Count}).", existing.Count);
            return existing;
        }

        var result = new List<(string Schema, string Name)>();

        foreach (var configured in configuredTables)
        {
            var parts = configured.Split('.', 2);
            var schema = parts.Length == 2 ? parts[0] : "dbo";
            var name = parts.Length == 2 ? parts[1] : parts[0];

            var match = existing.FirstOrDefault(t =>
                t.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) &&
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (match.Schema is null)
                _logger.LogWarning("[Database] La tabla configurada {Table} no existe y se omite.", configured);
            else
                result.Add(match);
        }

        if (result.Count == 0)
            throw new InvalidOperationException(
                "Ninguna de las tablas configuradas en Extraction:DatabaseTables existe en la BD.");

        return result;
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ReadTableAsync(
        string connectionString,
        (string Schema, string Name) table,
        int commandTimeoutSeconds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var schema = table.Schema.Replace("]", "]]");
        var name = table.Name.Replace("]", "]]");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand($"SELECT * FROM [{schema}].[{name}]", connection)
        {
            CommandTimeout = commandTimeoutSeconds
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);

            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

            yield return row;
        }
    }
}