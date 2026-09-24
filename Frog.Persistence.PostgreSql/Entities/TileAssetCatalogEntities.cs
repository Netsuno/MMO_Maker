namespace Frog.Persistence.PostgreSql.Entities;

/// <summary>
/// Tuile 48×48. <see cref="PngBytes"/> porte le RGBA prémultiplié canonique (9216 octets) en publication serveur V1.
/// Le nom de colonne vient de la migration <c>20260924214100_TileAssetCatalog</c>.
/// </summary>
public sealed class ContentTileEntity
{
    public string TileAssetId { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public byte[] PngBytes { get; set; } = Array.Empty<byte>();

    public short WidthPx { get; set; } = 48;

    public short HeightPx { get; set; } = 48;

    public string? DisplayName { get; set; }

    public string[] Tags { get; set; } = Array.Empty<string>();

    public string MetaJson { get; set; } = "{}";

    public int ContentBytesLen { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>Paquet versionné. Publié et retiré sont immuables côté déclencheur SQL.</summary>
public sealed class ContentTilePackEntity
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public short Status { get; set; }

    public string? FrogpackSha256 { get; set; }

    public byte[]? FrogpackBytes { get; set; }

    public int? FrogpackBytesLen { get; set; }

    public byte[]? Ed25519Signature { get; set; }

    public string? Ed25519PublicKeyId { get; set; }

    public int EntryCount { get; set; }

    public string ManifestJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }
}

public sealed class ContentTilePackEntryEntity
{
    public Guid PackId { get; set; }

    public string TileAssetId { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public string EntryMetaJson { get; set; } = "{}";
}
