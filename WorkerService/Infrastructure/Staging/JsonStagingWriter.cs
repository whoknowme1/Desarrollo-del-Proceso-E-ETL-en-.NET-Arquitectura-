using DataOpinionsExtractor.Application.Interfaces;
using DataOpinionsExtractor.Application.Options;
using DataOpinionsExtractor.Domain;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DataOpinionsExtractor.Infrastructure.Staging;

public class JsonStagingWriter : IStagingWriter
{
    private readonly string _outputFolderPath;
    private readonly ILogger<JsonStagingWriter> _logger;

    public JsonStagingWriter(IOptions<ExtractionOptions> options, ILogger<JsonStagingWriter> logger)
    {
        _outputFolderPath = options.Value.OutputFolderPath;
        _logger = logger;
    }

    public async Task<int> WriteAsync( string sourceName, ExtractedDataset dataset, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_outputFolderPath, sourceName);
        Directory.CreateDirectory(folder);

        var finalPath = Path.Combine(folder, $"{ToSafeFileName(dataset.Name)}.json");
        var tempPath = finalPath + ".tmp";

        var count = 0;

        try
        {
            await using (var stream = File.Create(tempPath))
            await using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            }))
            {
                writer.WriteStartArray();

                await foreach (var row in dataset.Rows.WithCancellation(cancellationToken))
                {
                    writer.WriteStartObject();

                    foreach (var (column, value) in row)
                    {
                        writer.WritePropertyName(column);
                        JsonSerializer.Serialize<object?>(writer, value);
                    }

                    writer.WriteEndObject();
                    count++;
                }

                writer.WriteEndArray();
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }

        _logger.LogInformation("[{Source}] {Dataset}: {Count} registros -> {Output}",
            sourceName, dataset.Name, count, finalPath);

        return count;
    }

    private static string ToSafeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch {  }
    }
}