using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Gameplay;

/// <summary>
/// IA monstre in-memory : aggro, poursuite, attaque. Pas d'arbre de comportement.
/// Le rayon d'aggro compte en tuiles de contenu 48 px (<see cref="TileAssetMetrics"/>),
/// pas en <see cref="WorldMetrics.DefaultTileSizePixels"/>.
/// </summary>
public static class MonsterAiLimits
{
    public const int AggroRadiusTiles = 5;

    public const int AggroRadiusPixels = AggroRadiusTiles * TileAssetMetrics.TargetTileSizePixels;

    public const int LeashRadiusTiles = 8;

    public const int LeashRadiusPixels = LeashRadiusTiles * TileAssetMetrics.TargetTileSizePixels;

    public const int HomeWanderRadiusTiles = 2;

    public const int HomeWanderRadiusPixels = HomeWanderRadiusTiles * TileAssetMetrics.TargetTileSizePixels;

    /// <summary>Pas de poursuite : une tuile de contenu par tic.</summary>
    public const int ChaseStepPixels = TileAssetMetrics.TargetTileSizePixels;

    public const int WanderStepPixels = TileAssetMetrics.TargetTileSizePixels;

    public const int WanderIntervalMs = 1200;

    /// <summary>Hors du rayon d'aggro mais encore sous la laisse : délai avant de lâcher.</summary>
    public const int AggroTimeoutMs = 3500;

    public const int TickIntervalMs = 400;

    public const string TickIntervalConfigKey = "Combat:MonsterAiTickMs";

    /// <summary>
    /// <c>true</c> / <c>false</c> force le tic. Absent : actif seulement dans le processus <c>Frog.Server</c>.
    /// </summary>
    public const string EnabledConfigKey = "Combat:MonsterAiEnabled";
}

public enum MonsterAiOrder : byte
{
    Hold = 0,
    Wander = 1,
    Chase = 2,
    Attack = 3,
}

/// <summary>Mémoire par instance. Une seule cible principale.</summary>
public sealed class MonsterAiMemory
{
    public Guid? TargetId { get; set; }

    public DateTime LastInAggroUtc { get; set; }

    public DateTime LastAttackUtc { get; set; }

    public DateTime LastWanderUtc { get; set; }

    public int WanderSign { get; set; } = 1;

    public int HomeX { get; set; }

    public int HomeY { get; set; }

    public bool HomeSet { get; set; }
}

public readonly record struct MonsterAiActor(
    Guid Id,
    int MapId,
    int X,
    int Y,
    bool Alive);

public readonly record struct MonsterAiIntent(
    MonsterAiOrder Order,
    Guid? TargetId,
    int X,
    int Y,
    AttackStyle Style);

/// <summary>Décision pure : pas de socket, pas de table.</summary>
public static class MonsterAi
{
    /// <summary>Sprite monde (slime) plutôt qu'un autre joueur. Le fil reste le paquet 9.</summary>
    public static bool TracksAsMonsterSprite(CombatTargetKind kind)
        => kind is CombatTargetKind.Monster or CombatTargetKind.Dummy;

    public static MonsterAiIntent Decide(
        int mapId,
        int x,
        int y,
        int hp,
        MonsterAiMemory memory,
        IReadOnlyList<MonsterAiActor> actors,
        DateTime utcNow,
        int widthPx,
        int heightPx,
        Func<int, int, bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(blocked);

        if (!memory.HomeSet)
        {
            memory.HomeX = x;
            memory.HomeY = y;
            memory.HomeSet = true;
        }

        if (hp <= 0)
        {
            memory.TargetId = null;
            return new MonsterAiIntent(MonsterAiOrder.Hold, null, x, y, AttackStyle.Melee);
        }

        var width = widthPx > 0 ? widthPx : TileAssetMetrics.TargetTileSizePixels;
        var height = heightPx > 0 ? heightPx : TileAssetMetrics.TargetTileSizePixels;
        var target = ResolveTarget(mapId, x, y, memory, actors, utcNow);
        if (target is { } engaged)
        {
            return Engage(x, y, memory, engaged, utcNow, width, height, blocked);
        }

        return Wander(x, y, memory, utcNow, width, height, blocked);
    }

