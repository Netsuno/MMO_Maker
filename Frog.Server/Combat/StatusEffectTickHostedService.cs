using Frog.Core.Constants;
using Frog.Server.Network;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Combat;

/// <summary>
/// Tique les effets in-memory et pousse apply/tick/clear aux occupants via le paquet 18.
/// L'intervalle vaut <see cref="StatusEffectLimits.TickIntervalMs"/> sauf <c>Combat:StatusTickMs</c>.
/// </summary>
public sealed class StatusEffectTickHostedService(
    CombatMvpService combat,
    ConnectionManager connections,
    ClientRegistry clients,
    PacketSender packets,
    IConfiguration config,
    ILogger<StatusEffectTickHostedService> logger) : BackgroundService
{
    private readonly CombatMvpService _combat = combat;
    private readonly ConnectionManager _connections = connections;
    private readonly ClientRegistry _clients = clients;
    private readonly PacketSender _packets = packets;
    private readonly IConfiguration _config = config;
    private readonly ILogger<StatusEffectTickHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TickInterval(), stoppingToken).ConfigureAwait(false);
                await _combat.RunTurnAsync(() => BroadcastTickAsync(stoppingToken), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Status effect tick failed.");
            }
        }
    }

    private async Task BroadcastTickAsync(CancellationToken cancellationToken)
    {
        var pulses = _combat.Tick();
        foreach (var pulse in pulses)
        {
            foreach (var session in _connections.GetActiveSessions())
            {
                if (session.CurrentMapId != pulse.MapId)
                {
                    continue;
                }

                if (!_clients.TryGet(session.Id, out var client) || client is null)
                {
                    continue;
                }

                await _packets.SendMeleeAttackResultAsync(
                    client,
                    pulse.Damage.Hit,
                    pulse.TargetName,
                    pulse.Message,
                    cancellationToken,
                    pulse.Damage,
                    pulse.Status).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan TickInterval()
    {
        var ms = _config.GetValue(StatusEffectLimits.TickIntervalConfigKey, StatusEffectLimits.TickIntervalMs);
        if (ms < 50)
        {
            ms = StatusEffectLimits.TickIntervalMs;
        }

        return TimeSpan.FromMilliseconds(ms);
    }
}
