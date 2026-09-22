using System.Text.Json;
using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Application.Prefabs;
using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

public sealed class PublishedCatalogService(
    IPublishedClassCatalog classes,
    IPublishedItemCatalog items,
    IPublishedSpellCatalog spells,
    IPublishedShopCatalog shops,
    IPublishedNpcCatalog npcs,
    IPublishedRecipeCatalog recipes,
    IPublishedTilesetCatalog? tilesets = null,
    IPublishedTilesetImageSource? tilesetImages = null,
    IPublishedPrefabCatalog? prefabs = null,
    IPublishedWorldCatalog? world = null)
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
        var recipeList = await recipes.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var tilesetList = tilesets is null
            ? Array.Empty<PublishedTilesetWireEntry>()
            : (await tilesets.ListPublishedAsync(cancellationToken).ConfigureAwait(false))
            .Select(t => ToTilesetWire(t, tilesetImages))
            .Where(t => t.PaletteId > 0)
            .ToArray();
        var prefabBundle = prefabs is null
            ? new PublishedPrefabCatalogBundle()
            : await prefabs.LoadPublishedAsync(cancellationToken).ConfigureAwait(false);

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
            Recipes = recipeList.Select(r => new PublishedRecipeWireEntry
            {
                Id = r.Id.ToString("D"),
                Name = r.Name,
            }).ToArray(),
            Tilesets = tilesetList,
            Prefabs = prefabBundle.Prefabs,
            PrefabMaps = AnnotatePrefabMaps(prefabBundle.PrefabMaps, world),
        };
    }

    private static IReadOnlyList<PublishedPrefabMapWireEntry> AnnotatePrefabMaps(
        IReadOnlyList<PublishedPrefabMapWireEntry> maps,
        IPublishedWorldCatalog? world)
    {
        if (world is null || maps.Count == 0)
        {
            return maps;
        }

        var list = new List<PublishedPrefabMapWireEntry>(maps.Count);
        foreach (var entry in maps)
        {
            // Garder l’id déjà porté par le catalogue (liaison PG) si le cache monde est encore froid.
            int? runtime = entry.RuntimeMapId is > 0 ? entry.RuntimeMapId : null;
            if (MapPrefabPackage.TryParseMapId(entry.MapId, out var guid)
                && world.TryGetRuntimeMapId(guid, out var runtimeId)
                && runtimeId != 0)
            {
                runtime = runtimeId;
            }

            list.Add(new PublishedPrefabMapWireEntry
            {
                MapId = entry.MapId,
                MapName = entry.MapName,
                RuntimeMapId = runtime,
                Placements = entry.Placements,
            });
        }

        return list;
    }

    private static PublishedTilesetWireEntry ToTilesetWire(
        Frog.Core.Models.TilesetDefinition definition,
        IPublishedTilesetImageSource? images)
    {
        string? png = null;
        if (images is not null && images.TryReadPng(definition, out var bytes) && bytes.Length > 0)
        {
            png = Convert.ToBase64String(bytes);
        }

        var paletteId = definition.EditorPaletteId
                        ?? TilesetPaletteAlignment.ResolveClientTilesetId(definition, Array.Empty<int>())
                        ?? 0;

        return new PublishedTilesetWireEntry
        {
            Id = definition.Id.ToString("D"),
            Name = definition.Name,
            PaletteId = paletteId,
            LogicalPath = definition.LogicalPath,
            Sha256Hex = definition.Sha256Hex,
            TileSizePixels = definition.TileSizePixels,
            WidthPixels = definition.WidthPixels,
            HeightPixels = definition.HeightPixels,
            PngBase64 = png,
        };
    }

    public async Task<string> BuildJsonAsync(CancellationToken cancellationToken = default)
    {
        var wire = await BuildAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(wire, JsonOptions);
    }
}
