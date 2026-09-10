using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Séquence d'effets déterministe après résolution Branch + CommonEvent (avant mutation persistante).</summary>
public sealed record MapEventExecutionPlan(
    MapEventExecutionIdentity Identity,
    IReadOnlyList<MapEventCommandDefinition> Effects,
    string? Error)
{
    public bool IsSuccess => Error is null;

    /// <summary>Découpe le plan en unité transactionnelle (wait + intents session inclus).</summary>
    public MapEventTransactionalUnit AsTransactionalUnit() => MapEventTransactionalUnit.FromPlan(this);

    public static MapEventExecutionPlan Ok(
        MapEventExecutionIdentity identity,
        IReadOnlyList<MapEventCommandDefinition> effects) =>
        new(identity, effects, null);

    public static MapEventExecutionPlan Fail(MapEventExecutionIdentity identity, string error) =>
        new(identity, Array.Empty<MapEventCommandDefinition>(), error);
}

/// <summary>Résolution de commandes sans identité (expansion common-event seule, compatibilité serveur).</summary>
public sealed record MapEventCommandResolution(
    IReadOnlyList<MapEventCommandDefinition> Effects,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static MapEventCommandResolution Ok(IReadOnlyList<MapEventCommandDefinition> effects) =>
        new(effects, null);

    public static MapEventCommandResolution Fail(string error) =>
        new(Array.Empty<MapEventCommandDefinition>(), error);
}
