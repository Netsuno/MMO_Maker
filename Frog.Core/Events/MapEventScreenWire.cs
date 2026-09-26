using System.Globalization;

namespace Frog.Core.Events;

/// <summary>
/// Transporte fondu et teinte dans le message <c>InteractResult</c> (opcode 32).
/// Même schéma que <c>pic:</c> et <c>shop:&lt;guid&gt;</c> : lignes préfixes, Hello 11, pas de nouvel opcode.
/// Les lignes <c>fade:</c>, <c>tint:</c> et <c>pic:</c> restent dans l'ordre de la page.
/// </summary>
public static class MapEventScreenWire
{
    public static string FormatLine(MapEventScreenOp op)
    {
        if (op.IsFadeOut)
        {
            return "fade:out:" + op.DurationMs.ToString(CultureInfo.InvariantCulture);
        }

        if (op.IsFadeIn)
        {
            return "fade:in:" + op.DurationMs.ToString(CultureInfo.InvariantCulture);
        }

        return FormattableString.Invariant(
            $"tint:{op.Red}:{op.Green}:{op.Blue}:{op.Opacity}:{op.DurationMs}");
    }

    public static string FormatLine(MapEventVisualOp visual) =>
        visual.Screen is { } screen
            ? FormatLine(screen)
            : visual.Picture is { } picture
                ? MapEventPictureWire.FormatLine(picture)
                : string.Empty;

    /// <summary>
    /// Préfixe les effets d'écran et les images dans l'ordre, puis le corps boutique / texte.
    /// Sans effet d'écran, le message est celui de <see cref="MapEventPictureWire"/>.
    /// </summary>
    public static string Compose(
        IReadOnlyList<MapEventVisualOp>? visuals,
        Guid? shopId,
        string? showText,
        string? fallbackMessage)
    {
        if (visuals is null || visuals.Count == 0)
        {
            return MapEventPictureWire.Compose(null, shopId, showText, fallbackMessage);
        }

        var screens = 0;
        var pictures = new List<MapEventPictureOp>();
        foreach (var visual in visuals)
        {
            if (visual.Screen is not null)
            {
                screens++;
            }
            else if (visual.Picture is { } picture)
            {
                pictures.Add(picture);
            }
        }

        if (screens == 0)
        {
            return MapEventPictureWire.Compose(pictures, shopId, showText, fallbackMessage);
        }

        var lines = new List<string>(visuals.Count);
        foreach (var visual in visuals)
        {
            var line = FormatLine(visual);
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return MapEventPictureWire.ComposeLines(lines, shopId, showText, fallbackMessage);
    }

    /// <summary>
    /// Retire les lignes <c>fade:</c>, <c>tint:</c> et <c>pic:</c> en tête.
    /// Le reste peut encore porter <c>shop:</c> puis le texte.
    /// </summary>
    public static bool TryTakeInteractMessage(
        string? message,
        out IReadOnlyList<MapEventVisualOp> ops,
        out string remainder)
    {
        ops = Array.Empty<MapEventVisualOp>();
        remainder = message ?? string.Empty;
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var parsed = new List<MapEventVisualOp>();
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

    public static bool TryParseLine(string line, out MapEventVisualOp op)
    {
        op = default;
        if (MapEventPictureWire.TryParseLine(line, out var picture))
        {
            op = MapEventVisualOp.ForPicture(picture);
            return true;
        }

        if (!TryParseScreenLine(line, out var screen))
        {
            return false;
        }

        op = MapEventVisualOp.ForScreen(screen);
        return true;
    }

    public static bool TryParseScreenLine(string line, out MapEventScreenOp op)
    {
        op = MapEventScreenOp.ForFadeIn(0);
        if (line.StartsWith("fade:", StringComparison.Ordinal))
        {
            var parts = line.Split(':');
            if (parts.Length != 3
                || parts[0] != "fade"
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var durationMs)
                || !MapEventScreen.IsDuration(durationMs))
            {
                return false;
            }

            op = parts[1] switch
            {
                "out" => MapEventScreenOp.ForFadeOut(durationMs),
                "in" => MapEventScreenOp.ForFadeIn(durationMs),
                _ => op,
            };
            return parts[1] is "out" or "in";
        }

        if (!line.StartsWith("tint:", StringComparison.Ordinal))
        {
            return false;
        }

        var tint = line.Split(':');
        if (tint.Length != 6
            || tint[0] != "tint"
            || !int.TryParse(tint[1], NumberStyles.None, CultureInfo.InvariantCulture, out var red)
            || !int.TryParse(tint[2], NumberStyles.None, CultureInfo.InvariantCulture, out var green)
            || !int.TryParse(tint[3], NumberStyles.None, CultureInfo.InvariantCulture, out var blue)
            || !int.TryParse(tint[4], NumberStyles.None, CultureInfo.InvariantCulture, out var opacity)
            || !int.TryParse(tint[5], NumberStyles.None, CultureInfo.InvariantCulture, out var tintDuration)
            || !MapEventScreen.IsChannel(red)
            || !MapEventScreen.IsChannel(green)
            || !MapEventScreen.IsChannel(blue)
            || !MapEventScreen.IsChannel(opacity)
            || !MapEventScreen.IsDuration(tintDuration))
        {
            return false;
        }

        op = MapEventScreenOp.ForTint(red, green, blue, opacity, tintDuration);
        return true;
    }
}
