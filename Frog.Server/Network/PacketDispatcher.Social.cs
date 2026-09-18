using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Models;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher
{
    private async Task HandleSocialRequestAsync(
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

        if (!SocialWire.TryParseRequest(payload.Span, out var kind, out var action, out var requestId, out var extra))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload social invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var result = await _social.ExecuteAsync(session, kind, action, requestId, extra, cancellationToken)
            .ConfigureAwait(false);
        await _packetSender.SendSocialResultAsync(clientSession, result, cancellationToken).ConfigureAwait(false);
    }

    /// <returns>Nombre de destinataires, ou -1 si le canal a été rejeté (erreur déjà envoyée).</returns>
    private async Task<int> DeliverSocialChannelChatAsync(
        ClientSession senderClient,
        Session session,
        ChatChannel channel,
        string from,
        string message,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            await _packetSender.SendErrorAsync(senderClient, "Personnage actif requis.", cancellationToken)
                .ConfigureAwait(false);
            return -1;
        }

        IReadOnlyList<Guid> members = channel == ChatChannel.Party
            ? _social.GetPartyMemberIds(characterId)
            : await _social.GetGuildMemberIdsAsync(characterId, cancellationToken).ConfigureAwait(false);

        if (members.Count == 0 || !members.Contains(characterId))
        {
            await _packetSender.SendErrorAsync(senderClient, "Vous n'etes pas membre de ce canal.", cancellationToken)
                .ConfigureAwait(false);
            return -1;
        }

        var delivered = 0;
        foreach (var memberId in members)
        {
            if (!_social.TryGetClientSession(memberId, out var client) || client is null)
            {
                continue;
            }

            await _packetSender.SendChatMessageAsync(
                    client,
                    channel,
                    from,
                    string.Empty,
                    message,
                    cancellationToken)
                .ConfigureAwait(false);
            delivered++;
        }

        return delivered;
    }
}
