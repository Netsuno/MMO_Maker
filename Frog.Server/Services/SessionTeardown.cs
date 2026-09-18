using Frog.Server.Network;
using Frog.Server.Persistence;
using Frog.Server.Social;

namespace Frog.Server.Services;

/// <summary>
/// Options for the shared live-session cleanup. Kick, ban, peer disconnect, logout,
/// idle expire, and reconnect-displace all go through <see cref="SessionTeardown"/>.
/// </summary>
public readonly record struct SessionTeardownOptions(
    bool NotifyPeers,
    bool DisconnectClient,
    bool ClearAuthenticatedSession)
{
    /// <summary>Operator kick / ban: notify peers, close TCP, clear the auth binding.</summary>
    public static SessionTeardownOptions KickBan { get; } = new(true, true, true);

    /// <summary>Peer closed the socket; <c>await using</c> still disposes the session.</summary>
    public static SessionTeardownOptions PeerDisconnect { get; } = new(true, false, true);

    /// <summary>Host-wide stop: skip leave fan-out (other clients are tearing down too).</summary>
    public static SessionTeardownOptions HostShutdown { get; } = new(false, false, true);

    /// <summary>LogoutRequest: bookkeeping first; caller sends LogoutAck then disconnects.</summary>
    public static SessionTeardownOptions Logout { get; } = new(true, false, true);

    /// <summary>Idle timeout: notify, close TCP.</summary>
    public static SessionTeardownOptions IdleExpire { get; } = new(true, true, true);

    /// <summary>Same account reconnects: save/cancel/close old TCP, do not broadcast leave.</summary>
    public static SessionTeardownOptions ReconnectDisplace { get; } = new(false, true, true);

    /// <summary>Failed login after a session row was created; keep the TCP for the error path.</summary>
    public static SessionTeardownOptions FailedLogin { get; } = new(false, false, true);
}

/// <summary>
/// Single idempotent entry point for live-session teardown. The first caller to
/// <see cref="ConnectionManager.TryRemoveSession"/> wins; later kick/ban/disconnect
/// calls are no-ops aside from a best-effort leftover TCP close.
/// </summary>
public sealed class SessionTeardown(
    ConnectionManager connections,
    ClientRegistry clients,
    PlayerLifecycleNotifier lifecycle,
    IPlayerStateStore playerState,
    ICharacterRuntimeCleanup characterRuntime,
    ISocialPresenceSink? socialPresence = null)
{
    private readonly ConnectionManager _connections = connections;
    private readonly ClientRegistry _clients = clients;
    private readonly PlayerLifecycleNotifier _lifecycle = lifecycle;
    private readonly IPlayerStateStore _playerState = playerState;
    private readonly ICharacterRuntimeCleanup _characterRuntime = characterRuntime;
    private readonly ISocialPresenceSink? _socialPresence = socialPresence;

    /// <summary>
    /// Test barrier: fires after the session row is dropped and before the
    /// coordinator uses the retained TCP to finish cleanup.
    /// </summary>
    internal Action<Guid>? AfterSessionRemovedBeforeClientLookup { get; set; }

    public Task TearDownByUsernameAsync(
        string username,
        SessionTeardownOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username)
            || !_connections.TryGetSessionByUsername(username, out var session)
            || session is null)
        {
            return Task.CompletedTask;
        }

        return TearDownAsync(session.Id, options, cancellationToken);
    }

    public async Task TearDownAsync(
        Guid sessionId,
        SessionTeardownOptions options,
        CancellationToken cancellationToken = default)
    {
        // Retain the TCP before dropping the session row. A concurrent packet may
        // observe the inactive session and Unregister the registry entry; cleanup
        // must still close this connection.
        _clients.TryGet(sessionId, out var client);
        if (client is not null)
        {
            _clients.Unregister(sessionId);
        }

        if (!_connections.TryRemoveSession(sessionId, out var session) || session is null)
        {
            if (options.DisconnectClient)
            {
                if (client is not null)
                {
                    DisconnectClient(client, options.ClearAuthenticatedSession);
                }
                else
                {
                    DisconnectLeftoverClient(sessionId, options.ClearAuthenticatedSession);
                }
            }

            return;
        }

        AfterSessionRemovedBeforeClientLookup?.Invoke(sessionId);

        if (session.CharacterGuid is Guid characterId)
        {
            _characterRuntime.CancelForCharacter(characterId);
            if (_socialPresence is not null && (options.NotifyPeers || options.DisconnectClient))
            {
                try
                {
                    await _socialPresence.NotifyCharacterOfflineAsync(characterId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(session.CharacterId))
        {
            _playerState.UpsertForCharacter(
                session.CharacterId,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY);
        }

        if (options.NotifyPeers)
        {
            try
            {
                await _lifecycle.NotifyPlayerLeftAsync(session.Username, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        if (client is not null)
        {
            DisconnectClient(client, options.ClearAuthenticatedSession, options.DisconnectClient);
        }
        else if (options.DisconnectClient)
        {
            DisconnectLeftoverClient(sessionId, options.ClearAuthenticatedSession);
        }
    }

    private static void DisconnectClient(
        ClientSession client,
        bool clearAuthenticatedSession,
        bool disconnect = true)
    {
        if (clearAuthenticatedSession)
        {
            client.AuthenticatedSession = null;
        }

        if (disconnect)
        {
            client.Disconnect();
        }
    }

    private void DisconnectLeftoverClient(Guid sessionId, bool clearAuthenticatedSession)
    {
        if (!_clients.TryGet(sessionId, out var leftover) || leftover is null)
        {
            return;
        }

        _clients.Unregister(sessionId);
        if (clearAuthenticatedSession)
        {
            leftover.AuthenticatedSession = null;
        }

        leftover.Disconnect();
    }
}
