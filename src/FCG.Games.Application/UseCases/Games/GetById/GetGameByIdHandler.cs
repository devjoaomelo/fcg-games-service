using FCG.Games.Domain.Interfaces;

namespace FCG.Games.Application.UseCases.Games.GetById;

public sealed class GetGameByIdRequest(Guid id)
{
    public Guid Id { get; } = id;
}

public sealed record GetGameByIdResponse(Guid Id, string Title, string Description, decimal Price);

public sealed class GetGameByIdHandler(IGameRepository gameRepository)
{
    public async Task<GetGameByIdResponse?> Handle(GetGameByIdRequest req, CancellationToken ct = default)
    {
        var game = await gameRepository.GetByIdAsync(req.Id, ct);

        if (game is null) return null;

        return new GetGameByIdResponse(game.Id, game.Title.Value, game.Description.Value, game.Price.Value);
    }
}
