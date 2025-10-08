using FCG.Games.Application.UseCases.Games.Metrics;
using FCG.Games.Domain.Interfaces;

public sealed class GetGamesMetricsHandler(IGameSearchRepository repo)
{
    public async Task<GamesMetricsResponse> Handle(CancellationToken ct = default)
    {
        var m = await repo.GetMetricsAsync(ct);
        return new GamesMetricsResponse(
            m.Count, m.AvgPrice, m.MinPrice, m.MaxPrice,
            m.Buckets.Select(b => new HistogramBucket(b.From, b.To, b.DocCount)).ToList()
        );
    }
}
