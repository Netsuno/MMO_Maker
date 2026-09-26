using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core.Events;
using Frog.Core.Models;
using CorePlanner = Frog.Core.Events.MapEventExecutionPlanner;

namespace Frog.Server.Gameplay;

/// <summary>
/// Filtre transactionnel serveur + délégation de la planification vers
/// <see cref="CorePlanner"/> (J4-CORE). Branch et CommonEvent sont résolus dans Core
/// avant le dépôt ; le repo ne doit pas les re-résoudre.
/// </summary>
internal static class MapEventExecutionPlanner
{
    public sealed record ResolvedCommands(IReadOnlyList<MapEventCommandDefinition> Commands, string? Error);

    /// <summary>
    /// True si l'arbre de commandes (branches incluses) peut être commis dans une
    /// seule transaction PG après planification Core. <c>start_dialogue</c>,
    /// <c>teleport</c> et <c>open_shop</c> appartiennent à l'unité unifiée
    /// (intents de session dans la même TX) ; le serveur les applique après commit.
    /// </summary>
    public static bool CanExecuteTransactionally(IReadOnlyList<MapEventCommandDefinition> commands) =>
        ValidateCommandTree(commands, 0, out _);

    /// <summary>
    /// True si les effets plats (dialogue / téléport inclus) tiennent dans une TX PG
    /// unifiée. Délègue au classifieur Core R2-4.
    /// </summary>
    public static bool AreEffectsTransactional(IReadOnlyList<MapEventCommandDefinition> effects) =>
        CorePlanner.IsUnifiedTransactionalUnit(effects);

    public static bool ContainsUnresolvedControlFlow(IReadOnlyList<MapEventCommandDefinition> effects) =>
        CorePlanner.ContainsUnresolvedControlFlow(effects);

    public static Task<MapEventExecutionPlan> PlanAsync(
        IReadOnlyList<MapEventCommandDefinition> commands,
        IPublishedCommonEventCatalog commonEvents,
        Func<MapEventConditionDefinition, Task<bool>> evaluateCondition,
        MapEventExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(commonEvents);
        ArgumentNullException.ThrowIfNull(evaluateCondition);

        return CorePlanner.PlanAsync(
            commands,
            new PublishedCommonEventSource(commonEvents),
            evaluateCondition,
            identity,
            cancellationToken);
    }

    /// <summary>Expansion common-event seule (branches conservées) — tests / compat.</summary>
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

            if (cmd.Discriminator == MapEventCommandDiscriminators.ShowChoices)
            {
                if (!MapEventParameterSchemas.TryParseShowChoices(
                        cmd.ParameterJson,
                        out _,
                        out _,
                        out var choiceBranches,
                        out var cancelCommands,
                        out error))
                {
                    return false;
                }

                foreach (var branch in choiceBranches)
                {
                    if (!ValidateCommandTree(branch, depth + 1, out error))
                    {
                        return false;
                    }
                }

                if (!ValidateCommandTree(cancelCommands, depth + 1, out error))
                {
                    return false;
                }

                continue;
            }

            if (cmd.Discriminator == MapEventCommandDiscriminators.CallCommonEvent)
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
                or MapEventCommandDiscriminators.Wait
                or MapEventCommandDiscriminators.StartQuest
                or MapEventCommandDiscriminators.AdvanceQuest
                or MapEventCommandDiscriminators.TurnInQuest
                or MapEventCommandDiscriminators.LearnProfession
                or MapEventCommandDiscriminators.StartDialogue
                or MapEventCommandDiscriminators.Teleport
                or MapEventCommandDiscriminators.OpenShop
                or MapEventCommandDiscriminators.PlayBgm
                or MapEventCommandDiscriminators.PlaySe
                or MapEventCommandDiscriminators.SetWeather
                or MapEventCommandDiscriminators.ShowPicture
                or MapEventCommandDiscriminators.MovePicture
                or MapEventCommandDiscriminators.TintPicture
                or MapEventCommandDiscriminators.ErasePicture
                or MapEventCommandDiscriminators.FadeOutScreen
                or MapEventCommandDiscriminators.FadeInScreen
                or MapEventCommandDiscriminators.TintScreen
                or MapEventCommandDiscriminators.ShakeScreen
                or MapEventCommandDiscriminators.FlashScreen
                or MapEventCommandDiscriminators.ShowAnimation => true,
            _ => false,
        };
}
