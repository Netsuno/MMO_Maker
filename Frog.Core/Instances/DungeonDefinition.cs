using Frog.Core.Enums;

namespace Frog.Core.Instances;

/// <summary>Catalogue donjon / raid — template carte + spawn. Pas un moteur procgen.</summary>
public sealed record DungeonDefinition(
    Guid Id,
    string Name,
    InstanceHubKind Kind,
    int TemplateMapId,
    int SpawnTileX,
    int SpawnTileY,
    int MinPartySize);
