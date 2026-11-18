using DataOpinionsExtractor;
using DataOpinionsExtractor.Application.Interfaces;
using DataOpinionsExtractor.Application.Options;
using DataOpinionsExtractor.Application.Services;
using DataOpinionsExtractor.Infrastructure.Extractor;
using Microsoft.Extensions.Options;
using DataOpinionsExtractor.Infrastructure.Staging;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ExtractionOptions>()
    .Bind(builder.Configuration.GetSection(ExtractionOptions.SectionName))
    .Validate(o =>
            !string.IsNullOrWhiteSpace(o.CsvFolderPath) &&
            !string.IsNullOrWhiteSpace(o.OutputFolderPath) &&
            Uri.TryCreate(o.ApiUrl, UriKind.Absolute, out _),
        "Configuración inválida: revise Extraction:CsvFolderPath, Extraction:OutputFolderPath y Extraction:ApiUrl.")
    .ValidateOnStart();

builder.Services.AddHttpClient(ApiExtractor.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ExtractionOptions>>().Value;

    client.Timeout = TimeSpan.FromSeconds(options.ApiTimeoutSeconds);

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
        client.DefaultRequestHeaders.Add(options.ApiKeyHeader, options.ApiKey);
});

builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddSingleton<IExtractor, ApiExtractor>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<ExtractionService>();
builder.Services.AddSingleton<IStagingWriter, JsonStagingWriter>();

var host = builder.Build();

host.Run();
