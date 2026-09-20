using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Core.Social;

/// <summary>
/// État social client (opcodes 80–83 déjà gelés). Pas de second backend :
/// snapshots / events / results existants seulement.
/// </summary>
public sealed class ClientSocialRoster
{
    /// <summary>Ami accepté — encodage <c>SocialService.PushFriendSnapshotAsync</c>.</summary>
    public const byte FriendRoleAccepted = 1;

    /// <summary>Demande sortante (nous avons envoyé).</summary>
    public const byte FriendRoleOutgoing = 2;

    /// <summary>Demande entrante (à accepter / refuser).</summary>
    public const byte FriendRoleIncoming = 3;

    /// <summary>Chef de groupe — encodage snapshot Party.</summary>
    public const byte PartyRoleLeader = 1;

    private readonly List<SocialPendingInvite> _pending = [];

    public SocialSnapshotWire? Party { get; private set; }

    public SocialSnapshotWire? Guild { get; private set; }

    public SocialSnapshotWire? Friends { get; private set; }

    public SocialSnapshotWire? Blocks { get; private set; }

    public string StatusLine { get; private set; } = string.Empty;

    public IReadOnlyList<SocialPendingInvite> PendingInvites => _pending;

    public bool HasParty =>
        Party is { Members.Count: > 0 } && Party.Value.SubjectId != Guid.Empty;

    public bool HasGuild =>
        Guild is { Members.Count: > 0 } && Guild.Value.SubjectId != Guid.Empty;

    public void ApplySnapshot(SocialSnapshotWire snapshot)
    {
        switch (snapshot.Kind)
        {
            case SocialKind.Party:
                Party = snapshot;
                break;
            case SocialKind.Guild:
                Guild = snapshot;
                break;
            case SocialKind.Friend:
                Friends = snapshot;
                break;
            case SocialKind.Block:
                Blocks = snapshot;
                break;
        }

        PruneResolvedInvites(snapshot);
        var label = KindLabel(snapshot.Kind);
        StatusLine = snapshot.Members.Count == 0
            ? $"{label} : liste vide."
            : $"{label} : {snapshot.Members.Count} entrée(s).";
        if (!string.IsNullOrWhiteSpace(snapshot.Motd))
        {
            StatusLine += " — " + snapshot.Motd;
        }
    }

    public void ApplyEvent(SocialEventWire ev)
    {
        StatusLine = string.IsNullOrWhiteSpace(ev.Message) ? KindLabel(ev.Kind) : ev.Message;
        switch (ev.Type)
        {
            case SocialEventType.InviteReceived:
                UpsertPending(new SocialPendingInvite(
                    ev.Kind,
                    ev.SubjectId,
                    ev.ActorId,
                    ev.OtherId,
                    ev.Message));
                break;
            case SocialEventType.InviteExpired:
            case SocialEventType.InviteDeclined:
            case SocialEventType.InviteCancelled:
                RemovePending(ev.Kind, ev.SubjectId, ev.ActorId);
                break;
            case SocialEventType.Disbanded:
                RemovePending(ev.Kind, ev.SubjectId, ev.ActorId);
                if (ev.Kind == SocialKind.Party)
                {
                    Party = EmptySnapshot(SocialKind.Party);
                }
                else if (ev.Kind == SocialKind.Guild)
                {
                    Guild = EmptySnapshot(SocialKind.Guild);
                }

                break;
            case SocialEventType.PresenceOnline:
            case SocialEventType.PresenceOffline:
                ApplyPresence(ev.ActorId, ev.Type == SocialEventType.PresenceOnline);
                break;
            case SocialEventType.MotdChanged:
                if (Guild is { } guild && (guild.SubjectId == ev.SubjectId || ev.SubjectId == Guid.Empty))
                {
                    Guild = guild with { Motd = ev.Message };
                }

                break;
        }
    }

    public void ApplyResult(SocialResultWire result)
    {
        StatusLine = result.Success
            ? (string.IsNullOrWhiteSpace(result.Message) ? "OK." : result.Message)
            : (string.IsNullOrWhiteSpace(result.Message) ? "Refusé." : result.Message);
        if (result.Success
            && result.Kind is SocialKind.Party or SocialKind.Guild or SocialKind.Friend
            && result.Action is (byte)PartyAction.Decline or (byte)PartyAction.Accept
                or (byte)GuildAction.Decline or (byte)GuildAction.Accept
                or (byte)FriendAction.Decline or (byte)FriendAction.Accept)
        {
            RemovePending(result.Kind, result.SubjectId, result.OtherId);
        }
    }

