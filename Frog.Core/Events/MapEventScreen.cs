namespace Frog.Core.Events;

/// <summary>
/// Effets d'écran style VX : fondu, teinte, tremblement, flash.
/// Le fondu et la teinte posent un état. Le tremblement et le flash sont passagers :
/// à la fin, le noir et la teinte déjà posés sont inchangés.
/// </summary>
public static class MapEventScreen
{
    public const int MinChannel = 0;
    public const int MaxChannel = 255;
    public const int MinDurationMs = 0;
    public const int MaxDurationMs = 60_000;

    /// <summary>Amplitude du tremblement, en pixels. Plafond = une tuile canonique (48).</summary>
    public const int MinPower = 0;

    public const int MaxPower = 48;

    /// <summary>Oscillations par seconde (1–9, comme la vitesse VX).</summary>
    public const int MinSpeed = 1;

    public const int MaxSpeed = 9;

    public const int DefaultDurationMs = 1000;
    public const int DefaultTintRed = 0;
    public const int DefaultTintGreen = 0;
    public const int DefaultTintBlue = 64;
    public const int DefaultTintOpacity = 128;
    public const int DefaultPower = 8;
    public const int DefaultSpeed = 5;
    public const int DefaultFlashRed = 255;
    public const int DefaultFlashGreen = 255;
    public const int DefaultFlashBlue = 255;
    public const int DefaultFlashOpacity = 170;

    public static bool IsChannel(int value) => value is >= MinChannel and <= MaxChannel;

    public static bool IsDuration(int value) => value is >= MinDurationMs and <= MaxDurationMs;

    public static bool IsPower(int value) => value is >= MinPower and <= MaxPower;

    public static bool IsSpeed(int value) => value is >= MinSpeed and <= MaxSpeed;
}

/// <summary>Couleur de teinte et son opacité (0 = pas de voile).</summary>
public readonly record struct MapEventScreenTone(int Red, int Green, int Blue, int Opacity)
{
    public static MapEventScreenTone Clear => new(0, 0, 0, 0);
}

/// <summary>
/// Image peinte : fondu noir et teinte posés, plus un flash et un décalage passagers.
/// </summary>
public readonly record struct MapEventScreenFrame(
    int Fade,
    int Red,
    int Green,
    int Blue,
    int Opacity,
    int FlashRed = 0,
    int FlashGreen = 0,
    int FlashBlue = 0,
    int FlashOpacity = 0,
    int ShakeX = 0)
{
    public static MapEventScreenFrame Clear => new(0, 0, 0, 0, 0);

    public bool IsClear => Fade <= 0 && Opacity <= 0 && FlashOpacity <= 0;
}

/// <summary>Une commande d'écran, avec sa durée d'animation.</summary>
public sealed record MapEventScreenOp(string Kind, int DurationMs, int Red, int Green, int Blue, int Opacity)
{
    public const string FadeOut = "fade_out";
    public const string FadeIn = "fade_in";
    public const string Tint = "tint";
    public const string Shake = "shake";
    public const string Flash = "flash";

    /// <summary>Amplitude du tremblement, en pixels. 0 pour les autres commandes.</summary>
    public int Power { get; init; }

    /// <summary>Oscillations par seconde. 0 pour les autres commandes.</summary>
    public int Speed { get; init; }

    public bool IsFadeOut => Kind == FadeOut;

    public bool IsFadeIn => Kind == FadeIn;

    public bool IsTint => Kind == Tint;

    public bool IsShake => Kind == Shake;

    public bool IsFlash => Kind == Flash;

    public MapEventScreenTone Tone => new(Red, Green, Blue, Opacity);

    public static MapEventScreenOp ForFadeOut(int durationMs) =>
        new(FadeOut, durationMs, 0, 0, 0, MapEventScreen.MaxChannel);

    public static MapEventScreenOp ForFadeIn(int durationMs) =>
        new(FadeIn, durationMs, 0, 0, 0, MapEventScreen.MinChannel);

    public static MapEventScreenOp ForTint(int red, int green, int blue, int opacity, int durationMs) =>
        new(Tint, durationMs, red, green, blue, opacity);

    public static MapEventScreenOp ForShake(int power, int speed, int durationMs) =>
        new(Shake, durationMs, 0, 0, 0, 0) { Power = power, Speed = speed };

    public static MapEventScreenOp ForFlash(int red, int green, int blue, int opacity, int durationMs) =>
        new(Flash, durationMs, red, green, blue, opacity);

    /// <summary>
    /// Pose l'état de fin. Le fondu ne change que le noir ; la teinte ne change que la couleur.
    /// Tremblement et flash ne posent rien.
    /// </summary>
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

/// <summary>Interpolation des effets d'écran pendant <see cref="MapEventScreenOp.DurationMs"/>.</summary>
public static class MapEventScreenPlayback
{
    public static MapEventScreenFrame EndState(MapEventScreenOp op, MapEventScreenFrame current)
    {
        if (op.IsShake || op.IsFlash)
        {
            return WithoutTransient(current);
        }

        var fade = current.Fade;
        var tint = new MapEventScreenTone(current.Red, current.Green, current.Blue, current.Opacity);
        op.ApplySettled(ref fade, ref tint);
        return new MapEventScreenFrame(fade, tint.Red, tint.Green, tint.Blue, tint.Opacity);
    }

