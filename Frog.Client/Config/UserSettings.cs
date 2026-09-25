using System;
using System.Collections.Generic;
using System.Linq;

using Frog.Core.Constants;
using Frog.Core.Gameplay;

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

    /// <summary>
    /// Échelle d’interface en pourcent entier (pas de 25, 75–200). Défaut 100.
    /// N’affecte pas la tuile monde (<see cref="WorldMetrics.DefaultTileSizePixels"/>).
    /// </summary>
    public int UiScalePercent { get; set; } = ClientUiScale.DefaultPercent;

    /// <summary>Dernier hôte TCP sélectionné (login + Options → Réseau).</summary>
    public string LastHost { get; set; } = "127.0.0.1";

    public int LastPort { get; set; } = 6000;

    /// <summary>Serveurs mémorisés pour le sélecteur. Jamais de mot de passe.</summary>
    public List<SavedServerEndpoint> SavedServers { get; set; } = new();

    /// <summary>Compte mémorisé (login souvenir). Jamais le mot de passe.</summary>
    public string LastUsername { get; set; } = string.Empty;

    public bool RememberAccount { get; set; }

    /// <summary>
    /// Base HTTP du canal contenu (<c>http://hôte:6080</c>). Variable
    /// <c>FROG_TILEPACK_CONTENT_BASE_URL</c> prioritaire. Défaut <c>http://127.0.0.1:6080</c>.
    /// </summary>
    public string TilePackContentBaseUrl { get; set; } = TilePackClientOptions.DefaultContentBaseUrl;

    /// <summary>
    /// Clé publique Ed25519 épinglée (64 hex). Variable <c>FROG_TILEPACK_PUBLIC_KEY_HEX</c> prioritaire.
    /// Vide : le client ne peint pas les tuiles du paquet.
    /// </summary>
    public string TilePackPublicKeyHex { get; set; } = string.Empty;

    /// <summary>Slug optionnel du paquet. Variable <c>FROG_TILEPACK_SLUG</c> prioritaire. Vide : paquet publié le plus récent.</summary>
    public string TilePackSlug { get; set; } = string.Empty;

    /// <summary>Dernier brouillon du sélecteur d'apparence (création). Local uniquement.</summary>
    public CharacterLookRecord? AppearanceDraft { get; set; }

    /// <summary>Looks enregistrés par personnage (id ou nom). Local uniquement, pas sur le fil.</summary>
    public List<CharacterLookRecord> CharacterLooks { get; set; } = new();

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
        UiScalePercent = ClientUiScale.ClampPercent(UiScalePercent);
        LastHost = string.IsNullOrWhiteSpace(LastHost) ? "127.0.0.1" : LastHost.Trim();
        LastPort = Math.Clamp(LastPort, 1, 65535);
        SavedServerList.Normalize(this);
        LastUsername = RememberAccount ? (LastUsername ?? string.Empty).Trim() : string.Empty;
        if (!TilePackClientOptions.TryNormalizeBaseUrl(TilePackContentBaseUrl, out var tilePackUrl))
        {
            tilePackUrl = TilePackClientOptions.DefaultContentBaseUrl;
        }

        TilePackContentBaseUrl = tilePackUrl;
        TilePackPublicKeyHex = (TilePackPublicKeyHex ?? string.Empty).Trim();
        TilePackSlug = (TilePackSlug ?? string.Empty).Trim();
        NormalizeLooks();
    }

    private void NormalizeLooks()
    {
        CharacterLooks ??= new List<CharacterLookRecord>();
        for (var i = CharacterLooks.Count - 1; i >= 0; i--)
        {
            var row = CharacterLooks[i];
            if (row is null)
            {
                CharacterLooks.RemoveAt(i);
                continue;
            }

            var look = row.ToLook();
            row.Body = look.Body;
            row.Hair = look.Hair;
            row.Tunic = look.Tunic;
            row.CharacterId = (row.CharacterId ?? string.Empty).Trim();
            row.DisplayName = (row.DisplayName ?? string.Empty).Trim();
            if (row.Tunic == 0)
            {
                row.TunicWorn = false;
            }

            if (row.CharacterId.Length == 0 && row.DisplayName.Length == 0)
            {
                CharacterLooks.RemoveAt(i);
            }
        }

        if (CharacterLooks.Count > CharacterLookBook.MaxEntries)
        {
            CharacterLooks.RemoveRange(0, CharacterLooks.Count - CharacterLookBook.MaxEntries);
        }

        if (AppearanceDraft is null)
        {
            return;
        }

        var draft = AppearanceDraft.ToLook();
        AppearanceDraft.Body = draft.Body;
        AppearanceDraft.Hair = draft.Hair;
        AppearanceDraft.Tunic = draft.Tunic;
        AppearanceDraft.CharacterId = string.Empty;
        AppearanceDraft.DisplayName = string.Empty;
        if (AppearanceDraft.Tunic == 0)
        {
            AppearanceDraft.TunicWorn = false;
        }
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
            UiScalePercent = UiScalePercent,
            LastHost = LastHost,
            LastPort = LastPort,
            SavedServers = (SavedServers ?? new List<SavedServerEndpoint>())
                .Where(static row => row is not null)
                .Select(static row => row.Copy())
                .ToList(),
            LastUsername = LastUsername,
            RememberAccount = RememberAccount,
            TilePackContentBaseUrl = TilePackContentBaseUrl,
            TilePackPublicKeyHex = TilePackPublicKeyHex,
            TilePackSlug = TilePackSlug,
            AppearanceDraft = AppearanceDraft?.Copy(),
            CharacterLooks = (CharacterLooks ?? new List<CharacterLookRecord>())
                .Where(static row => row is not null)
                .Select(static row => row.Copy())
                .ToList(),
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

    /// <summary>Mêlée (barre d’espace par défaut). Les flèches de déplacement restent toujours actives.</summary>
    public string Attack { get; set; } = "Space";

    public static InputBindingsSettings Azerty() => new();

    public static InputBindingsSettings Qwerty() => new()
    {
        MoveUp = "W",
        MoveDown = "S",
        MoveLeft = "A",
        MoveRight = "D",
        Interact = "E",
        Attack = "Space",
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
        Attack = NormalizeKeyName(Attack, FromPreset(preset).Attack);
    }

    public InputBindingsSettings Clone() => new()
    {
        MoveUp = MoveUp,
        MoveDown = MoveDown,
        MoveLeft = MoveLeft,
        MoveRight = MoveRight,
        Interact = Interact,
        Attack = Attack,
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

/// <summary>
/// Échelle d’interface joueur, en pourcent entier.
/// Le DPI Windows est déjà rendu par la police (points) et <c>AutoScaleMode.Font</c> :
/// ce facteur ne lit pas <c>DeviceDpi</c> et ne le multiplie pas une seconde fois.
/// La tuile monde reste <see cref="WorldMetrics.DefaultTileSizePixels"/> (32).
/// </summary>
public static class ClientUiScale
{
    public const int MinPercent = 75;

    public const int MaxPercent = 200;

    public const int StepPercent = 25;

    public const int DefaultPercent = 100;

    public static readonly int[] Steps = [75, 100, 125, 150, 175, 200];

    /// <summary>Tuile monde : jamais passée par <see cref="ScaleDip"/>.</summary>
    public static int WorldTilePixels => WorldMetrics.DefaultTileSizePixels;

    /// <summary>
    /// Pourcent joueur courant. 100 tant que le shell n’a pas appliqué les réglages.
    /// Les polices créées ensuite (CTA login) s’alignent sans relire le DPI.
    /// </summary>
    public static int ActivePercent { get; private set; } = DefaultPercent;

    public static void SetActive(int percent) => ActivePercent = ClampPercent(percent);

    /// <summary>
    /// 0 ou négatif → défaut (champ JSON absent / non écrit). Sinon pas de 25, borné 75–200.
    /// </summary>
    public static int ClampPercent(int percent)
    {
        if (percent <= 0)
        {
            return DefaultPercent;
        }

        var steps = (int)Math.Round(percent / (double)StepPercent, MidpointRounding.AwayFromZero);
        var snapped = steps * StepPercent;
        return Math.Clamp(snapped, MinPercent, MaxPercent);
    }

    public static int StepIndex(int percent)
    {
        var clamped = ClampPercent(percent);
        for (var i = 0; i < Steps.Length; i++)
        {
            if (Steps[i] == clamped)
            {
                return i;
            }
        }

        return 1;
    }

    /// <summary>Pixels logiques d’interface. À 100 %, rend exactement <paramref name="designDip"/>.</summary>
    public static int ScaleDip(int designDip, int percent)
    {
        if (designDip == 0)
        {
            return 0;
        }

        var clamped = ClampPercent(percent);
        if (clamped == DefaultPercent)
        {
            return designDip;
        }

        var sign = designDip < 0 ? -1 : 1;
        var scaled = (int)Math.Round(Math.Abs(designDip) * (clamped / 100.0), MidpointRounding.AwayFromZero);
        return sign * Math.Max(1, scaled);
    }

    /// <summary>Taille de police en points. À 100 %, rend exactement <paramref name="designEm"/>.</summary>
    public static float ScaleEm(float designEm, int percent)
    {
        if (designEm <= 0)
        {
            return designEm;
        }

        var clamped = ClampPercent(percent);
        if (clamped == DefaultPercent)
        {
            return designEm;
        }

        return designEm * (clamped / 100f);
    }
}
