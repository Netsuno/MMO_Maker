using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Résout branches et appels common-event avant exécution transactionnelle (P8-I4 / J4).</summary>
internal static class MapEventExecutionPlanner
{
    public sealed record ResolvedCommands(IReadOnlyList<MapEventCommandDefinition> Commands, string? Error);

    public static bool CanExecuteTransactionally(IReadOnlyList<MapEventCommandDefinition> commands) =>
        ValidateCommandTree(commands, 0, out _);

    public static async Task<ResolvedCommands> ResolveCommandsAsync(
        IPublishedCommonEventCatalog commonEvents,
        IReadOnlyList<MapEventCommandDefinition> commands,
        CancellationToken cancellationToken)
    {
        if (!ValidateCommandTree(commands, 0, out var validationError))
        {
            return new ResolvedCommands(Array.Empty<MapEventCommandDefinition>(), validationError);
        }

        var resolved = new List<MapEventCommandDefinition>();
        var stack = new HashSet<Guid>();
        foreach (var command in commands)
        {
            var err = await ExpandCommandAsync(commonEvents, command, resolved, stack, 0, cancellationToken)
                .ConfigureAwait(false);
            if (err is not null)
            {
                return new ResolvedCommands(Array.Empty<MapEventCommandDefinition>(), err);
            }
        }

        return new ResolvedCommands(resolved, null);
    }

    private static bool ValidateCommandTree(IReadOnlyList<MapEventCommandDefinition> commands, int depth, out string? error)
    {
        error = null;
        if (depth > MapEventRuntimeLimits.MaxBranchDepth)
        {
            error = "Profondeur de branche/common-event excessive.";
            return false;
        }

        foreach (var cmd in commands)
        {
            if (cmd.Discriminator == MapEventCommandDiscriminators.Branch)
            {
                if (!MapEventParameterSchemas.TryParseBranch(
                        cmd.ParameterJson,
                        out _,
                        out var thenCommands,
                        out var elseCommands,
                        out error))
                {
                    return false;
                }

                if (!ValidateCommandTree(thenCommands, depth + 1, out error)
                    || !ValidateCommandTree(elseCommands, depth + 1, out error))
                {
                    return false;
                }

                continue;
            }

            if (cmd.Discriminator == MapEventCommandDiscriminators.CallCommonEvent
                || cmd.Discriminator == MapEventCommandDiscriminators.Branch)
            {
                continue;
            }

            if (!IsRepositorySupported(cmd.Discriminator))
            {
                error = $"Commande non transactionnelle: {cmd.Discriminator}.";
                return false;
            }
        }

        return true;
    }

    private static bool IsRepositorySupported(string discriminator) =>
        discriminator switch
        {
            MapEventCommandDiscriminators.ShowText
                or MapEventCommandDiscriminators.SetSwitch
                or MapEventCommandDiscriminators.SetVariable
                or MapEventCommandDiscriminators.AddVariable
                or MapEventCommandDiscriminators.SubVariable
                or MapEventCommandDiscriminators.GiveItem
                or MapEventCommandDiscriminators.TakeItem
                or MapEventCommandDiscriminators.GiveGold
                or MapEventCommandDiscriminators.TakeGold
                or MapEventCommandDiscriminators.Wait => true,
            _ => false,
        };

    private static async Task<string?> ExpandCommandAsync(
        IPublishedCommonEventCatalog commonEvents,
        MapEventCommandDefinition command,
        List<MapEventCommandDefinition> output,
        HashSet<Guid> callStack,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MapEventRuntimeLimits.MaxBranchDepth)
        {
            return "Profondeur common-event excessive.";
        }

        if (command.Discriminator != MapEventCommandDiscriminators.CallCommonEvent)
        {
            output.Add(command);
            return null;
        }

        if (!MapEventParameterSchemas.TryParseCallCommonEvent(
                command.ParameterJson,
                out var commonEventId,
                out var aliasId,
                out var err))
        {
            return err;
        }

        CommonEventDefinition? definition = null;
        if (commonEventId != Guid.Empty)
        {
            definition = await commonEvents.TryGetPublishedByIdAsync(commonEventId, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (aliasId is > 0)
        {
            definition = await commonEvents.TryGetPublishedByAliasAsync(aliasId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        if (definition is null || definition.Pages.Count == 0)
        {
            return "Common event introuvable.";
        }

        if (!callStack.Add(definition.Id))
        {
            return "Cycle common-event détecté.";
        }

        var page = definition.Pages.OrderByDescending(p => p.Priority).First();
        foreach (var nested in page.Commands)
        {
            var nestedErr = await ExpandCommandAsync(commonEvents, nested, output, callStack, depth + 1, cancellationToken)
                .ConfigureAwait(false);
            if (nestedErr is not null)
            {
                callStack.Remove(definition.Id);
                return nestedErr;
            }
        }

        callStack.Remove(definition.Id);
        return null;
    }
}
