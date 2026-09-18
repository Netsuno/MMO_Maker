namespace Frog.Application.Identity;

/// <summary>
/// Sanctions opérationnelles (mute/ban) — schéma <c>ops</c>, pas un bit sur <c>auth.accounts</c>.
/// Kick n'est pas une sanction persistée : journal <c>ops.moderation_events</c> seulement.
/// </summary>
public interface IAccountSanctionStore
{
    Task<bool> HasActiveAsync(
        Guid accountId,
        string kind,
        CancellationToken cancellationToken = default);

    Task<AccountSanctionRecord?> GetActiveAsync(
        Guid accountId,
        string kind,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        Guid accountId,
        string kind,
        Guid actorAccountId,
        string reason,
        DateTimeOffset? expiresAtUtc = null,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid accountId,
        string kind,
        Guid actorAccountId,
        string reason,
        CancellationToken cancellationToken = default);

    Task RecordEventAsync(
        Guid actorAccountId,
        Guid targetAccountId,
        string action,
        string reason,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModerationEventRecord>> ListEventsForTargetAsync(
        Guid targetAccountId,
        CancellationToken cancellationToken = default);
}

public static class SanctionKinds
{
    public const string Mute = "mute";
    public const string Ban = "ban";

    public static bool IsPersistedKind(string? kind)
        => kind is Mute or Ban;
}

public static class ModerationEventActions
{
    public const string Mute = "mute";
    public const string Unmute = "unmute";
    public const string Kick = "kick";
    public const string Ban = "ban";
    public const string Unban = "unban";
}

public static class ModerationMessages
{
    public const string NotOperator = "Action reservee aux operateurs.";
    public const string TargetNotFound = "Compte introuvable.";
    public const string InvalidInput = "Payload moderation invalide.";
    public const string AuthRequired = "Authentification requise.";
    public const string Muted = "Compte reduit au silence.";
    public const string Banned = "Compte banni.";
    public const string Kicked = "Session fermee.";
    public const string MuteApplied = "Mute applique.";
    public const string MuteLifted = "Mute leve.";
    public const string KickApplied = "Kick applique.";
    public const string BanApplied = "Ban applique.";
    public const string BanLifted = "Ban leve.";
}

public sealed record AccountSanctionRecord(
    Guid Id,
    Guid AccountId,
    string Kind,
    string Reason,
    Guid ActorAccountId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record ModerationEventRecord(
    Guid Id,
    DateTimeOffset AtUtc,
    Guid ActorAccountId,
    Guid TargetAccountId,
    string Action,
    string Reason,
    string? DetailsJson);

public static class AccountSanctionStoreExtensions
{
    public static Task<bool> HasActiveMuteAsync(
        this IAccountSanctionStore store,
        Guid accountId,
        CancellationToken cancellationToken = default)
        => store.HasActiveAsync(accountId, SanctionKinds.Mute, cancellationToken);

    public static Task<bool> HasActiveBanAsync(
        this IAccountSanctionStore store,
        Guid accountId,
        CancellationToken cancellationToken = default)
        => store.HasActiveAsync(accountId, SanctionKinds.Ban, cancellationToken);
}
