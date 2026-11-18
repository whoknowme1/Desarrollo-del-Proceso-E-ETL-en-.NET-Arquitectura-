using DataOpinionsExtractor.Application.Services;

namespace DataOpinionsExtractor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ExtractionService _extractionService;
    private readonly IHostApplicationLifetime _lifetime;

    public Worker( ILogger<Worker> logger,
        ExtractionService extractionService,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _extractionService = extractionService;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var results = await _extractionService.RunAsync(stoppingToken);

            Environment.ExitCode = results.Any(r => !r.Success) ? 1 : 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Extracción cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error no controlado en el proceso de extracción.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}