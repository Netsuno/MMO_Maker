using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Models;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher
{
    private async Task HandleTradeRequestAsync(
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

        if (!TradeWire.TryParseRequest(payload.Span, out var action, out var tradeId, out var requestId, out var extra))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload echange invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var result = await _trade.ExecuteAsync(session, action, tradeId, requestId, extra, cancellationToken)
            .ConfigureAwait(false);
        await _packetSender.SendTradeResultAsync(clientSession, result, cancellationToken).ConfigureAwait(false);
    }
}
