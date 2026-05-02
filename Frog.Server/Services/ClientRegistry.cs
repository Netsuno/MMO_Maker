using System.Collections.Concurrent;
using Frog.Server.Network;

namespace Frog.Server.Services;

public sealed class ClientRegistry
{
    private readonly ConcurrentDictionary<Guid, ClientSession> _clientBySessionId = new();

    public void Register(Guid sessionId, ClientSession clientSession)
    {
        _clientBySessionId[sessionId] = clientSession;
    }

    public void Unregister(Guid sessionId)
    {
        _clientBySessionId.TryRemove(sessionId, out _);
    }

    public bool TryGet(Guid sessionId, out ClientSession? clientSession)
        => _clientBySessionId.TryGetValue(sessionId, out clientSession);

    public IReadOnlyCollection<ClientSession> GetAllAuthenticatedClients()
        => _clientBySessionId.Values.ToArray();
}
