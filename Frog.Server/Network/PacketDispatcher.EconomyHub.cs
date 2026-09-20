using Frog.Core.Protocol;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher
{
    private async Task HandleEconomyHubRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!EconomyHubWire.TryParseRequest(payload.Span, out var kind, out var action, out var requestId, out var extra))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload economie invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var (result, snapshot) = await _economyHub
            .ExecuteAsync(session, kind, action, requestId, extra, cancellationToken)
            .ConfigureAwait(false);
        await _packetSender.SendEconomyHubResultAsync(clientSession, result, cancellationToken).ConfigureAwait(false);
        if (snapshot is { } snap)
        {
            await _packetSender.SendEconomyHubSnapshotAsync(clientSession, snap, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
