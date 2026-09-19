using Frog.Core.Enums;

namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class GuildEntity
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string Motd { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<GuildMemberEntity> Members { get; set; } = new List<GuildMemberEntity>();

    public ICollection<GuildInviteEntity> Invites { get; set; } = new List<GuildInviteEntity>();
}

public sealed class GuildMemberEntity
{
    public Guid GuildId { get; set; }

    public Guid CharacterId { get; set; }

    public byte Role { get; set; }

    public DateTimeOffset JoinedAtUtc { get; set; }

    public GuildEntity Guild { get; set; } = null!;

    public CharacterEntity Character { get; set; } = null!;

    public GuildRole RoleEnum
    {
        get => (GuildRole)Role;
        set => Role = (byte)value;
    }
}

public sealed class GuildInviteEntity
{
    public Guid Id { get; set; }

    public Guid GuildId { get; set; }

    public Guid FromCharacterId { get; set; }

    public Guid ToCharacterId { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public GuildEntity Guild { get; set; } = null!;
}

public sealed class FriendshipEntity
{
    public Guid Id { get; set; }

    public Guid CharacterA { get; set; }

    public Guid CharacterB { get; set; }

    public Guid RequestedBy { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CharacterBlockEntity
{
    public Guid BlockerCharacterId { get; set; }

    public Guid BlockedCharacterId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