    public IReadOnlyList<SocialListItem> BuildRows(SocialKind kind)
    {
        var rows = new List<SocialListItem>();
        foreach (var pending in _pending)
        {
            if (pending.Kind != kind)
            {
                continue;
            }

            rows.Add(pending.ToListItem());
        }

        var snap = SnapshotOf(kind);
        if (snap is null)
        {
            return rows;
        }

        foreach (var member in snap.Value.Members)
        {
            if (rows.Exists(r => !r.IsPendingInvite && r.CharacterId == member.CharacterId))
            {
                continue;
            }

            if (kind == SocialKind.Friend
                && rows.Exists(r => r.IsPendingInvite && r.CharacterId == member.CharacterId))
            {
                continue;
            }

            rows.Add(ToMemberItem(kind, member, snap.Value.LeaderId));
        }

        return rows;
    }

    public string EmptyHint(SocialKind kind)
    {
        if (BuildRows(kind).Count > 0)
        {
            return string.Empty;
        }

        return kind switch
        {
            SocialKind.Friend => "Aucun ami. Ajoute un personnage par son identifiant (Guid).",
            SocialKind.Party => "Aucun groupe. Invite un joueur ou accepte une invitation.",
            SocialKind.Guild => "Aucune guilde. Crée-en une ou accepte une invitation.",
            _ => "Aucune entrée."
        };
    }

    public string MotdText(SocialKind kind) => kind switch
    {
        SocialKind.Guild when Guild is { Motd: { Length: > 0 } motd } => motd,
        SocialKind.Party when Party is { Motd: { Length: > 0 } motd } => motd,
        _ => string.Empty
    };

    public static string KindLabel(SocialKind kind) => kind switch
    {
        SocialKind.Party => "Groupe",
        SocialKind.Guild => "Guilde",
        SocialKind.Friend => "Amis",
        SocialKind.Block => "Blocage",
        _ => "Social"
    };

    public static string FormatMember(SocialKind kind, SocialMemberWire member, Guid leaderId = default)
    {
        var presence = member.Online ? "en ligne" : "hors ligne";
        var role = kind switch
        {
            SocialKind.Friend => member.Role switch
            {
                FriendRoleOutgoing => "demande envoyée",
                FriendRoleIncoming => "demande reçue",
                _ => "ami"
            },
            SocialKind.Party => member.Role == PartyRoleLeader || member.CharacterId == leaderId
                ? "chef"
                : "membre",
            SocialKind.Guild => ((GuildRole)member.Role) switch
            {
                GuildRole.Leader => "chef",
                GuildRole.Officer => "officier",
                _ => "membre"
            },
            _ => "membre"
        };
        var name = string.IsNullOrWhiteSpace(member.DisplayName)
            ? member.CharacterId.ToString("D")
            : member.DisplayName;
        return $"{name} — {role} ({presence})";
    }

    public static bool TryParseTargetGuid(string? text, out Guid id, out string error)
    {
        id = Guid.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || !Guid.TryParse(text.Trim(), out id) || id == Guid.Empty)
        {
            error = "Identifiant (Guid) invalide.";
            return false;
        }

