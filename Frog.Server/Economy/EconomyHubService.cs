using Frog.Application.Social;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Models;

namespace Frog.Server.Economy;

/// <summary>
/// Stub in-memory HdV / courrier / coffre. Query seulement ; listes vides (coffre : 8 slots vides si guilde).
/// TODO persistance PostgreSQL — voir docs/progress/auction-mail-guildbank/STATUS.md.
/// </summary>
public sealed class EconomyHubService
{
    private readonly ISocialStore _social;

    public EconomyHubService(ISocialStore social)
    {
        _social = social;
    }

    public async Task<(EconomyHubResultWire Result, EconomyHubSnapshotWire? Snapshot)> ExecuteAsync(
        Session session,
        EconomyHubKind kind,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        _ = extra;
        if (session.CharacterGuid is not Guid actorId || actorId == Guid.Empty)
        {
            return (Fail(kind, action, requestId, "Personnage actif requis."), null);
        }

        if (!EconomyHubWire.IsKnownKind((byte)kind))
        {
            return (Fail(kind, action, requestId, "Action inconnue."), null);
        }

        if (!EconomyHubWire.IsKnownAction(action))
        {
            return (Fail(kind, action, requestId, "Action inconnue."), null);
        }

        var snapshot = kind switch
        {
            EconomyHubKind.Auction => Empty(EconomyHubKind.Auction, Guid.Empty),
            EconomyHubKind.Mail => Empty(EconomyHubKind.Mail, actorId),
            EconomyHubKind.GuildBank => await BuildGuildBankAsync(actorId, cancellationToken).ConfigureAwait(false),
            _ => Empty(kind, Guid.Empty)
        };

        var message = kind switch
        {
            EconomyHubKind.Auction => "Hotel des ventes : aucune enchere (MVP).",
            EconomyHubKind.Mail => "Courrier : boite vide (MVP).",
            EconomyHubKind.GuildBank => snapshot.SubjectId == Guid.Empty
                ? "Pas de guilde — coffre indisponible."
                : "Coffre de guilde : emplacements vides (MVP).",
            _ => "Ok."
        };

        var result = new EconomyHubResultWire(kind, action, requestId, true, message, snapshot.SubjectId);
        return (result, snapshot);
    }

    private async Task<EconomyHubSnapshotWire> BuildGuildBankAsync(Guid characterId, CancellationToken cancellationToken)
    {
        var member = await _social.FindGuildMemberAsync(characterId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Empty(EconomyHubKind.GuildBank, Guid.Empty);
        }

        var slots = new EconomyHubEntryWire[EconomyHubLimits.GuildBankSlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new EconomyHubEntryWire(Guid.Empty, Guid.Empty, 0, i, string.Empty);
        }

        return new EconomyHubSnapshotWire(EconomyHubKind.GuildBank, member.GuildId, slots);
    }

    private static EconomyHubSnapshotWire Empty(EconomyHubKind kind, Guid subjectId)
        => new(kind, subjectId, Array.Empty<EconomyHubEntryWire>());

    private static EconomyHubResultWire Fail(EconomyHubKind kind, byte action, Guid requestId, string message)
        => new(kind, action, requestId, false, message, Guid.Empty);
}
