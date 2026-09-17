using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Unité transactionnelle d'une activation (ou d'une reprise wait) : tous les effets
/// jusqu'au premier <c>wait</c> inclus, y compris <c>start_dialogue</c> / <c>teleport</c>.
/// J4-PG doit les committer dans une seule TX + une ligne ledger
/// (<see cref="MapEventExecutionIdentity.LedgerKey"/>).
/// </summary>
public sealed record MapEventTransactionalUnit(
    MapEventExecutionIdentity Identity,
    IReadOnlyList<MapEventCommandDefinition> CommitEffects,
    IReadOnlyList<MapEventCommandDefinition> SessionSideEffects,
    MapEventWaitBoundary? Wait,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public bool HasSessionSideEffects => SessionSideEffects.Count > 0;

    public bool HasWait => Wait is not null;

    /// <summary>Plan de reprise ledger-bound (même activation, RequestId dérivé).</summary>
    public MapEventExecutionPlan? ResumePlan =>
        Wait is { RemainingEffects.Count: > 0 } wait
            ? MapEventExecutionPlan.Ok(wait.ResumeIdentity, wait.RemainingEffects)
            : null;

    public static MapEventTransactionalUnit Fail(MapEventExecutionIdentity identity, string error) =>
        new(identity, Array.Empty<MapEventCommandDefinition>(), Array.Empty<MapEventCommandDefinition>(), null, error);

    public static MapEventTransactionalUnit FromPlan(MapEventExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsSuccess)
        {
            return Fail(plan.Identity, plan.Error ?? "Planification événement échouée.");
        }

        if (!plan.Identity.IsValid)
        {
            return Fail(plan.Identity, "Identité d'exécution invalide.");
        }

        if (MapEventEffectClassifier.ContainsUnresolvedControlFlow(plan.Effects))
        {
            return Fail(plan.Identity, "Plan incomplet: branche ou common-event non résolu.");
        }

        if (!MapEventEffectClassifier.IsUnifiedTransactionalUnit(plan.Effects))
        {
            var unknown = plan.Effects
                .FirstOrDefault(e => MapEventEffectClassifier.Classify(e.Discriminator)
                    == MapEventEffectCommitKind.Unknown);
            return Fail(
                plan.Identity,
                unknown is null
                    ? "Effet hors unité transactionnelle."
                    : $"Commande inconnue: {unknown.Discriminator}.");
        }

        var waitIndex = -1;
        for (var i = 0; i < plan.Effects.Count; i++)
        {
            if (plan.Effects[i].Discriminator == MapEventCommandDiscriminators.Wait)
            {
                waitIndex = i;
                break;
            }
        }

        IReadOnlyList<MapEventCommandDefinition> commitEffects;
        MapEventWaitBoundary? wait = null;
        if (waitIndex < 0)
        {
            commitEffects = plan.Effects;
        }
        else
        {
            if (!MapEventParameterSchemas.TryParseWait(plan.Effects[waitIndex].ParameterJson, out var waitMs, out var waitErr))
            {
                return Fail(plan.Identity, waitErr ?? "wait invalide.");
            }

            commitEffects = plan.Effects.Take(waitIndex + 1).ToArray();
            var remaining = plan.Effects.Skip(waitIndex + 1).ToArray();
            var resumeIdentity = plan.Identity.ForWaitResume(plan.Identity.WaitOrdinal + 1);
            wait = new MapEventWaitBoundary(waitMs, resumeIdentity, remaining);
        }

        var sessionSide = commitEffects
            .Where(e => MapEventEffectClassifier.IsSessionSide(e.Discriminator))
            .ToArray();
        return new MapEventTransactionalUnit(plan.Identity, commitEffects, sessionSide, wait, null);
    }
}

/// <summary>
/// Frontière <c>wait</c> : le préfixe (wait inclus) est une TX ; la suite est une
/// nouvelle TX liée à la même activation via <see cref="ResumeIdentity"/>.
/// </summary>
public sealed record MapEventWaitBoundary(
    int WaitMilliseconds,
    MapEventExecutionIdentity ResumeIdentity,
    IReadOnlyList<MapEventCommandDefinition> RemainingEffects);
