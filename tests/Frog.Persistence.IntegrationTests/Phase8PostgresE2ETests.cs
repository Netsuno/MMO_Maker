using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase8PostgresE2ETests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase8PostgresE2ETests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FullPhase8Flow_PostgreSqlHeadless_AllMatrixSteps()
    {
        var seed = await SeedPublishedContentAsync();

        // Steps 1–3: published map events + Phase8 deps; draft quest stays unpublished.
        await AssertSeedContentAsync(seed);

        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();

        string token = string.Empty;
        string characterId = string.Empty;
        Guid characterGuid = Guid.Empty;
        byte[] dialogueToken = Array.Empty<byte>();
        var craftRequestId = Guid.NewGuid();
        var turnInRequestId = Guid.NewGuid();
        var startingGold = GameplayLimits.StartingGold;

        try
        {
            var user = $"p8-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";

            await using var client = new Phase7TcpTestClient();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

            // Step 5: auth + character select via network
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
            token = Phase7WireDecoders.DecodeLoginToken(await client.ReadUntilAsync(PacketId.LoginResult));
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("Phase8Hero", seed.Phase7.ClassId));
            characterId = Phase7WireDecoders.DecodeCharacterId(await client.ReadUntilAsync(PacketId.CharacterCreateResult));
            characterGuid = Guid.Parse(characterId);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            var autorunMsg = await Phase8TcpTestHelpers.DrainAccountSelectSnapshotsAsync(client);
            Assert.NotNull(autorunMsg);
            Assert.Contains("Welcome to Phase8", autorunMsg);

            // Step 6: enter map — published runtime map id
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
            var mapFrame = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);
            Assert.Equal(seed.RuntimeMapId, Phase7WireDecoders.DecodeMapId(mapFrame));

            // Step 6 continued: quest journal has no draft quest (journal already received during bootstrap)
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            _ = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
            var journalFrame = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(journalFrame, out var initialJournal));
            Assert.Null(Phase8WireDecoders.FindQuestEntry(initialJournal, seed.DraftQuestId));
            Assert.Null(Phase8WireDecoders.FindQuestEntry(initialJournal, seed.QuestId));

            // Step 19: region boundary — weather/lighting at spawn (re-select refreshes environment)
            var envFrame = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
            Assert.True(Phase8WireDecoders.TryDecodeEnvironmentState(
                envFrame, out var envMapId, out var envRegionId, out var envWeatherId, out var lighting));
            Assert.Equal(seed.RuntimeMapId, envMapId);
            Assert.Equal(seed.RegionId, envRegionId);
            Assert.Equal(seed.WeatherProfileId, envWeatherId);
            Assert.Equal(seed.ExpectedLightingLevel, lighting);

            // Step 19 continued: cross region boundary via public movement + re-select
            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.Region2TileX, seed.Region2TileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            _ = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
            var envRegion2 = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
            Assert.True(Phase8WireDecoders.TryDecodeEnvironmentState(
                envRegion2, out var env2MapId, out var env2RegionId, out var env2WeatherId, out var lighting2));
            Assert.Equal(seed.RuntimeMapId, env2MapId);
            Assert.Equal(seed.Region2Id, env2RegionId);
            Assert.Equal(seed.WeatherProfile2Id, env2WeatherId);
            Assert.Equal(seed.ExpectedLightingLevel2, lighting2);

            // Common-event execution via call_common_event on published map event
            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.CommonEventTileX, seed.CommonEventTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            var commonEventResult = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(commonEventResult, out var commonOk, out var commonMsg));
            Assert.True(commonOk);
            Assert.Contains("Common event fired", commonMsg);

            // Step 18: autorun already consumed during first bootstrap; re-select must not repeat
            await client.DrainPendingAsync(TimeSpan.FromMilliseconds(300));

            // Step 7 + 11 (page 0): action trigger on gate while switch false
            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.GateEventTileX, seed.GateEventTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            var locked = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(locked, out _, out var lockedMsg));
            Assert.Contains("Gate locked", lockedMsg);

            // Step 11: condition change via key event, then new page
            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.KeyEventTileX, seed.KeyEventTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            var keyResult = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(keyResult, out var keyOk, out _));
            Assert.True(keyOk);

            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.GateEventTileX, seed.GateEventTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());

            // Step 8: typed dialogue state push (sent before InteractResult)
            var dialogueFrame = await Phase8TcpTestHelpers.ReadDialogueThenInteractAsync(client);
            Assert.True(Phase8WireDecoders.TryDecodeDialogueStatePush(
                dialogueFrame,
                out var dialogueId,
                out var dialogueRevision,
                out dialogueToken,
                out var speaker,
                out var dialogueText,
                out var choices));
            Assert.Equal(seed.DialogueId, dialogueId);
            Assert.Equal(seed.DialoguePublishedRevision, dialogueRevision);
            Assert.Equal("Guide", speaker);
            Assert.Contains("Will you help", dialogueText);
            Assert.Contains(choices, c => c.ChoiceId == "accept");

            // Step 9: reject invalid / replayed dialogue choice
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildDialogueChoice(dialogueToken, "invalid-choice"));
            var badChoice = await client.ReadUntilAsync(PacketId.DialogueChoiceResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(badChoice, out var badOk, out _));
            Assert.False(badOk);

            // Step 10: valid choice → quest start
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildDialogueChoice(dialogueToken, "accept"));
            var goodChoice = await client.ReadUntilAsync(PacketId.DialogueChoiceResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(goodChoice, out var goodOk, out _));
            Assert.True(goodOk);
            var questJournalAfterStart = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(questJournalAfterStart, out var startedJournal));
            var activeQuest = Phase8WireDecoders.FindQuestEntry(startedJournal, seed.QuestId);
            Assert.NotNull(activeQuest);
            Assert.Equal((byte)CharacterQuestStatus.Active, activeQuest!.Status);
            Assert.Equal(1, activeQuest.StageIndex);
            Assert.Contains("Visit", activeQuest.StageDescription, StringComparison.OrdinalIgnoreCase);
            AssertObjectiveCounter(characterGuid, seed.QuestId, 0, 0, 1);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildDialogueChoice(dialogueToken, "accept"));
            var replayChoice = await client.ReadUntilAsync(PacketId.DialogueChoiceResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(replayChoice, out var replayOk, out _));
            Assert.False(replayOk);

            // Step 12: all five quest objective kinds via public gameplay (Talk done via dialogue accept)
            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.Region2TileX, seed.Region2TileY);
            AssertObjectiveCounter(characterGuid, seed.QuestId, 1, 0, 1);
            var visitJournal = await ReselectAndReadQuestJournalAsync(client, characterId);
            var visitQuest = Phase8WireDecoders.FindQuestEntry(visitJournal, seed.QuestId);
            Assert.NotNull(visitQuest);
            Assert.Equal(2, visitQuest!.StageIndex);
            Assert.Contains("Collect", visitQuest.StageDescription, StringComparison.OrdinalIgnoreCase);

            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.CollectObjectiveTileX, seed.CollectObjectiveTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(seed.CollectGroundItemId));
            var pickupResult = await client.ReadUntilAsync(PacketId.PickupItemResult);
            Assert.NotEqual(0, pickupResult[1]);
            var collectJournal = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(collectJournal, out var collectedJournal));
            var collectQuest = Phase8WireDecoders.FindQuestEntry(collectedJournal, seed.QuestId);
            Assert.NotNull(collectQuest);
            Assert.Equal(3, collectQuest!.StageIndex);
            AssertObjectiveCounter(characterGuid, seed.QuestId, 2, 0, 1);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee("Slime"));
            var killMelee = await client.ReadUntilAsync(PacketId.MeleeAttackResult);
            Assert.NotEqual(0, killMelee[1]);
            var killJournal = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(killJournal, out var killedJournal));
            var killQuest = Phase8WireDecoders.FindQuestEntry(killedJournal, seed.QuestId);
            Assert.NotNull(killQuest);
            Assert.Equal(4, killQuest!.StageIndex);
            AssertObjectiveCounter(characterGuid, seed.QuestId, 3, 0, 1);
            _ = await client.ReadUntilAsync(PacketId.CombatState);

            // Step 12 continued: objectives via gameplay — step-on gives ingredients
            var invAfterContact = await Phase8MovementTestHelpers.TeleportOntoContactAndReadInventoryAsync(
                client, seed.ContactEventTileX, seed.ContactEventTileY);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterContact, out var contactInv));
            Assert.True(contactInv.Slots.Where(s => s.ItemId == seed.Phase7.ConsumableId).Sum(s => s.Quantity) >= 2);
            await client.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            // Step 18 (wait/resume): action event waits then sets switch via heartbeat resume
            using (var gate = CreateGate())
            {
                var world = new PostgresCharacterWorldStateRepository(gate);
                Assert.False(await world.GetSwitchAsync(characterGuid, seed.WaitSwitchId) ?? false);
            }

            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.WaitEventTileX, seed.WaitEventTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            var waitInteract = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(waitInteract, out var waitOk, out _));
            Assert.True(waitOk);

            var switchSet = false;
            for (var i = 0; i < 8; i++)
            {
                await Phase8MovementTestHelpers.SendHeartbeatAsync(client);
                await Task.Delay(200);
                using (var gate = CreateGate())
                {
                    var world = new PostgresCharacterWorldStateRepository(gate);
                    if (await world.GetSwitchAsync(characterGuid, seed.WaitSwitchId) == true)
                    {
                        switchSet = true;
                        break;
                    }
                }
            }

            Assert.True(switchSet);

            // Step 18 (parallel): heartbeat fires parallel map event; repeats on subsequent heartbeats
            await Phase8MovementTestHelpers.SendHeartbeatAsync(client);
            var parallel1 = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(parallel1, out _, out var parallelMsg1));
            Assert.Contains("Parallel pulse", parallelMsg1);
            await Phase8MovementTestHelpers.SendHeartbeatAsync(client);
            var parallel2 = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(parallel2, out _, out var parallelMsg2));
            Assert.Contains("Parallel pulse", parallelMsg2);

            // Step 18 (route movement): one heartbeat advances route to the block tile (long dwell keeps it there)
            await Phase8MovementTestHelpers.SendHeartbeatAsync(client);
            await Task.Delay(300);

            var blockedMove = await Phase8MovementTestHelpers.TryMoveToTileExpectingErrorAsync(
                client, seed.RouteBlockTileX, seed.RouteBlockTileY);
            Assert.True(Phase8WireDecoders.TryDecodeError(blockedMove, out var blockMsg));
            Assert.Contains("evenement", blockMsg, StringComparison.OrdinalIgnoreCase);
            await client.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            // Step 12 continued: acquire profession via public map event path (not DB seed)
            await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.LearnProfessionTileX, seed.LearnProfessionTileY);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            var learnProfession = await client.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(learnProfession, out var learnOk, out var learnMsg));
            Assert.True(learnOk);
            Assert.Contains("Profession learned", learnMsg);

            // Step 14: craft recipe — atomic state
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCraft(seed.RecipeId, craftRequestId));
            var craftResult = await client.ReadUntilAsync(PacketId.CraftResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(craftResult, out var craftOk, out _));
            Assert.True(craftOk);
            var journalAfterCraft = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(journalAfterCraft, out var craftedJournal));
            var readyQuest = Phase8WireDecoders.FindQuestEntry(craftedJournal, seed.QuestId);
            Assert.NotNull(readyQuest);
            Assert.Equal((byte)CharacterQuestStatus.ReadyToTurnIn, readyQuest!.Status);
            AssertObjectiveCounter(characterGuid, seed.QuestId, 4, 0, 1);

            // Step 15: retry craft — no duplication; quest objective not replayed
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCraft(seed.RecipeId, craftRequestId));
            var craftReplay = await client.ReadUntilAsync(PacketId.CraftResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(craftReplay, out var craftReplayOk, out _));
            Assert.True(craftReplayOk);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                client.ReadUntilAsync(PacketId.QuestJournalSnapshot, TimeSpan.FromMilliseconds(400)));
            using (var gate = CreateGate())
            {
                var inv = new PostgresInventoryRepository(gate);
                var craftInv = await inv.GetAsync(characterGuid).ConfigureAwait(false);
                var consumableQty = craftInv.Slots.Where(s => s.ItemId == seed.Phase7.ConsumableId).Sum(s => s.Quantity);
                Assert.Equal(1, consumableQty);
                var questRepo = new PostgresCharacterQuestRepository(gate);
                var progress = await questRepo.TryGetAsync(characterGuid, seed.QuestId).ConfigureAwait(false);
                Assert.NotNull(progress);
                Assert.Equal(CharacterQuestStatus.ReadyToTurnIn, progress!.Status);
                Assert.Equal(1, progress.ObjectiveCounters.GetValueOrDefault(QuestObjectiveKeys.For(4, 0)));
            }

            // Step 16: quest completion — reward once
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            _ = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
            var combatBeforeTurnIn = await client.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combatBeforeTurnIn, out _, out _, out _, out _, out _, out _, out var goldBeforeTurnIn, out _));
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildQuestTurnIn(seed.QuestId, turnInRequestId));
            var turnIn = await client.ReadUntilAsync(PacketId.QuestTurnInResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(turnIn, out var turnInOk, out _));
            Assert.True(turnInOk);
            var combatAfterTurnIn = await client.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combatAfterTurnIn, out _, out _, out _, out _, out _, out _, out var goldAfterTurnIn, out _));
            Assert.Equal(goldBeforeTurnIn + seed.QuestRewardGold, goldAfterTurnIn);
            var journalCompleted = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(journalCompleted, out var completedJournal));
            Assert.Equal((byte)CharacterQuestStatus.Completed, Phase8WireDecoders.FindQuestEntry(completedJournal, seed.QuestId)!.Status);

            // Step 17: retry completion — no duplicate reward
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildQuestTurnIn(seed.QuestId, turnInRequestId));
            var turnInReplay = await client.ReadUntilAsync(PacketId.QuestTurnInResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(turnInReplay, out var turnInReplayOk, out _));
            Assert.True(turnInReplayOk);
            var combatReplay = await client.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combatReplay, out _, out _, out _, out _, out _, out _, out var goldReplay, out _));
            Assert.Equal(goldAfterTurnIn, goldReplay);

            // Step 13: disconnect/reconnect quest progress
            await client.DisconnectAsync();
            await Task.Delay(150);
            await using var client2 = new Phase7TcpTestClient();
            await client2.ConnectAsync("127.0.0.1", port);
            _ = await client2.ReadFrameAsync();
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            _ = await client2.ReadUntilAsync(PacketId.CombatState);
            _ = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            _ = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            _ = await client2.ReadUntilAsync(PacketId.GroundItemsSnapshot);
            var reconnectJournal = await client2.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            _ = await client2.ReadUntilAsync(PacketId.EnvironmentStatePush);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(reconnectJournal, out var reconnectedJournal));
            Assert.Equal((byte)CharacterQuestStatus.Completed, Phase8WireDecoders.FindQuestEntry(reconnectedJournal, seed.QuestId)!.Status);
            await client2.DrainPendingAsync();
            await client2.DisconnectAsync();
        }
        finally
        {
            await host.StopAsync();
        }

        // Step 20: server stop/restart
        using var host2 = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host2.StartAsync();
        try
        {
            // Step 21: reconnect — persistence
            await using var client3 = new Phase7TcpTestClient();
            await client3.ConnectAsync("127.0.0.1", port);
            _ = await client3.ReadFrameAsync();
            await client3.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await client3.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client3.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client3.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            var persistedCombat = await client3.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                persistedCombat, out _, out _, out _, out _, out _, out _, out var persistedGold, out _));
            Assert.Equal(startingGold + seed.QuestRewardGold, persistedGold);
            _ = await client3.ReadUntilAsync(PacketId.InventorySnapshot);
            _ = await client3.ReadUntilAsync(PacketId.BankSnapshot);
            _ = await client3.ReadUntilAsync(PacketId.GroundItemsSnapshot);
            var persistedJournal = await client3.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            _ = await client3.ReadUntilAsync(PacketId.EnvironmentStatePush);
            Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(persistedJournal, out var persistedEntries));
            Assert.Equal((byte)CharacterQuestStatus.Completed, Phase8WireDecoders.FindQuestEntry(persistedEntries, seed.QuestId)!.Status);

            // Step 22: republish + live refresh (same server process, no restart)
            const string republishedText = "Republished greeting.";
            using (var gate = CreateGate())
            {
                await Phase8PostgresContentSeed.RepublishDialogueAsync(gate, republishedText).ConfigureAwait(false);
            }

            var user2 = $"p8b-{Guid.NewGuid():N}"[..16];
            await using var refreshClient = new Phase7TcpTestClient();
            var refreshCharacterId = await RegisterLoginSelectReturningIdAsync(
                refreshClient, port, user2, "password12345", "Refresher", seed.Phase7.ClassId);
            _ = await refreshClient.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            _ = await refreshClient.ReadUntilAsync(PacketId.EnvironmentStatePush);
            await refreshClient.DrainPendingAsync(TimeSpan.FromMilliseconds(300));

            await refreshClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildAcquireProfession(seed.ProfessionId));
            var acquireResult = await refreshClient.ReadUntilAsync(PacketId.AcquireProfessionResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(acquireResult, out var acquireOk, out _));
            Assert.True(acquireOk);

            await Phase8MovementTestHelpers.TeleportToTileAsync(refreshClient, seed.KeyEventTileX, seed.KeyEventTileY);
            await refreshClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            _ = await refreshClient.ReadUntilAsync(PacketId.InteractResult);
            await Phase8MovementTestHelpers.TeleportToTileAsync(refreshClient, seed.GateEventTileX, seed.GateEventTileY);
            await refreshClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            var refreshedDialogue = await Phase8TcpTestHelpers.ReadDialogueThenInteractAsync(refreshClient);
            Assert.True(Phase8WireDecoders.TryDecodeDialogueStatePush(
                refreshedDialogue, out _, out _, out _, out _, out var refreshedText, out _));
            Assert.Contains("Republished greeting", refreshedText);
        }
        finally
        {
            // Step 23: clean shutdown
            await host2.StopAsync();
        }
    }

    private async Task AssertSeedContentAsync(Phase8PostgresContentSeedResult seed)
    {
        using var gate = CreateGate();
        var phase8 = new PostgresPhase8PublishedCatalogs(gate);
        IPublishedQuestCatalog quests = phase8;
        IPublishedDialogueCatalog dialogues = phase8;
        var mapEvents = new PostgresMapEventRepository(gate);

        Assert.NotNull(await dialogues.TryGetPublishedByIdAsync(seed.DialogueId));
        Assert.NotNull(await quests.TryGetPublishedByIdAsync(seed.QuestId));
        Assert.Null(await quests.TryGetPublishedByIdAsync(seed.DraftQuestId));

        var gateEvent = await mapEvents.TryGetPublishedByIdAsync(seed.GateMapEventId);
        Assert.NotNull(gateEvent);
        Assert.Equal(2, gateEvent!.Pages.Count);
        Assert.Contains(gateEvent.Pages, p => p.Conditions.Count > 0);
    }

    private async Task<Phase8PostgresContentSeedResult> SeedPublishedContentAsync()
    {
        using var gate = CreateGate();
        return await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private void AssertObjectiveCounter(
        Guid characterId,
        Guid questId,
        int stageIndex,
        int objectiveIndex,
        int expectedCount)
    {
        using var gate = CreateGate();
        var questRepo = new PostgresCharacterQuestRepository(gate);
        var progress = questRepo.TryGetAsync(characterId, questId).GetAwaiter().GetResult();
        Assert.NotNull(progress);
        var key = QuestObjectiveKeys.For(stageIndex, objectiveIndex);
        Assert.Equal(expectedCount, progress!.ObjectiveCounters.GetValueOrDefault(key));
    }

    private static async Task<IReadOnlyList<QuestJournalEntryWire>> ReselectAndReadQuestJournalAsync(
        Phase7TcpTestClient client,
        string characterId)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
        _ = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
        _ = await client.ReadUntilAsync(PacketId.CombatState);
        _ = await client.ReadUntilAsync(PacketId.InventorySnapshot);
        _ = await client.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await client.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        var journalFrame = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
        _ = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
        Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(journalFrame, out var journal));
        return journal;
    }

    private static async Task<Guid> RegisterLoginSelectReturningIdAsync(
        Phase7TcpTestClient tcp,
        int port,
        string user,
        string password,
        string charName,
        Guid classId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.LoginResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        _ = await tcp.ReadUntilAsync(PacketId.CombatState);
        _ = await tcp.ReadUntilAsync(PacketId.InventorySnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        return Guid.Parse(id);
    }
}
