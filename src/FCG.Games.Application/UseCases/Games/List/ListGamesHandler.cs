using FCG.Games.Domain.Interfaces;

namespace FCG.Games.Application.UseCases.Games.List;

public sealed class ListGamesRequest(int page = 1, int size = 10)
{
    public int Page { get; } = page <= 0 ? 1 : page;
    public int Size { get; } = size <= 0 || size > 100 ? 10 : size;
}

public sealed record ListGamesItem(Guid Id, string Title, string Description, decimal Price);
public sealed record ListGamesResponse(int Page, int Size, int Count, IReadOnlyList<ListGamesItem> Items);

public sealed class ListGamesHandler(IGameRepository gameRepository)
{
    public async Task<ListGamesResponse> Handle(ListGamesRequest req, CancellationToken ct = default)
    {
        var list = await gameRepository.ListAsync(req.Page, req.Size, ct);
        var items = list
            .Select(g => new ListGamesItem(g.Id, g.Title.Value, g.Description.Value, g.Price.Value))
            .ToList();

        return new ListGamesResponse(req.Page, req.Size, items.Count, items);
    }
}
