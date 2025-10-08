namespace FCG.Games.Application.UseCases.Games.Metrics;

public sealed record HistogramBucket(decimal From, decimal To, long DocCount);

public sealed record GamesMetricsResponse(
    long Count,
    decimal? AvgPrice,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<HistogramBucket> PriceHistogram);
