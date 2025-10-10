#region using
using FCG.Games.Application.Interfaces;
using FCG.Games.Application.UseCases.Games.CreateGame;
using FCG.Games.Application.UseCases.Games.Delete;
using FCG.Games.Application.UseCases.Games.GetById;
using FCG.Games.Application.UseCases.Games.List;
using FCG.Games.Application.UseCases.Games.Update;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.Services;
using FCG.Games.Infra.Data;
using FCG.Games.Infra.Events;
using FCG.Games.Infra.Repositories;
using FCG.Games.Infra.Search;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenSearch.Client;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using System.Reflection;
using System.Security.Claims;
using System.Text;

#endregion

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

#region swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FCG Games API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
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

#region DI - Services e Handlers

builder.Services.AddScoped<IEventStore>(sp =>
{
    var db = sp.GetRequiredService<GamesDbContext>();
    return new EfEventStore<GamesDbContext>(db);
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<GamesDbContext>(name: "mysql-games-db");
builder.Services.AddScoped<IGameRepository, MySqlGameRepository>();

var useOpenSearch = builder.Configuration.GetValue<bool>("Search:UseOpenSearch", false);

if (useOpenSearch)
{
    builder.Services.AddSingleton<IOpenSearchClient>(_ =>
        OpenSearchClientFactory.Create(osUrl, osIndex));

    builder.Services.AddScoped<IGameSearchRepository>(sp =>
        new OpenSearchGameRepository(sp.GetRequiredService<IOpenSearchClient>(), osIndex));
}
else
{
    builder.Services.AddScoped<IGameSearchRepository, MySqlLikeSearchGameRepository>();
}

builder.Services.AddScoped<IGameCreationService, GameCreationService>();
builder.Services.AddScoped<CreateGameHandler>();
builder.Services.AddScoped<GetGameByIdHandler>();
builder.Services.AddScoped<ListGamesHandler>();
builder.Services.AddScoped<UpdateGameHandler>();
builder.Services.AddScoped<DeleteGameHandler>();
builder.Services.AddScoped<GetGamesMetricsHandler>();
#endregion

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

builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
            o.Filter = ctx => true;
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(o =>
        {
            o.SetDbStatementForText = true;
            o.EnrichWithIDbCommand = (activity, command) =>
            {
                activity?.SetTag("db.command", command.CommandText?.Split(' ').FirstOrDefault());
            };
        })
        .AddConsoleExporter();
    });

var app = builder.Build();

app.MapHealthChecks("/health/db");

app.Use(async (ctx, next) =>
{
    const string header = "X-Correlation-ID";
    if (!ctx.Request.Headers.TryGetValue(header, out var cid) || string.IsNullOrWhiteSpace(cid))
        cid = Guid.NewGuid().ToString();

    ctx.Response.Headers[header] = cid!;
    using (LogContext.PushProperty("CorrelationId", cid!.ToString()))
    using (LogContext.PushProperty("UserId",
           ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
           ctx.User.FindFirst("sub")?.Value ?? string.Empty))
    {
        await next();
    }
});

app.UseExceptionHandler(a => a.Run(async context =>
{
    var problem = new { title = "Unexpected error", status = 500, traceId = context.TraceIdentifier };
    Log.Error("Unhandled exception. TraceId={TraceId}", problem.traceId);
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(problem);
}));

app.UseSerilogRequestLogging(opts =>
{
    opts.GetLevel = (httpCtx, elapsed, ex) =>
        ex != null || httpCtx.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : Serilog.Events.LogEventLevel.Information;

    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("RequestPath", ctx.Request.Path);
        diag.Set("QueryString", ctx.Request.QueryString.Value);
        diag.Set("UserAgent", ctx.Request.Headers["User-Agent"].ToString());
        diag.Set("ClientIP", ctx.Connection.RemoteIpAddress?.ToString());
    };
});

/*
 * if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
 */


app.UseAuthentication();
app.UseAuthorization();


var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:EnableUI", false);
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "FCG API v1");
        c.RoutePrefix = "swagger";
    });
}




app.UseAuthentication();
app.UseAuthorization();

#region Endpoints
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

// GET /api/games/{id}
app.MapGet("/api/games/{id:guid}", async (
    Guid id,
    [FromServices] GetGameByIdHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(new GetGameByIdRequest(id), ct);
    return res is null ? Results.NotFound() : Results.Ok(res);
})
.WithTags("Games")
.WithSummary("Busca jogo por ID")
.WithDescription("Retorna um jogo persistido no MySQL.");

// GET /api/games (lista paginada)
app.MapGet("/api/games", async (
    int page,
    int size,
    [FromServices] ListGamesHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(new ListGamesRequest(page, size), ct);
    return Results.Ok(res);
})
.WithTags("Games")
.WithSummary("Lista jogos (paginado)")
.WithDescription("Retorna jogos do MySQL com paginacao.");

// GET /api/games/search
app.MapGet("/api/games/search", async (
    string? q,
    int page,
    int size,
    [FromServices] IGameSearchRepository searchRepo,
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
.WithDescription("Pesquisa em title e description, com paginação.");

// DELETE /api/games/{id}
app.MapDelete("/api/games/{id:guid}", async (
    Guid id,
    [FromServices] DeleteGameHandler handler,
    CancellationToken ct) =>
{
    var ok = await handler.Handle(new DeleteGameRequest(id), ct);
    return ok ? Results.NoContent() : Results.NotFound();
})
.RequireAuthorization("AdminOnly")
.WithTags("Games")
.WithSummary("Remove um jogo")
.WithDescription("Deleta do MySQL e remove do índice do OpenSearch.");

app.MapPost("/api/games", async (
    CreateGameRequest body,
    [FromServices] CreateGameHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(body, ct);
    return Results.Created($"/api/games/{res.Id}", res);
})
.RequireAuthorization("AdminOnly")
.WithTags("Games");

app.MapPut("/api/games/{id:guid}", async (
    Guid id,
    UpdateGameRequest body,
    [FromServices] UpdateGameHandler handler,
    CancellationToken ct) =>
{
    var req = body with { Id = id };
    var res = await handler.Handle(req, ct);
    return res is null ? Results.NotFound() : Results.Ok(res);
})
.RequireAuthorization("AdminOnly")
.WithTags("Games");

app.MapGet("/api/games/metrics", async (HttpContext ctx, CancellationToken ct) =>
{
    var handler = ctx.RequestServices.GetRequiredService<GetGamesMetricsHandler>();
    var res = await handler.Handle(ct);
    return Results.Ok(res);
});

app.MapPost("/api/games/reindex", async (
    IGameRepository repo,
    IGameSearchRepository search,
    CancellationToken ct) =>
{
    var page = 1; var size = 200; var total = 0;
    while (true)
    {
        var batch = await repo.ListAsync(page, size, ct);
        if (batch.Count == 0) break;
        await search.BulkIndexAsync(batch, ct);
        total += batch.Count; page++;
    }
    return Results.Ok(new { indexed = total });
})
.WithTags("Games")
.WithSummary("Reindexa todos os jogos no OpenSearch");

app.MapGet("/api/games/{id:guid}/events",
    async (Guid id, IEventStore es, CancellationToken ct) =>
    {
        var list = await es.ListByAggregateAsync(id, ct);
        return Results.Ok(list);
    })
.WithTags("Infra")
.RequireAuthorization("AdminOnly");

#endregion

app.Run();
