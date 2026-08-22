using Frog.Application.Playtest;

namespace Frog.Server.Config;

/// <summary>Options runtime playtest (env / manifeste). Désactivé si <see cref="Enabled"/> est faux.</summary>
public sealed class PlaytestRuntimeOptions
{
    public const string ManifestPathEnvironmentVariable = "FROG_PLAYTEST_MANIFEST_PATH";
    public const string CorrelationIdEnvironmentVariable = "FROG_PLAYTEST_CORRELATION_ID";
    public const string PortEnvironmentVariable = "FROG_PLAYTEST_PORT";

    public bool Enabled { get; init; }
    public string? ManifestPath { get; init; }
    public Guid CorrelationId { get; init; }
    public int SpawnTileX { get; init; }
    public int SpawnTileY { get; init; }
    public int SpawnRuntimeMapId { get; init; } = 1;
    public Guid PrimaryCanonicalMapId { get; init; }
    public long PrimaryPublishedRevision { get; init; }

    public static PlaytestRuntimeOptions FromEnvironment()
    {
        var manifestPath = Environment.GetEnvironmentVariable(ManifestPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return new PlaytestRuntimeOptions { Enabled = false };
        }

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Manifeste playtest introuvable.", manifestPath);
        }

        var doc = PlaytestManifestWriter.Read(manifestPath);
        var correlationEnv = Environment.GetEnvironmentVariable(CorrelationIdEnvironmentVariable);
        var correlationId = Guid.TryParse(correlationEnv, out var parsed) ? parsed : doc.CorrelationId;

        return new PlaytestRuntimeOptions
        {
            Enabled = true,
            ManifestPath = Path.GetFullPath(manifestPath),
            CorrelationId = correlationId,
            SpawnTileX = doc.Spawn.TileX,
            SpawnTileY = doc.Spawn.TileY,
            SpawnRuntimeMapId = doc.Spawn.RuntimeMapId,
            PrimaryCanonicalMapId = doc.PrimaryCanonicalMapId,
            PrimaryPublishedRevision = doc.PrimaryPublishedRevision,
        };
    }
}
