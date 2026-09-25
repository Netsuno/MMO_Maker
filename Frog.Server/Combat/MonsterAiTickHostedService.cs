using System.Reflection;
using System.Text;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Network;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Combat;

/// <summary>
/// Pousse la poursuite via le paquet 9 (trailer de kind optionnel) et le coup via le paquet 18,
/// plus CombatState 50 / DeathNotify 63. Hello reste 11.
/// </summary>
public sealed class MonsterAiTickHostedService(
    MonsterAiService ai,
    ConnectionManager connections,
    ClientRegistry clients,
    PacketSender packets,
    GroundItemObserverNotifier ground,
    IConfiguration config,
    ILogger<MonsterAiTickHostedService> logger) : BackgroundService
{
    private readonly MonsterAiService _ai = ai;
    private readonly ConnectionManager _connections = connections;
    private readonly ClientRegistry _clients = clients;
    private readonly PacketSender _packets = packets;
    private readonly GroundItemObserverNotifier _ground = ground;
    private readonly IConfiguration _config = config;
    private readonly ILogger<MonsterAiTickHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TickInterval(), stoppingToken).ConfigureAwait(false);
                await BroadcastAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Monster AI tick failed.");
            }
        }
    }

    private async Task BroadcastAsync(CancellationToken cancellationToken)
    {
        var pulses = await _ai.TickAsync(DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        foreach (var pulse in pulses)
        {
            if (Encoding.UTF8.GetByteCount(pulse.Name) is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
            {
                continue;
            }

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

                await _packets.SendPositionUpdateAsync(
                    client,
                    pulse.Name,
                    pulse.MapId,
                    pulse.PixelX,
                    pulse.PixelY,
                    cancellationToken,
                    pulse.Kind).ConfigureAwait(false);
            }

            if (pulse.Strike is not { Success: true } strike || pulse.Victim is null)
            {
                continue;
            }

            if (!_clients.TryGet(pulse.Victim.Id, out var victimClient) || victimClient is null)
            {
                continue;
            }

            var ev = new DamageEvent(
                pulse.ActorId,
                pulse.Victim.CharacterGuid ?? pulse.Victim.Id,
                CombatTargetKind.Player,
                pulse.Victim.Username,
                strike.Damage,
                pulse.Victim.Hp,
                pulse.Victim.MaxHp,
                Hit: true,
                strike.TargetKilled,
                Ranged: pulse.Style == AttackStyle.Ranged);
            var incoming = pulse.Style == AttackStyle.Ranged
                ? "Subi une attaque à distance."
                : "Subi une attaque melee.";
            await _packets.SendMeleeAttackResultAsync(
                victimClient,
                true,
                pulse.Name,
                incoming,
                cancellationToken,
                ev).ConfigureAwait(false);
            await _packets.SendCombatStateAsync(
                victimClient,
                pulse.Victim.Level,
                pulse.Victim.Experience,
                pulse.Victim.Hp,
                pulse.Victim.MaxHp,
                pulse.Victim.Mp,
                pulse.Victim.MaxMp,
                pulse.Victim.Gold,
                pulse.Victim.IsDead,
                cancellationToken).ConfigureAwait(false);
            if (strike.TargetKilled)
            {
                await _packets.SendDeathNotifyAsync(victimClient, cancellationToken).ConfigureAwait(false);
            }

            if (strike.DroppedLoot.Count > 0)
            {
                await _ground.BroadcastAsync(pulse.MapId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Clé explicite d'abord. Sinon le processus <c>Frog.Server</c> joue l'IA ;
    /// le hôte de tests (testhost) reste silencieux tant qu'il ne force pas la clé.
    /// </summary>
    private bool IsEnabled()
    {
        var raw = _config[MonsterAiLimits.EnabledConfigKey];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return bool.TryParse(raw, out var parsed) && parsed;
        }

        var entry = Assembly.GetEntryAssembly()?.GetName().Name;
        return string.Equals(entry, "Frog.Server", StringComparison.OrdinalIgnoreCase);
    }

    private TimeSpan TickInterval()
    {
        var ms = _config.GetValue(MonsterAiLimits.TickIntervalConfigKey, MonsterAiLimits.TickIntervalMs);
        if (ms < 50)
        {
            ms = MonsterAiLimits.TickIntervalMs;
        }

        return TimeSpan.FromMilliseconds(ms);
    }
}
