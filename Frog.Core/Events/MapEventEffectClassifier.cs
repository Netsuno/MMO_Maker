using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Classifie un effet plat (après planification) pour une unité transactionnelle unique.
/// <c>start_dialogue</c> et <c>teleport</c> restent des effets de session, mais appartiennent
/// à la même unité que les mutations persistantes : PG les enregistre dans le snapshot
/// de la même TX et ne les applique qu'après commit.
/// </summary>
public enum MapEventEffectCommitKind
{
    UnresolvedControlFlow,
    Persistent,
    SessionSide,
    Unknown,
}

public static class MapEventEffectClassifier
{
    public static MapEventEffectCommitKind Classify(string? discriminator) =>
        discriminator switch
        {
            MapEventCommandDiscriminators.Branch
                or MapEventCommandDiscriminators.CallCommonEvent =>
                MapEventEffectCommitKind.UnresolvedControlFlow,
            MapEventCommandDiscriminators.ShowText
                or MapEventCommandDiscriminators.SetSwitch
                or MapEventCommandDiscriminators.SetVariable
                or MapEventCommandDiscriminators.AddVariable
                or MapEventCommandDiscriminators.SubVariable
                or MapEventCommandDiscriminators.GiveItem
                or MapEventCommandDiscriminators.TakeItem
                or MapEventCommandDiscriminators.GiveGold
                or MapEventCommandDiscriminators.TakeGold
                or MapEventCommandDiscriminators.StartQuest
                or MapEventCommandDiscriminators.AdvanceQuest
                or MapEventCommandDiscriminators.TurnInQuest
                or MapEventCommandDiscriminators.LearnProfession
                or MapEventCommandDiscriminators.Wait =>
                MapEventEffectCommitKind.Persistent,
            MapEventCommandDiscriminators.StartDialogue
                or MapEventCommandDiscriminators.Teleport =>
                MapEventEffectCommitKind.SessionSide,
            _ => MapEventEffectCommitKind.Unknown,
        };

    public static bool IsUnresolvedControlFlow(string? discriminator) =>
        Classify(discriminator) == MapEventEffectCommitKind.UnresolvedControlFlow;

    public static bool IsSessionSide(string? discriminator) =>
        Classify(discriminator) == MapEventEffectCommitKind.SessionSide;

    public static bool IsPersistent(string? discriminator) =>
        Classify(discriminator) == MapEventEffectCommitKind.Persistent;

    /// <summary>
    /// True si tous les effets peuvent être commis comme <em>une</em> unité
    /// (persistants + session-side mélangés, sans branche / common-event restants).
    /// </summary>
    public static bool IsUnifiedTransactionalUnit(IReadOnlyList<MapEventCommandDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects.Count == 0)
        {
            return true;
        }

        foreach (var effect in effects)
        {
            var kind = Classify(effect.Discriminator);
            if (kind is MapEventEffectCommitKind.UnresolvedControlFlow or MapEventEffectCommitKind.Unknown)
            {
                return false;
            }
        }

        return true;
    }

    public static bool ContainsUnresolvedControlFlow(IReadOnlyList<MapEventCommandDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        foreach (var effect in effects)
        {
            if (IsUnresolvedControlFlow(effect.Discriminator))
            {
                return true;
            }
        }

        return false;
    }
}
