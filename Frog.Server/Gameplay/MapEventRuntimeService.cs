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
    private readonly IPublishedCommonEventCatalog _commonEvents;
    private readonly CharacterMutationCoordinator _mutations;
    private readonly MapEventCommandExecutor _commands;
    private readonly MapEventExecutionTracker _executionTracker;
    private readonly IMapEventMutationRepository? _mutationRepository;
    private readonly ILogger<MapEventRuntimeService> _logger;

    public MapEventRuntimeService(
        IPublishedMapEventCatalog catalog,
        IPublishedCommonEventCatalog commonEvents,
        CharacterMutationCoordinator mutations,
        MapEventCommandExecutor commands,
        MapEventExecutionTracker executionTracker,
        ILogger<MapEventRuntimeService> logger,
        IMapEventMutationRepository? mutationRepository = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _commonEvents = commonEvents ?? throw new ArgumentNullException(nameof(commonEvents));
        _mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _executionTracker = executionTracker ?? throw new ArgumentNullException(nameof(executionTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mutationRepository = mutationRepository;
    }

    public Task<MapEventExecutionResult?> TryExecuteInteractAsync(
        Session session,
        MapEventWireEntry placement,
        CancellationToken cancellationToken = default) =>
        TryExecuteInteractAsync(session, placement, activationId: null, cancellationToken);

    public Task<MapEventExecutionResult?> TryExecuteInteractAsync(
        Session session,
        MapEventWireEntry placement,
        Guid? activationId,
        CancellationToken cancellationToken = default) =>
        TryExecuteForTriggerAsync(
            session,
            placement,
            Phase8MapEventTriggerKinds.Action,
            cancellationToken,
            activationId);

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

    public Task<MapEventExecutionResult?> TryExecuteParallelAsync(
        Session session,
        MapEventWireEntry placement,
        CancellationToken cancellationToken = default) =>
        TryExecuteForTriggerAsync(
            session,
            placement,
            Phase8MapEventTriggerKinds.Parallel,
            cancellationToken);

    public Task<MapEventExecutionResult?> TryExecuteForTriggerAsync(
        Session session,
        MapEventWireEntry placement,
        string triggerKind,
        CancellationToken cancellationToken = default,
        Guid? clientActivationId = null)
    {
        if (session.CharacterGuid is not Guid characterId || characterId == Guid.Empty)
        {
            return Task.FromResult<MapEventExecutionResult?>(MapEventExecutionResult.Fail("Personnage requis."));
        }

        return ExecuteWithCatalogAsync(
            session,
            characterId,
            placement,
            triggerKind,
            cancellationToken,
            clientActivationId);
    }

    /// <summary>
    /// Reprise <c>wait</c> : si un <see cref="PendingWaitResume.ResumePlan"/> est
    /// présent, 2e ligne ledger via <c>TryExecutePlanAsync</c> (même activation,
    /// RequestId dérivé). Sinon chemin in-memory (pas de repo PG).
    /// </summary>
    public async Task<IReadOnlyList<MapEventExecutionResult>> TryResumeWaitingAsync(
        Session session,
        CancellationToken cancellationToken = default)
    {
        if (session.CharacterGuid is not Guid characterId || characterId == Guid.Empty)
        {
            return Array.Empty<MapEventExecutionResult>();
        }

        var ready = _executionTracker.TakeReadyWaits(characterId, DateTimeOffset.UtcNow);
        if (ready.Count == 0)
        {
            return Array.Empty<MapEventExecutionResult>();
        }

        var results = new List<MapEventExecutionResult>(ready.Count);
        foreach (var wait in ready)
        {
            var resume = await _mutations.RunExclusiveAsync(
                characterId,
                ct => ResumeWaitAsync(session, characterId, wait, ct),
                cancellationToken).ConfigureAwait(false);
            if (resume is not null)
            {
                results.Add(resume);
            }
        }

        return results;
    }

    private async Task<MapEventExecutionResult?> ExecuteWithCatalogAsync(
        Session session,
        Guid characterId,
        MapEventWireEntry placement,
        string triggerKind,
        CancellationToken cancellationToken,
        Guid? clientActivationId)
    {
        var definition = await _catalog.TryGetPublishedByAliasAsync(placement.CatalogId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        return await _mutations.RunExclusiveAsync(
            characterId,
            ct => ExecuteDefinitionAsync(
                session,
                characterId,
                definition,
                placement,
                triggerKind,
                ct,
                clientActivationId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MapEventExecutionResult> ExecuteDefinitionAsync(
        Session session,
        Guid characterId,
        MapEventDefinition definition,
        MapEventWireEntry placement,
        string placementTrigger,
        CancellationToken cancellationToken,
        Guid? clientActivationId)
    {
        var page = await SelectPageAsync(session, characterId, definition, placementTrigger, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return MapEventExecutionResult.Fail("Aucune page active pour cet événement.");
        }

        // Always plan first (J5-FIX-07/08): missing CE refs and CE→CE cycles fail
        // with no effects, including when the mutation repository is absent.
        var planned = await TryExecutePlannedAsync(
                session,
                characterId,
                page.Commands,
                placement,
                cancellationToken,
                clientActivationId)
            .ConfigureAwait(false);
        if (planned is not null)
        {
            return planned;
        }

        return await ExecuteInMemoryAsync(
                session,
                characterId,
                page.Commands,
                placement,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Chemin public : planification Core (branches + common-events) puis
    /// <see cref="IMapEventMutationRepository.TryExecutePlanAsync"/> (une TX PG + ledger).
    /// Identité = <see cref="MapEventExecutionIdentity.BeginActivation"/> avec l'id
    /// client s'il est fourni (InteractRequest), sinon un nouvel id serveur
    /// (step_on / autorun / parallel). <c>teleport</c> / <c>start_dialogue</c>
    /// restent dans l'unité unifiée ; intents appliqués après commit.
    /// Retourne null seulement si le repo PG est absent ou si un effet inconnu
    /// force le repli in-memory.
    /// </summary>
    private async Task<MapEventExecutionResult?> TryExecutePlannedAsync(
        Session session,
        Guid characterId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        MapEventWireEntry placement,
        CancellationToken cancellationToken,
        Guid? clientActivationId)
    {
        var identity = MapEventExecutionIdentity.BeginActivation(
            characterId,
            placement.PlacementId,
            placement.CatalogId,
            clientActivationId);

        var plan = await MapEventExecutionPlanner.PlanAsync(
                commands,
                _commonEvents,
                condition => _commands.EvaluateConditionAsync(session, characterId, condition, cancellationToken),
                identity,
                cancellationToken)
            .ConfigureAwait(false);
        if (!plan.IsSuccess)
        {
            return MapEventExecutionResult.Fail(plan.Error ?? "Planification événement échouée.");
        }

        if (MapEventExecutionPlanner.ContainsUnresolvedControlFlow(plan.Effects))
        {
            return MapEventExecutionResult.Fail(
                "Plan incomplet: branche ou common-event non résolu.");
        }

        if (!MapEventExecutionPlanner.AreEffectsTransactional(plan.Effects)
            || _mutationRepository is null)
        {
            return null;
        }

        var unit = plan.AsTransactionalUnit();
        if (!unit.IsSuccess)
        {
            return MapEventExecutionResult.Fail(unit.Error ?? "Unité transactionnelle invalide.");
        }

        var mutation = await _mutationRepository
            .TryExecutePlanAsync(plan, cancellationToken)
            .ConfigureAwait(false);
        var label = $"{placement.DisplayName} ({placement.Slug})";
        return await CompleteCommittedMutationAsync(
                session,
                characterId,
                unit,
                mutation,
                label,
                registerWait: mutation.Status == MapEventMutationStatus.Executed,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<MapEventExecutionResult?> ResumeWaitAsync(
        Session session,
        Guid characterId,
        PendingWaitResume wait,
        CancellationToken cancellationToken)
    {
        if (wait.ResumePlan is not null && _mutationRepository is not null)
        {
            var mutation = await _mutationRepository
                .TryExecutePlanAsync(wait.ResumePlan, cancellationToken)
                .ConfigureAwait(false);
            var unit = wait.ResumePlan.AsTransactionalUnit();
            var committed = await CompleteCommittedMutationAsync(
                    session,
                    characterId,
                    unit,
                    mutation,
                    wait.PlacementLabel ?? string.Empty,
                    registerWait: mutation.Status == MapEventMutationStatus.Executed,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!committed.Success)
            {
                _logger.LogWarning(
                    "Reprise wait ledger échouée pour {CharacterId}: {Error}",
                    characterId,
                    committed.Message);
                return null;
            }

            return committed;
        }

        var state = new MapEventExecutionState();
        var err = await _commands.ExecuteCommandsAsync(
                session,
                characterId,
                wait.RemainingCommands,
                state,
                cancellationToken)
            .ConfigureAwait(false);
        if (err is not null)
        {
            _logger.LogWarning(
                "Reprise wait événement échouée pour {CharacterId}: {Error}",
                characterId,
                err);
            return null;
        }

        RegisterWaitIfNeeded(characterId, state, wait.PlacementLabel);
        return MapEventExecutionResult.Ok(
            message: state.ShowText ?? wait.PlacementLabel ?? string.Empty,
            showText: state.ShowText,
            switchesChanged: state.SwitchesChanged,
            variablesChanged: state.VariablesChanged,
            inventoryChanged: state.InventoryChanged,
            goldChanged: state.GoldChanged,
            teleportApplied: state.TeleportApplied,
            dialogueSummary: state.DialogueSummary,
            questSummary: state.QuestSummary,
            dialogueState: state.DialogueState,
            questsChanged: state.QuestsChanged,
            professionsChanged: state.ProfessionsChanged,
            recipesChanged: state.RecipesChanged,
            switchChanges: state.SwitchChanges,
            openShopId: state.OpenShopId,
            weatherChanged: state.WeatherChanged,
            pictureOps: state.PictureOps,
            screenOps: state.ScreenOps,
            visualOps: state.VisualOps);
    }

    private async Task<MapEventExecutionResult> CompleteCommittedMutationAsync(
        Session session,
        Guid characterId,
        MapEventTransactionalUnit unit,
        MapEventMutationResult mutation,
        string label,
        bool registerWait,
        CancellationToken cancellationToken)
    {
        if (mutation.Status == MapEventMutationStatus.Failed)
        {
            return MapEventExecutionResult.Fail(mutation.ErrorMessage ?? "Exécution événement échouée.");
        }

        var snap = mutation.Snapshot;
        if (snap?.ResultGold is int gold)
        {
            session.Gold = gold;
        }

        var applied = new MapEventExecutionState();
        if (snap is not null)
        {
            await _commands.ApplyCommittedSessionIntentsAsync(
                    session,
                    characterId,
                    snap,
                    applied,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (registerWait)
        {
            RegisterLedgerWaitIfNeeded(characterId, unit, snap, label);
        }

        return MapEventExecutionResult.FromMutationSnapshot(
            label,
            snap,
            applied.TeleportApplied,
            applied.DialogueSummary,
            applied.DialogueState,
            applied.OpenShopId,
            applied.ShowText,
            applied.WeatherChanged);
    }

    private void RegisterLedgerWaitIfNeeded(
        Guid characterId,
        MapEventTransactionalUnit unit,
        MapEventExecutionSnapshot? snap,
        string? label)
    {
        if (unit.ResumePlan is null)
        {
            return;
        }

        DateTimeOffset until;
        if (snap?.WaitUntilUtc is DateTimeOffset snapUntil)
        {
            until = snapUntil;
        }
        else if (unit.Wait is { } wait)
        {
            until = DateTimeOffset.UtcNow.AddMilliseconds(wait.WaitMilliseconds);
        }
        else
        {
            return;
        }

        _executionTracker.RegisterWait(
            characterId,
            new PendingWaitResume(
                until,
                unit.ResumePlan.Effects,
                label,
                unit.ResumePlan));
    }

    private async Task<MapEventExecutionResult> ExecuteInMemoryAsync(
        Session session,
        Guid characterId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        MapEventWireEntry placement,
        CancellationToken cancellationToken)
    {
        var state = new MapEventExecutionState();
        var err = await _commands.ExecuteCommandsAsync(session, characterId, commands, state, cancellationToken)
            .ConfigureAwait(false);
        if (err is not null)
        {
            return MapEventExecutionResult.Fail(err);
        }

        var placementLabel = $"{placement.DisplayName} ({placement.Slug})";
        RegisterWaitIfNeeded(characterId, state, placementLabel);

        return MapEventExecutionResult.Ok(
            message: state.ShowText ?? placementLabel,
            state.ShowText,
            state.SwitchesChanged,
            state.VariablesChanged,
            state.InventoryChanged,
            state.GoldChanged,
            state.TeleportApplied,
            state.DialogueSummary,
            state.QuestSummary,
            state.DialogueState,
            state.QuestsChanged,
            state.ProfessionsChanged,
            state.RecipesChanged,
            state.SwitchChanges,
            state.OpenShopId,
            state.WeatherChanged,
            state.PictureOps,
            state.ScreenOps,
            state.VisualOps);
    }

    private void RegisterWaitIfNeeded(Guid characterId, MapEventExecutionState state, string? label)
    {
        if (!state.Waiting || state.WaitUntilUtc is not DateTimeOffset until)
        {
            return;
        }

        var remaining = state.PendingCommands ?? Array.Empty<MapEventCommandDefinition>();
        if (remaining.Count == 0)
        {
            return;
        }

        _executionTracker.RegisterWait(
            characterId,
            new PendingWaitResume(until, remaining, label));
    }

    private async Task<MapEventPageDefinition?> SelectPageAsync(
        Session session,
        Guid characterId,
        MapEventDefinition definition,
        string placementTrigger,
        CancellationToken cancellationToken) =>
        await MapEventPageSelector.SelectBestPageAsync(
            definition.Pages,
            placementTrigger,
            condition => EvaluateConditionAsync(session, characterId, condition, cancellationToken))
            .ConfigureAwait(false);

    private async Task<bool> EvaluateConditionAsync(
        Session session,
        Guid characterId,
        MapEventConditionDefinition condition,
        CancellationToken cancellationToken) =>
        await _commands.EvaluateConditionAsync(session, characterId, condition, cancellationToken)
            .ConfigureAwait(false);

    private async Task<bool> ConditionsPassAsync(
        Session session,
        Guid characterId,
        IReadOnlyList<MapEventConditionDefinition> conditions,
        CancellationToken cancellationToken)
    {
        foreach (var condition in conditions)
        {
            if (!await EvaluateConditionAsync(session, characterId, condition, cancellationToken)
                    .ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }
}