    public static MapEventScreenFrame Sample(MapEventScreenOp op, MapEventScreenFrame from, int elapsedMs)
    {
        if (op.IsShake)
        {
            return SampleShake(op, from, elapsedMs);
        }

        if (op.IsFlash)
        {
            return SampleFlash(op, from, elapsedMs);
        }

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

    /// <summary>Décalage horizontal. À 0 ms et à la fin, le monde est revenu au repos.</summary>
    public static int ShakeOffset(int power, int speed, int elapsedMs)
    {
        if (power == 0 || speed <= 0 || elapsedMs <= 0)
        {
            return 0;
        }

        var phase = elapsedMs / 1000d * speed * (Math.PI * 2d);
        return (int)Math.Round(power * Math.Sin(phase), MidpointRounding.AwayFromZero);
    }

    public static int Lerp(int from, int to, double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        return from + (int)Math.Round((to - from) * t, MidpointRounding.AwayFromZero);
    }

    private static MapEventScreenFrame SampleShake(MapEventScreenOp op, MapEventScreenFrame from, int elapsedMs)
    {
        var settled = WithoutTransient(from);
        if (op.DurationMs <= 0 || elapsedMs <= 0 || elapsedMs >= op.DurationMs)
        {
            return settled;
        }

        return settled with { ShakeX = ShakeOffset(op.Power, op.Speed, elapsedMs) };
    }

    private static MapEventScreenFrame SampleFlash(MapEventScreenOp op, MapEventScreenFrame from, int elapsedMs)
    {
        var settled = WithoutTransient(from);
        if (op.DurationMs <= 0 || elapsedMs >= op.DurationMs)
        {
            return settled;
        }

        var t = elapsedMs <= 0 ? 0d : elapsedMs / (double)op.DurationMs;
        return settled with
        {
            FlashRed = op.Red,
            FlashGreen = op.Green,
            FlashBlue = op.Blue,
            FlashOpacity = Lerp(op.Opacity, 0, t),
        };
    }

    private static MapEventScreenFrame WithoutTransient(MapEventScreenFrame frame) =>
        frame with { FlashRed = 0, FlashGreen = 0, FlashBlue = 0, FlashOpacity = 0, ShakeX = 0 };
}

/// <summary>Une image ou un effet d'écran, dans l'ordre de la page.</summary>
public readonly record struct MapEventVisualOp
{
    public MapEventPictureOp? Picture { get; init; }

    public MapEventScreenOp? Screen { get; init; }

    public MapEventAnimationOp? Animation { get; init; }

    public bool IsPicture => Picture is not null;

    public bool IsAnimation => Animation is not null;

    public static MapEventVisualOp ForPicture(MapEventPictureOp op) => new() { Picture = op };

    public static MapEventVisualOp ForScreen(MapEventScreenOp op) => new() { Screen = op };

    public static MapEventVisualOp ForAnimation(MapEventAnimationOp op) => new() { Animation = op };
}

/// <summary>
/// Ordre commun des images et des effets d'écran.
/// Un snapshot ancien n'a pas d'ordre : les images précèdent les effets.
/// </summary>
public static class MapEventVisualSequence
{
    public const string Picture = "picture";
    public const string Screen = "screen";
    public const string Animation = "animation";

    public static IReadOnlyList<MapEventVisualOp> Expand(
        IReadOnlyList<string>? order,
        IReadOnlyList<MapEventPictureOp>? pictures,
        IReadOnlyList<MapEventScreenOp>? screens,
        IReadOnlyList<MapEventAnimationOp>? animations = null)
    {
        pictures ??= Array.Empty<MapEventPictureOp>();
        screens ??= Array.Empty<MapEventScreenOp>();
        animations ??= Array.Empty<MapEventAnimationOp>();
        if (order is null || order.Count == 0)
        {
            var fallback = new List<MapEventVisualOp>(pictures.Count + screens.Count + animations.Count);
            foreach (var picture in pictures)
            {
                fallback.Add(MapEventVisualOp.ForPicture(picture));
            }

            foreach (var screen in screens)
            {
                fallback.Add(MapEventVisualOp.ForScreen(screen));
            }

            foreach (var animation in animations)
            {
                fallback.Add(MapEventVisualOp.ForAnimation(animation));
            }

            return fallback;
        }

        var expanded = new List<MapEventVisualOp>(order.Count);
        var pictureIndex = 0;
        var screenIndex = 0;
        var animationIndex = 0;
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
            else if (kind == Animation && animationIndex < animations.Count)
            {
                expanded.Add(MapEventVisualOp.ForAnimation(animations[animationIndex++]));
            }
        }

        return expanded;
    }
}
