using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;
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
        // cria o índice se não existir
        var exists = await _client.Indices.ExistsAsync(_index, d => d, ct);

        if (!exists.Exists)
        {
            var create = await _client.Indices.CreateAsync(_index, c => c
                .Map<Game>(m => m.AutoMap()), ct);
            if (!create.IsValid) throw new InvalidOperationException(create.OriginalException?.Message ?? "Index creation failed");
        }

        var res = await _client.IndexAsync(game, i => i.Index(_index).Id(game.Id), ct);
        if (!res.IsValid) throw new InvalidOperationException(res.OriginalException?.Message ?? "Index failed");
    }

    public async Task<(IReadOnlyList<Game> items, long total)> SearchAsync(string? q, string? genre, int page, int size, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        size = size <= 0 || size > 100 ? 10 : size;
        var from = (page - 1) * size;

        var must = new List<QueryContainer>();
        if (!string.IsNullOrWhiteSpace(q))
            must.Add(new QueryStringQuery { Fields = new[] { "title^2", "genre" }, Query = q });

        if (!string.IsNullOrWhiteSpace(genre))
            must.Add(new TermQuery { Field = "genre.keyword", Value = genre });

        var res = await _client.SearchAsync<Game>(s => s
            .Index(_index)
            .From(from).Size(size)
            .Query(qd => must.Count == 0 ? qd.MatchAll() : qd.Bool(b => b.Must(must.ToArray())))
            .Sort(ss => ss.Ascending(f => f.Title)), ct);

        if (!res.IsValid) throw new InvalidOperationException(res.OriginalException?.Message ?? "Search failed");

        return (res.Documents.ToList(), res.Total);
    }
}
