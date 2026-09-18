namespace Frog.Server.Trade;

public interface ITradePresenceSink
{
    Task NotifyCharacterOfflineAsync(Guid characterId, CancellationToken cancellationToken);

    Task NotifyCharacterUnfitAsync(Guid characterId, string reason, CancellationToken cancellationToken);

    Task NotifyMovementAsync(Guid characterId, CancellationToken cancellationToken);
}

public sealed class NullTradePresenceSink : ITradePresenceSink
{
    public static NullTradePresenceSink Instance { get; } = new();

    public Task NotifyCharacterOfflineAsync(Guid characterId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task NotifyCharacterUnfitAsync(Guid characterId, string reason, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task NotifyMovementAsync(Guid characterId, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
