using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Core.IO;

/// <summary>
/// Versions <c>.fmap</c>. La lecture accepte v3, v4, v5 et v6.
/// <see cref="MapSerializer.Serialize"/> écrit v5 pour <see cref="TileGraphicIdentity.SheetSource"/>
/// (cartes actuelles, éditeur, undo) et v6 pour <see cref="TileGraphicIdentity.TileAsset"/>.
/// <see cref="Write"/> est le chemin des nouvelles sauvegardes adressées par <c>TileAssetId</c> (toujours v6).
/// Le protocole TCP (<see cref="Frog.Core.Constants.FrogWireProtocol.Version"/>) ne bouge pas avec ce format.
/// </summary>
public static class MapFormat
{
    public const byte Version5 = 5;
    public const byte Version6 = 6;

    /// <summary>Version d’écriture des cartes TileAsset. Les cartes feuille restent en v5 via <see cref="MapSerializer"/>.</summary>
    public const byte CurrentWriteVersion = Version6;

    public static Map CreateTileAssetMap(string name, int width, int height)
    {
        return new Map
        {
            Name = name,
            Width = width,
            Height = height,
            GraphicIdentity = TileGraphicIdentity.TileAsset,
            TileSizePixels = TileAssetMetrics.TargetTileSizePixels,
        };
    }

    /// <summary>Écrit une carte v6. Refuse une carte encore identifiée par coordonnées de feuille.</summary>
    public static byte[] Write(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.GraphicIdentity != TileGraphicIdentity.TileAsset)
        {
            throw new InvalidDataException(
                "MapFormat.Write enregistre en v6 (TileAssetId). Une carte feuille (v5) passe par MapSerializer.Serialize, sans conversion silencieuse.");
        }

        return new MapSerializer().Serialize(map);
    }

    public static Map Read(ReadOnlySpan<byte> data) => new MapSerializer().Deserialize(data);
}
