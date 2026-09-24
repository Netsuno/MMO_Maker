namespace Frog.Core.Distribution;

/// <summary>
/// <c>.frogpack</c> V1 — artefact immuable adressé par hash, paquet complet (pas de delta).
/// Entiers little-endian. Chiffrement : bit réservé, éteint. Toute incohérence (magic, version,
/// flags, SHA-256, signature Ed25519, hash de blob) est refusée ; le lecteur ne renvoie pas un paquet partiel.
/// </summary>
public static class FrogPackFormat
{
    public const string Magic = "FPK1";
    public const ushort FormatVersion = 1;
    public const ushort FlagsNone = 0;

    /// <summary>Bit 0. Les writers V1 l’écrivent à 0. Les readers refusent toute valeur non nulle.</summary>
    public const ushort FlagEncrypted = 1;

    public const int HeaderLength = 16;
    public const int ManifestEntryLength = 76;
    public const int ContentHashLength = 32;
    public const int PublicKeyLength = 32;
    public const int SignatureLength = 64;
    public const int TrailerLength = ContentHashLength + PublicKeyLength + SignatureLength;
    public const int MaxTileCount = 8192;

    public const int BlobOffsetField = 32;
    public const int BlobSizeField = 40;
    public const int BlobHashField = 44;
}
