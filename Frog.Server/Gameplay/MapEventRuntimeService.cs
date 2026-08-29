using System.Text.Json;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core;
using Frog.Core.Character;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Database;
using Frog.Server.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Gameplay;

/// <summary>Interpréteur serveur autoritaire pour événements carte publiés (P8-2).</summary>
public sealed class MapEventRuntimeService
{
    private readonly IPublishedMapEventCatalog _catalog;
    private readonly ICharacterWorldStateRepository _worldState;
    private readonly CharacterMutationCoordinator _mutations;
    private readonly ICharacterPayloadReader _payloadReader;
    private readonly ICharacterPayloadWriter _payloadWriter;
    private readonly ILogger<MapEventRuntimeService> _logger;

    public MapEventRuntimeService(
        IPublishedMapEventCatalog catalog,
        ICharacterWorldStateRepository worldState,
        CharacterMutationCoordinator mutations,
        ICharacterPayloadReader payloadReader,
        ICharacterPayloadWriter payloadWriter,
        ILogger<MapEventRuntimeService> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
        _mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
        _payloadReader = payloadReader ?? throw new ArgumentNullException(nameof(payloadReader));
        _payloadWriter = payloadWriter ?? throw new ArgumentNullException(nameof(payloadWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MapEventExecutionResult?> TryExecuteInteractAsync(
        Session session,
        MapEventWireEntry placement,
        CancellationToken cancellationToken = default)
    {
        if (session.CharacterGuid is not Guid characterId || characterId == Guid.Empty)
        {
            return MapEventExecutionResult.Fail("Personnage requis.");
        }

        var definition = await _catalog.TryGetPublishedByAliasAsync(placement.CatalogId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var placementTrigger = Phase8MapEventTriggerKinds.FromWireTriggerKind(
            MapEventTriggerNormalization.NormalizeTriggerKind(placement.TriggerKind));

        return await _mutations.RunExclusiveAsync(
            characterId,
            ct => ExecuteForPlacementAsync(session, characterId, definition, placement, placementTrigger, ct),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MapEventExecutionResult> ExecuteForPlacementAsync(
        Session session,
        Guid characterId,
        MapEventDefinition definition,
        MapEventWireEntry placement,
        string placementTrigger,
        CancellationToken cancellationToken)
    {
        var page = await SelectPageAsync(characterId, definition, placementTrigger, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return MapEventExecutionResult.Fail("Aucune page active pour cet événement.");
        }

        string? showText = null;
        var switchesChanged = false;
        var steps = 0;

        foreach (var command in page.Commands)
        {
            if (++steps > MapEventRuntimeLimits.MaxExecutionSteps)
            {
                return MapEventExecutionResult.Fail("Limite d'exécution événement atteinte.");
            }

            switch (command.Discriminator)
            {
                case MapEventCommandDiscriminators.ShowText:
                    if (!MapEventParameterSchemas.TryParseShowText(command.ParameterJson, out var text, out var showErr))
                    {
                        return MapEventExecutionResult.Fail(showErr ?? "show_text invalide.");
                    }

                    showText = text;
                    break;

                case MapEventCommandDiscriminators.SetSwitch:
                    if (!MapEventParameterSchemas.TryParseSetSwitch(
                            command.ParameterJson,
                            out var switchId,
                            out var switchValue,
                            out var switchErr))
                    {
                        return MapEventExecutionResult.Fail(switchErr ?? "set_switch invalide.");
                    }

                    await _worldState.SetSwitchAsync(characterId, switchId, switchValue, cancellationToken)
                        .ConfigureAwait(false);
                    if (await SyncSwitchToPayloadAsync(session, switchId, switchValue, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        switchesChanged = true;
                    }

                    break;

                default:
                    _logger.LogWarning(
                        "Commande événement non implémentée: {Discriminator} (event={EventSlug})",
                        command.Discriminator,
                        placement.Slug);
                    return MapEventExecutionResult.Fail($"Commande non supportée: {command.Discriminator}.");
            }
        }

        var message = showText ?? $"{placement.DisplayName} ({placement.Slug})";
        return MapEventExecutionResult.Ok(message, showText, switchesChanged);
    }

    private async Task<MapEventPageDefinition?> SelectPageAsync(
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

            if (!await ConditionsPassAsync(characterId, page.Conditions, cancellationToken).ConfigureAwait(false))
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
        Guid characterId,
        IReadOnlyList<MapEventConditionDefinition> conditions,
        CancellationToken cancellationToken)
    {
        foreach (var condition in conditions)
        {
            if (!await EvaluateConditionAsync(characterId, condition, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> EvaluateConditionAsync(
        Guid characterId,
        MapEventConditionDefinition condition,
        CancellationToken cancellationToken)
    {
        switch (condition.Kind)
        {
            case MapEventConditionKinds.CharacterSwitch:
                if (!MapEventParameterSchemas.TryParseCharacterSwitchCondition(
                        condition.ParameterJson,
                        out var switchId,
                        out var expected,
                        out _))
                {
                    return false;
                }

                var actual = await _worldState.GetSwitchAsync(characterId, switchId, cancellationToken)
                    .ConfigureAwait(false);
                return actual == expected;

            default:
                _logger.LogWarning("Condition événement non implémentée: {Kind}", condition.Kind);
                return false;
        }
    }

    private Task<bool> SyncSwitchToPayloadAsync(
        Session session,
        string switchId,
        bool value,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(session.CharacterId))
        {
            return Task.FromResult(false);
        }

        _payloadReader.TryGetPayloadJson(session.CharacterId, out var currentJson);
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            currentJson = CharacterPayloadDefaults.NewHeroJson;
        }

        var patchJson = JsonSerializer.Serialize(new Dictionary<string, bool> { [switchId] = value });
        if (!CharacterPayloadWorldFlags.TryMergeWorldFlags(currentJson, patchJson, out var merged, out _))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_payloadWriter.TryUpdatePayloadJson(session.CharacterId, merged));
    }
}
