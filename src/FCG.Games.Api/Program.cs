using FCG.Games.Application.UseCases.Games.CreateGame;
using FCG.Games.Application.UseCases.Games.Delete;
using FCG.Games.Application.UseCases.Games.GetById;
using FCG.Games.Application.UseCases.Games.List;
using FCG.Games.Application.UseCases.Games.Update;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.Services;
using FCG.Games.Infra.Data;
using FCG.Games.Infra.Repositories;
using FCG.Games.Infra.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenSearch.Client;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FCG Games API",
        Version = "v1",
        Description = "Microserviço de Jogos — persistência em MySQL + busca em OpenSearch"
    });
});
builder.Services.AddHealthChecks();


var conn = builder.Configuration.GetConnectionString("GamesDb")
           ?? Environment.GetEnvironmentVariable("ConnectionStrings__GamesDb")
           ?? "Server=localhost;Port=3317;Database=fcg_games;User=fcg;Password=fcgpwd;SslMode=None";

// OpenSearch
var osUrl = builder.Configuration["OpenSearch:Url"] ?? "http://localhost:9200";
var osIndex = builder.Configuration["OpenSearch:Index"] ?? "games";


builder.Services.AddDbContext<GamesDbContext>(opt =>
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn)));


builder.Services.AddScoped<IGameRepository, MySqlGameRepository>();
builder.Services.AddSingleton<IOpenSearchClient>(_ =>
    OpenSearchClientFactory.Create(osUrl, osIndex));
builder.Services.AddScoped<IGameSearchRepository>(sp =>
    new OpenSearchGameRepository(sp.GetRequiredService<IOpenSearchClient>(), osIndex));
builder.Services.AddScoped<IGameCreationService, GameCreationService>();
builder.Services.AddScoped<CreateGameHandler>();
builder.Services.AddScoped<GetGameByIdHandler>();
builder.Services.AddScoped<ListGamesHandler>();
builder.Services.AddScoped<UpdateGameHandler>();
builder.Services.AddScoped<DeleteGameHandler>();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
        await db.Database.MigrateAsync();
    }
    catch
    {
        // TODO: logar se quiser — em dev podemos ignorar silenciosamente
    }
}

app.MapGet("/", () => new { service = "fcg-games-service", status = "ok" })
   .WithTags("System");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .WithTags("System")
   .WithSummary("Health-check");

app.MapGet("/version", () => new
{
    service = "fcg-games-service",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
})
.WithTags("System")
.WithSummary("Versão do serviço");


app.MapPost("/api/games", async (
    CreateGameRequest body,
    CreateGameHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(body, ct);
    return Results.Created($"/api/games/{res.Id}", res);
})
.WithTags("Games")
.WithSummary("Cria um jogo")
.WithDescription("Persiste no MySQL e indexa no OpenSearch (best-effort).")
.Produces(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);


app.MapGet("/api/games/search", async (
    string? q,
    int page,
    int size,
    IGameSearchRepository searchRepo,
    CancellationToken ct) =>
{
    var (items, total) = await searchRepo.SearchAsync(q, page, size, ct);
    var response = new
    {
        total,
        page,
        size,
        items = items.Select(g => new {
            id = g.Id,
            title = g.Title.Value,
            description = g.Description.Value,
            price = g.Price.Value
        })
    };
    return Results.Ok(response);
})
.WithTags("Games")
.WithSummary("Busca jogos (OpenSearch)")
.WithDescription("Pesquisa por texto em 'title' (boost) e 'description', com paginação.");

app.MapGet("/api/games/{id:guid}", async (
    Guid id,
    GetGameByIdHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(new GetGameByIdRequest(id), ct);
    return res is null ? Results.NotFound() : Results.Ok(res);
})
.WithTags("Games")
.WithSummary("Busca jogo por ID")
.WithDescription("Retorna um jogo persistido no MySQL.");

app.MapGet("/api/games", async (
    int page,
    int size,
    ListGamesHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(new ListGamesRequest(page, size), ct);
    return Results.Ok(res);
})
.WithTags("Games")
.WithSummary("Lista jogos (paginado)")
.WithDescription("Retorna jogos do MySQL com paginação.");

app.MapPut("/api/games/{id:guid}", async (
    Guid id,
    UpdateGameRequest body,
    UpdateGameHandler handler,
    CancellationToken ct) =>
{
    var req = body with { Id = id };

    var res = await handler.Handle(req, ct);
    return res is null ? Results.NotFound() : Results.Ok(res);
})
.WithTags("Games")
.WithSummary("Atualiza um jogo")
.WithDescription("Atualiza título, descrição e preço. Reindexa no OpenSearch.");

app.MapDelete("/api/games/{id:guid}", async (
    Guid id,
    DeleteGameHandler handler,
    CancellationToken ct) =>
{
    var ok = await handler.Handle(new DeleteGameRequest(id), ct);
    return ok ? Results.NoContent() : Results.NotFound();
})
.WithTags("Games")
.WithSummary("Remove um jogo")
.WithDescription("Deleta do MySQL e remove do índice do OpenSearch.");



app.Run();
