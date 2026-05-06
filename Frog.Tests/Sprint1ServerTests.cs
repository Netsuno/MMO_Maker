using System;
using System.IO;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Server.Database;
using Frog.Server.Network;
using Frog.Server.Persistence;
using Frog.Server.Services;
using Xunit;

namespace Frog.Tests;

public sealed class Sprint1ServerTests
{
    [Fact]
    public void AuthService_ValidatesBootstrapAccount()
    {
        var repository = new AccountRepository();
        var auth = new AuthService(repository);

        Assert.True(auth.ValidateCredentials("demo", "demo"));
        Assert.False(auth.ValidateCredentials("demo", "wrong"));
    }

    [Fact]
    public void AccountRepository_CanCreateAndReadAccount()
    {
        var repository = new AccountRepository();
        var created = repository.Create("new-user", "p@ssword");
        var found = repository.TryGetByUsername("new-user", out var account);

        Assert.True(created);
        Assert.True(found);
        Assert.Equal("new-user", account.Username);
    }

    [Fact]
    public void AuthService_CanRegisterNewAccount()
    {
        var repository = new AccountRepository();
        var auth = new AuthService(repository);

        var created = auth.RegisterAccount("fresh-user", "fresh-pass");
        var authenticated = auth.ValidateCredentials("fresh-user", "fresh-pass");

        Assert.True(created);
        Assert.True(authenticated);
    }

    [Fact]
    public void ConnectionManager_RejectsDoubleLogin()
    {
        var manager = new ConnectionManager();

        var firstCreated = manager.TryCreateSession("demo", out var firstSession);
        var secondCreated = manager.TryCreateSession("demo", out var secondSession);

        Assert.True(firstCreated);
        Assert.NotNull(firstSession);
        Assert.False(secondCreated);
        Assert.Null(secondSession);
    }

