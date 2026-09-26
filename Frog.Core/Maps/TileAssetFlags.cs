namespace Frog.Core.Maps;

/// <summary>
/// Drapeaux d’une tuile 48×48, sémantique du mode tileset RPG Maker VX.
/// Le passage est à quatre directions : une direction cochée laisse sortir (et laisse entrer par l’opposé).
/// ○ = les quatre directions ouvertes. × = les quatre fermées.
/// La priorité va de 0 (sous le personnage) à 5 (au-dessus).
/// Le buisson signale le bas du sprite à translucider. Le comptoir prolonge l’interaction d’une case.
/// Les dégâts sont un sol blessant (drapeau, pas le montant).
/// Ces drapeaux ne changent pas le hash des pixels : ils vivent dans la meta du <see cref="TileAssetId"/>.
/// </summary>
public readonly record struct TileAssetFlags
{
    public const byte MaxPriority = 5;

    public bool PassageNorth { get; init; }

    public bool PassageEast { get; init; }

    public bool PassageSouth { get; init; }

    public bool PassageWest { get; init; }

    public byte Priority { get; init; }

    public bool Bush { get; init; }

    public bool Counter { get; init; }

    public bool Damage { get; init; }

    /// <summary>○ — quatre directions ouvertes, priorité 0, sans buisson, comptoir ni dégâts.</summary>
    public static TileAssetFlags Default { get; } = new()
    {
        PassageNorth = true,
        PassageEast = true,
        PassageSouth = true,
        PassageWest = true,
    };

    /// <summary>× — les quatre directions bloquent.</summary>
    public static TileAssetFlags Blocked { get; } = new();

    public bool IsDefault =>
        PassageNorth && PassageEast && PassageSouth && PassageWest
        && Priority == 0 && !Bush && !Counter && !Damage;

    public bool BlocksAllPassage =>
        !PassageNorth && !PassageEast && !PassageSouth && !PassageWest;

    public bool Allows(TilePassageDirection direction) => direction switch
    {
        TilePassageDirection.North => PassageNorth,
        TilePassageDirection.East => PassageEast,
        TilePassageDirection.South => PassageSouth,
        TilePassageDirection.West => PassageWest,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    public TileAssetFlags WithPassage(TilePassageDirection direction, bool allowed) => direction switch
    {
        TilePassageDirection.North => this with { PassageNorth = allowed },
        TilePassageDirection.East => this with { PassageEast = allowed },
        TilePassageDirection.South => this with { PassageSouth = allowed },
        TilePassageDirection.West => this with { PassageWest = allowed },
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    public TileAssetFlags WithPriority(int priority)
    {
        if ((uint)priority > MaxPriority)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                $"La priorité est entre 0 et {MaxPriority}.");
        }

        return this with { Priority = (byte)priority };
    }

    public TileAssetFlags WithBush(bool bush) => this with { Bush = bush };

    public TileAssetFlags WithCounter(bool counter) => this with { Counter = counter };

    public TileAssetFlags WithDamage(bool damage) => this with { Damage = damage };

    /// <summary>
    /// Un pas VX : on quitte <paramref name="from"/> dans <paramref name="direction"/>
    /// et on entre dans <paramref name="to"/> par la direction opposée.
    /// </summary>
    public static bool CanStep(TileAssetFlags from, TileAssetFlags to, TilePassageDirection direction)
        => from.Allows(direction) && to.Allows(TilePassage.Opposite(direction));
}
