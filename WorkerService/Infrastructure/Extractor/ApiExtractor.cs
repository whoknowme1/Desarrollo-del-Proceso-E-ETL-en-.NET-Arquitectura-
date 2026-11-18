using DataOpinionsExtractor.Application.Interfaces;
using DataOpinionsExtractor.Application.Options;
using DataOpinionsExtractor.Domain;
using Microsoft.Extensions.Options;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DataOpinionsExtractor.Infrastructure.Extractor;

public class ApiExtractor : IExtractor
{
    public const string HttpClientName = "DataOpinionsApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExtractionOptions _options;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(
        IHttpClientFactory httpClientFactory,
        IOptions<ExtractionOptions> options,
        ILogger<ApiExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "API";

    public int MaxParallelism => 1;

    public async Task<IReadOnlyList<ExtractedDataset>> GetDatasetsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiUrl))
            throw new InvalidOperationException("Falta Extraction:ApiUrl en la configuración.");

        var response = await SendWithRetriesAsync(cancellationToken);

        return new[]
        {
            new ExtractedDataset(GetDatasetName(), ReadRowsAsync(response, cancellationToken))
        };
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var maxRetries = Math.Max(0, _options.ApiMaxRetries);
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.ApiTimeoutSeconds));

        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            string reason;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _options.ApiUrl);

                if (!string.IsNullOrWhiteSpace(_options.ApiKey) &&
                    !client.DefaultRequestHeaders.Contains(_options.ApiKeyHeader))
                {
                    request.Headers.TryAddWithoutValidation(_options.ApiKeyHeader, _options.ApiKey);
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);

                var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                    return response;

                var status = response.StatusCode;
                response.Dispose();

                if (!IsTransient(status) || attempt >= maxRetries)
                    throw new HttpRequestException(
                        $"La API respondió {(int)status} ({status}).", null, status);

                reason = $"HTTP {(int)status}";
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null && attempt < maxRetries)
            {
                reason = ex.Message; 
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt < maxRetries)
            {
                reason = "tiempo de espera agotado";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"La API no respondió en {timeout.TotalSeconds:F0} s tras {attempt + 1} intento(s).");
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

            _logger.LogWarning(
                "[API] Intento {Attempt} de {Total} falló ({Reason}). Reintentando en {Delay} s",
                attempt + 1, maxRetries + 1, reason, delay.TotalSeconds);

            await Task.Delay(delay, ct);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout ||
        status == HttpStatusCode.TooManyRequests ||
        (int)status >= 500;

    private string GetDatasetName()
    {
        if (Uri.TryCreate(_options.ApiUrl, UriKind.Absolute, out var uri))
        {
            var last = uri.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(last))
                return last;
        }

        return "api";
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ReadRowsAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
                stream, cancellationToken: cancellationToken))
            {
                var row = new Dictionary<string, object?>();

                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in element.EnumerateObject())
                        row[property.Name] = property.Value;
                }
                else
                {
                    row["value"] = element;
                }

                yield return row;
            }
        }
    }
}
