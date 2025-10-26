// #TODO (FR) : Implémenter le format binaire des cartes compatible VB6.
// Inclure un en-tête versionné pour permettre les évolutions futures.
#nullable enable
namespace Frog.Core.IO;

using Frog.Core.Models;

/// <summary>
/// Sérialise/Désérialise une <see cref="Map"/> en binaire.
/// Doit être isomorphe au format VB6 pour assurer la migration progressive.
/// </summary>
public sealed class MapSerializer : ISerializer<Map>
{
    public byte[] Serialize(Map value)
    {
        // #TODO (FR) : Écrire largeur/hauteur, couches, tuiles, attributs, métadonnées.
        // Décider si les chaînes sont UTF-8 + longueur (VarInt?) ou taille fixe.
        throw new System.NotImplementedException();
    }

    public Map Deserialize(ReadOnlySpan<byte> data)
    {
        // #TODO (FR) : Lire l’en-tête (version), puis hydrater Map et ses couches/tuiles.
        throw new System.NotImplementedException();
    }
}
