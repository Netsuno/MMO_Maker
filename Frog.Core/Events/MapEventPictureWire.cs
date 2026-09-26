using System.Globalization;
using System.Text;

namespace Frog.Core.Events;

/// <summary>
/// Transporte les images d'événement dans le message <c>InteractResult</c> (opcode 32).
/// Même schéma que <c>shop:&lt;guid&gt;</c> : lignes préfixes, Hello 11, pas de nouvel opcode.
/// </summary>
public static class MapEventPictureWire
{
    public const int MaxInteractUtf8Bytes = MapEventShopOpen.MaxInteractUtf8Bytes;

    public static string FormatLine(MapEventPictureOp op)
    {
        if (op.Erase)
        {
            return "pic:erase:" + op.PictureId.ToString(CultureInfo.InvariantCulture);
        }

        return FormattableString.Invariant(
            $"pic:show:{op.PictureId}:{op.X}:{op.Y}:{op.Opacity}:{op.Blend}:{op.Asset}");
    }

    /// <summary>
    /// Préfixe les opérations image, puis le corps boutique / texte déjà compris par le client.
    /// Sans opération, le message reste celui de <see cref="MapEventShopOpen"/> ou le texte seul.
    /// </summary>
    public static string Compose(
        IReadOnlyList<MapEventPictureOp>? ops,
        Guid? shopId,
        string? showText,
        string? fallbackMessage)
    {
        if (ops is null || ops.Count == 0)
        {
            return Bare(shopId, showText, fallbackMessage);
        }

        var lines = new string[ops.Count];
        for (var i = 0; i < ops.Count; i++)
        {
            lines[i] = FormatLine(ops[i]);
        }

        return ComposeLines(lines, shopId, showText, fallbackMessage);
    }

    /// <summary>
    /// Préfixe des lignes déjà formatées (<c>pic:</c>, <c>fade:</c>, <c>tint:</c>), puis le corps.
    /// Une liste vide laisse le message boutique / texte tel quel.
    /// </summary>
    public static string ComposeLines(
        IReadOnlyList<string>? lines,
        Guid? shopId,
        string? showText,
        string? fallbackMessage)
    {
        if (lines is null || lines.Count == 0)
        {
            return Bare(shopId, showText, fallbackMessage);
        }

        var kept = new List<string>(lines.Count);
        var pictureBytes = 0;
        foreach (var line in lines)
        {
            var lineBytes = Encoding.UTF8.GetByteCount(line);
            var separator = kept.Count == 0 ? 0 : 1;
            if (pictureBytes + separator + lineBytes > MaxInteractUtf8Bytes)
            {
                break;
            }

            kept.Add(line);
            pictureBytes += separator + lineBytes;
        }

        var block = string.Join('\n', kept);
        var room = MaxInteractUtf8Bytes - pictureBytes - (block.Length > 0 ? 1 : 0);
        var body = ClipBody(shopId, showText, fallbackMessage, Math.Max(0, room));
        if (block.Length == 0)
        {
            return body;
        }

        if (body.Length == 0)
        {
            return block;
        }

        return block + "\n" + body;
    }

    /// <summary>
    /// Retire les lignes <c>pic:</c> en tête. Le reste peut encore porter <c>shop:</c> puis le texte.
    /// </summary>
    public static bool TryTakeInteractMessage(
        string? message,
        out IReadOnlyList<MapEventPictureOp> ops,
        out string remainder)
    {
        ops = Array.Empty<MapEventPictureOp>();
        remainder = message ?? string.Empty;
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var parsed = new List<MapEventPictureOp>();
        var index = 0;
        for (; index < lines.Length; index++)
        {
            if (!TryParseLine(lines[index].Trim(), out var op))
            {
                break;
            }

            parsed.Add(op);
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        ops = parsed;
        remainder = string.Join('\n', lines.Skip(index)).Trim();
        return true;
    }

    public static bool TryParseLine(string line, out MapEventPictureOp op)
    {
        op = MapEventPictureOp.ForErase(0);
        if (line.StartsWith("pic:erase:", StringComparison.Ordinal))
        {
            if (!int.TryParse(line["pic:erase:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || id is < MapEventPicture.MinId or > MapEventPicture.MaxId)
            {
                return false;
            }

            op = MapEventPictureOp.ForErase(id);
            return true;
        }

        const string prefix = "pic:show:";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = line.Split(':', 8);
        if (parts.Length != 8
            || parts[0] != "pic"
            || parts[1] != "show"
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var pictureId)
            || !int.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(parts[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var opacity)
            || !MapEventPicture.TryCanonicalBlend(parts[6], out var blend)
            || !MapEventPicture.TryNormalizeAsset(parts[7], out var asset, out _))
        {
            return false;
        }

        if (pictureId is < MapEventPicture.MinId or > MapEventPicture.MaxId
            || x is < MapEventPicture.MinCoord or > MapEventPicture.MaxCoord
            || y is < MapEventPicture.MinCoord or > MapEventPicture.MaxCoord
            || opacity is < MapEventPicture.MinOpacity or > MapEventPicture.MaxOpacity)
        {
            return false;
        }

        op = MapEventPictureOp.ForShow(pictureId, asset, x, y, opacity, blend);
        return true;
    }

    private static string Bare(Guid? shopId, string? showText, string? fallbackMessage)
    {
        if (shopId is Guid id && id != Guid.Empty)
        {
            return MapEventShopOpen.FormatInteractMessage(id, showText);
        }

        return showText ?? fallbackMessage ?? string.Empty;
    }

    private static string ClipBody(Guid? shopId, string? showText, string? fallbackMessage, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        var full = Bare(shopId, showText, fallbackMessage);
        if (Encoding.UTF8.GetByteCount(full) <= maxBytes)
        {
            return full;
        }

        if (MapEventShopOpen.TryTakeInteractMessage(full, out var shop, out var rest))
        {
            var head = "shop:" + shop.ToString("D");
            var headBytes = Encoding.UTF8.GetByteCount(head);
            if (headBytes > maxBytes)
            {
                return string.Empty;
            }

            if (rest.Length == 0 || headBytes == maxBytes)
            {
                return head;
            }

            var prefix = head + "\n";
            var clipped = ClipUtf8(rest, maxBytes - Encoding.UTF8.GetByteCount(prefix));
            return clipped.Length == 0 ? head : prefix + clipped;
        }

        return ClipUtf8(full, maxBytes);
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
