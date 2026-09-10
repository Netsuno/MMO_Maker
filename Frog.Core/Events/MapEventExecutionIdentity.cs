namespace Frog.Core.Events;

/// <summary>
/// Identité stable d'une exécution d'événement carte, prête pour le ledger PostgreSQL (J4-PG).
/// La clé d'idempotence est <c>(CharacterId, RequestId)</c> ; <see cref="PlacementId"/> et
/// <see cref="CatalogAliasId"/> lient la requête à un placement catalogue.
/// </summary>
public readonly record struct MapEventExecutionIdentity(
    Guid RequestId,
    Guid CharacterId,
    long PlacementId,
    int CatalogAliasId)
{
    public bool IsValid => RequestId != Guid.Empty && CharacterId != Guid.Empty;

    /// <summary>Clé ledger (characterId, requestId) consommée par J4-PG.</summary>
    public (Guid CharacterId, Guid RequestId) LedgerKey => (CharacterId, RequestId);

    public static MapEventExecutionIdentity Create(
        Guid characterId,
        long placementId,
        int catalogAliasId,
        Guid? requestId = null)
    {
        var id = requestId is { } rid && rid != Guid.Empty ? rid : Guid.NewGuid();
        return new MapEventExecutionIdentity(id, characterId, placementId, catalogAliasId);
    }
}
