using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.ValueObjects;

namespace FCG.Games.Application.UseCases.Games.Update;

public sealed record UpdateGameRequest(Guid Id, string Title, string Description, decimal Price);
public sealed record UpdateGameResponse(Guid Id, string Title, string Description, decimal Price);

public sealed class UpdateGameHandler(IGameRepository repo, IGameSearchRepository search)
{
    public async Task<UpdateGameResponse?> Handle(UpdateGameRequest req, CancellationToken ct = default)
    {
        var entity = await repo.GetByIdAsync(req.Id, ct);
        if (entity is null) return null;

        var t = GameTitle.Create(req.Title);
        var d = Description.Create(req.Description);
        var p = Price.Parse(req.Price);

        entity.Update(t, d, p);

        await repo.UpdateAsync(entity, ct);

        try 
        { 
            await search.IndexAsync(entity, ct); 
        }
        catch 
        { 
            /* logar futuramente */ 
        }

        return new UpdateGameResponse(entity.Id, entity.Title.Value, entity.Description.Value, entity.Price.Value);
    }
}
