namespace Frog.Core.Gameplay;

/// <summary>
/// Emplacements d'apparence choisis à la création (et repris en jeu).
/// Visuel client seulement : pas un champ de protocole. Hello reste 11.
/// Corps = couche body, Cheveux = couche head, Tunique = overlay déjà en dépôt.
/// </summary>
public enum CharacterLookSlot : byte
{
    Body = 0,
    Hair = 1,
    Tunic = 2,
}

/// <summary>
/// Indices de palette procédurale sur les planches déjà en jeu.
/// <see cref="Tunic"/> 0 = aucune. 1 = ocre d'origine. 2+ = recolor du même overlay.
/// </summary>
public readonly record struct CharacterLook(byte Body, byte Hair, byte Tunic)
{
    public const byte BodyCount = 4;
    public const byte HairCount = 5;
    public const byte TunicCount = 4;

    /// <summary>Rien d'enregistré : corps et tête Eldiran, pas de tunique.</summary>
    public static CharacterLook Default => new(0, 0, 0);

    /// <summary>Brouillon de création : la tunique ocre est visible tout de suite.</summary>
    public static CharacterLook CreateDraft => new(0, 0, 1);

    public bool WearsTunic => Tunic != 0;

    public CharacterLook Normalized() => new(
        Mod(Body, BodyCount),
        Mod(Hair, HairCount),
        Mod(Tunic, TunicCount));

    public string Label(CharacterLookSlot slot)
    {
        var look = Normalized();
        return slot switch
        {
            CharacterLookSlot.Body => BodyLabels[look.Body],
            CharacterLookSlot.Hair => HairLabels[look.Hair],
            CharacterLookSlot.Tunic => TunicLabels[look.Tunic],
            _ => "—",
        };
    }

    public static string Title(CharacterLookSlot slot) => slot switch
    {
        CharacterLookSlot.Body => "Corps",
        CharacterLookSlot.Hair => "Cheveux",
        CharacterLookSlot.Tunic => "Tunique",
        _ => "—",
    };

    public CharacterLook Cycle(CharacterLookSlot slot, int delta)
    {
        var look = Normalized();
        var count = slot switch
        {
            CharacterLookSlot.Body => BodyCount,
            CharacterLookSlot.Hair => HairCount,
            _ => TunicCount,
        };
        var current = slot switch
        {
            CharacterLookSlot.Body => look.Body,
            CharacterLookSlot.Hair => look.Hair,
            _ => look.Tunic,
        };
        var next = Wrap(current + delta, count);
        return slot switch
        {
            CharacterLookSlot.Body => look with { Body = next },
            CharacterLookSlot.Hair => look with { Hair = next },
            _ => look with { Tunic = next },
        };
    }

    public static readonly string[] BodyLabels = ["Chevalier", "Forêt", "Nuit", "Sable"];

    public static readonly string[] HairLabels = ["Naturel", "Brun", "Blond", "Noir", "Roux"];

    public static readonly string[] TunicLabels = ["Aucune", "Ocre", "Lin", "Cramoisi"];

    private static byte Mod(byte value, byte count) => (byte)(value % count);

    private static byte Wrap(int value, int count)
    {
        var n = value % count;
        if (n < 0)
        {
            n += count;
        }

        return (byte)n;
    }
}

/// <summary>
/// Recolor procédural (pas une nouvelle planche). L'index 0 et la tunique ocre
/// laissent les pixels d'origine. Le visage (peau) n'est pas teinté.
/// </summary>
public static class CharacterLookTint
{
    public readonly record struct Rgba(byte R, byte G, byte B, byte A);

