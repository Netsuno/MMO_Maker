using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Config;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Microsoft.Extensions.Options;

namespace Frog.Server.Network;

/// <summary>Handlers Phase 8 (dialogue, quêtes, craft, environnement).</summary>
public sealed class Phase8GameplayHandlers(
    DialogSessionService dialogSessions,
    QuestGameplayService quests,
    CraftGameplayService craft,
    ProfessionGameplayService professions,
    WeatherGameplayService weather,
    MapEventRuntimeService mapEventRuntime,
    MapEventExecutionTracker executionTracker,
    MapEventMovementService eventMovement,
    IMapEventStore mapEventStore,
    PacketSender packetSender,
    IOptions<Phase8SmokeBootstrapOptions> smokeOptions)
{
    private readonly Phase8SmokeBootstrapOptions _smoke = smokeOptions.Value;
    public void CancelForCharacter(Guid characterId)
    {
        dialogSessions.CancelForCharacter(characterId);
        executionTracker.ClearForCharacter(characterId);
    }

    public void ClearMapEventExecutionsForCharacter(Guid characterId, int? mapId = null)
    {
        executionTracker.ClearForCharacter(characterId);
        if (mapId is int mid)
        {
            executionTracker.ClearAutorunForMap(characterId, mid);
        }
    }

    public async Task<string> BuildMapEventsWireJsonAsync(int mapId, CancellationToken cancellationToken)
    {
        var (ok, placements) = await mapEventStore.GetPlacementsAsync(mapId, cancellationToken).ConfigureAwait(false);
        if (!ok || placements.Count == 0)
        {
            return "[]";
        }

        eventMovement.SyncMapPlacements(mapId, placements);
        var runtime = eventMovement.ApplyRuntimePositions(mapId, placements);
        return System.Text.Json.JsonSerializer.Serialize(runtime);
    }

    public async Task TickMapEventMovementAsync(Session session, CancellationToken cancellationToken)
    {
        var mapId = session.CurrentMapId;
        var (ok, placements) = await mapEventStore.GetPlacementsAsync(mapId, cancellationToken).ConfigureAwait(false);
        if (!ok)
        {
            return;
        }

        eventMovement.SyncMapPlacements(mapId, placements);
        eventMovement.TickMap(mapId);
    }

    public async Task NotifyTalkProgressAsync(
        Guid characterId,
        Guid dialogueId,
        CancellationToken cancellationToken) =>
        await quests.NotifyObjectiveProgressAsync(
                characterId,
                QuestObjectiveKind.Talk,
                new QuestObjectiveSignal(DialogueId: dialogueId),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task NotifyKillProgressAsync(
        Guid characterId,
        Guid npcDefinitionId,
        CancellationToken cancellationToken) =>
        await quests.NotifyObjectiveProgressAsync(
                characterId,
                QuestObjectiveKind.Kill,
                new QuestObjectiveSignal(NpcId: npcDefinitionId),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task NotifyCollectProgressAsync(
        Guid characterId,
        Guid itemId,
        CancellationToken cancellationToken) =>
        await quests.NotifyObjectiveProgressAsync(
                characterId,
                QuestObjectiveKind.Collect,
                new QuestObjectiveSignal(ItemId: itemId),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task NotifyVisitProgressAsync(
        Guid characterId,
        int mapId,
        int tileX,
        int tileY,
        CancellationToken cancellationToken) =>
        await quests.NotifyObjectiveProgressAsync(
                characterId,
                QuestObjectiveKind.Visit,
                new QuestObjectiveSignal(MapId: mapId, TileX: tileX, TileY: tileY),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task SendQuestJournalAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var entries = await quests.BuildJournalAsync(characterId, cancellationToken).ConfigureAwait(false);
        await packetSender.SendQuestJournalSnapshotAsync(client, entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task TryPushSmokeBootstrapAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        if (!_smoke.Enabled || session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var started = await dialogSessions.TryStartSessionAsync(characterId, _smoke.DialogueId, cancellationToken)
            .ConfigureAwait(false);
        if (started is not null)
        {
            await packetSender.SendDialogueStatePushAsync(
                    client,
                    _smoke.DialogueId,
                    started.PublishedRevision,
                    started.SessionToken,
                    started.Speaker,
                    started.Text,
                    started.Choices,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task SendEnvironmentStateAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        var snapshot = await weather.GetWeatherForSessionAsync(
                session.CurrentMapId,
                session.PositionX,
                session.PositionY,
                cancellationToken)
            .ConfigureAwait(false);
        await packetSender.SendEnvironmentStatePushAsync(
                client,
                session.CurrentMapId,
                snapshot.RegionId,
                snapshot.WeatherProfileId,
                (byte)Math.Clamp((int)(snapshot.LightingFactor * 255), 0, 255),
                cancellationToken)
            .ConfigureAwait(false);

        if (session.CharacterGuid is Guid characterId)
        {
            await NotifyVisitProgressAsync(
                    characterId,
                    session.CurrentMapId,
                    session.PositionX,
                    session.PositionY,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task HandleDialogueChoiceRequestAsync(
        ClientSession client,
        Session session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            await packetSender.SendDialogueChoiceResultAsync(client, false, "Personnage requis.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!Phase8Wire.TryParseDialogueChoiceRequest(payload.Span, out var token, out var choiceId))
        {
            await packetSender.SendDialogueChoiceResultAsync(client, false, "Payload invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var tokenBytes = token;
        var result = await dialogSessions.TryChooseAsync(characterId, tokenBytes, choiceId, cancellationToken)
            .ConfigureAwait(false);
        if (result is null)
        {
            await packetSender.SendDialogueChoiceResultAsync(client, false, "Choix refusé.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await packetSender.SendDialogueChoiceResultAsync(client, result.Success, result.Message, cancellationToken)
            .ConfigureAwait(false);
        if (result.Success)
        {
            await SendQuestJournalAsync(client, session, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task HandleQuestTurnInRequestAsync(
        ClientSession client,
        Session session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            await packetSender.SendQuestTurnInResultAsync(client, false, "Personnage requis.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!Phase8Wire.TryParseQuestTurnInRequest(payload.Span, out var questId, out var requestId))
        {
            await packetSender.SendQuestTurnInResultAsync(client, false, "Payload invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var result = await quests.TryTurnInQuestAsync(characterId, questId, requestId, cancellationToken)
            .ConfigureAwait(false);
        if (result is null)
        {
            await packetSender.SendQuestTurnInResultAsync(client, false, "Turn-in refusé.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var ok = result.Status is QuestTurnInStatus.TurnedIn or QuestTurnInStatus.IdempotentReplay;
        await packetSender.SendQuestTurnInResultAsync(client, ok, result.Message ?? result.Status.ToString(), cancellationToken)
            .ConfigureAwait(false);
        if (ok)
        {
            if (result.GoldGranted is int goldGranted and > 0)
            {
                session.Gold = checked(session.Gold + goldGranted);
            }

            await packetSender.SendCombatStateAsync(
                    client,
                    session.Level,
                    session.Experience,
                    session.Hp,
                    session.MaxHp,
                    session.Mp,
                    session.MaxMp,
                    session.Gold,
                    session.IsDead,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendQuestJournalAsync(client, session, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task HandleCraftRequestAsync(
        ClientSession client,
        Session session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            await packetSender.SendCraftResultAsync(client, false, "Personnage requis.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!Phase8Wire.TryParseCraftRequest(payload.Span, out var recipeId, out var requestId))
        {
            await packetSender.SendCraftResultAsync(client, false, "Payload invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var result = await craft.TryCraftAsync(characterId, recipeId, requestId, cancellationToken)
            .ConfigureAwait(false);
        var ok = result.Status is EventCraftStatus.Crafted or EventCraftStatus.IdempotentReplay;
        await packetSender.SendCraftResultAsync(client, ok, result.Message ?? result.Status.ToString(), cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == EventCraftStatus.Crafted)
        {
            if (result.RemainingGold is int gold)
            {
                session.Gold = gold;
            }

            await quests.NotifyObjectiveProgressAsync(
                    characterId,
                    QuestObjectiveKind.Craft,
                    new QuestObjectiveSignal(RecipeId: recipeId),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendQuestJournalAsync(client, session, cancellationToken).ConfigureAwait(false);
            if (result.GoldSpent is int spent and > 0)
            {
                await packetSender.SendCombatStateAsync(
                        client,
                        session.Level,
                        session.Experience,
                        session.Hp,
                        session.MaxHp,
                        session.Mp,
                        session.MaxMp,
                        session.Gold,
                        session.IsDead,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else if (result.Status == EventCraftStatus.IdempotentReplay && result.RemainingGold is int replayGold)
        {
            session.Gold = replayGold;
        }
    }

    public async Task HandleAcquireProfessionRequestAsync(
        ClientSession client,
        Session session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            await packetSender.SendAcquireProfessionResultAsync(client, false, "Personnage requis.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!Phase8Wire.TryParseAcquireProfessionRequest(payload.Span, out var professionId))
        {
            await packetSender.SendAcquireProfessionResultAsync(client, false, "Payload invalide.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var (success, message) = await professions.TryAcquireProfessionAsync(
                characterId,
                professionId,
                cancellationToken)
            .ConfigureAwait(false);
        await packetSender.SendAcquireProfessionResultAsync(client, success, message, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task TryFireAutorunMapEventsAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var (ok, placements) = await mapEventStore.GetPlacementsAsync(session.CurrentMapId, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
        {
            return;
        }

        foreach (var ev in placements.Where(p =>
                     MapEventTriggerNormalization.NormalizeTriggerKind(p.TriggerKind)
                     == Phase8MapEventTriggerKinds.Autorun))
        {
            if (!executionTracker.TryFireAutorunOnce(characterId, ev.PlacementId, Guid.Empty, session.CurrentMapId))
            {
                continue;
            }

            var runtimeResult = await mapEventRuntime.TryExecuteAutorunAsync(session, ev, cancellationToken)
                .ConfigureAwait(false);
            if (runtimeResult is null)
            {
                continue;
            }

            var clientMessage = runtimeResult.ShowText ?? runtimeResult.Message;
            await packetSender.SendInteractResultAsync(client, runtimeResult.Success, clientMessage, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task TryFireParallelMapEventsAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var (ok, placements) = await mapEventStore.GetPlacementsAsync(session.CurrentMapId, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
        {
            return;
        }

        foreach (var ev in placements.Where(p =>
                     MapEventTriggerNormalization.NormalizeTriggerKind(p.TriggerKind)
                     == Phase8MapEventTriggerKinds.Parallel))
        {
            if (!executionTracker.TryBeginParallel(characterId, ev.PlacementId, Guid.Empty, session.CurrentMapId))
            {
                continue;
            }

            try
            {
                var runtimeResult = await mapEventRuntime.TryExecuteParallelAsync(session, ev, cancellationToken)
                    .ConfigureAwait(false);
                if (runtimeResult is null)
                {
                    continue;
                }

                var clientMessage = runtimeResult.ShowText ?? runtimeResult.Message;
                await packetSender.SendInteractResultAsync(client, runtimeResult.Success, clientMessage, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                executionTracker.EndParallel(characterId, ev.PlacementId, Guid.Empty);
            }
        }
    }

    public Task TryResumeWaitingMapEventsAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        _ = client;
        return mapEventRuntime.TryResumeWaitingAsync(session, cancellationToken);
    }
}
