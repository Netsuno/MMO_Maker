namespace Frog.Server.Gameplay;

/// <summary>
/// Table de butin simple (id d'objet publié + quantité).
/// Le défaut réutilise les objets Phase 7 (potion). Remplaçable au composition root.
/// La mort d'un joueur ne vide pas l'inventaire ni l'équipement : elle dépose
/// <see cref="PlayerDeath"/> seulement.
/// </summary>
public sealed record GroundLootStack(Guid ItemId, int Quantity);

public sealed class GroundLootTable
{
    public IReadOnlyList<GroundLootStack> MonsterFallback { get; init; } = Array.Empty<GroundLootStack>();

    public IReadOnlyDictionary<Guid, IReadOnlyList<GroundLootStack>> ByNpcId { get; init; }
        = new Dictionary<Guid, IReadOnlyList<GroundLootStack>>();

    public IReadOnlyList<GroundLootStack> PlayerDeath { get; init; } = Array.Empty<GroundLootStack>();

    public IReadOnlyList<GroundLootStack> ForMonster(Guid npcDefinitionId)
    {
        if (ByNpcId.TryGetValue(npcDefinitionId, out var specific) && specific.Count > 0)
        {
            return specific;
        }

        return MonsterFallback;
    }

    public static GroundLootTable CreateDefault()
    {
        var one = new GroundLootStack(Phase7ContentSeed.DefaultItemId, 1);
        var two = new GroundLootStack(Phase7ContentSeed.DefaultItemId, 1);
        return new GroundLootTable
        {
            MonsterFallback = [one, new GroundLootStack(Phase7ContentSeed.DefaultItemId, 2)],
            ByNpcId = new Dictionary<Guid, IReadOnlyList<GroundLootStack>>
            {
                [Phase7ContentSeed.DefaultMonsterId] = [one, two],
            },
            PlayerDeath = [one],
        };
    }
}
