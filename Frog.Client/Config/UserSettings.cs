namespace Frog.Client.Config;

/// <summary>Réglages persistés (%LocalAppData%\Frog\client-settings.json).</summary>
public sealed class UserSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public KeyboardLayoutPreset KeyboardPreset { get; set; } = KeyboardLayoutPreset.Azerty;

    public InputBindingsSettings Bindings { get; set; } = InputBindingsSettings.Azerty();

    public int VolumePercent { get; set; } = 80;

    /// <summary>Mute explicite (SFX + musique), distinct du slider à 0.</summary>
    public bool AudioMuted { get; set; }

    /// <summary>Opt-in : boucle placeholder <c>music-loop.wav</c>. Défaut off.</summary>
    public bool MusicEnabled { get; set; }

    public WindowSettings Window { get; set; } = new();

    /// <summary>Dernier hôte TCP (login + Options → Réseau). Une seule vérité JSON.</summary>
    public string LastHost { get; set; } = "127.0.0.1";

    public int LastPort { get; set; } = 6000;

    /// <summary>Compte mémorisé (login souvenir). Jamais le mot de passe.</summary>
    public string LastUsername { get; set; } = string.Empty;

    public bool RememberAccount { get; set; }

    public void Normalize()
    {
        if (SchemaVersion < 1)
        {
            SchemaVersion = CurrentSchemaVersion;
        }

        VolumePercent = Math.Clamp(VolumePercent, 0, 100);
        Bindings ??= InputBindingsSettings.FromPreset(KeyboardPreset);
        Bindings.Normalize(KeyboardPreset);
        Window ??= new WindowSettings();
        Window.Normalize();
        LastHost = string.IsNullOrWhiteSpace(LastHost) ? "127.0.0.1" : LastHost.Trim();
        LastPort = Math.Clamp(LastPort, 1, 65535);
        LastUsername = RememberAccount ? (LastUsername ?? string.Empty).Trim() : string.Empty;
    }

    public void ApplyPreset(KeyboardLayoutPreset preset)
    {
        KeyboardPreset = preset;
        Bindings = InputBindingsSettings.FromPreset(preset);
    }

    public UserSettings Clone()
    {
        var copy = new UserSettings
        {
            SchemaVersion = SchemaVersion,
            KeyboardPreset = KeyboardPreset,
            Bindings = Bindings.Clone(),
            VolumePercent = VolumePercent,
            AudioMuted = AudioMuted,
            MusicEnabled = MusicEnabled,
            Window = Window.Clone(),
            LastHost = LastHost,
            LastPort = LastPort,
            LastUsername = LastUsername,
            RememberAccount = RememberAccount,
        };
        copy.Normalize();
        return copy;
    }
}

public enum KeyboardLayoutPreset
{
    Azerty,
    Qwerty,
}

public sealed class InputBindingsSettings
{
    public string MoveUp { get; set; } = "Z";

    public string MoveDown { get; set; } = "S";

    public string MoveLeft { get; set; } = "Q";

    public string MoveRight { get; set; } = "D";

    public string Interact { get; set; } = "E";

    public static InputBindingsSettings Azerty() => new();

    public static InputBindingsSettings Qwerty() => new()
    {
        MoveUp = "W",
        MoveDown = "S",
        MoveLeft = "A",
        MoveRight = "D",
        Interact = "E",
    };

    public static InputBindingsSettings FromPreset(KeyboardLayoutPreset preset) =>
        preset == KeyboardLayoutPreset.Qwerty ? Qwerty() : Azerty();

    public void Normalize(KeyboardLayoutPreset preset)
    {
        MoveUp = NormalizeKeyName(MoveUp, FromPreset(preset).MoveUp);
        MoveDown = NormalizeKeyName(MoveDown, FromPreset(preset).MoveDown);
        MoveLeft = NormalizeKeyName(MoveLeft, FromPreset(preset).MoveLeft);
        MoveRight = NormalizeKeyName(MoveRight, FromPreset(preset).MoveRight);
        Interact = NormalizeKeyName(Interact, FromPreset(preset).Interact);
    }

    public InputBindingsSettings Clone() => new()
    {
        MoveUp = MoveUp,
        MoveDown = MoveDown,
        MoveLeft = MoveLeft,
        MoveRight = MoveRight,
        Interact = Interact,
    };

    private static string NormalizeKeyName(string? raw, string fallback)
    {
        var name = (raw ?? string.Empty).Trim();
        return string.IsNullOrEmpty(name) ? fallback : name;
    }
}

public sealed class WindowSettings
{
    public int Width { get; set; } = 1040;

    public int Height { get; set; } = 720;

    public bool Maximized { get; set; }

    public bool FullScreen { get; set; }

    public void Normalize()
    {
        Width = Math.Clamp(Width, 980, 7680);
        Height = Math.Clamp(Height, 640, 4320);
    }

    public WindowSettings Clone() => new()
    {
        Width = Width,
        Height = Height,
        Maximized = Maximized,
        FullScreen = FullScreen,
    };
}
