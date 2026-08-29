using Frog.Core.Enums;
using Frog.Server.Models;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher
{
    private async Task DispatchPhase8Async(
        ClientSession clientSession,
        PacketId packetId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        switch (packetId)
        {
            case PacketId.DialogueChoiceRequest:
                await _phase8.HandleDialogueChoiceRequestAsync(clientSession, session, payload, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case PacketId.QuestTurnInRequest:
                await _phase8.HandleQuestTurnInRequestAsync(clientSession, session, payload, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case PacketId.CraftRequest:
                await _phase8.HandleCraftRequestAsync(clientSession, session, payload, cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private async Task SendPhase8SnapshotsAsync(
        ClientSession clientSession,
        Session session,
        CancellationToken cancellationToken)
    {
        await _phase8.SendQuestJournalAsync(clientSession, session, cancellationToken).ConfigureAwait(false);
        await _phase8.SendEnvironmentStateAsync(clientSession, session, cancellationToken).ConfigureAwait(false);
        await _phase8.TryFireAutorunMapEventsAsync(clientSession, session, cancellationToken).ConfigureAwait(false);
    }
}
