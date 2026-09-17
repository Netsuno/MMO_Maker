using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventTransactionalUnitTests
{
    private static readonly Guid CharacterId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ActivationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid ItemId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
    private static readonly Guid DialogueId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Create_IsPerActivation_NotSessionLongPlacement()
    {
        var first = MapEventExecutionIdentity.BeginActivation(CharacterId, placementId: 9, catalogAliasId: 3);
        var second = MapEventExecutionIdentity.BeginActivation(CharacterId, placementId: 9, catalogAliasId: 3);

        Assert.True(first.IsValid);
        Assert.True(first.IsInitialActivation);
        Assert.Equal(first.RequestId, first.ActivationId);
        Assert.Equal(first.RequestId, first.EffectiveActivationId);
        Assert.NotEqual(first.ActivationId, second.ActivationId);
        Assert.NotEqual(first.LedgerKey, second.LedgerKey);
        Assert.False(first.IsSameActivation(second));
    }

    [Fact]
    public void Restore_Reconnect_RebuildsSameLedgerKey()
    {
        var original = MapEventExecutionIdentity.Create(CharacterId, 9, 3, ActivationId);
        var restored = MapEventExecutionIdentity.Restore(CharacterId, 9, 3, ActivationId);

        Assert.Equal(original.LedgerKey, restored.LedgerKey);
        Assert.Equal(original.ActivationId, restored.ActivationId);
        Assert.Equal(0, restored.WaitOrdinal);
        Assert.True(original.IsSameActivation(restored));
    }

    [Fact]
    public void ForWaitResume_IsLedgerBoundToSameActivation()
    {
        var activation = MapEventExecutionIdentity.Create(CharacterId, 9, 3, ActivationId);
        var resume = activation.ForWaitResume(1);
        var restoredResume = MapEventExecutionIdentity.Restore(CharacterId, 9, 3, ActivationId, waitOrdinal: 1);

        Assert.Equal(ActivationId, resume.ActivationId);
        Assert.Equal(1, resume.WaitOrdinal);
        Assert.NotEqual(activation.RequestId, resume.RequestId);
        Assert.NotEqual(activation.LedgerKey, resume.LedgerKey);
        Assert.Equal(resume.RequestId, MapEventExecutionIdentity.DeriveLedgerRequestId(ActivationId, 1));
        Assert.Equal(resume.LedgerKey, restoredResume.LedgerKey);
        Assert.True(activation.IsSameActivation(resume));
        Assert.True(resume.IsValid);
    }

    [Fact]
    public void LegacyFourArgConstructor_FallsBackToRequestIdAsActivation()
    {
        var identity = new MapEventExecutionIdentity(ActivationId, CharacterId, 9, 3);
        Assert.Equal(Guid.Empty, identity.ActivationId);
        Assert.Equal(ActivationId, identity.EffectiveActivationId);
        Assert.True(identity.IsValid);
        Assert.Equal((CharacterId, ActivationId), identity.LedgerKey);
    }

    [Fact]
    public void Classifier_MixedDialogueTeleport_IsUnifiedTransactionalUnit()
    {
        MapEventCommandDefinition[] effects =
        [
            GiveItem(1),
            StartDialogue(),
            Teleport(),
            ShowText("ok"),
        ];

        Assert.True(MapEventEffectClassifier.IsUnifiedTransactionalUnit(effects));
        Assert.True(MapEventExecutionPlanner.IsUnifiedTransactionalUnit(effects));
        Assert.False(MapEventExecutionPlanner.ContainsUnresolvedControlFlow(effects));
        Assert.Equal(MapEventEffectCommitKind.SessionSide, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.Teleport));
        Assert.Equal(MapEventEffectCommitKind.SessionSide, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.StartDialogue));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.GiveItem));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.Wait));
    }

    [Fact]
    public void Classifier_UnresolvedBranch_IsNotUnifiedUnit()
    {
        MapEventCommandDefinition[] effects =
        [
            GiveItem(1),
            new() { Discriminator = MapEventCommandDiscriminators.Branch, ParameterJson = "{}" },
        ];

        Assert.False(MapEventExecutionPlanner.IsUnifiedTransactionalUnit(effects));
        Assert.True(MapEventExecutionPlanner.ContainsUnresolvedControlFlow(effects));
    }

    [Fact]
    public void Plan_MixedDialogueTeleportPage_OneTransactionalUnit()
    {
        var plan = MapEventExecutionPlanner.Plan(
            [GiveItem(1), ShowText("hi"), StartDialogue(), Teleport(), SetSwitch("gate_open", true)],
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(5, plan.Effects.Count);
        Assert.True(MapEventExecutionPlanner.IsUnifiedTransactionalUnit(plan.Effects));

        var unit = plan.AsTransactionalUnit();
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(5, unit.CommitEffects.Count);
        Assert.Equal(2, unit.SessionSideEffects.Count);
        Assert.Null(unit.Wait);
        Assert.Contains(unit.SessionSideEffects, c => c.Discriminator == MapEventCommandDiscriminators.StartDialogue);
        Assert.Contains(unit.SessionSideEffects, c => c.Discriminator == MapEventCommandDiscriminators.Teleport);
        Assert.Equal(Identity().LedgerKey, unit.Identity.LedgerKey);
    }

    [Fact]
    public void FromPlan_WaitSplitsLedgerBoundResume()
    {
        var plan = MapEventExecutionPlan.Ok(
            Identity(),
            [
                SetSwitch("before_wait", true),
                Wait(250),
                GiveItem(1),
                Teleport(),
            ]);

        var unit = MapEventTransactionalUnit.FromPlan(plan);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(2, unit.CommitEffects.Count);
        Assert.Equal(MapEventCommandDiscriminators.Wait, unit.CommitEffects[^1].Discriminator);
        Assert.NotNull(unit.Wait);
        Assert.Equal(250, unit.Wait!.WaitMilliseconds);
        Assert.Equal(2, unit.Wait.RemainingEffects.Count);
        Assert.Equal(1, unit.Wait.ResumeIdentity.WaitOrdinal);
        Assert.Equal(ActivationId, unit.Wait.ResumeIdentity.ActivationId);
        Assert.NotEqual(unit.Identity.LedgerKey, unit.Wait.ResumeIdentity.LedgerKey);
        Assert.True(unit.Identity.IsSameActivation(unit.Wait.ResumeIdentity));

        var resume = unit.ResumePlan;
        Assert.NotNull(resume);
        var resumeUnit = resume!.AsTransactionalUnit();
        Assert.True(resumeUnit.IsSuccess, resumeUnit.Error);
        Assert.Single(resumeUnit.SessionSideEffects);
        Assert.Equal(MapEventCommandDiscriminators.Teleport, resumeUnit.SessionSideEffects[0].Discriminator);
        Assert.Null(resumeUnit.Wait);
    }

    [Fact]
    public void FromPlan_FailedPlan_DoesNotProduceUnit()
    {
        var unit = MapEventExecutionPlan.Fail(Identity(), "plan-error-from-core").AsTransactionalUnit();
        Assert.False(unit.IsSuccess);
        Assert.Equal("plan-error-from-core", unit.Error);
        Assert.Empty(unit.CommitEffects);
    }

    [Fact]
    public void Sandbox_MixedPage_CommitsAsOneUnitIncludingSessionSide()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var unit = Unit([GiveItem(1), StartDialogue(), Teleport(), SetSwitch("mixed", true)]);

        var outcome = sandbox.TryCommit(unit);

        Assert.Equal(MapEventCommitDisposition.Committed, outcome.Disposition);
        Assert.Equal(1, sandbox.TransactionsBegun);
        Assert.Equal(1, sandbox.LedgerCount);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
        Assert.True(sandbox.World.Switches["mixed"]);
        Assert.Equal(DialogueId, sandbox.World.DialogueId);
        Assert.Equal((4, 1, 2), sandbox.World.Teleport);
        Assert.True(outcome.WroteLedger);
    }

    [Fact]
    public void Sandbox_MidFailure_RollsBackPersistentAndSessionSide()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        sandbox.World.Gold = 10;
        var unit = Unit(
        [
            GiveItem(1),
            SetSwitch("should_rollback", true),
            StartDialogue(),
            Teleport(),
            Cmd(MapEventCommandDiscriminators.GiveItem, """{"itemId":"00000000-0000-0000-0000-000000000000","quantity":1}"""),
        ]);

        var outcome = sandbox.TryCommit(unit);

        Assert.Equal(MapEventCommitDisposition.RolledBack, outcome.Disposition);
        Assert.Equal(1, sandbox.TransactionsBegun);
        Assert.Equal(0, sandbox.LedgerCount);
        Assert.False(sandbox.World.Items.ContainsKey(ItemId));
        Assert.False(sandbox.World.Switches.ContainsKey("should_rollback"));
        Assert.Null(sandbox.World.DialogueId);
        Assert.Null(sandbox.World.Teleport);
        Assert.Equal(10, sandbox.World.Gold);
        Assert.False(outcome.WroteLedger);
    }

    [Fact]
    public async Task Sandbox_CancelBeforeCommit_WritesNothing()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        using var cts = new CancellationTokenSource();
        sandbox.BeforeCommitAsync = _ =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        var outcome = await sandbox.TryCommitAsync(Unit([GiveItem(1), Teleport()]), cts.Token);

        Assert.Equal(MapEventCommitDisposition.Cancelled, outcome.Disposition);
        Assert.Equal(1, sandbox.TransactionsBegun);
        Assert.Equal(0, sandbox.LedgerCount);
        Assert.False(sandbox.World.Items.ContainsKey(ItemId));
        Assert.Null(sandbox.World.Teleport);
        Assert.Contains("annulé", outcome.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sandbox_RetryAfterRollback_SucceedsWithoutPoison()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var injectOnce = 0;
        sandbox.BeforeCommitAsync = _ =>
        {
            if (Interlocked.CompareExchange(ref injectOnce, 1, 0) == 0)
            {
                throw new InvalidOperationException("injected-before-commit");
            }

            return Task.CompletedTask;
        };

        var failedIdentity = MapEventExecutionIdentity.BeginActivation(CharacterId, 9, 3);
        var retryIdentity = MapEventExecutionIdentity.BeginActivation(CharacterId, 9, 3);
        var effects = new[] { GiveItem(1), StartDialogue(), SetSwitch("retry_ok", true) };

        var failed = sandbox.TryCommit(MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(failedIdentity, effects)));
        Assert.Equal(MapEventCommitDisposition.RolledBack, failed.Disposition);
        Assert.Equal(0, sandbox.LedgerCount);
        Assert.False(sandbox.World.Items.ContainsKey(ItemId));

        var retry = sandbox.TryCommit(MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(retryIdentity, effects)));
        Assert.Equal(MapEventCommitDisposition.Committed, retry.Disposition);
        Assert.Equal(2, sandbox.TransactionsBegun);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
        Assert.True(sandbox.World.Switches["retry_ok"]);
        Assert.Equal(DialogueId, sandbox.World.DialogueId);
        Assert.NotEqual(failedIdentity.LedgerKey, retryIdentity.LedgerKey);
    }

    [Fact]
    public void Sandbox_ReplaySameActivation_IsIdempotent()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var unit = Unit([GiveItem(1), GiveGold(15), StartDialogue()]);

        var first = sandbox.TryCommit(unit);
        var replay = sandbox.TryCommit(unit);

        Assert.Equal(MapEventCommitDisposition.Committed, first.Disposition);
        Assert.Equal(MapEventCommitDisposition.IdempotentReplay, replay.Disposition);
        Assert.Equal(1, sandbox.TransactionsBegun);
        Assert.Equal(1, sandbox.LedgerCount);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
        Assert.Equal(15, sandbox.World.Gold);
        Assert.Equal(DialogueId, replay.Snapshot!.DialogueId);
    }

    [Fact]
    public void Sandbox_WaitResume_SecondLedgerRowSameActivation()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var prefix = MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(
            Identity(),
            [SetSwitch("pre", true), Wait(10), GiveItem(1), Teleport()]));
        Assert.True(prefix.IsSuccess, prefix.Error);

        var first = sandbox.TryCommit(prefix);
        Assert.Equal(MapEventCommitDisposition.Committed, first.Disposition);
        Assert.True(sandbox.World.Switches["pre"]);
        Assert.False(sandbox.World.Items.ContainsKey(ItemId));
        Assert.Null(sandbox.World.Teleport);
        Assert.NotNull(first.Wait);

        var resumeUnit = prefix.ResumePlan!.AsTransactionalUnit();
        var resume = sandbox.TryCommit(resumeUnit);
        Assert.Equal(MapEventCommitDisposition.Committed, resume.Disposition);
        Assert.Equal(2, sandbox.TransactionsBegun);
        Assert.Equal(2, sandbox.LedgerCount);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
        Assert.Equal((4, 1, 2), sandbox.World.Teleport);
        Assert.True(prefix.Identity.IsSameActivation(resumeUnit.Identity));
        Assert.NotEqual(prefix.Identity.LedgerKey, resumeUnit.Identity.LedgerKey);

        var restored = MapEventExecutionIdentity.Restore(CharacterId, 101, 3, ActivationId, waitOrdinal: 1);
        Assert.True(sandbox.TryGetLedger(restored, out var entry));
        Assert.Equal(1, entry.Snapshot.Items[ItemId]);
    }

    [Fact]
    public void Sandbox_ReconnectRestore_ReplaysCommittedActivation()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var original = MapEventExecutionIdentity.BeginActivation(CharacterId, 7, 2, ActivationId);
        var unit = MapEventTransactionalUnit.FromPlan(
            MapEventExecutionPlan.Ok(original, [GiveItem(1), Teleport()]));
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);

        var restored = MapEventExecutionIdentity.Restore(CharacterId, 7, 2, ActivationId);
        var replay = sandbox.TryCommit(MapEventTransactionalUnit.FromPlan(
            MapEventExecutionPlan.Ok(restored, [GiveItem(1), Teleport()])));

        Assert.Equal(MapEventCommitDisposition.IdempotentReplay, replay.Disposition);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
        Assert.Equal(1, sandbox.TransactionsBegun);
    }

    [Fact]
    public void Sandbox_RejectedFailedPlan_DoesNotBeginTransaction()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var outcome = sandbox.TryCommit(MapEventExecutionPlan.Fail(Identity(), "plan-error-from-core").AsTransactionalUnit());
        Assert.Equal(MapEventCommitDisposition.Rejected, outcome.Disposition);
        Assert.Equal(0, sandbox.TransactionsBegun);
        Assert.Equal(0, sandbox.LedgerCount);
    }

    [Fact]
    public async Task PlanAsync_CancelBeforeExpand_ThrowsWithoutEffects()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MapEventExecutionPlanner.PlanAsync(
                [GiveItem(1)],
                InMemoryCommonEventSource.Empty,
                _ => Task.FromResult(true),
                Identity(),
                cts.Token));
    }

    private static MapEventExecutionIdentity Identity() =>
        MapEventExecutionIdentity.Create(CharacterId, placementId: 101, catalogAliasId: 3, ActivationId);

    private static MapEventTransactionalUnit Unit(IReadOnlyList<MapEventCommandDefinition> effects) =>
        MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(Identity(), effects));

    private static MapEventCommandDefinition Cmd(string discriminator, string json) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = json,
        };

    private static MapEventCommandDefinition ShowText(string text) =>
        Cmd(MapEventCommandDiscriminators.ShowText, JsonSerializer.Serialize(new { text }));

    private static MapEventCommandDefinition SetSwitch(string switchId, bool value) =>
        Cmd(MapEventCommandDiscriminators.SetSwitch, JsonSerializer.Serialize(new { switchId, value }));

    private static MapEventCommandDefinition GiveItem(int quantity) =>
        Cmd(MapEventCommandDiscriminators.GiveItem, JsonSerializer.Serialize(new { itemId = ItemId, quantity }));

    private static MapEventCommandDefinition GiveGold(int amount) =>
        Cmd(MapEventCommandDiscriminators.GiveGold, JsonSerializer.Serialize(new { amount }));

    private static MapEventCommandDefinition Wait(int milliseconds) =>
        Cmd(MapEventCommandDiscriminators.Wait, JsonSerializer.Serialize(new { milliseconds }));

    private static MapEventCommandDefinition Teleport() =>
        Cmd(MapEventCommandDiscriminators.Teleport, """{"mapId":4,"tileX":1,"tileY":2}""");

    private static MapEventCommandDefinition StartDialogue() =>
        Cmd(MapEventCommandDiscriminators.StartDialogue, $"{{\"dialogueId\":\"{DialogueId:D}\"}}");
}
