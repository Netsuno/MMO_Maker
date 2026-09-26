using System.Text;

namespace Frog.Core.Events;

/// <summary>
/// Ouverture de boutique depuis une commande <c>open_shop</c>.
/// Le client réutilise le jeton <c>shop:&lt;guid&gt;</c> déjà compris par
/// <see cref="Frog.Core.Shop.ShopNpcLink"/>, porté par <c>InteractResult</c> (Hello 11, pas de nouvel opcode).
/// </summary>
public static class MapEventShopOpen
{
    public const int MaxInteractUtf8Bytes = 255;

    public const string UnavailableMessage = "Boutique indisponible.";

    public static string FormatInteractMessage(Guid? shopId, string? showText)
    {
        var body = (showText ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (shopId is not Guid id || id == Guid.Empty)
        {
            return body;
        }

        var head = "shop:" + id.ToString("D");
        if (TryTakeInteractMessage(body, out var existing, out var rest) && existing == id)
        {
            body = rest;
        }

        if (body.Length == 0)
        {
            return head;
        }

        var prefix = head + "\n";
        var room = MaxInteractUtf8Bytes - Encoding.UTF8.GetByteCount(prefix);
        return prefix + ClipUtf8(body, room);
    }

    /// <summary>
    /// Retire un jeton <c>shop:&lt;guid&gt;</c> lorsqu'il occupe seul la première ligne.
    /// </summary>
    public static bool TryTakeInteractMessage(string? message, out Guid shopId, out string remainder)
    {
        shopId = Guid.Empty;
        remainder = message ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart();
        var lineEnd = normalized.IndexOf('\n');
        var first = (lineEnd < 0 ? normalized : normalized[..lineEnd]).Trim();
        const string prefix = "shop:";
        if (!first.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var raw = first[prefix.Length..].Trim();
        if (raw.Length != 36 || !Guid.TryParse(raw, out shopId) || shopId == Guid.Empty)
        {
            shopId = Guid.Empty;
            return false;
        }

        remainder = lineEnd < 0 ? string.Empty : normalized[(lineEnd + 1)..].Trim();
        return true;
    }

    private static string ClipUtf8(string text, int maxBytes)
    {
        if (maxBytes <= 0 || text.Length == 0)
        {
            return string.Empty;
        }

        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (Encoding.UTF8.GetByteCount(text.AsSpan(0, mid)) <= maxBytes)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return text[..low];
    }
}
