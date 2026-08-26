using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Carte publiée exposée au runtime serveur (Guid stable + identifiant int de session).</summary>
public sealed record PublishedMapRuntimeEntry(
    Guid MapId,
    int RuntimeMapId,
    long PublishedRevision,
    Map Map,
    byte[] SerializedFmap,
    string ContentSha256Hex);

/// <summary>Point d'entrée / respawn validé depuis la configuration monde publiée.</summary>
public sealed record PublishedWorldSpawnConfig(
    Guid StartMapId,
    int StartRuntimeMapId,
    int StartTileX,
    int StartTileY,
    Guid RespawnMapId,
    int RespawnRuntimeMapId,
    int RespawnTileX,
    int RespawnTileY);

/// <summary>Spawn monstre publié (carte + NPC Guid + tuile).</summary>
public sealed record PublishedMonsterSpawnEntry(
    Guid MapId,
    int RuntimeMapId,
    Guid NpcId,
    int TileX,
    int TileY,
    byte Direction);

/// <summary>Catalogue monde publié (cartes + spawns) — consommation serveur Phase 7.</summary>
public interface IPublishedWorldCatalog
{
    Task<IReadOnlyList<PublishedMapRuntimeEntry>> ListPublishedMapsAsync(
        CancellationToken cancellationToken = default);

    Task<PublishedMapRuntimeEntry?> LoadPublishedMapAsync(
        Guid mapId,
        CancellationToken cancellationToken = default);

    Task<PublishedWorldSpawnConfig> GetSpawnConfigAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublishedMonsterSpawnEntry>> ListMonsterSpawnsAsync(
        CancellationToken cancellationToken = default);

    bool TryGetRuntimeMapId(Guid mapId, out int runtimeMapId);

    bool TryGetMapGuid(int runtimeMapId, out Guid mapId);
}
