using System.Collections.Concurrent;
using Frog.Core.Identity;
using Frog.Server.Models;

namespace Frog.Server.Services;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<Guid, Session> _sessionsById = new();
    private readonly ConcurrentDictionary<string, Guid> _sessionIdByUsername = new(AccountUsername.Comparer);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _usernameGates = new(AccountUsername.Comparer);

    public async Task RunExclusiveForUsernameAsync(
        string username,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(action);

        var gate = _usernameGates.GetOrAdd(username, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public bool TryCreateSession(string username, out Session? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        if (_sessionIdByUsername.ContainsKey(username))
        {
            session = null;
            return false;
        }

        var createdSession = new Session
        {
            Id = Guid.NewGuid(),
            Username = username
        };

        if (!_sessionsById.TryAdd(createdSession.Id, createdSession))
        {
            session = null;
            return false;
        }

        if (!_sessionIdByUsername.TryAdd(username, createdSession.Id))
        {
            _sessionsById.TryRemove(createdSession.Id, out _);
            session = null;
            return false;
        }

        session = createdSession;
        return true;
    }

    /// <summary>
    /// Creates a session when the username is free. Does not delete an occupant —
    /// the caller must run any existing session through <see cref="SessionTeardown"/> first.
    /// </summary>
    public bool TryDisplaceAndCreateSession(string username, out Session? session, out Guid? displacedSessionId)
    {
        displacedSessionId = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        if (_sessionIdByUsername.TryGetValue(username, out var existingId)
            && _sessionsById.ContainsKey(existingId))
        {
            displacedSessionId = existingId;
            session = null;
            return false;
        }

        return TryCreateSession(username, out session);
    }

    public void RemoveSession(Guid sessionId) => TryRemoveSession(sessionId, out _);

    /// <summary>
    /// Atomically drops the session. Returns <c>true</c> only for the first remover —
    /// used as the idempotency gate for <see cref="SessionTeardown"/>.
    /// </summary>
    public bool TryRemoveSession(Guid sessionId, out Session? session)
    {
        if (!_sessionsById.TryRemove(sessionId, out session))
        {
            session = null;
            return false;
        }

        _sessionIdByUsername.TryRemove(new KeyValuePair<string, Guid>(session.Username, sessionId));
        return true;
    }

    public bool TryTouchSession(Guid sessionId)
    {
        if (!_sessionsById.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        session.LastActivityUtc = DateTime.UtcNow;
        return true;
    }

    public bool IsSessionActive(Guid sessionId)
        => _sessionsById.ContainsKey(sessionId);

    public IReadOnlyList<Session> RemoveExpiredSessions(TimeSpan idleTimeout)
    {
        var now = DateTime.UtcNow;
        var toRemove = new List<Session>();

        foreach (var entry in _sessionsById)
        {
            if (now - entry.Value.LastActivityUtc <= idleTimeout)
            {
                continue;
            }

            toRemove.Add(entry.Value);
        }

        foreach (var session in toRemove)
        {
            RemoveSession(session.Id);
        }

        return toRemove;
    }

    public IReadOnlyCollection<Session> GetActiveSessions()
        => _sessionsById.Values.ToArray();

    public bool TryGetSessionByUsername(string username, out Session? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (!_sessionIdByUsername.TryGetValue(username, out var id))
        {
            session = null;
            return false;
        }

        return _sessionsById.TryGetValue(id, out session);
    }
}
