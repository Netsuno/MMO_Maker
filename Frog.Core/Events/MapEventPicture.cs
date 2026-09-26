using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Image d'écran style VX (commande <c>show_picture</c> / <c>erase_picture</c>).
/// Le numéro est un slot 1–100. Le fichier est un chemin relatif projet, pas une planche.
/// </summary>
public static class MapEventPicture
{
    public const int MinId = 1;
    public const int MaxId = 100;
    public const int MinCoord = -9999;
    public const int MaxCoord = 9999;
    public const int MinOpacity = 0;
    public const int MaxOpacity = 255;
    public const int MaxAssetLength = 180;

    public const string BlendNormal = "normal";
    public const string BlendAdd = "add";
    public const string BlendSubtract = "subtract";

    public const string DefaultAsset = "Assets/Pictures/placeholder.png";

    public static readonly IReadOnlyList<string> Blends = new[] { BlendNormal, BlendAdd, BlendSubtract };

    public static bool TryCanonicalBlend(string? raw, out string blend)
    {
        blend = (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "normal" => BlendNormal,
            "add" or "addition" => BlendAdd,
            "subtract" or "sub" or "soustraction" => BlendSubtract,
            _ => string.Empty,
        };
        return blend.Length > 0;
    }

    public static bool TryNormalizeAsset(string? raw, out string asset, out string? error)
    {
        asset = string.Empty;
        if (!MapAudioTrack.TryNormalizeAsset(raw, out asset, out error))
        {
            return false;
        }

        if (asset.Length == 0)
        {
            error = "fichier requis.";
            return false;
        }

        if (asset.Length > MaxAssetLength)
        {
            asset = string.Empty;
            error = "chemin trop long.";
            return false;
        }

        if (asset.Contains(':'))
        {
            asset = string.Empty;
            error = "chemin absolu interdit.";
            return false;
        }

        var ext = Path.GetExtension(asset);
        if (ext.Length == 0
            || ext.ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif"))
        {
            asset = string.Empty;
            error = "extension image requise (png, jpg, jpeg, bmp, gif).";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>Image actuellement affichée sur un numéro.</summary>
public sealed record MapEventShownPicture(int PictureId, string Asset, int X, int Y, int Opacity, string Blend);

/// <summary>Une opération <c>show_picture</c> ou <c>erase_picture</c>, dans l'ordre de la page.</summary>
public sealed record MapEventPictureOp(bool Erase, int PictureId, string Asset, int X, int Y, int Opacity, string Blend)
{
    public static MapEventPictureOp ForShow(int pictureId, string asset, int x, int y, int opacity, string blend) =>
        new(false, pictureId, asset, x, y, opacity, blend);

    public static MapEventPictureOp ForErase(int pictureId) =>
        new(true, pictureId, string.Empty, 0, 0, MapEventPicture.MaxOpacity, MapEventPicture.BlendNormal);

    public MapEventShownPicture ToShown() => new(PictureId, Asset, X, Y, Opacity, Blend);
}
