#region using
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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenSearch.Client;
using System.Reflection;
using System.Security.Claims;
using System.Text;
#endregion

var builder = WebApplication.CreateBuilder(args);

#region swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FCG Games API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",              // <- minúsculo
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole APENAS o token (sem 'Bearer ')"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks();
#endregion

#region Connection String - OpenSearch and MySQL
var conn = builder.Configuration.GetConnectionString("GamesDb")
           ?? Environment.GetEnvironmentVariable("ConnectionStrings__GamesDb")
           ?? "Server=localhost;Port=3317;Database=fcg_games;User=fcg;Password=fcgpwd;SslMode=None";

var osUrl = builder.Configuration["OpenSearch:Url"] ?? "http://localhost:9200";
var osIndex = builder.Configuration["OpenSearch:Index"] ?? "games";


builder.Services.AddDbContext<GamesDbContext>(opt =>
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn)));
#endregion

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

#region JWT Auth
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "fcg-users";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "fcg-clients";
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            // aceita role no tipo padrão (ClaimTypes.Role)
            RoleClaimType = ClaimsIdentity.DefaultRoleClaimType
        };

        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT] Auth failed: {ctx.Exception.GetType().Name} - {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var sub = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? ctx.Principal?.FindFirst("sub")?.Value;
                var role = ctx.Principal?.FindFirst(ClaimTypes.Role)?.Value
                           ?? ctx.Principal?.FindFirst("role")?.Value;
                Console.WriteLine($"[JWT] Validated. sub={sub}, role={role}");
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                Console.WriteLine($"[JWT] Challenge: {ctx.Error} - {ctx.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AdminOnly", policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(c =>
            (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "Admin")));
});

#endregion

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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
        // TODO: logar
    }
}

#region controllers
app.MapGet("/", () => new { service = "fcg-games-service", status = "ok" })
   .WithTags("Health");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .WithTags("Health")
   .WithSummary("Health-check");

app.MapGet("/version", () => new
{
    service = "fcg-games-service",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
})
.WithTags("Health")
.WithSummary("Versao do servico");


app.MapPost("/api/games", async (
    CreateGameRequest body,
    CreateGameHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(body, ct);
    return Results.Created($"/api/games/{res.Id}", res);
})
.RequireAuthorization("AdminOnly")
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
.WithDescription("Retorna jogos do MySQL com paginacao.");

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
.RequireAuthorization("AdminOnly")
.WithTags("Games")
.WithSummary("Atualiza um jogo")
.WithDescription("Atualiza título, descricao e preco. Reindexa no OpenSearch.");

app.MapDelete("/api/games/{id:guid}", async (
    Guid id,
    DeleteGameHandler handler,
    CancellationToken ct) =>
{
    var ok = await handler.Handle(new DeleteGameRequest(id), ct);
    return ok ? Results.NoContent() : Results.NotFound();
})
.RequireAuthorization("AdminOnly")
.WithTags("Games")
.WithSummary("Remove um jogo")
.WithDescription("Deleta do MySQL e remove do indice do OpenSearch.");
#endregion


app.Run();
