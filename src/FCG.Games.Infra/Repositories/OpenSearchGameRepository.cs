using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.ValueObjects;
using FCG.Games.Infra.Documents;
using OpenSearch.Client;

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

    public async Task IndexAsync(Game game, CancellationToken ct = default)
    {
        var exists = await _client.Indices.ExistsAsync(_index);
        if (!exists.Exists)
        {
            var create = await _client.Indices.CreateAsync(_index, c => c
                .Map<GameDocument>(m => m.AutoMap()));
            if (!create.IsValid)
                throw new InvalidOperationException(create.OriginalException?.Message ?? "Index creation failed");
        }

        var doc = ToDocument(game);

        var res = await _client.IndexAsync(doc, i => i.Index(_index).Id(doc.Id), ct);
        if (!res.IsValid)
            throw new InvalidOperationException(res.OriginalException?.Message ?? "Index failed");
    }

    public async Task DeleteByIdAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _client.DeleteAsync<GameDocument>(id, d => d.Index(_index), ct);
        if (!res.IsValid && res.Result != Result.NotFound)
            throw new InvalidOperationException(res.OriginalException?.Message ?? "Delete failed");
    }

    public async Task<(IReadOnlyList<Game> items, long total)> SearchAsync(string? q, int page, int size, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        size = size <= 0 || size > 100 ? 10 : size;
        var from = (page - 1) * size;

        var must = new List<QueryContainer>();
        if (!string.IsNullOrWhiteSpace(q))
        {
            must.Add(new QueryStringQuery
            {
                // busca em title (peso 2) e description
                Fields = new[] { "title^2", "description" },
                Query = q
            });
        }

        var res = await _client.SearchAsync<GameDocument>(s => s
            .Index(_index)
            .From(from).Size(size)
            .Query(qd => must.Count == 0 ? qd.MatchAll() : qd.Bool(b => b.Must(must.ToArray())))
            .Sort(ss => ss.Ascending(f => f.Title)), ct);

        if (!res.IsValid)
            throw new InvalidOperationException(res.OriginalException?.Message ?? "Search failed");

        var list = new List<Game>(res.Documents.Count);
        foreach (var d in res.Documents)
        {
            var game = new Game(
                GameTitle.Create(d.Title),
                Description.Create(d.Description),
                Price.Parse(d.Price)
            );

            var idProp = typeof(Game).GetProperty(nameof(Game.Id));
            idProp!.SetValue(game, d.Id);

            list.Add(game);
        }

        return (list, res.Total);
    }

    private static GameDocument ToDocument(Game g) => new()
    {
        Id = g.Id,
        Title = g.Title.Value,
        Description = g.Description.Value,
        Price = g.Price.Value
    };
}
