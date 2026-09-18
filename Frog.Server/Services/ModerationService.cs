using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Network;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>
/// Mute / kick / ban serveur-autoritaire. Toujours
/// <see cref="IOperatorDirectory.IsOperatorAsync"/> avant d'agir — jamais un drapeau client.
/// </summary>
public sealed class ModerationService(
    IOperatorDirectory operators,
    IAccountRepository accounts,
    IAccountSanctionStore sanctions,
    IAuthSessionRepository authSessions,
    ConnectionManager connections,
    ClientRegistry clients,
    PacketSender packetSender,
    SessionTeardown sessionTeardown,
    ILogger<ModerationService> logger)
{
    private readonly IOperatorDirectory _operators = operators;
    private readonly IAccountRepository _accounts = accounts;
    private readonly IAccountSanctionStore _sanctions = sanctions;
    private readonly IAuthSessionRepository _authSessions = authSessions;
    private readonly ConnectionManager _connections = connections;
    private readonly ClientRegistry _clients = clients;
    private readonly PacketSender _packetSender = packetSender;
    private readonly SessionTeardown _sessionTeardown = sessionTeardown;
    private readonly ILogger<ModerationService> _logger = logger;

    public async Task<ModerationCommandResult> ExecuteAsync(
        Guid actorAccountId,
        ModerationAction action,
        string targetUsername,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (actorAccountId == Guid.Empty)
        {
            return ModerationCommandResult.Denied(ModerationMessages.AuthRequired);
        }

        if (!await _operators.IsOperatorAsync(actorAccountId, cancellationToken).ConfigureAwait(false))
        {
            return ModerationCommandResult.Denied(ModerationMessages.NotOperator);
        }

        if (string.IsNullOrWhiteSpace(targetUsername)
            || !AccountInputRules.IsValidUsername(targetUsername))
        {
            return ModerationCommandResult.Denied(ModerationMessages.InvalidInput);
        }

        var target = await _accounts.FindByUsernameAsync(targetUsername.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
        {
            return ModerationCommandResult.Denied(ModerationMessages.TargetNotFound);
        }

        var reasonText = string.IsNullOrWhiteSpace(reason) ? ModerateWire.DefaultReason : reason.Trim();

        switch (action)
        {
            case ModerationAction.Mute:
                await _sanctions.ApplyAsync(
                    target.Id,
                    SanctionKinds.Mute,
                    actorAccountId,
                    reasonText,
                    expiresAtUtc: null,
                    cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Operator {Actor} muted {Target}", actorAccountId, target.Username);
                return ModerationCommandResult.Applied(ModerationMessages.MuteApplied);

            case ModerationAction.Unmute:
                await _sanctions.RevokeAsync(
                    target.Id,
                    SanctionKinds.Mute,
                    actorAccountId,
                    reasonText,
                    cancellationToken).ConfigureAwait(false);
                return ModerationCommandResult.Applied(ModerationMessages.MuteLifted);

            case ModerationAction.Kick:
                await _sanctions.RecordEventAsync(
                    actorAccountId,
                    target.Id,
                    ModerationEventActions.Kick,
                    reasonText,
                    detailsJson: null,
                    cancellationToken).ConfigureAwait(false);
                await DropLiveSessionAsync(target.Username, ModerationMessages.Kicked, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("Operator {Actor} kicked {Target}", actorAccountId, target.Username);
                return ModerationCommandResult.Applied(ModerationMessages.KickApplied);

            case ModerationAction.Ban:
                await _sanctions.ApplyAsync(
                    target.Id,
                    SanctionKinds.Ban,
                    actorAccountId,
                    reasonText,
                    expiresAtUtc: null,
                    cancellationToken).ConfigureAwait(false);
                await _authSessions.RevokeAllForAccountAsync(target.Id, cancellationToken).ConfigureAwait(false);
                await DropLiveSessionAsync(target.Username, ModerationMessages.Banned, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("Operator {Actor} banned {Target}", actorAccountId, target.Username);
                return ModerationCommandResult.Applied(ModerationMessages.BanApplied);

            case ModerationAction.Unban:
                await _sanctions.RevokeAsync(
                    target.Id,
                    SanctionKinds.Ban,
                    actorAccountId,
                    reasonText,
                    cancellationToken).ConfigureAwait(false);
                return ModerationCommandResult.Applied(ModerationMessages.BanLifted);

            default:
                return ModerationCommandResult.Denied(ModerationMessages.InvalidInput);
        }
    }

    private async Task DropLiveSessionAsync(string username, string message, CancellationToken cancellationToken)
    {
        if (!_connections.TryGetSessionByUsername(username, out var session) || session is null)
        {
            return;
        }

        if (_clients.TryGet(session.Id, out var client) && client is not null)
        {
            try
            {
                await _packetSender.SendErrorAsync(client, message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not notify {Username} before drop.", username);
            }
        }

        await _sessionTeardown.TearDownAsync(session.Id, SessionTeardownOptions.KickBan, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record ModerationCommandResult(bool Success, string Message)
{
    public static ModerationCommandResult Applied(string message) => new(true, message);

    public static ModerationCommandResult Denied(string message) => new(false, message);
}
