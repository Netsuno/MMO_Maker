using System.Text.Json;
using Frog.Application.Content;
using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

public sealed class PublishedCatalogService(
    IPublishedClassCatalog classes,
    IPublishedItemCatalog items,
    IPublishedSpellCatalog spells,
    IPublishedShopCatalog shops,
    IPublishedNpcCatalog npcs)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<PublishedCatalogWire> BuildAsync(CancellationToken cancellationToken = default)
    {
        var classList = await classes.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var itemList = await items.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var spellList = await spells.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var shopList = await shops.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var npcList = await npcs.ListPublishedAsync(cancellationToken).ConfigureAwait(false);

        return new PublishedCatalogWire
        {
            Classes = classList.Select(c => new PublishedClassWireEntry
            {
                Id = c.Id.ToString("D"),
                Name = c.Name,
                Description = c.Description ?? string.Empty,
            }).ToArray(),
            Items = itemList.Select(i => new PublishedItemWireEntry
            {
                Id = i.Id.ToString("D"),
                Name = i.Name,
                Type = i.Kind.ToString(),
                Stackable = i.MaxStack > 1,
            }).ToArray(),
            Spells = spellList.Select(s => new PublishedSpellWireEntry
            {
                Id = s.Id.ToString("D"),
                Name = s.Name,
                MpCost = s.ManaCost,
            }).ToArray(),
            Shops = shopList.Select(s => new PublishedShopWireEntry
            {
                Id = s.Id.ToString("D"),
                Name = s.Name,
                ItemIds = s.Listings.Select(l => l.ItemId.ToString("D")).ToArray(),
            }).ToArray(),
            Npcs = npcList.Select(n => new PublishedNpcWireEntry
            {
                Id = n.Id.ToString("D"),
                Name = n.Name,
            }).ToArray(),
        };
    }

    public async Task<string> BuildJsonAsync(CancellationToken cancellationToken = default)
    {
        var wire = await BuildAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(wire, JsonOptions);
    }
}
