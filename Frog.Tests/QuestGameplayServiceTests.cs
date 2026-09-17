using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// R2-5: Talk/Visit/Collect/Kill/Craft counters are exposed on the journal (not inferred
/// from StageIndex alone). Replay re-fetches a fresh journal — never reuses the previous
/// decoded list.
/// </summary>
public sealed class QuestGameplayServiceTests
{
    private static readonly Guid DialogueId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");
    private static readonly Guid NpcId = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000001");
    private static readonly Guid ItemId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000001");
    private static readonly Guid RecipeId = Guid.Parse("aaaaaaaa-0004-4000-8000-000000000001");

    [Fact]
    public async Task Journal_ExposesCountersForTalkVisitCollectKillCraft()
    {
        var (service, characterId, questId) = CreateStarted();

        Assert.True(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Talk, new QuestObjectiveSignal(DialogueId: DialogueId)));
        var afterTalk = await FreshJournalAsync(service, characterId, questId);
        AssertKind(afterTalk, QuestObjectiveKind.Talk, stageIndex: 0, current: 1, completed: true);
        AssertKind(afterTalk, QuestObjectiveKind.Visit, stageIndex: 1, current: 0, completed: false);
        Assert.Equal(1, afterTalk.StageIndex);

        Assert.True(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Visit, new QuestObjectiveSignal(MapId: 4, TileX: 7, TileY: 0)));
        var afterVisit = await FreshJournalAsync(service, characterId, questId);
        AssertKind(afterVisit, QuestObjectiveKind.Talk, stageIndex: 0, current: 1, completed: true);
        AssertKind(afterVisit, QuestObjectiveKind.Visit, stageIndex: 1, current: 1, completed: true);
        Assert.Equal(2, afterVisit.StageIndex);

        Assert.True(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Collect, new QuestObjectiveSignal(ItemId: ItemId)));
        var afterCollect = await FreshJournalAsync(service, characterId, questId);
        AssertKind(afterCollect, QuestObjectiveKind.Collect, stageIndex: 2, current: 1, completed: true);
        Assert.Equal(3, afterCollect.StageIndex);

        Assert.True(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Kill, new QuestObjectiveSignal(NpcId: NpcId)));
        var afterKill = await FreshJournalAsync(service, characterId, questId);
        AssertKind(afterKill, QuestObjectiveKind.Kill, stageIndex: 3, current: 1, completed: true);
        Assert.Equal(4, afterKill.StageIndex);

        Assert.True(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Craft, new QuestObjectiveSignal(RecipeId: RecipeId)));
        var afterCraft = await FreshJournalAsync(service, characterId, questId);
        AssertKind(afterCraft, QuestObjectiveKind.Talk, stageIndex: 0, current: 1, completed: true);
        AssertKind(afterCraft, QuestObjectiveKind.Visit, stageIndex: 1, current: 1, completed: true);
        AssertKind(afterCraft, QuestObjectiveKind.Collect, stageIndex: 2, current: 1, completed: true);
        AssertKind(afterCraft, QuestObjectiveKind.Kill, stageIndex: 3, current: 1, completed: true);
        AssertKind(afterCraft, QuestObjectiveKind.Craft, stageIndex: 4, current: 1, completed: true);
        Assert.Equal((byte)CharacterQuestStatus.ReadyToTurnIn, afterCraft.Status);
        Assert.Equal(5, afterCraft.AllObjectives.Count);
    }

    [Fact]
    public async Task Replay_DoesNotIncrement_AndFreshJournalIsRefetched()
    {
        var (service, characterId, questId) = CreateStarted();
        Assert.True(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Talk, new QuestObjectiveSignal(DialogueId: DialogueId)));
        var beforeReplay = await FreshJournalAsync(service, characterId, questId);
        AssertKind(beforeReplay, QuestObjectiveKind.Talk, 0, 1, true);

        Assert.False(await service.NotifyObjectiveProgressAsync(
            characterId, QuestObjectiveKind.Talk, new QuestObjectiveSignal(DialogueId: DialogueId)));

        var afterReplay = await FreshJournalAsync(service, characterId, questId);
        Assert.NotSame(beforeReplay, afterReplay);
        AssertKind(afterReplay, QuestObjectiveKind.Talk, 0, 1, true);
        Assert.Equal(1, afterReplay.StageIndex);

        foreach (var kind in new[]
                 {
                     QuestObjectiveKind.Visit,
                     QuestObjectiveKind.Collect,
                     QuestObjectiveKind.Kill,
                     QuestObjectiveKind.Craft,
                 })
        {
            var signal = kind switch
            {
                QuestObjectiveKind.Visit => new QuestObjectiveSignal(MapId: 4, TileX: 7, TileY: 0),
                QuestObjectiveKind.Collect => new QuestObjectiveSignal(ItemId: ItemId),
                QuestObjectiveKind.Kill => new QuestObjectiveSignal(NpcId: NpcId),
                _ => new QuestObjectiveSignal(RecipeId: RecipeId),
            };
            Assert.True(await service.NotifyObjectiveProgressAsync(characterId, kind, signal));
            var progressed = await FreshJournalAsync(service, characterId, questId);
            AssertKind(progressed, kind, StageFor(kind), 1, true);

            Assert.False(await service.NotifyObjectiveProgressAsync(characterId, kind, signal));
            var replayed = await FreshJournalAsync(service, characterId, questId);
            Assert.NotSame(progressed, replayed);
            AssertKind(replayed, kind, StageFor(kind), 1, true);
        }
    }

    private static (QuestGameplayService Service, Guid CharacterId, Guid QuestId) CreateStarted()
    {
        var questId = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");
        var characterId = Guid.Parse("aaaaaaaa-0006-4000-8000-000000000001");
        var catalog = new Phase8InMemoryPublishedContent();
        catalog.RegisterQuest(new QuestDefinition
        {
            Id = questId,
            Name = "Counter quest",
            Stages =
            [
                Stage("Talk", QuestObjectiveKind.Talk, o => o.TargetDialogueId = DialogueId),
                Stage("Visit", QuestObjectiveKind.Visit, o =>
                {
                    o.TargetMapId = 4;
                    o.TargetTileX = 7;
                    o.TargetTileY = 0;
                }),
                Stage("Collect", QuestObjectiveKind.Collect, o => o.TargetItemId = ItemId),
                Stage("Kill", QuestObjectiveKind.Kill, o => o.TargetNpcId = NpcId),
                Stage("Craft", QuestObjectiveKind.Craft, o => o.TargetRecipeId = RecipeId),
            ],
        });
        var progress = new InMemoryCharacterQuestRepository();
        var service = new QuestGameplayService(catalog, progress);
        var started = service.TryStartQuestAsync(characterId, questId).GetAwaiter().GetResult();
        Assert.NotNull(started);
        return (service, characterId, questId);
    }

    private static QuestStageDefinition Stage(
        string description,
        QuestObjectiveKind kind,
        Action<QuestObjectiveDefinition> configure)
    {
        var objective = new QuestObjectiveDefinition
        {
            Kind = kind,
            Description = description,
            RequiredCount = 1,
        };
        configure(objective);
        return new QuestStageDefinition
        {
            Description = description,
            Objectives = [objective],
        };
    }

    private static async Task<QuestJournalEntryWire> FreshJournalAsync(
        QuestGameplayService service,
        Guid characterId,
        Guid questId)
    {
        var journal = await service.BuildJournalAsync(characterId);
        var entry = journal.FirstOrDefault(e => e.QuestId == questId);
        Assert.NotNull(entry);
        return entry!;
    }

    private static void AssertKind(
        QuestJournalEntryWire entry,
        QuestObjectiveKind kind,
        int stageIndex,
        int current,
        bool completed)
    {
        var objective = entry.AllObjectives.FirstOrDefault(o =>
            o.StageIndex == stageIndex
            && string.Equals(o.Kind, kind.ToString(), StringComparison.Ordinal));
        Assert.NotNull(objective);
        Assert.Equal(current, objective!.Current);
        Assert.Equal(1, objective.Required);
        Assert.Equal(completed, objective.Completed);
    }

    private static int StageFor(QuestObjectiveKind kind) => kind switch
    {
        QuestObjectiveKind.Talk => 0,
        QuestObjectiveKind.Visit => 1,
        QuestObjectiveKind.Collect => 2,
        QuestObjectiveKind.Kill => 3,
        QuestObjectiveKind.Craft => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
