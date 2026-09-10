using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Frog.Core.Events;

/// <summary>
/// Identité d'une <em>activation</em> d'événement carte, prête pour le ledger PostgreSQL (J4-PG).
/// La clé d'idempotence reste <c>(CharacterId, RequestId)</c>.
/// <see cref="ActivationId"/> est unique par activation (pas par placement de session) :
/// un replay / reconnect reconstitue la même identité, une réactivation légitime en crée une nouvelle.
/// <see cref="WaitOrdinal"/> lie chaque reprise <c>wait</c> au même <see cref="ActivationId"/>
/// avec un <see cref="RequestId"/> dérivé déterministe (nouvelle ligne ledger, pas un replay du préfixe).
/// </summary>
public readonly record struct MapEventExecutionIdentity(
    Guid RequestId,
    Guid CharacterId,
    long PlacementId,
    int CatalogAliasId,
    Guid ActivationId = default,
    int WaitOrdinal = 0)
{
    public bool IsValid =>
        RequestId != Guid.Empty && CharacterId != Guid.Empty && WaitOrdinal >= 0;

    /// <summary>
    /// Identifiant d'activation : <see cref="ActivationId"/> s'il est renseigné,
    /// sinon repli compatibilité sur <see cref="RequestId"/> (constructeur historique 4 args).
    /// </summary>
    public Guid EffectiveActivationId => ActivationId != Guid.Empty ? ActivationId : RequestId;

    /// <summary>Clé ledger (characterId, requestId) consommée par J4-PG.</summary>
    public (Guid CharacterId, Guid RequestId) LedgerKey => (CharacterId, RequestId);

    public bool IsInitialActivation => WaitOrdinal == 0;

    /// <summary>
    /// Nouvelle activation légitime. Distincte de tout replay : un nouvel
    /// <see cref="ActivationId"/> (et donc un nouveau <see cref="RequestId"/>).
    /// </summary>
    public static MapEventExecutionIdentity Create(
        Guid characterId,
        long placementId,
        int catalogAliasId,
        Guid? requestId = null)
    {
        var id = requestId is { } rid && rid != Guid.Empty ? rid : Guid.NewGuid();
        return new MapEventExecutionIdentity(
            id,
            characterId,
            placementId,
            catalogAliasId,
            id,
            WaitOrdinal: 0);
    }

    /// <summary>Alias explicite de <see cref="Create"/> (identité par activation, pas par placement de session).</summary>
    public static MapEventExecutionIdentity BeginActivation(
        Guid characterId,
        long placementId,
        int catalogAliasId,
        Guid? activationId = null) =>
        Create(characterId, placementId, catalogAliasId, activationId);

    /// <summary>
    /// Reconstitue l'identité après reconnect / replay : même activation, même ordinal de wait,
    /// donc même <see cref="LedgerKey"/>.
    /// </summary>
    public static MapEventExecutionIdentity Restore(
        Guid characterId,
        long placementId,
        int catalogAliasId,
        Guid activationId,
        int waitOrdinal = 0)
    {
        if (activationId == Guid.Empty || waitOrdinal < 0)
        {
            return new MapEventExecutionIdentity(
                Guid.Empty,
                characterId,
                placementId,
                catalogAliasId,
                Guid.Empty,
                waitOrdinal);
        }

        var requestId = DeriveLedgerRequestId(activationId, waitOrdinal);
        return new MapEventExecutionIdentity(
            requestId,
            characterId,
            placementId,
            catalogAliasId,
            activationId,
            waitOrdinal);
    }

    /// <summary>
    /// Reprise transactionnelle après un <c>wait</c> : même activation, nouvel ordinal,
    /// <see cref="RequestId"/> dérivé (clé ledger distincte du préfixe déjà commis).
    /// </summary>
    public MapEventExecutionIdentity ForWaitResume(int waitOrdinal)
    {
        var activationId = EffectiveActivationId;
        if (activationId == Guid.Empty || waitOrdinal < 0)
        {
            return this with { WaitOrdinal = waitOrdinal, RequestId = Guid.Empty };
        }

        return new MapEventExecutionIdentity(
            DeriveLedgerRequestId(activationId, waitOrdinal),
            CharacterId,
            PlacementId,
            CatalogAliasId,
            activationId,
            waitOrdinal);
    }

    public bool IsSameActivation(in MapEventExecutionIdentity other) =>
        CharacterId == other.CharacterId
        && PlacementId == other.PlacementId
        && CatalogAliasId == other.CatalogAliasId
        && EffectiveActivationId == other.EffectiveActivationId
        && EffectiveActivationId != Guid.Empty;

    /// <summary>
    /// <see cref="RequestId"/> déterministe pour un ordinal de wait.
    /// Ordinal 0 = l'activation elle-même (RequestId == ActivationId).
    /// </summary>
    public static Guid DeriveLedgerRequestId(Guid activationId, int waitOrdinal)
    {
        if (waitOrdinal == 0)
        {
            return activationId;
        }

        Span<byte> data = stackalloc byte[20];
        activationId.TryWriteBytes(data);
        BinaryPrimitives.WriteInt32LittleEndian(data[16..], waitOrdinal);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
