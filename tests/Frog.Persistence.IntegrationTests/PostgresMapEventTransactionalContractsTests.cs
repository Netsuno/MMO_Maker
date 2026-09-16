using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities.Player;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

/// <summary>Preuves R2-4 : une activation = une TX PG, ledger ActivationId/WaitOrdinal, wait-resume, reconnect.</summary>
[Collection("PostgresIsolated")]
public sealed class PostgresMapEventTransactionalContractsTests
{
    private const long MixedPlacementId = 401;
    private const int MixedCatalogAliasId = 1;
    private const int TeleportMapId = 4;
    private const int TeleportTileX = 1;
    private const int TeleportTileY = 2;

    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMapEventTransactionalContractsTests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task MixedDialogueTeleport_CommitsPersistentAndSessionIntentsInOneTx()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        var effects = MixedEffects(seed);
        var plan = MapEventExecutionPlan.Ok(identity, effects);
        var unit = MapEventTransactionalUnit.FromPlan(plan);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(2, unit.SessionSideEffects.Count);

        var repo = CreateRepo(gate);
        var result = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Executed, result.Status);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.Equal(Phase8PostgresContentSeed.DefaultDialogueId, result.Snapshot!.DialogueId);
        Assert.Equal((TeleportMapId, TeleportTileX, TeleportTileY), result.Snapshot.Teleport);
        Assert.Equal(identity.EffectiveActivationId, result.Snapshot.ActivationId);
        Assert.Equal(0, result.Snapshot.WaitOrdinal);
        Assert.Contains(
            result.Snapshot.SwitchChanges,
            s => s.SwitchId == Phase8PostgresContentSeed.GateSwitchId && s.Value);

        await AssertMixedWorldAsync(characterId, seed, identity, expectGranted: true, expectIntents: true);
        await AssertCharacterPositionUnchangedAsync(characterId);
        Assert.Equal(1, await CountLedgerRowsAsync(CreateGate(), identity.LedgerKey));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task MixedDialogueTeleport_LaterFailure_RollsBackPersistentAndSessionIntents()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        var unknownItemId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff");
        var effects = MixedEffects(seed)
            .Append(Cmd(MapEventCommandDiscriminators.GiveItem, $"{{\"itemId\":\"{unknownItemId}\",\"quantity\":1}}"))
            .ToArray();
        var repo = CreateRepo(gate);

        var result = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(identity, effects));
        Assert.Equal(MapEventMutationStatus.Failed, result.Status);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.Null(result.Snapshot);

        await AssertMixedWorldAsync(characterId, seed, identity, expectGranted: false, expectIntents: false);
        await AssertCharacterPositionUnchangedAsync(characterId);
        Assert.Equal(0, await CountLedgerRowsAsync(CreateGate(), identity.LedgerKey));
        Assert.Equal(0, await CountActivationRowsAsync(characterId, identity.EffectiveActivationId));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Mixed_CancelBeforeCommit_WritesNothing()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        using var cts = new CancellationTokenSource();
        var repo = CreateRepo(gate);
        repo.TestBeforeCommitAsync = _ =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        var result = await repo.TryExecutePlanAsync(
            MapEventExecutionPlan.Ok(identity, MixedEffects(seed)),
            cts.Token);

        Assert.Equal(MapEventMutationStatus.Failed, result.Status);
        Assert.Contains("annulé", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.Null(result.Snapshot);

        await AssertMixedWorldAsync(characterId, seed, identity, expectGranted: false, expectIntents: false);
        Assert.Equal(0, await CountLedgerRowsAsync(CreateGate(), identity.LedgerKey));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Mixed_RetryAfterRollback_SucceedsWithoutPoison()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var failedIdentity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        var retryIdentity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        Assert.NotEqual(failedIdentity.LedgerKey, retryIdentity.LedgerKey);
        var effects = MixedEffects(seed);
        var repo = CreateRepo(gate);
        var injectOnce = 0;
        repo.TestBeforeCommitAsync = _ =>
        {
            if (Interlocked.CompareExchange(ref injectOnce, 1, 0) == 0)
            {
                throw new InvalidOperationException("injected-before-commit-mixed");
            }

            return Task.CompletedTask;
        };

        var failed = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(failedIdentity, effects));
        Assert.Equal(MapEventMutationStatus.Failed, failed.Status);
        await AssertMixedWorldAsync(characterId, seed, failedIdentity, expectGranted: false, expectIntents: false);

        var retry = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(retryIdentity, effects));
        Assert.Equal(MapEventMutationStatus.Executed, retry.Status);
        Assert.Equal(2, repo.TransactionsBegun);
        Assert.Equal(Phase8PostgresContentSeed.DefaultDialogueId, retry.Snapshot!.DialogueId);
        Assert.Equal((TeleportMapId, TeleportTileX, TeleportTileY), retry.Snapshot.Teleport);
        await AssertMixedWorldAsync(characterId, seed, retryIdentity, expectGranted: true, expectIntents: true);
        Assert.Equal(0, await CountLedgerRowsAsync(CreateGate(), failedIdentity.LedgerKey));
        Assert.Equal(1, await CountLedgerRowsAsync(CreateGate(), retryIdentity.LedgerKey));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task WaitResume_WritesSecondLedgerRowBoundToSameActivation()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        var plan = MapEventExecutionPlan.Ok(
            identity,
            [
                Cmd(MapEventCommandDiscriminators.SetSwitch,
                    $"{{\"switchId\":\"{Phase8PostgresContentSeed.GateSwitchId}\",\"value\":true}}"),
                Cmd(MapEventCommandDiscriminators.Wait, """{"milliseconds":10}"""),
                Cmd(MapEventCommandDiscriminators.GiveItem,
                    $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1}}"),
                Cmd(MapEventCommandDiscriminators.Teleport,
                    $"{{\"mapId\":{TeleportMapId},\"tileX\":{TeleportTileX},\"tileY\":{TeleportTileY}}}"),
            ]);
        var unit = MapEventTransactionalUnit.FromPlan(plan);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.NotNull(unit.ResumePlan);
        Assert.NotEqual(identity.LedgerKey, unit.ResumePlan!.Identity.LedgerKey);
        Assert.True(identity.IsSameActivation(unit.ResumePlan.Identity));
        Assert.Equal(1, unit.ResumePlan.Identity.WaitOrdinal);

        var repo = CreateRepo(gate);
        var first = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Executed, first.Status);
        Assert.True(first.Snapshot!.Waiting);
        Assert.Equal(identity.EffectiveActivationId, first.Snapshot.ActivationId);
        Assert.Equal(0, first.Snapshot.WaitOrdinal);
        Assert.Null(first.Snapshot.Teleport);
        Assert.Equal(1, first.Snapshot.PendingCommands!.Count);

        using var afterPrefix = CreateGate();
        Assert.Equal(true, await new PostgresCharacterWorldStateRepository(afterPrefix)
            .GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId));
        var qtyAfterPrefix = (await new PostgresInventoryRepository(afterPrefix).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(0, qtyAfterPrefix);
        Assert.Equal(1, await CountLedgerRowsAsync(afterPrefix, identity.LedgerKey));

        var prefixReplay = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.IdempotentReplay, prefixReplay.Status);
        Assert.Equal(1, repo.TransactionsBegun);

        var resume = await repo.TryExecutePlanAsync(unit.ResumePlan);
        Assert.Equal(MapEventMutationStatus.Executed, resume.Status);
        Assert.Equal(2, repo.TransactionsBegun);
        Assert.Equal((TeleportMapId, TeleportTileX, TeleportTileY), resume.Snapshot!.Teleport);
        Assert.Equal(1, resume.Snapshot.WaitOrdinal);
        Assert.Equal(identity.EffectiveActivationId, resume.Snapshot.ActivationId);

        var restoredResume = MapEventExecutionIdentity.Restore(
            characterId,
            MixedPlacementId,
            MixedCatalogAliasId,
            identity.EffectiveActivationId,
            waitOrdinal: 1);
        Assert.Equal(unit.ResumePlan.Identity.LedgerKey, restoredResume.LedgerKey);
        Assert.Equal(1, await CountLedgerRowsAsync(CreateGate(), identity.LedgerKey));
        Assert.Equal(1, await CountLedgerRowsAsync(CreateGate(), restoredResume.LedgerKey));
        Assert.Equal(2, await CountActivationRowsAsync(characterId, identity.EffectiveActivationId));

        using var verify = CreateGate();
        var qty = (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
        var resumeRow = await LoadLedgerAsync(verify, restoredResume.LedgerKey);
        Assert.NotNull(resumeRow);
        Assert.Equal(identity.EffectiveActivationId, resumeRow!.ActivationId);
        Assert.Equal(1, resumeRow.WaitOrdinal);
        await AssertCharacterPositionUnchangedAsync(characterId);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ReconnectRestore_ReplaysCommittedActivation_NewBeginActivationIsDistinct()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var original = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        var effects = MixedEffects(seed);
        var repo = CreateRepo(gate);

        var first = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(original, effects));
        Assert.Equal(MapEventMutationStatus.Executed, first.Status);
        Assert.Equal(original.EffectiveActivationId, first.Snapshot!.ActivationId);

        using var ledgerGate = CreateGate();
        var row = await LoadLedgerAsync(ledgerGate, original.LedgerKey);
        Assert.NotNull(row);
        var restored = MapEventExecutionIdentity.Restore(
            row!.CharacterId,
            row.PlacementId,
            row.CatalogAliasId,
            row.ActivationId,
            row.WaitOrdinal);
        Assert.Equal(original.LedgerKey, restored.LedgerKey);
        Assert.True(original.IsSameActivation(restored));

        var replay = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(restored, effects));
        Assert.Equal(MapEventMutationStatus.IdempotentReplay, replay.Status);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.Equal(Phase8PostgresContentSeed.DefaultDialogueId, replay.Snapshot!.DialogueId);
        Assert.Equal((TeleportMapId, TeleportTileX, TeleportTileY), replay.Snapshot.Teleport);

        var qtyAfterReplay = await ItemQtyAsync(characterId, seed.Phase7.ConsumableId);
        Assert.Equal(1, qtyAfterReplay);

        var next = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        Assert.False(original.IsSameActivation(next));
        Assert.NotEqual(original.LedgerKey, next.LedgerKey);

        var second = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(next, effects));
        Assert.Equal(MapEventMutationStatus.Executed, second.Status);
        Assert.Equal(2, repo.TransactionsBegun);
        Assert.Equal(2, await ItemQtyAsync(characterId, seed.Phase7.ConsumableId));
        Assert.Equal(1, await CountLedgerRowsAsync(CreateGate(), original.LedgerKey));
        Assert.Equal(1, await CountLedgerRowsAsync(CreateGate(), next.LedgerKey));
        Assert.Equal(1, await CountActivationRowsAsync(characterId, original.EffectiveActivationId));
        Assert.Equal(1, await CountActivationRowsAsync(characterId, next.EffectiveActivationId));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task PublicPath_CorePlanMixedDialogueTeleport_OneTx()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.BeginActivation(characterId, MixedPlacementId, MixedCatalogAliasId);
        var pageCommands = new[]
        {
            BranchThen(MixedEffects(seed)),
        };

        var plan = await MapEventExecutionPlanner.PlanAsync(
            pageCommands,
            InMemoryCommonEventSource.Empty,
            _ => Task.FromResult(true),
            identity);
        Assert.True(plan.IsSuccess, plan.Error);
        Assert.True(MapEventExecutionPlanner.IsUnifiedTransactionalUnit(plan.Effects));
        Assert.DoesNotContain(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.Branch);

        var repo = CreateRepo(gate);
        var result = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Executed, result.Status);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.Equal(Phase8PostgresContentSeed.DefaultDialogueId, result.Snapshot!.DialogueId);
        Assert.Equal((TeleportMapId, TeleportTileX, TeleportTileY), result.Snapshot.Teleport);
        await AssertMixedWorldAsync(characterId, seed, identity, expectGranted: true, expectIntents: true);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static PostgresMapEventMutationRepository CreateRepo(FrogDbContextGate gate)
    {
        var catalogs = new PostgresPhase8PublishedCatalogs(gate);
        return new PostgresMapEventMutationRepository(
            gate,
            new PostgresItemRepository(gate),
            catalogs,
            catalogs,
            catalogs);
    }

    private static MapEventCommandDefinition Cmd(string discriminator, string json) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = json,
        };

    private static MapEventCommandDefinition BranchThen(IReadOnlyList<MapEventCommandDefinition> thenCommands) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.Branch,
            SchemaVersion = 1,
            ParameterJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                conditionKind = MapEventConditionKinds.CharacterSwitch,
                conditionParameterJson = """{"switchId":"gate_open","value":true}""",
                thenCommands = thenCommands.Select(c => new
                {
                    discriminator = c.Discriminator,
                    parameterJson = c.ParameterJson,
                }).ToArray(),
                elseCommands = Array.Empty<object>(),
            }),
        };

    private static MapEventCommandDefinition[] MixedEffects(Phase8PostgresContentSeedResult seed) =>
    [
        Cmd(MapEventCommandDiscriminators.GiveItem,
            $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1}}"),
        Cmd(MapEventCommandDiscriminators.StartDialogue,
            $"{{\"dialogueId\":\"{Phase8PostgresContentSeed.DefaultDialogueId:D}\"}}"),
        Cmd(MapEventCommandDiscriminators.Teleport,
            $"{{\"mapId\":{TeleportMapId},\"tileX\":{TeleportTileX},\"tileY\":{TeleportTileY}}}"),
        Cmd(MapEventCommandDiscriminators.SetSwitch,
            $"{{\"switchId\":\"{Phase8PostgresContentSeed.GateSwitchId}\",\"value\":true}}"),
    ];

    private async Task AssertMixedWorldAsync(
        Guid characterId,
        Phase8PostgresContentSeedResult seed,
        MapEventExecutionIdentity identity,
        bool expectGranted,
        bool expectIntents)
    {
        using var verify = CreateGate();
        var qty = (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(expectGranted ? 1 : 0, qty);
        var gateOpen = await new PostgresCharacterWorldStateRepository(verify)
            .GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId);
        if (expectGranted)
        {
            Assert.Equal(true, gateOpen);
        }
        else
        {
            Assert.NotEqual(true, gateOpen);
        }

        var row = await LoadLedgerAsync(verify, identity.LedgerKey);
        if (!expectGranted)
        {
            Assert.Null(row);
            return;
        }

        Assert.NotNull(row);
        Assert.Equal(identity.EffectiveActivationId, row!.ActivationId);
        Assert.Equal(identity.WaitOrdinal, row.WaitOrdinal);
        if (expectIntents)
        {
            Assert.Contains($"\"dialogueId\":\"{Phase8PostgresContentSeed.DefaultDialogueId:D}\"", row.ResultJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"\"teleportMapId\":{TeleportMapId}", row.ResultJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task AssertCharacterPositionUnchangedAsync(Guid characterId)
    {
        using var verify = CreateGate();
        var character = await new PostgresCharacterRepository(verify).FindByIdAsync(characterId);
        Assert.NotNull(character);
        Assert.Equal(GameplayLimits.DefaultSpawnMapId, character!.MapId);
        Assert.Equal(32, character.PixelX);
        Assert.Equal(48, character.PixelY);
    }

    private async Task<int> ItemQtyAsync(Guid characterId, Guid itemId)
    {
        using var verify = CreateGate();
        return (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == itemId)
            .Sum(s => s.Quantity);
    }

    private static async Task<int> CountLedgerRowsAsync(
        FrogDbContextGate gate,
        (Guid CharacterId, Guid RequestId) ledgerKey)
    {
        using (gate)
        {
            return await gate.ExecuteAsync(async (db, ct) =>
                    await db.PlayerMapEventExecutionRequests.CountAsync(
                        r => r.CharacterId == ledgerKey.CharacterId && r.RequestId == ledgerKey.RequestId,
                        ct),
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<int> CountActivationRowsAsync(Guid characterId, Guid activationId)
    {
        using var gate = CreateGate();
        return await gate.ExecuteAsync(async (db, ct) =>
                await db.PlayerMapEventExecutionRequests.CountAsync(
                    r => r.CharacterId == characterId && r.ActivationId == activationId,
                    ct),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<MapEventExecutionRequestEntity?> LoadLedgerAsync(
        FrogDbContextGate gate,
        (Guid CharacterId, Guid RequestId) ledgerKey)
    {
        return await gate.ExecuteAsync(async (db, ct) =>
                await db.PlayerMapEventExecutionRequests.AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.CharacterId == ledgerKey.CharacterId && r.RequestId == ledgerKey.RequestId,
                        ct),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<Guid> CreateCharacterAsync(FrogDbContextGate gate, Phase8PostgresContentSeedResult seed)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"tx-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var result = await chars.CreateAsync(
            created.AccountId!.Value,
            "TxHero",
            seed.Phase7.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.Phase7.SpellId,
            1,
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, result.Status);
        return result.Character!.Id;
    }
}
