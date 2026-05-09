namespace Frog.Core.Constants;

/// <summary>
/// Contrat TCP partagé client/serveur : incrémenter quand le format des frames ou Hello change de façon incompatible.
/// Voir aussi <see cref="IO.MapSerializer.MapFileFormatVersion"/> pour les blobs carte dans MapData / fichiers .fmap.
/// </summary>
public static class FrogWireProtocol
{
    /// <summary>Valeur émise par le serveur dans <c>Hello</c> après le message UTF‑8 (<see cref="Protocol.WireHello"/>).</summary>
    /// <summary>Résultat client : <b>8</b> — <see cref="PositionSyncRequest"/> (client pilote le mouvement, serveur valide et relaye).</summary>
    public const ushort Version = 8;
}
