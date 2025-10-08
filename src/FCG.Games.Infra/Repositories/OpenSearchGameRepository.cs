using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.Models;
using FCG.Games.Domain.ValueObjects;
using FCG.Games.Infra.Documents;
using OpenSearch.Client;
using OpenSearch.Net;

namespace FCG.Games.Infra.Repositories;

public sealed class OpenSearchGameRepository : IGameSearchRepository
{
    private readonly IOpenSearchClient _client;
    private readonly string _index;

    public OpenSearchGameRepository(IOpenSearchClient client, string indexName)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _index = string.IsNullOrWhiteSpace(indexName) ? "games" : indexName;
    }

    private async Task EnsureIndexAsync(CancellationToken ct)
    {
        var exists = await _client.Indices.ExistsAsync(_index);
        if (exists.Exists) return;

        var create = await _client.Indices.CreateAsync(_index, c => c
            .Map<GameDocument>(m => m
                .AutoMap()
                .Properties(ps => ps
                    .Text(t => t
                        .Name(d => d.Title)
                        .Fields(f => f
                            .Keyword(k => k
                                .Name("keyword")
                                .IgnoreAbove(256)
                            )
                        )
                    )
                    .Text(t => t
                        .Name(d => d.Description)
                        .Fields(f => f
                            .Keyword(k => k
                                .Name("keyword")
                                .IgnoreAbove(256)
                            )
                        )
                    )
                    .Number(n => n
                        .Name(d => d.Price)
                        .Type(NumberType.Double)
                    )
                )
            ), ct
        );

        if (!create.IsValid)
            throw new InvalidOperationException(create.ServerError?.Error?.Reason
                ?? create.OriginalException?.Message
                ?? "Index creation failed");
    }

    private static GameDocument ToDocument(Game g) => new()
    {
        Id = g.Id,
        Title = g.Title.Value,
        Description = g.Description.Value,
        Price = g.Price.Value
    };

    private static Game ToEntity(GameDocument d)
    {
        var game = new Game(
            GameTitle.Create(d.Title),
            Description.Create(d.Description),
            Price.Parse(d.Price)
        );
        typeof(Game).GetProperty(nameof(Game.Id))!.SetValue(game, d.Id);
        return game;
    }

    // Indexação
    public async Task IndexAsync(Game game, CancellationToken ct = default)
    {
        await EnsureIndexAsync(ct);

        var doc = ToDocument(game);
        var res = await _client.IndexAsync(doc, i => i.Index(_index).Id(doc.Id), ct);
        if (!res.IsValid)
            throw new InvalidOperationException(res.ServerError?.Error?.Reason
                ?? res.OriginalException?.Message
                ?? "Index failed");
    }

    public async Task BulkIndexAsync(IEnumerable<Game> games, CancellationToken ct = default)
    {
        await EnsureIndexAsync(ct);

        var docs = games.Select(ToDocument).ToList();

        var resp = await _client.BulkAsync(b => b
            .Index(_index)
            .IndexMany(docs)                
            .Refresh(Refresh.WaitFor)       
        , ct);

        if (!resp.IsValid)
            throw new InvalidOperationException(
                resp.ServerError?.Error?.Reason
                ?? resp.OriginalException?.Message
                ?? "Bulk index error");
    }

    public async Task DeleteByIdAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _client.DeleteAsync<GameDocument>(id, d => d.Index(_index), ct);
        if (!res.IsValid && res.Result != Result.NotFound)
            throw new InvalidOperationException(res.ServerError?.Error?.Reason
                ?? res.OriginalException?.Message
                ?? "Delete failed");
    }

    public async Task<(IReadOnlyList<Game> items, long total)> SearchAsync(string? q, int page, int size, CancellationToken ct = default)
    {
        await EnsureIndexAsync(ct); // <- garante o índice

        page = page <= 0 ? 1 : page;
        size = size <= 0 || size > 100 ? 10 : size;
        var from = (page - 1) * size;

        QueryContainer query =
            string.IsNullOrWhiteSpace(q)
                ? new MatchAllQuery()
                : new MultiMatchQuery
                {
                    Query = q,
                    Fields = new[] { "title^2", "description" },
                    Type = TextQueryType.BestFields,
                    Lenient = true // tolera diferenças de mapeamento
                };

        var res = await _client.SearchAsync<GameDocument>(s => s
            .Index(_index)
            .From(from)
            .Size(size)
            .TrackTotalHits(true)
            .Query(_ => query)
            .Sort(ss => ss.Ascending(f => f.Title.Suffix("keyword")))
        , ct);

        if (!res.IsValid)
            throw new InvalidOperationException(res.ServerError?.Error?.Reason
                ?? res.OriginalException?.Message
                ?? "Search failed");

        var list = res.Documents.Select(ToEntity).ToList();
        var total = res.HitsMetadata?.Total?.Value ?? res.Total;
        return (list, total);
    }

    // Métricas
    public async Task<GameMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        const decimal bucketSize = 10m;
        const decimal maxExpected = 260m;

        var resp = await _client.SearchAsync<GameDocument>(s => s
            .Index(_index)
            .Size(0)
            .Aggregations(a => a
                .ValueCount("total_docs", v => v.Field(f => f.Price))
                .Average("avg_price", v => v.Field(f => f.Price))
                .Min("min_price", v => v.Field(f => f.Price))
                .Max("max_price", v => v.Field(f => f.Price))
                .Histogram("price_hist", h => h
                    .Field(f => f.Price)
                    .Interval((double)bucketSize)
                    .ExtendedBounds(0, (double)maxExpected)
                )
            ), ct);

        if (!resp.IsValid)
            throw new InvalidOperationException($"OpenSearch metrics error: {resp.ServerError?.Error?.Reason ?? resp.OriginalException?.Message}");

        var total = (long)(resp.Aggregations.ValueCount("total_docs")?.Value ?? 0);
        var avg = (decimal?)resp.Aggregations.Average("avg_price")?.Value;
        var min = (decimal?)resp.Aggregations.Min("min_price")?.Value;
        var max = (decimal?)resp.Aggregations.Max("max_price")?.Value;

        var histAgg = resp.Aggregations.Histogram("price_hist");
        var buckets = histAgg?.Buckets?.Select(b =>
            new PriceBucket((decimal)b.Key, (decimal)b.Key + bucketSize, b.DocCount ?? 0)
        ).ToList() ?? new List<PriceBucket>();

        return new GameMetrics(total, avg, min, max, buckets);
    }
}