    public static Rgba Apply(CharacterLookSlot slot, int index, byte r, byte g, byte b, byte a)
    {
        if (a == 0)
        {
            return new Rgba(r, g, b, a);
        }

        var count = slot switch
        {
            CharacterLookSlot.Body => CharacterLook.BodyCount,
            CharacterLookSlot.Hair => CharacterLook.HairCount,
            _ => CharacterLook.TunicCount,
        };
        var palette = index % count;
        if (palette < 0)
        {
            palette += count;
        }

        if (IsIdentity(slot, palette) || IsOutline(r, g, b))
        {
            return new Rgba(r, g, b, a);
        }

        if (slot == CharacterLookSlot.Hair && IsSkin(r, g, b))
        {
            return new Rgba(r, g, b, a);
        }

        var (hue, satScale, lumScale) = Target(slot, palette);
        RgbToHsl(r, g, b, out _, out var sat, out var lum);
        sat = Clamp01(Math.Max(sat, 0.35f) * satScale);
        lum = Clamp01(lum * lumScale);
        var (nr, ng, nb) = HslToRgb(hue, sat, lum);
        return new Rgba(nr, ng, nb, a);
    }

    private static bool IsIdentity(CharacterLookSlot slot, int palette) =>
        palette == 0 || (slot == CharacterLookSlot.Tunic && palette == 1);

    private static bool IsOutline(byte r, byte g, byte b) => r < 32 && g < 32 && b < 32;

    private static bool IsSkin(byte r, byte g, byte b) =>
        r >= 200 && g >= 160 && b >= 150 && r > b && r - g < 80;

    private static (float Hue, float SatScale, float LumScale) Target(CharacterLookSlot slot, int palette) =>
        slot switch
        {
            CharacterLookSlot.Body => palette switch
            {
                1 => (128f, 1.00f, 0.95f),
                2 => (230f, 0.85f, 0.55f),
                _ => (36f, 0.55f, 1.05f),
            },
            CharacterLookSlot.Hair => palette switch
            {
                1 => (24f, 0.75f, 0.55f),
                2 => (46f, 0.70f, 1.15f),
                3 => (30f, 0.35f, 0.28f),
                _ => (16f, 0.90f, 0.72f),
            },
            _ => palette switch
            {
                2 => (42f, 0.40f, 1.20f),
                _ => (352f, 0.80f, 0.85f),
            },
        };

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static void RgbToHsl(byte r, byte g, byte b, out float h, out float s, out float l)
    {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        l = (max + min) / 2f;
        var d = max - min;
        if (d < 0.0001f)
        {
            h = 0f;
            s = 0f;
            return;
        }

        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
        float hue;
        if (Math.Abs(max - rf) < 0.0001f)
        {
            hue = ((gf - bf) / d) % 6f;
        }
        else if (Math.Abs(max - gf) < 0.0001f)
        {
            hue = ((bf - rf) / d) + 2f;
        }
        else
        {
            hue = ((rf - gf) / d) + 4f;
        }

        h = hue * 60f;
        if (h < 0f)
        {
            h += 360f;
        }
    }

    private static (byte R, byte G, byte B) HslToRgb(float h, float s, float l)
    {
        h = ((h % 360f) + 360f) % 360f;
        if (s <= 0.0001f)
        {
            var gray = ToByte(l);
            return (gray, gray, gray);
        }

        var q = l < 0.5f ? l * (1f + s) : l + s - (l * s);
        var p = (2f * l) - q;
        var hk = h / 360f;
        return (ToByte(Hue(p, q, hk + (1f / 3f))), ToByte(Hue(p, q, hk)), ToByte(Hue(p, q, hk - (1f / 3f))));
    }

    private static float Hue(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + ((q - p) * 6f * t);
        }

        if (t < 0.5f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + ((q - p) * ((2f / 3f) - t) * 6f);
        }

        return p;
    }

    private static byte ToByte(float channel) =>
        (byte)Math.Clamp((int)Math.Round(channel * 255f), 0, 255);
}

/// <summary>Entrée <c>client-settings.json</c>. Jamais envoyée sur le fil.</summary>
public sealed class CharacterLookRecord
{
    public string CharacterId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public byte Body { get; set; }

    public byte Hair { get; set; }

    public byte Tunic { get; set; }

    /// <summary>false après « Retirer la tunique ». Le style <see cref="Tunic"/> reste.</summary>
    public bool TunicWorn { get; set; }

    public CharacterLook ToLook() => new CharacterLook(Body, Hair, Tunic).Normalized();

