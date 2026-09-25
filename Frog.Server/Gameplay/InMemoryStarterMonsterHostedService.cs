using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Microsoft.Extensions.Hosting;

namespace Frog.Server.Gameplay;

/// <summary>
/// Slime de départ sur la carte 1 (playtest / mémoire), pour voir le butin au sol
/// sans monde PostgreSQL publié. La prod charge les spawns publiés à la place.
/// </summary>
public sealed class InMemoryStarterMonsterHostedService(CombatGameplayService combat) : IHostedService
{
    private readonly CombatGameplayService _combat = combat;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(3, 1);
        await _combat.SpawnMonsterAsync(
            GameplayLimits.DefaultSpawnMapId,
            Phase7ContentSeed.DefaultMonsterId,
            pixelX,
            pixelY,
            cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
