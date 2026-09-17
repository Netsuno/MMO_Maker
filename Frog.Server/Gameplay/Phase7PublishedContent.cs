using System.Collections.Concurrent;
using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Catalogues publiés en mémoire pour playtest / E2E sans éditeur PostgreSQL.</summary>
public sealed class Phase7PublishedContent
    : IPublishedClassCatalog,
        IPublishedItemCatalog,
        IPublishedSpellCatalog,
        IPublishedNpcCatalog,
        IPublishedShopCatalog
{
    private readonly ConcurrentDictionary<Guid, ClassDefinition> _classes = new();
    private readonly ConcurrentDictionary<Guid, ItemDefinition> _items = new();
    private readonly ConcurrentDictionary<Guid, SpellDefinition> _spells = new();
    private readonly ConcurrentDictionary<Guid, NpcDefinition> _npcs = new();
    private readonly ConcurrentDictionary<Guid, ShopDefinition> _shops = new();

    public Phase7PublishedContent()
    {
        Publish(Phase7ContentSeed.CreateDefaultClass());
        Publish(Phase7ContentSeed.CreateDefaultSpell());
        Publish(Phase7ContentSeed.CreateDefaultConsumable());
        Publish(Phase7ContentSeed.CreateDefaultWeapon());
        Publish(Phase7ContentSeed.CreateDefaultArmor());
        Publish(Phase7ContentSeed.CreateDefaultMonster());
        Publish(Phase7ContentSeed.CreateDefaultShop());
    }

    public void Publish(ClassDefinition definition) => _classes[definition.Id] = definition;

    public void Publish(ItemDefinition definition) => _items[definition.Id] = definition;

    public void Publish(SpellDefinition definition) => _spells[definition.Id] = definition;

    public void Publish(NpcDefinition definition) => _npcs[definition.Id] = definition;

    public void Publish(ShopDefinition definition) => _shops[definition.Id] = definition;

    public ClassDefinition? GetClass(Guid id) => _classes.TryGetValue(id, out var d) ? d : null;

    public ItemDefinition? GetItem(Guid id) => _items.TryGetValue(id, out var d) ? d : null;

    public SpellDefinition? GetSpell(Guid id) => _spells.TryGetValue(id, out var d) ? d : null;

    public NpcDefinition? GetNpc(Guid id) => _npcs.TryGetValue(id, out var d) ? d : null;

    public ShopDefinition? GetShop(Guid id) => _shops.TryGetValue(id, out var d) ? d : null;

    Task<IReadOnlyList<ClassDefinition>> IPublishedClassCatalog.ListPublishedAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ClassDefinition>>(_classes.Values.OrderBy(c => c.Name).ToArray());

    Task<IReadOnlyList<ItemDefinition>> IPublishedItemCatalog.ListPublishedAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ItemDefinition>>(_items.Values.OrderBy(i => i.Name).ToArray());

    Task<ItemDefinition?> IPublishedItemCatalog.LoadPublishedByIdAsync(Guid itemId, CancellationToken cancellationToken)
        => Task.FromResult(GetItem(itemId));

    Task<IReadOnlyList<SpellDefinition>> IPublishedSpellCatalog.ListPublishedAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SpellDefinition>>(_spells.Values.OrderBy(s => s.Name).ToArray());

    Task<IReadOnlyList<NpcDefinition>> IPublishedNpcCatalog.ListPublishedAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NpcDefinition>>(_npcs.Values.OrderBy(n => n.Name).ToArray());

    Task<IReadOnlyList<ShopDefinition>> IPublishedShopCatalog.ListPublishedAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ShopDefinition>>(_shops.Values.OrderBy(s => s.Name).ToArray());
}