        return true;
    }

    public static SocialClientRequest Invite(SocialKind kind, Guid target) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.Invite, SocialWire.BuildGuidPayload(target)),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.Invite, SocialWire.BuildGuidPayload(target)),
        SocialKind.Friend => new(SocialKind.Friend, (byte)FriendAction.Request, SocialWire.BuildGuidPayload(target)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest Accept(SocialKind kind, Guid subjectOrOther) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.Accept, SocialWire.BuildGuidPayload(subjectOrOther)),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.Accept, SocialWire.BuildGuidPayload(subjectOrOther)),
        SocialKind.Friend => new(SocialKind.Friend, (byte)FriendAction.Accept, SocialWire.BuildGuidPayload(subjectOrOther)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest Decline(SocialKind kind, Guid subjectOrOther) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.Decline, SocialWire.BuildGuidPayload(subjectOrOther)),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.Decline, SocialWire.BuildGuidPayload(subjectOrOther)),
        SocialKind.Friend => new(SocialKind.Friend, (byte)FriendAction.Decline, SocialWire.BuildGuidPayload(subjectOrOther)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest Leave(SocialKind kind) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.Leave, []),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.Leave, []),
        SocialKind.Friend => throw new ArgumentOutOfRangeException(nameof(kind)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest Kick(SocialKind kind, Guid target) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.Kick, SocialWire.BuildGuidPayload(target)),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.Kick, SocialWire.BuildGuidPayload(target)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest TransferLeader(SocialKind kind, Guid target) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.TransferLeader, SocialWire.BuildGuidPayload(target)),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.TransferLeader, SocialWire.BuildGuidPayload(target)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest Disband(SocialKind kind) => kind switch
    {
        SocialKind.Party => new(SocialKind.Party, (byte)PartyAction.Disband, SocialWire.BuildConfirmPayload(true)),
        SocialKind.Guild => new(SocialKind.Guild, (byte)GuildAction.Disband, SocialWire.BuildConfirmPayload(true)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static SocialClientRequest FriendRemove(Guid target) =>
        new(SocialKind.Friend, (byte)FriendAction.Remove, SocialWire.BuildGuidPayload(target));

    public static bool TryGuildCreate(string? name, out SocialClientRequest request, out string error)
    {
        request = default;
        error = string.Empty;
        var normalized = SocialWire.NormalizeGuildName(name);
        if (normalized.Length == 0)
        {
            error = "Nom de guilde requis.";
            return false;
        }

        try
        {
            request = new SocialClientRequest(
                SocialKind.Guild,
                (byte)GuildAction.Create,
                SocialWire.BuildUtf8Payload(normalized, SocialProtocolLimits.MaxGuildNameUtf8Bytes));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "Nom de guilde trop long.";
            return false;
        }
    }

    public static bool TryGuildSetMotd(string? motd, out SocialClientRequest request, out string error)
    {
        request = default;
        error = string.Empty;
        try
        {
            request = new SocialClientRequest(
                SocialKind.Guild,
                (byte)GuildAction.SetMotd,
                SocialWire.BuildUtf8Payload(motd ?? string.Empty, SocialProtocolLimits.MaxMotdUtf8Bytes));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "Message trop long.";
            return false;
        }
    }

    /// <summary>
    /// Cible d'Accept/Decline : ami = l'autre personnage ; groupe/guilde = id sujet (party/guild).
    /// </summary>
    public static Guid AcceptTarget(SocialListItem item) =>
        item.Kind == SocialKind.Friend ? item.CharacterId : item.SubjectId;

    private SocialSnapshotWire? SnapshotOf(SocialKind kind) => kind switch
    {
        SocialKind.Party => Party,
        SocialKind.Guild => Guild,
        SocialKind.Friend => Friends,
        SocialKind.Block => Blocks,
        _ => null
    };

    private void UpsertPending(SocialPendingInvite invite)
    {
        _pending.RemoveAll(p =>
            p.Kind == invite.Kind
            && p.SubjectId == invite.SubjectId
            && p.ActorId == invite.ActorId);
        _pending.Add(invite);
    }

    private void RemovePending(SocialKind kind, Guid subjectId, Guid actorOrOther)
    {
        _pending.RemoveAll(p =>
            p.Kind == kind
            && (p.SubjectId == subjectId || subjectId == Guid.Empty)
            && (actorOrOther == Guid.Empty
                || p.ActorId == actorOrOther
                || p.OtherId == actorOrOther));
    }

    private void PruneResolvedInvites(SocialSnapshotWire snapshot)
    {
        _pending.RemoveAll(p =>
        {
            if (p.Kind != snapshot.Kind)
            {
                return false;
            }

            if (snapshot.Kind == SocialKind.Friend)
            {
                return snapshot.Members.Any(m =>
                    m.CharacterId == p.ActorId || m.CharacterId == p.OtherId);
            }

            return snapshot.SubjectId != Guid.Empty
                   && snapshot.SubjectId == p.SubjectId
                   && snapshot.Members.Count > 0;
        });
    }

    private void ApplyPresence(Guid characterId, bool online)
    {
        Party = WithPresence(Party, characterId, online);
        Guild = WithPresence(Guild, characterId, online);
        Friends = WithPresence(Friends, characterId, online);
    }

    private static SocialSnapshotWire? WithPresence(SocialSnapshotWire? snap, Guid characterId, bool online)
    {
        if (snap is null)
        {
            return null;
        }

        var members = snap.Value.Members.Select(m =>
            m.CharacterId == characterId ? m with { Online = online } : m).ToArray();
        return snap.Value with { Members = members };
    }

    private static SocialSnapshotWire EmptySnapshot(SocialKind kind) =>
        new(kind, Guid.Empty, Guid.Empty, string.Empty, Array.Empty<SocialMemberWire>());

    private static SocialListItem ToMemberItem(SocialKind kind, SocialMemberWire member, Guid leaderId) =>
        new(
            kind,
            member.CharacterId,
            Guid.Empty,
            member.Role,
            member.Online,
            IsPendingInvite: false,
            member.DisplayName,
            FormatMember(kind, member, leaderId));
}

public readonly record struct SocialPendingInvite(
    SocialKind Kind,
    Guid SubjectId,
    Guid ActorId,
    Guid OtherId,
    string Message)
{
    public SocialListItem ToListItem()
    {
        var who = ActorId == Guid.Empty ? "Invitation" : ActorId.ToString("D")[..8];
        var text = string.IsNullOrWhiteSpace(Message)
            ? $"Invitation {ClientSocialRoster.KindLabel(Kind)} — {who}"
            : Message;
        return new SocialListItem(
            Kind,
            ActorId,
            SubjectId,
            Role: ClientSocialRoster.FriendRoleIncoming,
            Online: true,
            IsPendingInvite: true,
            DisplayName: who,
            Text: "⏳ " + text);
    }
}

public readonly record struct SocialListItem(
    SocialKind Kind,
    Guid CharacterId,
    Guid SubjectId,
    byte Role,
    bool Online,
    bool IsPendingInvite,
    string DisplayName,
    string Text);

public readonly record struct SocialClientRequest(SocialKind Kind, byte Action, byte[] Extra);
