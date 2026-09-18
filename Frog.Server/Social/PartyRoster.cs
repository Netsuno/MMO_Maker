using Frog.Core.Enums;

namespace Frog.Server.Social;

public sealed record PartyMemberState(Guid CharacterId, bool IsLeader);

public sealed record PartyInviteState(
    Guid Id,
    Guid PartyId,
    Guid FromCharacterId,
    Guid ToCharacterId,
    DateTimeOffset ExpiresAtUtc);

public sealed class PartyState
{
    public required Guid Id { get; init; }
    public required Guid LeaderId { get; set; }
    public List<PartyMemberState> Members { get; } = [];
}

/// <summary>Groupes temporaires — mémoire processus, dissous au redémarrage.</summary>
public sealed class PartyRoster
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PartyState> _parties = new();
    private readonly Dictionary<Guid, Guid> _partyByCharacter = new();
    private readonly Dictionary<Guid, PartyInviteState> _invites = new();

    public PartyState? FindByCharacter(Guid characterId)
    {
        lock (_gate)
        {
            return _partyByCharacter.TryGetValue(characterId, out var id) && _parties.TryGetValue(id, out var p)
                ? Clone(p)
                : null;
        }
    }

    public PartyState? FindById(Guid partyId)
    {
        lock (_gate)
        {
            return _parties.TryGetValue(partyId, out var p) ? Clone(p) : null;
        }
    }

    public IReadOnlyList<PartyInviteState> PendingInvitesFor(Guid characterId)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            return _invites.Values.Where(i => i.FromCharacterId == characterId || i.ToCharacterId == characterId).ToArray();
        }
    }

    public int CountPendingOutgoing(Guid characterId)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            return _invites.Values.Count(i => i.FromCharacterId == characterId);
        }
    }

    public int CountPendingIncoming(Guid characterId)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            return _invites.Values.Count(i => i.ToCharacterId == characterId);
        }
    }

    public PartyInviteResult Invite(
        Guid fromCharacterId,
        Guid toCharacterId,
        int maxMembers,
        TimeSpan ttl,
        DateTimeOffset now)
    {
        lock (_gate)
        {
            ExpireLocked(now);
            if (fromCharacterId == toCharacterId)
            {
                return PartyInviteResult.Fail("Cible invalide.");
            }

            if (_partyByCharacter.ContainsKey(toCharacterId))
            {
                return PartyInviteResult.Fail("Ce personnage est deja dans un groupe.");
            }

            PartyState party;
            if (_partyByCharacter.TryGetValue(fromCharacterId, out var existingId)
                && _parties.TryGetValue(existingId, out var found))
            {
                party = found;
                if (party.LeaderId != fromCharacterId)
                {
                    return PartyInviteResult.Fail("Seul le chef peut inviter.");
                }

                if (party.Members.Count >= maxMembers)
                {
                    return PartyInviteResult.Fail("Groupe complet.");
                }
            }
            else
            {
                party = new PartyState { Id = Guid.NewGuid(), LeaderId = fromCharacterId };
                party.Members.Add(new PartyMemberState(fromCharacterId, true));
                _parties[party.Id] = party;
                _partyByCharacter[fromCharacterId] = party.Id;
            }

            foreach (var inv in _invites.Values)
            {
                if (inv.PartyId == party.Id && inv.ToCharacterId == toCharacterId)
                {
                    return PartyInviteResult.Fail("Invitation deja en attente.");
                }
            }

            var invite = new PartyInviteState(
                Guid.NewGuid(),
                party.Id,
                fromCharacterId,
                toCharacterId,
                now + ttl);
            _invites[invite.Id] = invite;
            return new PartyInviteResult(true, "Invitation envoyee.", party.Id, invite.Id, toCharacterId);
        }
    }

    public PartyInviteResult Accept(Guid characterId, Guid inviteOrPartyId, int maxMembers, DateTimeOffset now)
    {
        lock (_gate)
        {
            ExpireLocked(now);
            if (_partyByCharacter.ContainsKey(characterId))
            {
                return PartyInviteResult.Fail("Deja dans un groupe.");
            }

            if (!TryGetPendingInviteLocked(inviteOrPartyId, characterId, out var invite))
            {
                return PartyInviteResult.Fail("Invitation invalide ou expiree.");
            }

            if (!_parties.TryGetValue(invite.PartyId, out var party))
            {
                _invites.Remove(invite.Id);
                return PartyInviteResult.Fail("Le groupe n'existe plus.");
            }

            if (party.Members.Count >= maxMembers)
            {
                return PartyInviteResult.Fail("Groupe complet.");
            }

            party.Members.Add(new PartyMemberState(characterId, false));
            _partyByCharacter[characterId] = party.Id;
            _invites.Remove(invite.Id);
            foreach (var other in _invites.Values.Where(i => i.ToCharacterId == characterId).ToArray())
            {
                _invites.Remove(other.Id);
            }

            return new PartyInviteResult(true, "Vous avez rejoint le groupe.", party.Id, invite.Id, characterId);
        }
    }

    public PartyInviteResult Decline(Guid characterId, Guid inviteOrPartyId, DateTimeOffset now)
    {
        lock (_gate)
        {
            ExpireLocked(now);
            if (!TryGetPendingInviteLocked(inviteOrPartyId, characterId, out var invite))
            {
                return PartyInviteResult.Fail("Invitation invalide ou expiree.");
            }

            _invites.Remove(invite.Id);
            return new PartyInviteResult(true, "Invitation refusee.", invite.PartyId, invite.Id, characterId);
        }
    }

    public PartyInviteResult Cancel(Guid fromCharacterId, Guid inviteOrPartyId, DateTimeOffset now)
    {
        lock (_gate)
        {
            ExpireLocked(now);
            PartyInviteState? found = null;
            foreach (var inv in _invites.Values)
            {
                if (inv.FromCharacterId == fromCharacterId
                    && (inv.Id == inviteOrPartyId || inv.PartyId == inviteOrPartyId || inv.ToCharacterId == inviteOrPartyId))
                {
                    found = inv;
                    break;
                }
            }

            if (found is null)
            {
                return PartyInviteResult.Fail("Invitation invalide ou expiree.");
            }

            _invites.Remove(found.Id);
            return new PartyInviteResult(true, "Invitation annulee.", found.PartyId, found.Id, found.ToCharacterId);
        }
    }

    public PartyInviteResult Leave(Guid characterId)
    {
        lock (_gate)
        {
            if (!_partyByCharacter.TryGetValue(characterId, out var partyId) || !_parties.TryGetValue(partyId, out var party))
            {
                return PartyInviteResult.Fail("Vous n'etes pas dans un groupe.");
            }

            if (party.LeaderId == characterId && party.Members.Count > 1)
            {
                return PartyInviteResult.Fail("Transferez le role de chef avant de quitter.");
            }

            RemoveMemberLocked(party, characterId);
            return new PartyInviteResult(true, "Vous avez quitte le groupe.", partyId, Guid.Empty, characterId);
        }
    }

    public PartyInviteResult Kick(Guid actorId, Guid targetId)
    {
        lock (_gate)
        {
            if (!_partyByCharacter.TryGetValue(actorId, out var partyId) || !_parties.TryGetValue(partyId, out var party))
            {
                return PartyInviteResult.Fail("Vous n'etes pas dans un groupe.");
            }

            if (party.LeaderId != actorId)
            {
                return PartyInviteResult.Fail("Seul le chef peut expulser.");
            }

            if (actorId == targetId)
            {
                return PartyInviteResult.Fail("Impossible de s'expulser soi-meme.");
            }

            if (!_partyByCharacter.TryGetValue(targetId, out var targetParty) || targetParty != partyId)
            {
                return PartyInviteResult.Fail("Cible introuvable dans le groupe.");
            }

            RemoveMemberLocked(party, targetId);
            return new PartyInviteResult(true, "Membre expulse.", partyId, Guid.Empty, targetId);
        }
    }

    public PartyInviteResult TransferLeader(Guid actorId, Guid targetId)
    {
        lock (_gate)
        {
            if (!_partyByCharacter.TryGetValue(actorId, out var partyId) || !_parties.TryGetValue(partyId, out var party))
            {
                return PartyInviteResult.Fail("Vous n'etes pas dans un groupe.");
            }

            if (party.LeaderId != actorId)
            {
                return PartyInviteResult.Fail("Seul le chef peut transferer le role.");
            }

            if (actorId == targetId)
            {
                return PartyInviteResult.Fail("Cible invalide.");
            }

            if (!_partyByCharacter.TryGetValue(targetId, out var targetParty) || targetParty != partyId)
            {
                return PartyInviteResult.Fail("Cible introuvable dans le groupe.");
            }

            party.LeaderId = targetId;
            for (var i = 0; i < party.Members.Count; i++)
            {
                var m = party.Members[i];
                party.Members[i] = m with { IsLeader = m.CharacterId == targetId };
            }

            return new PartyInviteResult(true, "Chef transfere.", partyId, Guid.Empty, targetId);
        }
    }

    public PartyInviteResult Disband(Guid actorId, bool confirm)
    {
        lock (_gate)
        {
            if (!confirm)
            {
                return PartyInviteResult.Fail("Confirmation requise.");
            }

            if (!_partyByCharacter.TryGetValue(actorId, out var partyId) || !_parties.TryGetValue(partyId, out var party))
            {
                return PartyInviteResult.Fail("Vous n'etes pas dans un groupe.");
            }

            if (party.LeaderId != actorId)
            {
                return PartyInviteResult.Fail("Seul le chef peut dissoudre le groupe.");
            }

            var memberIds = party.Members.Select(m => m.CharacterId).ToArray();
            foreach (var id in memberIds)
            {
                _partyByCharacter.Remove(id);
            }

            foreach (var inv in _invites.Values.Where(i => i.PartyId == partyId).ToArray())
            {
                _invites.Remove(inv.Id);
            }

            _parties.Remove(partyId);
            return new PartyInviteResult(true, "Groupe dissous.", partyId, Guid.Empty, actorId);
        }
    }

    public IReadOnlyList<Guid> MemberIds(Guid partyId)
    {
        lock (_gate)
        {
            return _parties.TryGetValue(partyId, out var p)
                ? p.Members.Select(m => m.CharacterId).ToArray()
                : Array.Empty<Guid>();
        }
    }

    private bool TryGetPendingInviteLocked(Guid inviteOrPartyId, Guid toCharacterId, out PartyInviteState invite)
    {
        foreach (var inv in _invites.Values)
        {
            if (inv.ToCharacterId == toCharacterId
                && (inv.Id == inviteOrPartyId || inv.PartyId == inviteOrPartyId))
            {
                invite = inv;
                return true;
            }
        }

        invite = null!;
        return false;
    }

    private void RemoveMemberLocked(PartyState party, Guid characterId)
    {
        party.Members.RemoveAll(m => m.CharacterId == characterId);
        _partyByCharacter.Remove(characterId);
        if (party.Members.Count == 0)
        {
            foreach (var inv in _invites.Values.Where(i => i.PartyId == party.Id).ToArray())
            {
                _invites.Remove(inv.Id);
            }

            _parties.Remove(party.Id);
        }
    }

    private void ExpireLocked(DateTimeOffset now)
    {
        foreach (var inv in _invites.Values.Where(i => i.ExpiresAtUtc <= now).ToArray())
        {
            _invites.Remove(inv.Id);
        }
    }

    private static PartyState Clone(PartyState p)
    {
        var copy = new PartyState { Id = p.Id, LeaderId = p.LeaderId };
        copy.Members.AddRange(p.Members);
        return copy;
    }
}

public readonly record struct PartyInviteResult(
    bool Success,
    string Message,
    Guid PartyId,
    Guid InviteId,
    Guid OtherId)
{
    public static PartyInviteResult Fail(string message) => new(false, message, Guid.Empty, Guid.Empty, Guid.Empty);
}
