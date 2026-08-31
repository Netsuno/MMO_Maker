using System;
using System.Linq;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Server.Gameplay;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventExecutionTrackerTests
{
    [Fact]
    public void TryBeginParallel_BlocksReentryUntilEnded()
    {
        var tracker = new MapEventExecutionTracker();
        var characterId = Guid.NewGuid();
        const long placementId = 42;
        var eventId = Guid.NewGuid();

        Assert.True(tracker.TryBeginParallel(characterId, placementId, eventId, mapId: 1));
        Assert.False(tracker.TryBeginParallel(characterId, placementId, eventId, mapId: 1));

        tracker.EndParallel(characterId, placementId, eventId);
        Assert.True(tracker.TryBeginParallel(characterId, placementId, eventId, mapId: 1));
    }

    [Fact]
    public void TryBeginParallel_IsolatedPerCharacterAndPlacement()
    {
        var tracker = new MapEventExecutionTracker();
        var characterA = Guid.NewGuid();
        var characterB = Guid.NewGuid();
        const long placementA = 1;
        const long placementB = 2;
        var eventId = Guid.NewGuid();

        Assert.True(tracker.TryBeginParallel(characterA, placementA, eventId, mapId: 1));
        Assert.True(tracker.TryBeginParallel(characterB, placementA, eventId, mapId: 1));
        Assert.True(tracker.TryBeginParallel(characterA, placementB, eventId, mapId: 1));
    }

    [Fact]
    public void TryFireAutorunOnce_FiresOnlyOncePerMapVisit()
    {
        var tracker = new MapEventExecutionTracker();
        var characterId = Guid.NewGuid();
        const long placementId = 7;
        var eventId = Guid.NewGuid();

        Assert.True(tracker.TryFireAutorunOnce(characterId, placementId, eventId, mapId: 3));
        Assert.False(tracker.TryFireAutorunOnce(characterId, placementId, eventId, mapId: 3));

        tracker.ClearAutorunForMap(characterId, mapId: 3);
        Assert.True(tracker.TryFireAutorunOnce(characterId, placementId, eventId, mapId: 3));
    }

    [Fact]
    public void TakeReadyWaits_ReturnsDueWaitsAndKeepsPendingOnes()
    {
        var tracker = new MapEventExecutionTracker();
        var characterId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var ready = new PendingWaitResume(
            now.AddMilliseconds(-50),
            [new MapEventCommandDefinition { Discriminator = MapEventCommandDiscriminators.ShowText, ParameterJson = """{"text":"done"}""" }],
            "wait-event");
        var pending = new PendingWaitResume(
            now.AddMinutes(1),
            [new MapEventCommandDefinition { Discriminator = MapEventCommandDiscriminators.ShowText, ParameterJson = """{"text":"later"}""" }],
            "wait-event");

        tracker.RegisterWait(characterId, pending);
        tracker.RegisterWait(characterId, ready);

        var due = tracker.TakeReadyWaits(characterId, now);
        Assert.Single(due);
        Assert.Equal("done", MapEventParameterSchemas.TryParseShowText(due[0].RemainingCommands[0].ParameterJson, out var text, out _)
            ? text
            : null);

        var stillPending = tracker.TakeReadyWaits(characterId, now);
        Assert.Empty(stillPending);
    }

    [Fact]
    public void ClearForCharacter_RemovesParallelAutorunAndWaits()
    {
        var tracker = new MapEventExecutionTracker();
        var characterId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        Assert.True(tracker.TryBeginParallel(characterId, 1, eventId, mapId: 1));
        Assert.True(tracker.TryFireAutorunOnce(characterId, 2, eventId, mapId: 1));
        tracker.RegisterWait(
            characterId,
            new PendingWaitResume(
                DateTimeOffset.UtcNow.AddMinutes(1),
                Array.Empty<MapEventCommandDefinition>()));

        tracker.ClearForCharacter(characterId);

        Assert.True(tracker.TryBeginParallel(characterId, 1, eventId, mapId: 1));
        Assert.True(tracker.TryFireAutorunOnce(characterId, 2, eventId, mapId: 1));
        Assert.Empty(tracker.TakeReadyWaits(characterId, DateTimeOffset.UtcNow.AddMinutes(2)));
    }
}
