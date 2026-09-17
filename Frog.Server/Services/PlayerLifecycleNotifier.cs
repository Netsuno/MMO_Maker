using Frog.Server.Network;

namespace Frog.Server.Services;

public sealed class PlayerLifecycleNotifier(PacketSender packetSender, ClientRegistry clientRegistry)
{
    private readonly PacketSender _packetSender = packetSender;
    private readonly ClientRegistry _clientRegistry = clientRegistry;

    public async Task NotifyPlayerLeftAsync(string username, CancellationToken cancellationToken)
    {
        foreach (var client in _clientRegistry.GetAllAuthenticatedClients())
        {
            if (client.IsClosed)
            {
                continue;
            }

            await _packetSender.SendPlayerLeaveAsync(client, username, cancellationToken);
        }
    }
}
