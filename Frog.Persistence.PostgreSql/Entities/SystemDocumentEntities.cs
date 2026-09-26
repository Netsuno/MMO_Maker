using Frog.Application.Content;

namespace Frog.Persistence.PostgreSql.Entities;

/// <summary>Brouillon unique du catalogue Système (interrupteurs et variables nommés).</summary>
public sealed class SystemDocumentEntity
{
    public Guid Id { get; set; }

    public string SwitchesJson { get; set; } = "[]";

    public string VariablesJson { get; set; } = "[]";

    public ContentPublishStatus Status { get; set; }

    public long Revision { get; set; }

    public long? PublishedRevision { get; set; }

    public Guid? PublishedSnapshotId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable du document Système publié.</summary>
public sealed class SystemPublishedSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid SystemDocumentId { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset PublishedAtUtc { get; set; }

    public string SwitchesJson { get; set; } = "[]";

    public string VariablesJson { get; set; } = "[]";

    public SystemDocumentEntity Document { get; set; } = null!;
}

public sealed class SystemPublicationHistoryEntity
{
    public Guid Id { get; set; }

    public Guid SystemDocumentId { get; set; }

    public Guid SnapshotId { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset PublishedAtUtc { get; set; }

    public SystemDocumentEntity Document { get; set; } = null!;
}
