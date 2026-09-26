using Frog.Application.Content;
using Frog.Core.Enums;

namespace Frog.Persistence.PostgreSql.Entities;

public sealed class GameSystemEntryEntity
{
    public Guid Id { get; set; }
    public GameSystemEntryKind Kind { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string StartingBgmAsset { get; set; } = string.Empty;
    public int StartingBgmVolume { get; set; } = 100;
    public int StartingBgmFadeMs { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’une entrée système publiée.</summary>
public sealed class GameSystemPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public GameSystemEntryKind Kind { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string StartingBgmAsset { get; set; } = string.Empty;
    public int StartingBgmVolume { get; set; } = 100;
    public int StartingBgmFadeMs { get; set; }
    public GameSystemEntryEntity Entry { get; set; } = null!;
}

public sealed class GameSystemPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public GameSystemEntryEntity Entry { get; set; } = null!;
}
