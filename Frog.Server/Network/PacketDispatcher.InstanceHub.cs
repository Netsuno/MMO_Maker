using Frog.Core.Protocol;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher
{
    private async Task HandleInstanceHubRequestAsync(
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

        if (!InstanceHubWire.TryParseRequest(payload.Span, out var kind, out var action, out var requestId, out var extra))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload instance invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var (result, snapshot) = _instanceHub.Execute(session, kind, action, requestId, extra);
        await _packetSender.SendInstanceHubResultAsync(clientSession, result, cancellationToken).ConfigureAwait(false);
        if (snapshot is { } snap)
        {
            await _packetSender.SendInstanceHubSnapshotAsync(clientSession, snap, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
