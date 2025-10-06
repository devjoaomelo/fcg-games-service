using OpenSearch.Client;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Infra.Search;
using FCG.Games.Infra.Repositories;
using System.Reflection;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// OpenSearch (DI)
var osUrl = builder.Configuration["OpenSearch:Url"] ?? "http://localhost:9200";
var osIndex = builder.Configuration["OpenSearch:Index"] ?? "games";
builder.Services.AddSingleton<IOpenSearchClient>(_ => OpenSearchClientFactory.Create(osUrl, osIndex));
builder.Services.AddScoped<IGameSearchRepository>(sp =>
    new OpenSearchGameRepository(sp.GetRequiredService<IOpenSearchClient>(), osIndex));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => new { service = "fcg-games-service", status = "ok" });
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/version", () => new
{
    service = "fcg-games-service",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
});

// Endpoints mínimos
app.MapPost("/api/games", async (
    string title,
    string? genre,
    decimal price,
    IGameSearchRepository repo,
    CancellationToken ct) =>
{
    var game = new FCG.Games.Domain.Entities.Game(title, genre, price);
    await repo.IndexAsync(game, ct);
    return Results.Created($"/api/games/{game.Id}", game);
})
.WithTags("Games")
.WithSummary("Indexa um jogo")
.WithDescription("Cria/atualiza um jogo no índice do OpenSearch.");

app.MapGet("/api/games/search", async (
    string? q,
    string? genre,
    int page,
    int size,
    IGameSearchRepository repo,
    CancellationToken ct) =>
{
    var (items, total) = await repo.SearchAsync(q, genre, page, size, ct);
    return Results.Ok(new { total, page = page <= 0 ? 1 : page, size = size <= 0 ? 10 : size, items });
})
.WithTags("Games")
.WithSummary("Busca jogos")
.WithDescription("Pesquisa por texto e/ou gênero, com paginação.");

app.Run();
