using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Persistence.PostgreSql.Entities;

public sealed class Phase8ContentDefinitionEntity
{
    public Guid Id { get; set; }

    public byte Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? EditorAliasId { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public ContentPublishStatus Status { get; set; }

    public long Revision { get; set; }

    public long? PublishedRevision { get; set; }

    public Guid? PublishedSnapshotId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class Phase8ContentPublishedSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid ContentDefinitionId { get; set; }

    public byte Kind { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset PublishedAtUtc { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? EditorAliasId { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public Phase8ContentDefinitionEntity ContentDefinition { get; set; } = null!;
}

public sealed class Phase8ContentPublicationHistoryEntity
{
    public Guid Id { get; set; }

    public Guid ContentDefinitionId { get; set; }

    public Guid SnapshotId { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset PublishedAtUtc { get; set; }

    public Phase8ContentDefinitionEntity ContentDefinition { get; set; } = null!;
}
