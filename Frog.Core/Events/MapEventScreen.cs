namespace Frog.Core.Events;

/// <summary>
/// Fondu et teinte d'écran style VX (<c>fadeout_screen</c>, <c>fadein_screen</c>, <c>tint_screen</c>).
/// La durée anime le client. L'état posé (noir, clair, couleur) est celui de fin de commande.
/// </summary>
public static class MapEventScreen
{
    public const int MinChannel = 0;
    public const int MaxChannel = 255;
    public const int MinDurationMs = 0;
    public const int MaxDurationMs = 60_000;

    public const int DefaultDurationMs = 1000;
    public const int DefaultTintRed = 0;
    public const int DefaultTintGreen = 0;
    public const int DefaultTintBlue = 64;
    public const int DefaultTintOpacity = 128;

    public static bool IsChannel(int value) => value is >= MinChannel and <= MaxChannel;

    public static bool IsDuration(int value) => value is >= MinDurationMs and <= MaxDurationMs;
}

/// <summary>Couleur de teinte et son opacité (0 = pas de voile).</summary>
public readonly record struct MapEventScreenTone(int Red, int Green, int Blue, int Opacity)
{
    public static MapEventScreenTone Clear => new(0, 0, 0, 0);
}

/// <summary>Image peinte : fondu noir (0–255) plus teinte.</summary>
public readonly record struct MapEventScreenFrame(int Fade, int Red, int Green, int Blue, int Opacity)
{
    public static MapEventScreenFrame Clear => new(0, 0, 0, 0, 0);

    public bool IsClear => Fade <= 0 && Opacity <= 0;
}

/// <summary>Une commande fondu ou teinte, avec sa durée d'animation.</summary>
public sealed record MapEventScreenOp(string Kind, int DurationMs, int Red, int Green, int Blue, int Opacity)
{
    public const string FadeOut = "fade_out";
    public const string FadeIn = "fade_in";
    public const string Tint = "tint";

    public bool IsFadeOut => Kind == FadeOut;

    public bool IsFadeIn => Kind == FadeIn;

    public bool IsTint => Kind == Tint;

    public MapEventScreenTone Tone => new(Red, Green, Blue, Opacity);

    public static MapEventScreenOp ForFadeOut(int durationMs) =>
        new(FadeOut, durationMs, 0, 0, 0, MapEventScreen.MaxChannel);

    public static MapEventScreenOp ForFadeIn(int durationMs) =>
        new(FadeIn, durationMs, 0, 0, 0, MapEventScreen.MinChannel);

    public static MapEventScreenOp ForTint(int red, int green, int blue, int opacity, int durationMs) =>
        new(Tint, durationMs, red, green, blue, opacity);

    /// <summary>Pose l'état de fin. Le fondu ne change que le noir ; la teinte ne change que la couleur.</summary>
    public void ApplySettled(ref int fade, ref MapEventScreenTone tint)
    {
        if (IsFadeOut)
        {
            fade = MapEventScreen.MaxChannel;
        }
        else if (IsFadeIn)
        {
            fade = MapEventScreen.MinChannel;
        }
        else if (IsTint)
        {
            tint = Tone;
        }
    }
}

/// <summary>Interpolation du fondu et de la teinte pendant <see cref="MapEventScreenOp.DurationMs"/>.</summary>
public static class MapEventScreenPlayback
{
    public static MapEventScreenFrame EndState(MapEventScreenOp op, MapEventScreenFrame current)
    {
        var fade = current.Fade;
        var tint = new MapEventScreenTone(current.Red, current.Green, current.Blue, current.Opacity);
        op.ApplySettled(ref fade, ref tint);
        return new MapEventScreenFrame(fade, tint.Red, tint.Green, tint.Blue, tint.Opacity);
    }

    public static MapEventScreenFrame Sample(MapEventScreenOp op, MapEventScreenFrame from, int elapsedMs)
    {
        var end = EndState(op, from);
        if (op.DurationMs <= 0 || elapsedMs >= op.DurationMs)
        {
            return end;
        }

        if (elapsedMs <= 0)
        {
            return from;
        }

        var t = elapsedMs / (double)op.DurationMs;
        return new MapEventScreenFrame(
            Lerp(from.Fade, end.Fade, t),
            Lerp(from.Red, end.Red, t),
            Lerp(from.Green, end.Green, t),
            Lerp(from.Blue, end.Blue, t),
            Lerp(from.Opacity, end.Opacity, t));
    }

    public static int Lerp(int from, int to, double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        return from + (int)Math.Round((to - from) * t, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Une image ou un effet d'écran, dans l'ordre de la page.</summary>
public readonly record struct MapEventVisualOp
{
    public MapEventPictureOp? Picture { get; init; }

    public MapEventScreenOp? Screen { get; init; }

    public bool IsPicture => Picture is not null;

    public static MapEventVisualOp ForPicture(MapEventPictureOp op) => new() { Picture = op };

    public static MapEventVisualOp ForScreen(MapEventScreenOp op) => new() { Screen = op };
}

/// <summary>
/// Ordre commun des images et des effets d'écran.
/// Un snapshot ancien n'a pas d'ordre : les images précèdent les effets.
/// </summary>
public static class MapEventVisualSequence
{
    public const string Picture = "picture";
    public const string Screen = "screen";

    public static IReadOnlyList<MapEventVisualOp> Expand(
        IReadOnlyList<string>? order,
        IReadOnlyList<MapEventPictureOp>? pictures,
        IReadOnlyList<MapEventScreenOp>? screens)
    {
        pictures ??= Array.Empty<MapEventPictureOp>();
        screens ??= Array.Empty<MapEventScreenOp>();
        if (order is null || order.Count == 0)
        {
            var fallback = new List<MapEventVisualOp>(pictures.Count + screens.Count);
            foreach (var picture in pictures)
            {
                fallback.Add(MapEventVisualOp.ForPicture(picture));
            }

            foreach (var screen in screens)
            {
                fallback.Add(MapEventVisualOp.ForScreen(screen));
            }

            return fallback;
        }

        var expanded = new List<MapEventVisualOp>(order.Count);
        var pictureIndex = 0;
        var screenIndex = 0;
        foreach (var kind in order)
        {
            if (kind == Picture && pictureIndex < pictures.Count)
            {
                expanded.Add(MapEventVisualOp.ForPicture(pictures[pictureIndex++]));
            }
            else if (kind == Screen && screenIndex < screens.Count)
            {
                expanded.Add(MapEventVisualOp.ForScreen(screens[screenIndex++]));
            }
        }

        return expanded;
    }
}
