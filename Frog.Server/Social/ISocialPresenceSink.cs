namespace Frog.Server.Social;

public interface ISocialPresenceSink
{
    Task NotifyCharacterOnlineAsync(Guid characterId, string displayName, CancellationToken cancellationToken);

    Task NotifyCharacterOfflineAsync(Guid characterId, CancellationToken cancellationToken);
}

public sealed class NullSocialPresenceSink : ISocialPresenceSink
{
    public static NullSocialPresenceSink Instance { get; } = new();

    public Task NotifyCharacterOnlineAsync(Guid characterId, string displayName, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task NotifyCharacterOfflineAsync(Guid characterId, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
