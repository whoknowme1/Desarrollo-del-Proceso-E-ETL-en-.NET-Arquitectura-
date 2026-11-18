using CsvHelper;
using CsvHelper.Configuration;
using DataOpinionsExtractor.Application.Interfaces;
using DataOpinionsExtractor.Application.Options;
using DataOpinionsExtractor.Domain;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace DataOpinionsExtractor.Infrastructure.Extractor;

public class CsvExtractor : IExtractor
{
    private const int MaxRowWarningsPerFile = 5;

    private readonly ExtractionOptions _options;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<ExtractionOptions> options, ILogger<CsvExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "CSV";

    public int MaxParallelism => _options.CsvMaxParallelism;

    public Task<IReadOnlyList<ExtractedDataset>> GetDatasetsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_options.CsvFolderPath))
            throw new DirectoryNotFoundException($"No existe la carpeta de CSV: {_options.CsvFolderPath}");

        var files = Directory.GetFiles(_options.CsvFolderPath, "*.csv");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("[CSV] {Count} archivo(s) encontrados en {Path}", files.Length, _options.CsvFolderPath);

        if (files.Length == 0)
            _logger.LogWarning("[CSV] No hay archivos .csv en {Path}", _options.CsvFolderPath);

        IReadOnlyList<ExtractedDataset> datasets = files
            .Select(file => new ExtractedDataset(
                Path.GetFileNameWithoutExtension(file),
                ReadRowsAsync(file, cancellationToken)))
            .ToList();

        return Task.FromResult(datasets);
    }

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ReadRowsAsync(
        string file,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(file);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = args => _logger.LogWarning(
                "[CSV] {File}: dato mal formado omitido: {Field}", fileName, args.Field)
        };

        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync())
            throw new InvalidDataException($"El archivo {fileName} está vacío.");

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        ValidateHeaders(fileName, headers);

        var skippedEmpty = 0;
        var mismatchWarnings = 0;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fieldCount = csv.Parser.Count;

            if (IsEmptyRow(csv, fieldCount))
            {
                skippedEmpty++;
                continue;
            }

            if (fieldCount != headers.Length && mismatchWarnings < MaxRowWarningsPerFile)
            {
                mismatchWarnings++;
                _logger.LogWarning(
                    "[CSV] {File}: la línea {Line} tiene {Found} columnas y se esperaban {Expected}.",
                    fileName, csv.Parser.Row, fieldCount, headers.Length);
            }

            var row = new Dictionary<string, object?>(headers.Length);

            for (var i = 0; i < headers.Length; i++)
                row[headers[i]] = csv.GetField(i);

            yield return row;
        }

        if (skippedEmpty > 0)
            _logger.LogWarning("[CSV] {File}: {Count} fila(s) vacías omitidas.", fileName, skippedEmpty);
    }

    private static void ValidateHeaders(string fileName, string[] headers)
    {
        if (headers.Length == 0)
            throw new InvalidDataException($"El archivo {fileName} no tiene encabezados.");

        if (headers.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"El archivo {fileName} tiene una columna con encabezado vacío.");

        var duplicated = headers
            .GroupBy(h => h)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicated.Count > 0)
            throw new InvalidDataException(
                $"El archivo {fileName} tiene encabezados duplicados: {string.Join(", ", duplicated)}.");
    }

    private static bool IsEmptyRow(CsvReader csv, int fieldCount)
    {
        for (var i = 0; i < fieldCount; i++)
        {
            if (!string.IsNullOrWhiteSpace(csv.GetField(i)))
                return false;
        }

        return true;
    }
}