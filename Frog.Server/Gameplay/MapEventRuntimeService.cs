using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Gameplay;

/// <summary>Interpréteur serveur autoritaire pour événements carte publiés (P8-2+).</summary>
public sealed class MapEventRuntimeService
{
    private readonly IPublishedMapEventCatalog _catalog;
    private readonly CharacterMutationCoordinator _mutations;
    private readonly MapEventCommandExecutor _commands;
    private readonly ILogger<MapEventRuntimeService> _logger;

    public MapEventRuntimeService(
        IPublishedMapEventCatalog catalog,
        CharacterMutationCoordinator mutations,
        MapEventCommandExecutor commands,
        ILogger<MapEventRuntimeService> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<MapEventExecutionResult?> TryExecuteInteractAsync(
        Session session,
        MapEventWireEntry placement,
        CancellationToken cancellationToken = default) =>
        TryExecuteForTriggerAsync(
            session,
            placement,
            Phase8MapEventTriggerKinds.Action,
            cancellationToken);

    public Task<MapEventExecutionResult?> TryExecuteStepOnAsync(
        Session session,
        MapEventWireEntry placement,
        CancellationToken cancellationToken = default) =>
        TryExecuteForTriggerAsync(
            session,
            placement,
            Phase8MapEventTriggerKinds.PlayerContact,
            cancellationToken);

    public Task<MapEventExecutionResult?> TryExecuteAutorunAsync(
        Session session,
        MapEventWireEntry placement,
        CancellationToken cancellationToken = default) =>
        TryExecuteForTriggerAsync(
            session,
            placement,
            Phase8MapEventTriggerKinds.Autorun,
            cancellationToken);

    public Task<MapEventExecutionResult?> TryExecuteForTriggerAsync(
        Session session,
        MapEventWireEntry placement,
        string triggerKind,
        CancellationToken cancellationToken = default)
    {
        if (session.CharacterGuid is not Guid characterId || characterId == Guid.Empty)
        {
            return Task.FromResult<MapEventExecutionResult?>(MapEventExecutionResult.Fail("Personnage requis."));
        }

        return ExecuteWithCatalogAsync(session, characterId, placement, triggerKind, cancellationToken);
    }

    private async Task<MapEventExecutionResult?> ExecuteWithCatalogAsync(
        Session session,
        Guid characterId,
        MapEventWireEntry placement,
        string triggerKind,
        CancellationToken cancellationToken)
    {
        var definition = await _catalog.TryGetPublishedByAliasAsync(placement.CatalogId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        return await _mutations.RunExclusiveAsync(
            characterId,
            ct => ExecuteDefinitionAsync(session, characterId, definition, placement, triggerKind, ct),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MapEventExecutionResult> ExecuteDefinitionAsync(
        Session session,
        Guid characterId,
        MapEventDefinition definition,
        MapEventWireEntry placement,
        string placementTrigger,
        CancellationToken cancellationToken)
    {
        var page = await SelectPageAsync(session, characterId, definition, placementTrigger, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return MapEventExecutionResult.Fail("Aucune page active pour cet événement.");
        }

        var state = new MapEventExecutionState();
        var err = await _commands.ExecuteCommandsAsync(session, characterId, page.Commands, state, cancellationToken)
            .ConfigureAwait(false);
        if (err is not null)
        {
            return MapEventExecutionResult.Fail(err);
        }

        var message = state.ShowText ?? $"{placement.DisplayName} ({placement.Slug})";
        return MapEventExecutionResult.Ok(
            message,
            state.ShowText,
            state.SwitchesChanged,
            state.VariablesChanged,
            state.InventoryChanged,
            state.GoldChanged,
            state.TeleportApplied,
            state.DialogueSummary,
            state.QuestSummary,
            state.DialogueState);
    }

    private async Task<MapEventPageDefinition?> SelectPageAsync(
        Session session,
        Guid characterId,
        MapEventDefinition definition,
        string placementTrigger,
        CancellationToken cancellationToken)
    {
        MapEventPageDefinition? best = null;
        var bestPriority = int.MinValue;

        foreach (var page in definition.Pages.OrderBy(p => p.PageOrder))
        {
            if (!string.Equals(page.TriggerKind, placementTrigger, StringComparison.Ordinal))
            {
                continue;
            }

            if (!await ConditionsPassAsync(session, characterId, page.Conditions, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (page.Priority > bestPriority)
            {
                bestPriority = page.Priority;
                best = page;
            }
        }

        return best;
    }

    private async Task<bool> ConditionsPassAsync(
        Session session,
        Guid characterId,
        IReadOnlyList<MapEventConditionDefinition> conditions,
        CancellationToken cancellationToken)
    {
        foreach (var condition in conditions)
        {
            if (!await _commands.EvaluateConditionAsync(session, characterId, condition, cancellationToken)
                    .ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }
}
