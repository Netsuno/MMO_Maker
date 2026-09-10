using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Protocol;
using Frog.Server.Database;
using Frog.Server.Models;
using Frog.Server.Network;
using Frog.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Frog.Tests;

public sealed class PublishedContentLiveRefreshCoordinatorTests
{
    [Fact]
    public async Task RefreshConnectedSessions_InvalidatesMapEventStore()
    {
        var store = new RecordingMapEventStore();
        var sut = new PublishedContentLiveRefreshCoordinator(
            store,
            new ConnectionManager(),
            new ClientRegistry(),
            new NoopLiveRefreshSink(),
            NullLogger<PublishedContentLiveRefreshCoordinator>.Instance);

        await sut.RefreshConnectedSessionsAsync();

        Assert.Equal(1, store.InvalidateCount);
    }

    private sealed class NoopLiveRefreshSink : IPublishedContentLiveRefreshSink
    {
        public Task PushToSessionAsync(
            ClientSession client,
            Session session,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingMapEventStore : IMapEventStore
    {
        public int InvalidateCount { get; private set; }

        public bool TryGetEventsWireJson(int mapId, out string json)
        {
            _ = mapId;
            json = "[]";
            return true;
        }

        public bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> placements)
        {
            _ = mapId;
            placements = Array.Empty<MapEventWireEntry>();
            return true;
        }

        public Task<(bool Ok, IReadOnlyList<MapEventWireEntry> Placements)> GetPlacementsAsync(
            int mapId,
            CancellationToken cancellationToken = default)
        {
            _ = mapId;
            _ = cancellationToken;
            return Task.FromResult((true, (IReadOnlyList<MapEventWireEntry>)Array.Empty<MapEventWireEntry>()));
        }

        public void InvalidateAll() => InvalidateCount++;
    }
}
