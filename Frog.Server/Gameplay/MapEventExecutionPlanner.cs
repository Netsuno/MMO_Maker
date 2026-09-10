using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core.Events;
using Frog.Core.Models;
using CorePlanner = Frog.Core.Events.MapEventExecutionPlanner;

namespace Frog.Server.Gameplay;

/// <summary>
/// Filtre transactionnel serveur + délégation de l'expansion common-event vers
/// <see cref="CorePlanner"/> (J4-CORE). La planification Branch+identité vit dans Core.
/// </summary>
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

        var source = new PublishedCommonEventSource(commonEvents);
        var resolved = await CorePlanner.ExpandCommonEventsAsync(commands, source, cancellationToken)
            .ConfigureAwait(false);
        return new ResolvedCommands(resolved.Effects, resolved.Error);
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
}