    public CharacterLookRecord Copy() => new()
    {
        CharacterId = CharacterId,
        DisplayName = DisplayName,
        Body = Body,
        Hair = Hair,
        Tunic = Tunic,
        TunicWorn = TunicWorn,
    };

    public static CharacterLookRecord FromLook(
        CharacterLook look,
        bool tunicWorn,
        string? characterId = null,
        string? displayName = null)
    {
        look = look.Normalized();
        return new CharacterLookRecord
        {
            CharacterId = (characterId ?? string.Empty).Trim(),
            DisplayName = (displayName ?? string.Empty).Trim(),
            Body = look.Body,
            Hair = look.Hair,
            Tunic = look.Tunic,
            TunicWorn = tunicWorn && look.Tunic != 0,
        };
    }
}

/// <summary>Associe un look au perso créé, dans le JSON client déjà utilisé.</summary>
public static class CharacterLookBook
{
    public const int MaxEntries = 24;

    public static CharacterLookRecord Remember(
        List<CharacterLookRecord> rows,
        string? characterId,
        string? displayName,
        CharacterLook look,
        bool tunicWorn)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var id = (characterId ?? string.Empty).Trim();
        var name = (displayName ?? string.Empty).Trim();
        if (id.Length == 0 && name.Length == 0)
        {
            throw new ArgumentException("Un identifiant ou un nom est requis.");
        }

        CharacterLookRecord? row = null;
        if (id.Length > 0)
        {
            row = FindId(rows, id);
        }

        if (row is null && name.Length > 0)
        {
            row = FindPendingName(rows, name) ?? FindName(rows, name);
        }

        if (row is null)
        {
            row = new CharacterLookRecord();
            rows.Add(row);
        }

        var normalized = look.Normalized();
        if (id.Length > 0)
        {
            row.CharacterId = id;
        }

        if (name.Length > 0)
        {
            row.DisplayName = name;
        }

        row.Body = normalized.Body;
        row.Hair = normalized.Hair;
        row.Tunic = normalized.Tunic;
        row.TunicWorn = tunicWorn && normalized.Tunic != 0;
        rows.Remove(row);
        rows.Add(row);
        while (rows.Count > MaxEntries)
        {
            rows.RemoveAt(0);
        }

        return row;
    }

    public static bool BindCreatedId(List<CharacterLookRecord> rows, string displayName, string characterId)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var name = (displayName ?? string.Empty).Trim();
        var id = (characterId ?? string.Empty).Trim();
        if (name.Length == 0 || id.Length == 0)
        {
            return false;
        }

        var pending = FindPendingName(rows, name);
        var target = pending ?? FindName(rows, name);
        if (target is null)
        {
            return false;
        }

        target.CharacterId = id;
        return true;
    }

    public static bool TryGet(
        IReadOnlyList<CharacterLookRecord>? rows,
        string? characterId,
        string? displayName,
        out CharacterLookRecord record)
    {
        record = null!;
        if (rows is null || rows.Count == 0)
        {
            return false;
        }

        var id = (characterId ?? string.Empty).Trim();
        if (id.Length > 0)
        {
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                if (string.Equals(rows[i].CharacterId, id, StringComparison.OrdinalIgnoreCase))
                {
                    record = rows[i];
                    return true;
                }
            }
        }

        var name = (displayName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return false;
        }

        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (string.Equals(rows[i].DisplayName, name, StringComparison.OrdinalIgnoreCase))
            {
                record = rows[i];
                return true;
            }
        }

        return false;
    }

    private static CharacterLookRecord? FindId(List<CharacterLookRecord> rows, string id)
    {
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (string.Equals(rows[i].CharacterId, id, StringComparison.OrdinalIgnoreCase))
            {
                return rows[i];
            }
        }

        return null;
    }

    private static CharacterLookRecord? FindPendingName(List<CharacterLookRecord> rows, string name)
    {
        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.CharacterId)
                && string.Equals(row.DisplayName, name, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    private static CharacterLookRecord? FindName(List<CharacterLookRecord> rows, string name)
    {
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (string.Equals(rows[i].DisplayName, name, StringComparison.OrdinalIgnoreCase))
            {
                return rows[i];
            }
        }

        return null;
    }
}
