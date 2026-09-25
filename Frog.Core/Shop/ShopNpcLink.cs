using System.Text.RegularExpressions;
using Frog.Core.Protocol;

namespace Frog.Core.Shop;

public enum ShopAnchorKind
{
    Event = 0,
    Npc = 1,
}

/// <summary>Point déjà connu du client (événement de carte ou PNJ de test) qui peut ouvrir une boutique.</summary>
public sealed record ShopNpcAnchor(
    int TileX,
    int TileY,
    ShopAnchorKind Kind,
    string? ScriptKey,
    string? DisplayName,
    string? Notes);

/// <summary>
/// Lien PNJ → boutique sans nouveau paquet. <c>shop:&lt;guid&gt;</c> dans les notes ou le scriptKey,
/// ou le nom du PNJ publié avec <see cref="PublishedNpcWireEntry.ShopId"/>.
/// Un événement compte sur la tuile du joueur ; un PNJ compte à une tuile (Chebyshev).
/// </summary>
public static class ShopNpcLink
{
    public const int NpcTalkDistanceTiles = 1;

    private static readonly Regex ShopKey = new(
        @"shop\s*[:=]\s*([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryExtractShopId(string? text, out Guid shopId)
    {
        shopId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = ShopKey.Match(text);
        return match.Success && Guid.TryParse(match.Groups[1].Value, out shopId) && shopId != Guid.Empty;
    }

    public static bool TryResolve(
        int playerTileX,
        int playerTileY,
        IReadOnlyList<ShopNpcAnchor>? anchors,
        PublishedCatalogWire? catalog,
        out Guid shopId,
        out string label)
    {
        shopId = Guid.Empty;
        label = string.Empty;
        if (anchors is null || anchors.Count == 0 || catalog is null)
        {
            return false;
        }

        var known = new HashSet<Guid>();
        var byShopName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var shop in catalog.Shops)
        {
            if (!Guid.TryParse(shop.Id, out var id) || id == Guid.Empty)
            {
                continue;
            }

            known.Add(id);
            if (!string.IsNullOrWhiteSpace(shop.Name))
            {
                byShopName.TryAdd(shop.Name.Trim(), id);
            }
        }

        if (known.Count == 0)
        {
            return false;
        }

        var byNpcName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in catalog.Npcs)
        {
            if (string.IsNullOrWhiteSpace(npc.Name))
            {
                continue;
            }

            if (Guid.TryParse(npc.ShopId, out var linked) && known.Contains(linked))
            {
                byNpcName[npc.Name.Trim()] = linked;
            }
        }

        var bestDistance = int.MaxValue;
        var found = false;
        foreach (var anchor in anchors)
        {
            var distance = Math.Max(Math.Abs(anchor.TileX - playerTileX), Math.Abs(anchor.TileY - playerTileY));
            var max = anchor.Kind == ShopAnchorKind.Npc ? NpcTalkDistanceTiles : 0;
            if (distance > max)
            {
                continue;
            }

            if (!TryShopForAnchor(anchor, known, byShopName, byNpcName, out var id, out var source))
            {
                continue;
            }

            if (found && distance >= bestDistance)
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            shopId = id;
            label = source;
        }

        return found;
    }

    private static bool TryShopForAnchor(
        ShopNpcAnchor anchor,
        HashSet<Guid> known,
        Dictionary<string, Guid> byShopName,
        Dictionary<string, Guid> byNpcName,
        out Guid shopId,
        out string label)
    {
        shopId = Guid.Empty;
        label = string.Empty;
        if (TryExtractShopId(anchor.ScriptKey, out var fromScript) && known.Contains(fromScript))
        {
            shopId = fromScript;
            label = string.IsNullOrWhiteSpace(anchor.DisplayName) ? "Boutique" : anchor.DisplayName.Trim();
            return true;
        }

        if (TryExtractShopId(anchor.Notes, out var fromNotes) && known.Contains(fromNotes))
        {
            shopId = fromNotes;
            label = string.IsNullOrWhiteSpace(anchor.DisplayName) ? "PNJ" : anchor.DisplayName.Trim();
            return true;
        }

        var name = anchor.DisplayName?.Trim();
        if (!string.IsNullOrEmpty(name) && byNpcName.TryGetValue(name, out var fromNpc))
        {
            shopId = fromNpc;
            label = name;
            return true;
        }

        if (!string.IsNullOrEmpty(name) && byShopName.TryGetValue(name, out var fromShop))
        {
            shopId = fromShop;
            label = name;
            return true;
        }

        return false;
    }
}
