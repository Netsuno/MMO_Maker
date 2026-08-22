using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>Point d’apparition configurable pour une session playtest.</summary>
public sealed class PlaytestSpawnPoint
{
    public int RuntimeMapId { get; init; } = 1;
    public int TileX { get; init; }
    public int TileY { get; init; }
}

/// <summary>Carte publiée prête pour le runtime serveur (identifiant int + blob .fmap).</summary>
public sealed class PlaytestRuntimeMap
{
    public required Guid CanonicalMapId { get; init; }
    public required long PublishedRevision { get; init; }
    public required int RuntimeMapId { get; init; }
    public required string Name { get; init; }
    public required Map Map { get; init; }
    public required byte[] SerializedFmap { get; init; }
}

/// <summary>Plan de lancement playtest (jamais un brouillon non publié).</summary>
public sealed class PlaytestLaunchPlan
{
    public required Guid CorrelationId { get; init; }
    public required Guid PrimaryCanonicalMapId { get; init; }
    public required long PrimaryPublishedRevision { get; init; }
    public required PlaytestSpawnPoint Spawn { get; init; }
    public required IReadOnlyList<PlaytestRuntimeMap> Maps { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string WorkDirectory { get; init; }
    public required string ManifestPath { get; init; }
}

public abstract record PlaytestPreparationResult
{
    public sealed record Success(PlaytestLaunchPlan Plan) : PlaytestPreparationResult;

    public sealed record Failed(string Error, PlaytestFailureKind Kind) : PlaytestPreparationResult;
}

public enum PlaytestFailureKind
{
    Validation,
    DirtyUnsaved,
    NotDurable,
    NotPublished,
    MissingPublishedRevision,
    Cancellation,
    Timeout,
    LaunchFailure,
    Persistence,
}

/// <summary>Manifeste JSON lu par le serveur en mode playtest (aucune chaîne PostgreSQL).</summary>
public sealed class PlaytestManifestDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required Guid CorrelationId { get; init; }
    public required Guid PrimaryCanonicalMapId { get; init; }
    public required long PrimaryPublishedRevision { get; init; }
    public required PlaytestSpawnPoint Spawn { get; init; }
    public required IReadOnlyList<PlaytestManifestMapEntry> Maps { get; init; }
}

public sealed class PlaytestManifestMapEntry
{
    public required Guid CanonicalMapId { get; init; }
    public required long PublishedRevision { get; init; }
    public required int RuntimeMapId { get; init; }
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
}