    private static MonsterAiActor? ResolveTarget(
        int mapId,
        int x,
        int y,
        MonsterAiMemory memory,
        IReadOnlyList<MonsterAiActor> actors,
        DateTime utcNow)
    {
        if (memory.TargetId is Guid current)
        {
            var kept = Find(actors, current);
            if (kept is null || !kept.Value.Alive || kept.Value.MapId != mapId)
            {
                memory.TargetId = null;
            }
            else
            {
                var dist = WorldMetrics.DistanceSquaredPixels(x, y, kept.Value.X, kept.Value.Y);
                if (dist > (long)MonsterAiLimits.LeashRadiusPixels * MonsterAiLimits.LeashRadiusPixels)
                {
                    memory.TargetId = null;
                }
                else if (dist <= (long)MonsterAiLimits.AggroRadiusPixels * MonsterAiLimits.AggroRadiusPixels)
                {
                    memory.LastInAggroUtc = utcNow;
                }
                else if (memory.LastInAggroUtc != default
                         && (utcNow - memory.LastInAggroUtc).TotalMilliseconds >= MonsterAiLimits.AggroTimeoutMs)
                {
                    memory.TargetId = null;
                }
            }
        }

        if (memory.TargetId is Guid still)
        {
            return Find(actors, still);
        }

        MonsterAiActor? best = null;
        var bestDist = long.MaxValue;
        foreach (var actor in actors)
        {
            if (!actor.Alive || actor.MapId != mapId)
            {
                continue;
            }

            var dist = WorldMetrics.DistanceSquaredPixels(x, y, actor.X, actor.Y);
            if (dist <= (long)MonsterAiLimits.AggroRadiusPixels * MonsterAiLimits.AggroRadiusPixels && dist < bestDist)
            {
                best = actor;
                bestDist = dist;
            }
        }

        if (best is { } acquired)
        {
            memory.TargetId = acquired.Id;
            memory.LastInAggroUtc = utcNow;
            return acquired;
        }

        return null;
    }

    private static MonsterAiActor? Find(IReadOnlyList<MonsterAiActor> actors, Guid id)
    {
        foreach (var actor in actors)
        {
            if (actor.Id == id)
            {
                return actor;
            }
        }

        return null;
    }

    private static MonsterAiIntent Engage(
        int x,
        int y,
        MonsterAiMemory memory,
        MonsterAiActor target,
        DateTime utcNow,
        int widthPx,
        int heightPx,
        Func<int, int, bool> blocked)
    {
        var dist = WorldMetrics.DistanceSquaredPixels(x, y, target.X, target.Y);
        var melee = CombatFormulas.BasicAttackRangePixels;
        var ranged = CombatFormulas.RangedAttackRangePixels;
        var ready = memory.LastAttackUtc == default
                    || (utcNow - memory.LastAttackUtc).TotalMilliseconds >= CombatFormulas.BasicAttackCooldownMs;
        if (dist <= (long)melee * melee)
        {
            var order = ready ? MonsterAiOrder.Attack : MonsterAiOrder.Hold;
            return new MonsterAiIntent(order, target.Id, x, y, AttackStyle.Melee);
        }

        if (dist <= (long)ranged * ranged)
        {
            var order = ready ? MonsterAiOrder.Attack : MonsterAiOrder.Hold;
            return new MonsterAiIntent(order, target.Id, x, y, AttackStyle.Ranged);
        }

        if (TryStep(x, y, target.X, target.Y, MonsterAiLimits.ChaseStepPixels, widthPx, heightPx, blocked, out var nx, out var ny))
        {
            return new MonsterAiIntent(MonsterAiOrder.Chase, target.Id, nx, ny, AttackStyle.Melee);
        }

        return new MonsterAiIntent(MonsterAiOrder.Hold, target.Id, x, y, AttackStyle.Melee);
    }

