using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Network;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>
/// Invalidates published-content caches and pushes catalog / map events / environment
/// to already-connected sessions (no reconnect, reselect, or restart).
/// </summary>
public sealed class PublishedContentLiveRefreshCoordinator(
    IMapEventStore mapEvents,
    ConnectionManager connections,
    ClientRegistry clients,
    IPublishedContentLiveRefreshSink sink,
    ILogger<PublishedContentLiveRefreshCoordinator> logger)
{
    public async Task RefreshConnectedSessionsAsync(CancellationToken cancellationToken = default)
    {
        mapEvents.InvalidateAll();

        foreach (var session in connections.GetActiveSessions())
        {
            if (!session.HasActiveCharacter())
            {
                continue;
            }

            if (!clients.TryGet(session.Id, out var client) || client is null)
            {
                continue;
            }

            try
            {
                await sink.PushToSessionAsync(client, session, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Live refresh push failed for session {SessionId} user {Username}.",
                    session.Id,
                    session.Username);
            }
        }
    }
}

/// <summary>Pushes the three live-refresh packets to one authenticated session.</summary>
public interface IPublishedContentLiveRefreshSink
{
    Task PushToSessionAsync(ClientSession client, Session session, CancellationToken cancellationToken);
}
