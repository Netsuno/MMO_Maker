namespace Frog.Application.Content;

/// <summary>
/// Fingerprint of published content (catalog, map events, Phase 8). The game server
/// polls this to push a live refresh to already-connected sessions after republish.
/// </summary>
public interface IPublishedContentRevisionStamp
{
    Task<long> GetStampAsync(CancellationToken cancellationToken = default);
}

/// <summary>No published world (in-memory / playtest / MariaDB). Stamp never changes.</summary>
public sealed class NullPublishedContentRevisionStamp : IPublishedContentRevisionStamp
{
    public static readonly NullPublishedContentRevisionStamp Instance = new();

    private NullPublishedContentRevisionStamp()
    {
    }

    public Task<long> GetStampAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(0L);
    }
}
