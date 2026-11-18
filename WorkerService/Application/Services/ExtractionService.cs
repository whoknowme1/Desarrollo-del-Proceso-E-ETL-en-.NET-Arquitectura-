using DataOpinionsExtractor.Application.Interfaces;
using DataOpinionsExtractor.Application.Options;
using DataOpinionsExtractor.Domain;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace DataOpinionsExtractor.Application.Services
{
    public class ExtractionService
    {
        private readonly ILogger<ExtractionService> _logger;
        private readonly IEnumerable<IExtractor> _extractors;
        private readonly IStagingWriter _stagingWriter;
        private readonly ExtractionOptions _options;

        public ExtractionService(
            ILogger<ExtractionService> logger,
            IEnumerable<IExtractor> extractors,
            IStagingWriter stagingWriter,
            IOptions<ExtractionOptions> options)
        {
            _logger = logger;
            _extractors = extractors;
            _stagingWriter = stagingWriter;
            _options = options.Value;
        }

        public async Task<ExtractionResult[]> RunAsync(CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Iniciando extracción paralela de {Count} fuente(s): {Sources}",
                _extractors.Count(),
                string.Join(", ", _extractors.Select(e => e.Name)));

            var results = await Task.WhenAll(
                _extractors.Select(e => RunSourceAsync(e, ct)));

            stopwatch.Stop();

            foreach (var r in results)
            {
                if (r.Success)
                    _logger.LogInformation("Resumen [{Source}]: OK, {Records} registros, {Ms} ms",
                        r.Source, r.Records, (long)r.Elapsed.TotalMilliseconds);
                else
                    _logger.LogError("Resumen [{Source}]: FALLÓ ({Error})", r.Source, r.Error);
            }

            _logger.LogInformation("Extracción finalizada en {ElapsedSeconds:F2} s. Registros totales: {Total}",
                stopwatch.Elapsed.TotalSeconds, results.Sum(r => r.Records));

            await WriteSummaryAsync(results, stopwatch.Elapsed, ct);

            return results;
        }

        private async Task<ExtractionResult> RunSourceAsync(IExtractor extractor, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var total = 0;
            var failed = new ConcurrentBag<string>();

            try
            {
                _logger.LogInformation("[{Source}] Extracción iniciada", extractor.Name);

                var datasets = await extractor.GetDatasetsAsync(ct);

                await Parallel.ForEachAsync(
                    datasets,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, extractor.MaxParallelism),
                        CancellationToken = ct
                    },
                    async (dataset, token) =>
                    {
                        try
                        {
                            var count = await _stagingWriter.WriteAsync(extractor.Name, dataset, token);
                            Interlocked.Add(ref total, count);
                        }
                        catch (Exception ex) when (!token.IsCancellationRequested)
                        {
                            failed.Add(dataset.Name);
                            _logger.LogError(ex, "[{Source}] Error extrayendo {Dataset}",
                                extractor.Name, dataset.Name);
                        }
                    });

                sw.Stop();

                if (failed.IsEmpty)
                    return new ExtractionResult(extractor.Name, total, sw.Elapsed, true);

                return new ExtractionResult(extractor.Name, total, sw.Elapsed, false,
                    $"Fallaron {failed.Count} conjunto(s): {string.Join(", ", failed)}. " +
                    "Los demás se extrajeron; revise los logs.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[{Source}] Error durante la extracción", extractor.Name);
                return new ExtractionResult(extractor.Name, total, sw.Elapsed, false, ex.Message);
            }
        }

        private async Task WriteSummaryAsync(
            ExtractionResult[] results, TimeSpan total, CancellationToken ct)
        {
            Directory.CreateDirectory(_options.OutputFolderPath);

            var summary = new
            {
                ExecutedAtUtc = DateTime.UtcNow,
                TotalSeconds = Math.Round(total.TotalSeconds, 2),
                TotalRecords = results.Sum(r => r.Records),
                Sources = results
            };

            var path = Path.Combine(_options.OutputFolderPath, "extraction_summary.json");

            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
                ct);

            _logger.LogInformation("Resumen de la corrida guardado en {Path}", path);
        }
    }
}