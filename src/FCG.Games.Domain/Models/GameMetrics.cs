namespace FCG.Games.Domain.Models;

public sealed record PriceBucket(decimal From, decimal To, long DocCount);

public sealed record GameMetrics(
    long Count,
    decimal? AvgPrice,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<PriceBucket> Buckets);