using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["ApiSecurity:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException(
        "Falta ApiSecurity:ApiKey (use appsettings.Development.json, user-secrets o una variable de entorno).");

var csvRelativePath = builder.Configuration["SocialComments:CsvPath"] ?? "SeedData/social_comments.csv";
var csvPath = Path.Combine(builder.Environment.ContentRootPath, csvRelativePath);

// Los datos se cargan una sola vez al arrancar.
builder.Services.AddSingleton(new SocialCommentsStore(csvPath));

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var provided = context.Request.Headers["X-Api-Key"].ToString();

        if (!KeyMatches(provided, apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key invalida o ausente." });
            return;
        }
    }

    await next();
});

app.MapGet("/api/social-comments", (
    SocialCommentsStore store, HttpResponse response, int? page, int? pageSize) =>
{
    response.Headers["X-Total-Count"] = store.Items.Count.ToString();

    IEnumerable<SocialComment> items = store.Items;

    if (page is not null)
    {
        var size = Math.Clamp(pageSize ?? 50, 1, 200);
        var skip = (Math.Max(page.Value, 1) - 1) * size;
        items = items.Skip(skip).Take(size);
    }

    return Results.Ok(items);
});

app.Run();

static bool KeyMatches(string provided, string expected)
{
    var a = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
    var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
    return CryptographicOperations.FixedTimeEquals(a, b);
}

public record SocialComment(
    string IdComment,
    string? IdCliente,
    string? IdProducto,
    string? Fuente,
    string? Fecha,
    string? Comentario);

public sealed class SocialCommentsStore
{
    public IReadOnlyList<SocialComment> Items { get; }

    public SocialCommentsStore(string csvPath)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"No se encontro el CSV de comentarios: {csvPath}");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };

        using var reader = new StreamReader(csvPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        var list = new List<SocialComment>();

        while (csv.Read())
        {
            list.Add(new SocialComment(
                csv.GetField("IdComment") ?? string.Empty,
                NullIfEmpty(csv.GetField("IdCliente")),
                NullIfEmpty(csv.GetField("IdProducto")),
                NullIfEmpty(csv.GetField("Fuente")),
                NullIfEmpty(csv.GetField("Fecha")),
                NullIfEmpty(csv.GetField("Comentario"))));
        }

        Items = list;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
