using DataOpinionsExtractor.Application.Interfaces;
using System.Diagnostics;

namespace DataOpinionsExtractor.Application.Services
{
    public class DimensionLoadService
    {
        private readonly IDimensionLoader _dimensionLoader;
        private readonly ILogger<DimensionLoadService> _logger;

        private static readonly string[] Dimensions =
        {
            "DIM_CLIENTE",
            "DIM_PRODUCTO",
            "DIM_FUENTE",
            "DIM_SENTIMIENTO",
            "DIM_FECHA",
            "DIM_CANAL",
            "DIM_EVENTO"
        };

        public DimensionLoadService(
            IDimensionLoader dimensionLoader,
            ILogger<DimensionLoadService> logger)
        {
            _dimensionLoader = dimensionLoader;
            _logger = logger;
        }

        public async Task<bool> RunAsync(CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            int totalRecords = 0;
            int successful = 0;
            int failed = 0;

            _logger.LogInformation(
                "Iniciando carga de dimensiones del Data Warehouse.");

            foreach (var dimension in Dimensions)
            {
                ct.ThrowIfCancellationRequested();

                var sw = Stopwatch.StartNew();

                try
                {
                    int records = await _dimensionLoader
                        .LoadAsync(dimension, ct);

                    sw.Stop();

                    totalRecords += records;
                    successful++;

                    _logger.LogInformation(
                        "[{Dimension}] OK | Registros: {Records} | Tiempo: {Time} ms",
                        dimension,
                        records,
                        sw.ElapsedMilliseconds);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;

                    _logger.LogError(
                        ex,
                        "[{Dimension}] Error durante la carga.",
                        dimension);
                }
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "========== RESUMEN DE CARGA ==========");

            _logger.LogInformation(
                "Dimensiones procesadas: {Total}", Dimensions.Length);

            _logger.LogInformation(
                "Dimensiones exitosas: {Successful}", successful);

            _logger.LogInformation(
                "Dimensiones fallidas: {Failed}", failed);

            _logger.LogInformation(
                "Total registros en dimensiones exitosas: {Records}",
                totalRecords);

            _logger.LogInformation(
                "Tiempo total: {Seconds:F2} segundos",
                stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation(
                "======================================");

            return failed == 0;
        }
    }
}