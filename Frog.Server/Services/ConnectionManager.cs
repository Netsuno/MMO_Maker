using System.Collections.Concurrent;
using Frog.Server.Models;

namespace Frog.Server.Services;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<Guid, Session> _sessionsById = new();
    private readonly ConcurrentDictionary<string, Guid> _sessionIdByUsername = new(StringComparer.OrdinalIgnoreCase);

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

    public void RemoveSession(Guid sessionId)
    {
        if (!_sessionsById.TryRemove(sessionId, out var session))
        {
            return;
        }

        _sessionIdByUsername.TryRemove(session.Username, out _);
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
