using Frog.Persistence.PostgreSql.Entities.Auth;

namespace Frog.Persistence.PostgreSql.Entities.Ops;

/// <summary>Journal mute/unmute/kick/ban/unban — table <c>ops.moderation_events</c>.</summary>
public sealed class ModerationEventEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public Guid ActorAccountId { get; set; }

    public AccountEntity ActorAccount { get; set; } = null!;

    public Guid TargetAccountId { get; set; }

    public AccountEntity TargetAccount { get; set; } = null!;

    public string Action { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? DetailsJson { get; set; }
}
