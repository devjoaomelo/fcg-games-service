using FCG.Games.Domain.Interfaces;

namespace FCG.Games.Application.UseCases.Games.Delete;

public sealed class DeleteGameRequest(Guid id)
{
    public Guid Id { get; } = id;
}

public sealed class DeleteGameHandler(IGameRepository gameRepository, IGameSearchRepository search)
{
    public async Task<bool> Handle(DeleteGameRequest req, CancellationToken ct = default)
    {
        var found = await gameRepository.GetByIdAsync(req.Id, ct);
        if (found is null) return false;

        await gameRepository.DeleteAsync(req.Id, ct);

        try 
        { 
            await search.DeleteByIdAsync(req.Id, ct); 
        } 
        catch 
        { 
            /* logar futuramente */ 
        }

        return true;
    }
}
