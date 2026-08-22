using Frog.Application.Playtest;

namespace Frog.Server.Config;

/// <summary>Options runtime playtest (env / manifeste). Désactivé si <see cref="Enabled"/> est faux.</summary>
public sealed class PlaytestRuntimeOptions
{
    public const string ManifestPathEnvironmentVariable = "FROG_PLAYTEST_MANIFEST_PATH";
    public const string CorrelationIdEnvironmentVariable = "FROG_PLAYTEST_CORRELATION_ID";
    public const string PortEnvironmentVariable = "FROG_PLAYTEST_PORT";
    public const string BindAddressEnvironmentVariable = "FROG_PLAYTEST_BIND_ADDRESS";

    public bool Enabled { get; init; }
    public string? ManifestPath { get; init; }
    public Guid CorrelationId { get; init; }
    public int SpawnTileX { get; init; }
    public int SpawnTileY { get; init; }
    public int SpawnRuntimeMapId { get; init; } = 1;
    public Guid PrimaryCanonicalMapId { get; init; }
    public long PrimaryPublishedRevision { get; init; }
    public string BindAddress { get; init; } = "127.0.0.1";
    /// <summary>Port TCP playtest (prioritaire sur l’env process — évite les courses entre tests).</summary>
    public int Port { get; init; }
    /// <summary>Jeton playtest (env uniquement — jamais loggé).</summary>
    public string? AuthToken { get; init; }

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
        var bind = Environment.GetEnvironmentVariable(BindAddressEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(bind))
        {
            bind = "127.0.0.1";
        }

        var token = Environment.GetEnvironmentVariable(PlaytestAuthToken.EnvironmentVariable);
        var port = 0;
        var portEnv = Environment.GetEnvironmentVariable(PortEnvironmentVariable);
        _ = int.TryParse(portEnv, out port);

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
            BindAddress = bind,
            Port = port is > 0 and <= 65535 ? port : 0,
            AuthToken = string.IsNullOrWhiteSpace(token) ? null : token,
        };
    }
}
