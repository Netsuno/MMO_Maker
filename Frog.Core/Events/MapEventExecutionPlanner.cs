using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Résout branches et appels common-event avant toute mutation persistante (J4-CORE).
/// Produit une séquence d'effets plate et une identité d'exécution stable pour J4-PG.
/// </summary>
public static class MapEventExecutionPlanner
{
    public static MapEventExecutionPlan Plan(
        IReadOnlyList<MapEventCommandDefinition> commands,
        IMapEventCommonEventSource commonEvents,
        Func<MapEventConditionDefinition, bool> evaluateCondition,
        MapEventExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(evaluateCondition);
        return PlanAsync(
                commands,
                commonEvents,
                condition => Task.FromResult(evaluateCondition(condition)),
                identity)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task<MapEventExecutionPlan> PlanAsync(
        IReadOnlyList<MapEventCommandDefinition> commands,
        IMapEventCommonEventSource commonEvents,
        Func<MapEventConditionDefinition, Task<bool>> evaluateCondition,
        MapEventExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(commonEvents);
        ArgumentNullException.ThrowIfNull(evaluateCondition);

        if (!identity.IsValid)
        {
            return MapEventExecutionPlan.Fail(identity, "Identité d'exécution invalide.");
        }

        var effects = new List<MapEventCommandDefinition>();
        var steps = new StepCounter();
        var error = await ExpandAsync(
                commands,
                commonEvents,
                evaluateCondition,
                effects,
                callStack: new HashSet<Guid>(),
                branchDepth: 0,
                commonEventDepth: 0,
                steps,
                resolveBranches: true,
                useRuntimeCommonEventLimits: true,
                cancellationToken)
            .ConfigureAwait(false);

        return error is null
            ? MapEventExecutionPlan.Ok(identity, effects)
            : MapEventExecutionPlan.Fail(identity, error);
    }

    /// <summary>
    /// Expansion common-event seule (branches conservées), extraite du planificateur serveur historique.
    /// </summary>
    public static async Task<MapEventCommandResolution> ExpandCommonEventsAsync(
        IReadOnlyList<MapEventCommandDefinition> commands,
        IMapEventCommonEventSource commonEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(commonEvents);

        var effects = new List<MapEventCommandDefinition>();
        var error = await ExpandAsync(
                commands,
                commonEvents,
                evaluateCondition: static _ => Task.FromResult(true),
                effects,
                callStack: new HashSet<Guid>(),
                branchDepth: 0,
                commonEventDepth: 0,
                new StepCounter(),
                resolveBranches: false,
                useRuntimeCommonEventLimits: false,
                cancellationToken)
            .ConfigureAwait(false);

        return error is null
            ? MapEventCommandResolution.Ok(effects)
            : MapEventCommandResolution.Fail(error);
    }

    private static async Task<string?> ExpandAsync(
        IReadOnlyList<MapEventCommandDefinition> commands,
        IMapEventCommonEventSource commonEvents,
        Func<MapEventConditionDefinition, Task<bool>> evaluateCondition,
        List<MapEventCommandDefinition> output,
        HashSet<Guid> callStack,
        int branchDepth,
        int commonEventDepth,
        StepCounter steps,
        bool resolveBranches,
        bool useRuntimeCommonEventLimits,
        CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (resolveBranches && ++steps.Value > MapEventRuntimeLimits.MaxExecutionSteps)
            {
                return "Limite d'exécution événement atteinte.";
            }

            if (command.Discriminator == MapEventCommandDiscriminators.Branch)
            {
                if (!resolveBranches)
                {
                    output.Add(command);
                    continue;
                }

                if (branchDepth >= MapEventRuntimeLimits.MaxBranchDepth)
                {
                    return "Profondeur de branche excessive.";
                }

                if (!MapEventParameterSchemas.TryParseBranch(
                        command.ParameterJson,
                        out var condition,
                        out var thenCommands,
                        out var elseCommands,
                        out var parseErr))
                {
                    return parseErr;
                }

                if (!condition.Validate(out var conditionErr))
                {
                    return conditionErr;
                }

                var pass = await evaluateCondition(condition).ConfigureAwait(false);
                var taken = pass ? thenCommands : elseCommands;
                var nestedErr = await ExpandAsync(
                        taken,
                        commonEvents,
                        evaluateCondition,
                        output,
                        callStack,
                        branchDepth + 1,
                        commonEventDepth,
                        steps,
                        resolveBranches: true,
                        useRuntimeCommonEventLimits,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (nestedErr is not null)
                {
                    return nestedErr;
                }

                continue;
            }

            if (command.Discriminator == MapEventCommandDiscriminators.CallCommonEvent)
            {
                var commonErr = await ExpandCommonEventAsync(
                        command,
                        commonEvents,
                        evaluateCondition,
                        output,
                        callStack,
                        branchDepth,
                        commonEventDepth,
                        steps,
                        resolveBranches,
                        useRuntimeCommonEventLimits,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (commonErr is not null)
                {
                    return commonErr;
                }

                continue;
            }

            if (resolveBranches)
            {
                if (!command.Validate(out var validateErr))
                {
                    return validateErr;
                }

                if (!MapEventCommandParameterValidator.ValidateParameters(command, out var paramErr))
                {
                    return paramErr;
                }
            }

            output.Add(command);
        }

        return null;
    }

    private static async Task<string?> ExpandCommonEventAsync(
        MapEventCommandDefinition command,
        IMapEventCommonEventSource commonEvents,
        Func<MapEventConditionDefinition, Task<bool>> evaluateCondition,
        List<MapEventCommandDefinition> output,
        HashSet<Guid> callStack,
        int branchDepth,
        int commonEventDepth,
        StepCounter steps,
        bool resolveBranches,
        bool useRuntimeCommonEventLimits,
        CancellationToken cancellationToken)
    {
        if (useRuntimeCommonEventLimits)
        {
            if (commonEventDepth + 1 > MapEventRuntimeLimits.MaxCommonEventRecursionDepth)
            {
                return "Profondeur call_common_event dépassée.";
            }
        }
        else if (commonEventDepth > MapEventRuntimeLimits.MaxBranchDepth)
        {
            return "Profondeur common-event excessive.";
        }

        if (!MapEventParameterSchemas.TryParseCallCommonEvent(
                command.ParameterJson,
                out var commonEventId,
                out var aliasId,
                out var parseErr))
        {
            return parseErr;
        }

        var definition = await ResolveDefinitionAsync(
                commonEvents,
                commonEventId,
                aliasId,
                cancellationToken)
            .ConfigureAwait(false);
        if (definition is null || definition.Pages.Count == 0)
        {
            return "Common event introuvable.";
        }

        if (!callStack.Add(definition.Id))
        {
            return "Cycle common-event détecté.";
        }

        try
        {
            IReadOnlyList<MapEventCommandDefinition> nestedCommands;
            if (resolveBranches)
            {
                var page = await MapEventPageSelector.SelectBestPageAsync(
                        definition.Pages,
                        placementTrigger: null,
                        evaluateCondition)
                    .ConfigureAwait(false);
                if (page is null)
                {
                    return "Aucune page active pour cet événement commun.";
                }

                nestedCommands = page.Commands;
            }
            else
            {
                nestedCommands = definition.Pages.OrderByDescending(p => p.Priority).First().Commands;
            }

            return await ExpandAsync(
                    nestedCommands,
                    commonEvents,
                    evaluateCondition,
                    output,
                    callStack,
                    branchDepth,
                    commonEventDepth + 1,
                    steps,
                    resolveBranches,
                    useRuntimeCommonEventLimits,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            callStack.Remove(definition.Id);
        }
    }

    private static async Task<CommonEventDefinition?> ResolveDefinitionAsync(
        IMapEventCommonEventSource commonEvents,
        Guid commonEventId,
        int? aliasId,
        CancellationToken cancellationToken)
    {
        if (commonEventId != Guid.Empty)
        {
            return await commonEvents.TryGetByIdAsync(commonEventId, cancellationToken).ConfigureAwait(false);
        }

        if (aliasId is > 0)
        {
            return await commonEvents.TryGetByAliasAsync(aliasId.Value, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private sealed class StepCounter
    {
        public int Value;
    }
}