    [Fact]
    public void MapService_ReturnsSerializableMapPayload()
    {
        var mapService = MapTestHelpers.CreateMapService();
        var bytes = mapService.GetSerializedMapForSession(Guid.NewGuid());

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 8);
    }

    [Fact]
    public void PacketDispatcher_ParsesLoginPayload()
    {
        var username = "demo";
        var password = "demo";
        var userBytes = Encoding.UTF8.GetBytes(username);
        var passBytes = Encoding.UTF8.GetBytes(password);
        var payload = new byte[1 + userBytes.Length + 1 + passBytes.Length];
        payload[0] = (byte)userBytes.Length;
        userBytes.CopyTo(payload, 1);
        payload[1 + userBytes.Length] = (byte)passBytes.Length;
        passBytes.CopyTo(payload, 1 + userBytes.Length + 1);

        var parsed = PacketDispatcher.TryParseLoginPayload(payload, out var parsedUser, out var parsedPassword);

        Assert.True(parsed);
        Assert.Equal(username, parsedUser);
        Assert.Equal(password, parsedPassword);
    }

    [Fact]
    public void ConnectionManager_RemovesExpiredSessions()
    {
        var manager = new ConnectionManager();
        var created = manager.TryCreateSession("stale-user", out var session);
        Assert.True(created);
        Assert.NotNull(session);

        session!.LastActivityUtc = DateTime.UtcNow.AddMinutes(-10);
        var removed = manager.RemoveExpiredSessions(TimeSpan.FromMinutes(5));
        var recreated = manager.TryCreateSession("stale-user", out _);

        Assert.Single(removed);
        Assert.True(recreated);
    }

    [Fact]
    public void MovementService_AppliesValidMove()
    {
        var mapService = MapTestHelpers.CreateMapService();
        var movement = new MovementService(mapService, new ConnectionManager());
        var session = new Frog.Server.Models.Session
        {
            Id = Guid.NewGuid(),
            Username = "mover",
            PositionX = 0,
            PositionY = 0
        };

        var moved = movement.TryApplyMove(session, 1, 0, out var error);

        Assert.True(moved);
        Assert.Equal(string.Empty, error);
        Assert.Equal(1, session.PositionX);
        Assert.Equal(0, session.PositionY);
    }

    [Fact]
    public void MovementService_RejectsOutOfBoundsMove()
    {
        var mapService = MapTestHelpers.CreateMapService();
        var movement = new MovementService(mapService, new ConnectionManager());
        var session = new Frog.Server.Models.Session
        {
            Id = Guid.NewGuid(),
            Username = "edge",
            PositionX = 0,
            PositionY = 0
        };

        var moved = movement.TryApplyMove(session, -1, 0, out var error);

        Assert.False(moved);
        Assert.Equal("Mouvement hors limites.", error);
        Assert.Equal(0, session.PositionX);
        Assert.Equal(0, session.PositionY);
    }

    [Fact]
    public void MovementService_RejectsBlockedTileMove()
    {
        var mapService = MapTestHelpers.CreateMapService();
        var movement = new MovementService(mapService, new ConnectionManager());
        var session = new Frog.Server.Models.Session
        {
            Id = Guid.NewGuid(),
            Username = "collider",
            PositionX = 4,
            PositionY = 5
        };

        var moved = movement.TryApplyMove(session, 1, 0, out var error);

        Assert.False(moved);
        Assert.Equal("Mouvement bloque par collision.", error);
        Assert.Equal(4, session.PositionX);
        Assert.Equal(5, session.PositionY);
    }

    [Fact]
    public void MapService_ReportsBlockedTiles()
    {
        var mapService = MapTestHelpers.CreateMapService();

        Assert.True(mapService.IsBlocked(MapService.DefaultWorldMapId, 5, 5));
        Assert.False(mapService.IsBlocked(MapService.DefaultWorldMapId, 1, 1));
    }

    [Fact]
    public void PacketDispatcher_ParsesChatSendPayload_Global()
    {
        var message = "salut";
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var payload = new byte[1 + sizeof(ushort) + msgBytes.Length];
        payload[0] = (byte)ChatChannel.Global;
        BitConverter.GetBytes((ushort)msgBytes.Length).CopyTo(payload, 1);
        msgBytes.CopyTo(payload, 1 + sizeof(ushort));

        var ok = PacketDispatcher.TryParseChatSendPayload(payload, out var ch, out var target, out var parsed);
        Assert.True(ok);
        Assert.Equal(ChatChannel.Global, ch);
        Assert.Equal(string.Empty, target);
        Assert.Equal(message, parsed);
    }

    [Fact]
    public void PacketDispatcher_ParsesChatSendPayload_Whisper()
    {
        var target = "bob";
        var message = "psst";
        var tBytes = Encoding.UTF8.GetBytes(target);
        var mBytes = Encoding.UTF8.GetBytes(message);
        var payload = new byte[1 + 1 + tBytes.Length + sizeof(ushort) + mBytes.Length];
        var o = 0;
        payload[o++] = (byte)ChatChannel.Whisper;
        payload[o++] = (byte)tBytes.Length;
        tBytes.CopyTo(payload.AsSpan(o));
        o += tBytes.Length;
        BitConverter.GetBytes((ushort)mBytes.Length).CopyTo(payload.AsSpan(o));
        o += sizeof(ushort);
        mBytes.CopyTo(payload.AsSpan(o));

        var ok = PacketDispatcher.TryParseChatSendPayload(payload, out var ch, out var whisperTo, out var parsed);
        Assert.True(ok);
        Assert.Equal(ChatChannel.Whisper, ch);
        Assert.Equal(target, whisperTo);
        Assert.Equal(message, parsed);
    }

    [Fact]
    public void InMemoryPlayerStateStore_Roundtrip()
    {
        var store = new InMemoryPlayerStateStore();
        store.UpsertForCharacter("char-uuid-a", 1, 3, 4);
        Assert.True(store.TryGetForCharacter("char-uuid-a", out var st));
        Assert.Equal(1, st.MapId);
        Assert.Equal(3, st.X);
        Assert.Equal(4, st.Y);
        Assert.Equal("char-uuid-a", st.CharacterId);

        store.UpsertForCharacter("char-uuid-a", 2, 5, 6);
        Assert.True(store.TryGetForCharacter("char-uuid-a", out st));
        Assert.Equal(2, st.MapId);
        Assert.Equal(5, st.X);
        Assert.Equal(6, st.Y);
    }

    [Fact]
    public void InMemoryCharacterBootstrap_ListAndOwnsHero()
    {
        var b = new InMemoryCharacterBootstrap();
        var id = b.EnsureDefaultHero("zoe");
        var list = b.ListCharacters("zoe");
        Assert.Single(list);
        Assert.Equal(id, list[0].Id);
        Assert.Equal("Hero", list[0].DisplayName);
        Assert.True(b.IsCharacterOwned("zoe", id));
        Assert.False(b.IsCharacterOwned("zoe", "not-a-real-uuid"));
        Assert.False(b.IsCharacterOwned("other", id));
    }

    [Fact]
    public void InMemoryCharacterBootstrap_TryCreateCharacter_adds_slot()
    {
        var b = new InMemoryCharacterBootstrap();
        _ = b.EnsureDefaultHero("multi");
        Assert.True(b.TryCreateCharacter("multi", "Rogue", out var rid, out var err));
        Assert.NotEmpty(rid);
        Assert.Empty(err);
        var list = b.ListCharacters("multi");
        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.DisplayName == "Hero");
        Assert.Contains(list, x => x.Id == rid && x.DisplayName == "Rogue");
        Assert.False(b.TryCreateCharacter("multi", "Rogue", out _, out var dup));
        Assert.Contains("deja", dup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PacketDispatcher_TryParseCharacterSelectRequest_Roundtrip()
    {
        var id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        var idBytes = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + idBytes.Length];
        payload[0] = (byte)idBytes.Length;
        idBytes.CopyTo(payload.AsSpan(1));
        Assert.True(PacketDispatcher.TryParseCharacterSelectRequest(payload, out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(PacketDispatcher.TryParseCharacterSelectRequest(ReadOnlySpan<byte>.Empty, out _));
        payload[0] = (byte)(idBytes.Length + 1);
        Assert.False(PacketDispatcher.TryParseCharacterSelectRequest(payload, out _));
        var tooLong = new byte[2 + ChatProtocolLimits.MaxUsernameUtf8Bytes];
        tooLong[0] = (byte)(ChatProtocolLimits.MaxUsernameUtf8Bytes + 1);
        Assert.False(PacketDispatcher.TryParseCharacterSelectRequest(tooLong, out _));
    }

    [Fact]
    public void MapService_IndexWarpAtDemoCell()
    {
        var mapService = MapTestHelpers.CreateMapService();
        Assert.True(mapService.TryGetWarpDestination(MapService.DefaultWorldMapId, 3, 3, out var mapId, out var x, out var y));
        Assert.Equal(MapService.DefaultWorldMapId, mapId);
        Assert.Equal(18, x);
        Assert.Equal(18, y);
    }

    [Fact]
    public void MovementService_WarpsAcrossMaps_WhenSecondaryBlobExists()
    {
        var serializer = new MapSerializer();
        var interior = new Map { Width = 22, Height = 22, Name = "Interior" };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < interior.Height; y++)
        {
            for (var x = 0; x < interior.Width; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = 0,
                    SrcY = 0,
                    Type = TileType.Ground
                });
            }
        }

        interior.Layers.Add(ground);

        var store = new MemoryMapBlobStore();
        store.Seed(42, serializer.Serialize(interior), revision: 7);

        var outdoor = MapSamples.StarterMeadow(42);
        var tmp = Path.Combine(Path.GetTempPath(), $"frog-outdoor-{Guid.NewGuid():N}.fmap");
        File.WriteAllBytes(tmp, serializer.Serialize(outdoor));

        try
        {
            var mapService = MapTestHelpers.CreateMapService(tmp, store);
            var connections = new ConnectionManager();
            Assert.True(connections.TryCreateSession("warp-x", out var session));
            session!.CurrentMapId = MapService.DefaultWorldMapId;
            session.PositionX = 2;
            session.PositionY = 3;
            var movement = new MovementService(mapService, connections);
            Assert.True(movement.TryApplyMove(session, 1, 0, out _));
            Assert.True(movement.TryApplyWarpAfterMove(session));
            Assert.Equal(42, session.CurrentMapId);
            Assert.Equal(18, session.PositionX);
            Assert.Equal(18, session.PositionY);
        }
        finally
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void MapService_TryMatchMapFingerprint_IsScopedToMapId()
    {
        var serializer = new MapSerializer();
        var interior = new Map { Width = 8, Height = 8, Name = "Interior" };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < interior.Height; y++)
        {
            for (var x = 0; x < interior.Width; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = 0,
                    SrcY = 0,
                    Type = TileType.Ground
                });
            }
        }

        interior.Layers.Add(ground);

        var store = new MemoryMapBlobStore();
        store.Seed(42, serializer.Serialize(interior), revision: 11);

        var outdoor = MapSamples.StarterMeadow(42);
        var tmp = Path.Combine(Path.GetTempPath(), $"frog-fp-test-{Guid.NewGuid():N}.fmap");
        File.WriteAllBytes(tmp, serializer.Serialize(outdoor));

        try
        {
            var mapService = MapTestHelpers.CreateMapService(tmp, store);
            Assert.True(mapService.TryEnsureMapLoaded(MapService.DefaultWorldMapId));
            Assert.True(mapService.TryEnsureMapLoaded(42));

            var revWorld = mapService.GetFingerprintRevision(MapService.DefaultWorldMapId);
            var shaWorld = mapService.GetFingerprintSha256(MapService.DefaultWorldMapId).ToArray();
            var rev42 = mapService.GetFingerprintRevision(42);
            var sha42 = mapService.GetFingerprintSha256(42).ToArray();

            Assert.True(mapService.TryMatchMapFingerprint(MapService.DefaultWorldMapId, revWorld, shaWorld));
            Assert.True(mapService.TryMatchMapFingerprint(42, rev42, sha42));
            Assert.False(mapService.TryMatchMapFingerprint(42, revWorld, shaWorld));
            Assert.False(mapService.TryMatchMapFingerprint(MapService.DefaultWorldMapId, rev42, sha42));
        }
        finally
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void MovementService_AppliesWarpAfterSteppingOnWarpTile()
    {
        var mapService = MapTestHelpers.CreateMapService();
        var connections = new ConnectionManager();
        Assert.True(connections.TryCreateSession("walker", out var session));
        session!.PositionX = 2;
        session.PositionY = 3;
        session.CurrentMapId = MapService.DefaultWorldMapId;

        var movement = new MovementService(mapService, connections);
        Assert.True(movement.TryApplyMove(session, 1, 0, out _));
        Assert.Equal(3, session.PositionX);
        Assert.Equal(3, session.PositionY);

        var warped = movement.TryApplyWarpAfterMove(session);
        Assert.True(warped);
        Assert.Equal(18, session.PositionX);
        Assert.Equal(18, session.PositionY);
    }

    [Fact]
    public void MovementService_AllowsMoveOntoOtherPlayerWhenMapFlagEnabled()
    {
        var map = MapSamples.StarterMeadow(MapService.DefaultWorldMapId);
        map.AllowPlayerOverlap = true;
        var mapService = MapTestHelpers.CreateMapServiceFromMap(map);
        var connections = new ConnectionManager();
        Assert.True(connections.TryCreateSession("p1", out var s1));
        Assert.True(connections.TryCreateSession("p2", out var s2));
        s1!.PositionX = 1;
        s1.PositionY = 1;
        s2!.PositionX = 2;
        s2.PositionY = 1;

        var movement = new MovementService(mapService, connections);
        var moved = movement.TryApplyMove(s2, -1, 0, out var error);

        Assert.True(moved);
        Assert.Equal(string.Empty, error);
        Assert.Equal(1, s2.PositionX);
        Assert.Equal(1, s2.PositionY);
    }

    [Fact]
    public void MovementService_RejectsMoveOntoOtherPlayer()
    {
        var mapService = MapTestHelpers.CreateMapService();
        var connections = new ConnectionManager();
        Assert.True(connections.TryCreateSession("p1", out var s1));
        Assert.True(connections.TryCreateSession("p2", out var s2));
        s1!.PositionX = 1;
        s1.PositionY = 1;
        s2!.PositionX = 2;
        s2.PositionY = 1;

        var movement = new MovementService(mapService, connections);
        var moved = movement.TryApplyMove(s2, -1, 0, out var error);

        Assert.False(moved);
        Assert.Equal("Case occupee par un autre joueur.", error);
        Assert.Equal(2, s2.PositionX);
    }

    [Fact]
    public void PacketDispatcher_ParsesMovePayload()
    {
        var payload = new byte[] { unchecked((byte)(sbyte)-1), unchecked((byte)(sbyte)1) };

        var parsed = PacketDispatcher.TryParseMovePayload(payload, out var dx, out var dy);

        Assert.True(parsed);
        Assert.Equal(-1, dx);
        Assert.Equal(1, dy);
    }
}
