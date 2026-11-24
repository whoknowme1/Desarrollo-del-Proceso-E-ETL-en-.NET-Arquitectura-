using DataOpinionsExtractor.Application.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DataOpinionsExtractor.Infrastructure.Loading
{
    public class SqlDimensionLoader : IDimensionLoader
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqlDimensionLoader> _logger;

        private static readonly HashSet<string> ValidDimensions = new()
        {
            "DIM_CLIENTE",
            "DIM_PRODUCTO",
            "DIM_FUENTE",
            "DIM_SENTIMIENTO",
            "DIM_FECHA",
            "DIM_CANAL",
            "DIM_EVENTO"
        };

        public SqlDimensionLoader(
            IConfiguration configuration,
            ILogger<SqlDimensionLoader> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<int> LoadAsync(
            string dimensionName,
            CancellationToken cancellationToken)
        {
            if (!ValidDimensions.Contains(dimensionName))
                throw new ArgumentException(
                    $"Dimension no valida: {dimensionName}");

            var connectionString =
                _configuration.GetConnectionString("SourceDatabase");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "No se encontro la conexion a DataOpinions.");

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            var procedureName = $"dw.SP_Cargar_{dimensionName}";

            _logger.LogInformation(
                "Ejecutando procedimiento: {Procedure}",
                procedureName);

            await using var command =
                new SqlCommand(procedureName, connection);

            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;

            await command.ExecuteNonQueryAsync(cancellationToken);

            await using var countCommand = new SqlCommand(
                $"SELECT COUNT(*) FROM [dw].[{dimensionName}]",
                connection);

            var result = await countCommand.ExecuteScalarAsync(
                cancellationToken);

            int totalRecords = Convert.ToInt32(result);

            _logger.LogInformation(
                "Dimension {Dimension} cargada. Total: {Records} registros.",
                dimensionName,
                totalRecords);

            return totalRecords;
        }
    }
}