using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Models;

namespace Frog.Server.Network;

/// <summary>Handlers Phase 8 (dialogue, quêtes, craft, environnement).</summary>
public sealed class Phase8GameplayHandlers(
    DialogSessionService dialogSessions,
    QuestGameplayService quests,
    CraftGameplayService craft,
    WeatherGameplayService weather,
    MapEventRuntimeService mapEventRuntime,
    MapEventExecutionTracker executionTracker,
    IMapEventStore mapEventStore,
    PacketSender packetSender)
{
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
        if (ok)
        {
            await quests.NotifyObjectiveProgressAsync(
                    characterId,
                    QuestObjectiveKind.Craft,
                    new QuestObjectiveSignal(RecipeId: recipeId),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendQuestJournalAsync(client, session, cancellationToken).ConfigureAwait(false);
        }
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

        if (!mapEventStore.TryGetPlacements(session.CurrentMapId, out var placements))
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
}
