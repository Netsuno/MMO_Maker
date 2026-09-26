using System.Text.Json.Serialization;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Image d'écran style VX (<c>show_picture</c>, <c>move_picture</c>, <c>tint_picture</c>, <c>erase_picture</c>).
/// Le numéro est un slot 1–100. Le fichier est un chemin relatif projet, pas une planche.
/// Déplacer et teinter s'appliquent au slot déjà affiché ; un slot vide est ignoré.
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

/// <summary>Image actuellement affichée sur un numéro. Teinte à opacité 0 = couleurs d'origine.</summary>
public sealed record MapEventShownPicture(
    int PictureId,
    string Asset,
    int X,
    int Y,
    int Opacity,
    string Blend,
    int TintRed = 0,
    int TintGreen = 0,
    int TintBlue = 0,
    int TintOpacity = 0);

/// <summary>
/// Une opération image, dans l'ordre de la page.
/// <see cref="Erase"/> reste le drapeau historique ; <see cref="Kind"/> distingue show, move et tint.
/// </summary>
public sealed record MapEventPictureOp(
    bool Erase,
    int PictureId,
    string Asset,
    int X,
    int Y,
    int Opacity,
    string Blend,
    string Kind = MapEventPictureOp.ShowKind,
    int Red = 0,
    int Green = 0,
    int Blue = 0,
    int TintOpacity = 0)
{
    public const string ShowKind = "show";
    public const string EraseKind = "erase";
    public const string MoveKind = "move";
    public const string TintKind = "tint";

    [JsonIgnore]
    public bool IsErase => Erase || string.Equals(Kind, EraseKind, StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsMove => !IsErase && string.Equals(Kind, MoveKind, StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsTint => !IsErase && string.Equals(Kind, TintKind, StringComparison.Ordinal);

    public static MapEventPictureOp ForShow(int pictureId, string asset, int x, int y, int opacity, string blend) =>
        new(false, pictureId, asset, x, y, opacity, blend, ShowKind);

    public static MapEventPictureOp ForErase(int pictureId) =>
        new(true, pictureId, string.Empty, 0, 0, MapEventPicture.MaxOpacity, MapEventPicture.BlendNormal, EraseKind);

    public static MapEventPictureOp ForMove(int pictureId, int x, int y, int opacity, string blend) =>
        new(false, pictureId, string.Empty, x, y, opacity, blend, MoveKind);

    public static MapEventPictureOp ForTint(int pictureId, int red, int green, int blue, int tintOpacity) =>
        new(
            false,
            pictureId,
            string.Empty,
            0,
            0,
            0,
            MapEventPicture.BlendNormal,
            TintKind,
            red,
            green,
            blue,
            tintOpacity);

    public MapEventShownPicture ToShown() => new(PictureId, Asset, X, Y, Opacity, Blend);
}

/// <summary>Applique une opération au dictionnaire de slots (session ou bac à sable).</summary>
public static class MapEventPictureSlots
{
    public static void Apply(IDictionary<int, MapEventShownPicture> pictures, MapEventPictureOp op)
    {
        ArgumentNullException.ThrowIfNull(pictures);
        if (op.IsErase)
        {
            pictures.Remove(op.PictureId);
            return;
        }

        if (op.IsMove)
        {
            if (pictures.TryGetValue(op.PictureId, out var moving))
            {
                pictures[op.PictureId] = moving with
                {
                    X = op.X,
                    Y = op.Y,
                    Opacity = op.Opacity,
                    Blend = op.Blend,
                };
            }

            return;
        }

        if (op.IsTint)
        {
            if (pictures.TryGetValue(op.PictureId, out var tinted))
            {
                pictures[op.PictureId] = tinted with
                {
                    TintRed = op.Red,
                    TintGreen = op.Green,
                    TintBlue = op.Blue,
                    TintOpacity = op.TintOpacity,
                };
            }

            return;
        }

        pictures[op.PictureId] = op.ToShown();
    }
}