    private static MonsterAiIntent Wander(
        int x,
        int y,
        MonsterAiMemory memory,
        DateTime utcNow,
        int widthPx,
        int heightPx,
        Func<int, int, bool> blocked)
    {
        if (memory.LastWanderUtc != default
            && (utcNow - memory.LastWanderUtc).TotalMilliseconds < MonsterAiLimits.WanderIntervalMs)
        {
            return new MonsterAiIntent(MonsterAiOrder.Hold, null, x, y, AttackStyle.Melee);
        }

        memory.LastWanderUtc = utcNow;
        var sign = memory.WanderSign == 0 ? 1 : memory.WanderSign;
        if (TryWander(x, y, sign, memory, widthPx, heightPx, blocked, out var nx, out var ny))
        {
            return new MonsterAiIntent(MonsterAiOrder.Wander, null, nx, ny, AttackStyle.Melee);
        }

        memory.WanderSign = -sign;
        if (TryWander(x, y, memory.WanderSign, memory, widthPx, heightPx, blocked, out nx, out ny))
        {
            return new MonsterAiIntent(MonsterAiOrder.Wander, null, nx, ny, AttackStyle.Melee);
        }

        return new MonsterAiIntent(MonsterAiOrder.Hold, null, x, y, AttackStyle.Melee);
    }

    private static bool TryWander(
        int x,
        int y,
        int sign,
        MonsterAiMemory memory,
        int widthPx,
        int heightPx,
        Func<int, int, bool> blocked,
        out int nx,
        out int ny)
    {
        nx = x;
        ny = y;
        var step = sign * MonsterAiLimits.WanderStepPixels;
        var candidate = x + step;
        var homeDx = candidate - memory.HomeX;
        var homeDy = y - memory.HomeY;
        var homeDist = (long)homeDx * homeDx + (long)homeDy * homeDy;
        var leash = (long)MonsterAiLimits.HomeWanderRadiusPixels * MonsterAiLimits.HomeWanderRadiusPixels;
        if (homeDist > leash)
        {
            return false;
        }

        return TryCommit(candidate, y, widthPx, heightPx, blocked, out nx, out ny);
    }

    private static bool TryStep(
        int x,
        int y,
        int targetX,
        int targetY,
        int maxStep,
        int widthPx,
        int heightPx,
        Func<int, int, bool> blocked,
        out int nx,
        out int ny)
    {
        var sx = AxisStep(x, targetX, maxStep);
        var sy = AxisStep(y, targetY, maxStep);
        if (sx == 0 && sy == 0)
        {
            nx = x;
            ny = y;
            return false;
        }

        if (TryCommit(x + sx, y + sy, widthPx, heightPx, blocked, out nx, out ny) && (nx != x || ny != y))
        {
            return true;
        }

        if (sx != 0 && TryCommit(x + sx, y, widthPx, heightPx, blocked, out nx, out ny) && (nx != x || ny != y))
        {
            return true;
        }

        if (sy != 0 && TryCommit(x, y + sy, widthPx, heightPx, blocked, out nx, out ny) && (nx != x || ny != y))
        {
            return true;
        }

        nx = x;
        ny = y;
        return false;
    }

    private static int AxisStep(int from, int to, int maxStep)
    {
        var delta = to - from;
        if (delta == 0)
        {
            return 0;
        }

        var step = Math.Min(Math.Abs(delta), Math.Max(1, maxStep));
        return Math.Sign(delta) * step;
    }

    private static bool TryCommit(
        int x,
        int y,
        int widthPx,
        int heightPx,
        Func<int, int, bool> blocked,
        out int nx,
        out int ny)
    {
        nx = Math.Clamp(x, 0, Math.Max(0, widthPx - 1));
        ny = Math.Clamp(y, 0, Math.Max(0, heightPx - 1));
        if (blocked(nx, ny))
        {
            return false;
        }

        return true;
    }
}
