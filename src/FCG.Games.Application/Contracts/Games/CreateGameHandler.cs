using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;


namespace FCG.Games.Application.UseCases.Games.CreateGame;

public sealed record CreateGameRequest(string Title, string? Description, decimal Price);
public sealed record CreateGameResponse(Guid Id, string Title, string? Description, decimal Price)
{
    public static CreateGameResponse FromDomain(Game game)
    {
        return new(game.Id, game.Title.Value, game.Description.Value, game.Price.Value);
    }
}
public sealed class CreateGameHandler(
    IGameCreationService gameCreation,
    IGameRepository gameRepository,
    IGameSearchRepository searchRepository)
{
    public async Task<CreateGameResponse> Handle(CreateGameRequest req, CancellationToken ct = default)
    {
        var game = gameCreation.Create(req.Title, req.Description, req.Price);

        await gameRepository.AddAsync(game, ct);               
        try 
        { 
            await searchRepository.IndexAsync(game, ct);
        }   
        catch 
        { 
            /* TODO: logar */ 
        }

        return CreateGameResponse.FromDomain(game);
    }
}
