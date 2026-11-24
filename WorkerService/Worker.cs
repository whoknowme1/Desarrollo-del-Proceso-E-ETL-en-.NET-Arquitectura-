using DataOpinionsExtractor.Application.Services;

namespace DataOpinionsExtractor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ExtractionService _extractionService;
    private readonly DimensionLoadService _dimensionLoadService;
    private readonly IHostApplicationLifetime _lifetime;

    public Worker(ILogger<Worker> logger,
        ExtractionService extractionService,
        DimensionLoadService dimensionLoadService,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _extractionService = extractionService;
        _dimensionLoadService = dimensionLoadService;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(
                "========== FASE 1: EXTRACCION ==========");

            var results = await _extractionService
                .RunAsync(stoppingToken);

            if (results.Any(r => !r.Success))
            {
                _logger.LogError(
                    "La extraccion contiene errores. Se cancela la carga.");

                Environment.ExitCode = 1;
                return;
            }

            _logger.LogInformation(
                "========== FASE 2: CARGA DE DIMENSIONES ==========");

            bool loadSuccess = await _dimensionLoadService
                .RunAsync(stoppingToken);

            Environment.ExitCode = loadSuccess ? 0 : 1;

            if (loadSuccess)
            {
                _logger.LogInformation(
                    "Proceso ETL finalizado correctamente.");
            }
            else
            {
                _logger.LogError(
                    "El proceso ETL finalizo con errores.");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Proceso ETL cancelado.");
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex, "Error no controlado en el proceso ETL.");

            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}