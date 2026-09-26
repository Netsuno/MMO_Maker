#nullable enable
namespace Frog.Core.Models;

/// <summary>
/// Sélection BGM ou ambiance (SE) d’une carte. Asset vide = aucune.
/// Chemin relatif projet (<c>Assets/Audio/…</c>) ou identifiant de cue. Pas de RTP.
/// </summary>
public sealed class MapAudioTrack
{
    public const int DefaultVolume = 100;
    public const int MinVolume = 0;
    public const int MaxVolume = 100;
    public const int MinFadeMs = 0;
    public const int MaxFadeMs = 60_000;
    public const int MaxAssetLength = 240;

    /// <summary>Chemin relatif ou cue. Vide = aucune piste.</summary>
    public string Asset { get; set; } = string.Empty;

    /// <summary>Volume 0–100. Ignoré à l’enregistrement si <see cref="Asset"/> est vide.</summary>
    public int Volume { get; set; } = DefaultVolume;

    /// <summary>Fondu d’entrée en millisecondes. 0 = coupure sèche.</summary>
    public int FadeMs { get; set; }

    public bool IsNone => string.IsNullOrWhiteSpace(Asset);

    public static MapAudioTrack CopyOf(MapAudioTrack? source)
    {
        if (source is null)
        {
            return new MapAudioTrack();
        }

        return new MapAudioTrack
        {
            Asset = source.Asset ?? string.Empty,
            Volume = source.Volume,
            FadeMs = source.FadeMs,
        };
    }

    public static bool Same(MapAudioTrack? left, MapAudioTrack? right)
    {
        var leftOk = TryCreate(left?.Asset, left?.Volume ?? DefaultVolume, left?.FadeMs ?? 0, out var a, out _);
        var rightOk = TryCreate(right?.Asset, right?.Volume ?? DefaultVolume, right?.FadeMs ?? 0, out var b, out _);
        if (!leftOk || !rightOk)
        {
            return false;
        }

        return string.Equals(a.Asset, b.Asset, StringComparison.Ordinal)
            && a.Volume == b.Volume
            && a.FadeMs == b.FadeMs;
    }

    public static bool HasSelection(MapAudioTrack? track)
        => track is not null
           && TryNormalizeAsset(track.Asset, out var asset, out _)
           && asset.Length > 0;

    /// <summary>
    /// Valide et canonise. Asset vide → volume 100 et fondu 0 (aucune piste).
    /// </summary>
    public static bool TryCreate(
        string? asset,
        int volume,
        int fadeMs,
        out MapAudioTrack track,
        out string? error)
        => TryCreate(asset, volume, fadeMs, "Audio", out track, out error);

    public static bool TryCreate(
        string? asset,
        int volume,
        int fadeMs,
        string label,
        out MapAudioTrack track,
        out string? error)
    {
        track = new MapAudioTrack();
        if (volume < MinVolume || volume > MaxVolume)
        {
            error = $"{label} : le volume doit rester entre {MinVolume} et {MaxVolume}.";
            return false;
        }

        if (fadeMs < MinFadeMs || fadeMs > MaxFadeMs)
        {
            error = $"{label} : le fondu doit rester entre {MinFadeMs} et {MaxFadeMs} ms.";
            return false;
        }

        if (!TryNormalizeAsset(asset, out var normalized, out var assetError))
        {
            error = $"{label} : {assetError}";
            return false;
        }

        if (normalized.Length == 0)
        {
            error = null;
            return true;
        }

        track = new MapAudioTrack
        {
            Asset = normalized,
            Volume = volume,
            FadeMs = fadeMs,
        };
        error = null;
        return true;
    }

    public static bool TryNormalizeAsset(string? raw, out string asset, out string? error)
    {
        asset = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = null;
            return true;
        }

        var text = raw.Trim().Replace('\\', '/');
        while (text.StartsWith("./", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        if (text.Length > MaxAssetLength)
        {
            error = "chemin trop long.";
            return false;
        }

        if (text.Contains(':') || text.StartsWith('/'))
        {
            error = "chemin absolu interdit.";
            return false;
        }

        var segments = text.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                error = "chemin invalide (traversée interdite).";
                return false;
            }

            foreach (var c in segment)
            {
                if (char.IsControl(c) || c is '*' or '?' or '"' or '<' or '>' or '|')
                {
                    error = "chemin invalide.";
                    return false;
                }
            }
        }

        asset = text;
        error = null;
        return true;
    }

    /// <summary>
    /// Fichier choisi : <c>Assets/Audio/…</c> si le chemin le contient, sinon relatif à
    /// <paramref name="baseDirectory"/>, sinon le nom de fichier seul. Jamais un chemin machine absolu.
    /// </summary>
    public static bool TryFromPickedFile(string? pickedPath, string? baseDirectory, out string asset, out string? error)
    {
        asset = string.Empty;
        if (string.IsNullOrWhiteSpace(pickedPath))
        {
            error = "Aucun fichier audio.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(pickedPath);
        }
        catch (Exception ex)
        {
            error = "Chemin audio illisible : " + ex.Message;
            return false;
        }

        var slashed = full.Replace('\\', '/');
        const string marker = "Assets/Audio/";
        var markerAt = slashed.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerAt >= 0)
        {
            return TryNormalizeAsset(slashed[markerAt..], out asset, out error);
        }

        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            try
            {
                var root = Path.GetFullPath(baseDirectory);
                var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return TryNormalizeAsset(Path.GetRelativePath(root, full), out asset, out error);
                }
            }
            catch (Exception ex)
            {
                error = "Racine audio illisible : " + ex.Message;
                return false;
            }
        }

        return TryNormalizeAsset(Path.GetFileName(full), out asset, out error);
    }

    public string FileLabel()
    {
        if (IsNone)
        {
            return string.Empty;
        }

        var slash = Asset.LastIndexOf('/');
        return slash >= 0 && slash < Asset.Length - 1 ? Asset[(slash + 1)..] : Asset;
    }
}
