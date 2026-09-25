using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Net.Sockets;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class GroundLootTests
{
    [Fact]
    public void Protocol_Stays11_GroundSnapshotLayoutUnchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(47, (byte)PacketId.GroundItemsSnapshot);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(180, GameplayLimits.GroundItemTimeToLiveSeconds);

        var itemId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000001");
        var groundId = Guid.Parse("bbbbbbbb-0003-4000-8000-000000000009");
        var body = Phase7PacketCodec.BuildGroundItemsSnapshotBody(
            1,
            [
                new GroundItemWire
                {
                    GroundItemId = groundId,
                    ItemId = itemId,
                    Quantity = 2,
                    PixelX = 80,
                    PixelY = 48,
                },
            ]);
        Assert.Equal(4 + 2 + 44, body.Length);
        Assert.True(Phase7PacketCodec.TryParseGroundItemsSnapshot(body, out var parsed));
        Assert.Equal(1, parsed.MapId);
        var only = Assert.Single(parsed.Items);
        Assert.Equal(groundId, only.GroundItemId);
        Assert.Equal(itemId, only.ItemId);
        Assert.Equal(2, only.Quantity);
        Assert.Equal(80, only.PixelX);
        Assert.Equal(48, only.PixelY);
    }

    [Fact]
    public void Placement_UsesDeathTileThenAdjacentFreeTile()
    {
        var origin = WorldMetrics.TileCenterToPixels(2, 2);
        var spots = GroundLootPlacement.ChooseDropPixels(origin.PixelX, origin.PixelY, 2);
        Assert.Equal(new[] { (80, 80), (112, 80) }, spots.Select(s => (s.PixelX, s.PixelY)).ToArray());

        var blockedOrigin = GroundLootPlacement.ChooseDropPixels(
            origin.PixelX,
            origin.PixelY,
            2,
            (tx, ty) => !(tx == 2 && ty == 2));
        Assert.Equal(new[] { (112, 80), (80, 112) }, blockedOrigin.Select(s => (s.PixelX, s.PixelY)).ToArray());
    }

    [Fact]
    public async Task MonsterKill_DropsStacks_PickupMovesThemIntoInventory()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var ground = new InMemoryGroundItemRepository();
        var equip = new InMemoryEquipmentRepository();
        var inventory = CreateInventory(invRepo, equip, ground, content);
        var loot = CreateLoot(ground, content, inventory, blocked: null);
        var combat = new CombatGameplayService(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(chars, content, invRepo),
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(chars),
            groundLoot: loot);

        var created = await Phase7TestHelpers.CreateCharacterService(chars, content, invRepo)
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "hunter" };
        session.ApplyFromCharacter(created.Character!);
        session.CurrentMapId = 1;
        session.PixelX = 64;
        session.PixelY = 64;
        var spawned = combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 80, 80);
        Assert.NotNull(spawned);

        MeleeCombatResult? last = null;
        for (var i = 0; i < 8; i++)
        {
            last = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            if (last.MonsterKilled)
            {
                break;
            }

            session.LastMeleeUtc = DateTime.MinValue;
        }

        Assert.NotNull(last);
        Assert.True(last!.MonsterKilled);
        Assert.Equal(2, last.DroppedLoot.Count);
        Assert.Contains(last.DroppedLoot, item => item.PixelX == 80 && item.PixelY == 80);
        Assert.Contains(last.DroppedLoot, item => item.PixelX == 112 && item.PixelY == 80);
        Assert.All(last.DroppedLoot, item => Assert.Equal(Phase7ContentSeed.DefaultItemId, item.ItemId));

        var onDeathTile = await loot.PickupOnCurrentTileAsync(session);
        var taken = Assert.Single(onDeathTile, pick => pick.Success);
        Assert.Equal(Phase7ContentSeed.DefaultItemId, taken.ItemId);
        var bag = await inventory.GetInventoryAsync(session.RequireCharacterGuid());
        Assert.Contains(bag.Slots, slot => slot.ItemId == Phase7ContentSeed.DefaultItemId && slot.Quantity == 1);

        var remaining = await ground.ListOnMapAsync(1);
        var left = Assert.Single(remaining);
        Assert.Equal(112, left.PixelX);

        session.PixelX = 400;
        session.PixelY = 400;
        var tooFar = await inventory.TryPickupAsync(session, left.Id);
        Assert.False(tooFar.Success);

        session.PixelX = 112;
        session.PixelY = 80;
        var walked = await loot.PickupOnCurrentTileAsync(session);
        Assert.Contains(walked, pick => pick.Success);
        Assert.Empty(await ground.ListOnMapAsync(1));
    }

    [Fact]
    public async Task PlayerDeath_DropsConfiguredStack_NotInventory()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var ground = new InMemoryGroundItemRepository();
        var equip = new InMemoryEquipmentRepository();
        var inventory = CreateInventory(invRepo, equip, ground, content);
        var loot = CreateLoot(ground, content, inventory, blocked: null);
        var combat = new CombatGameplayService(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(chars, content, invRepo),
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(chars),
            groundLoot: loot);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, invRepo);
        var attackerChar = (await charSvc.CreateAsync(Guid.NewGuid(), "A", Phase7ContentSeed.DefaultClassId)).Character!;
        var victimChar = (await charSvc.CreateAsync(Guid.NewGuid(), "V", Phase7ContentSeed.DefaultClassId)).Character!;
        await invRepo.TryAddAsync(victimChar.Id, Phase7ContentSeed.DefaultWeaponId, 1, 1);
        var attacker = new Session { Id = Guid.NewGuid(), Username = "a" };
        attacker.ApplyFromCharacter(attackerChar);
        attacker.CurrentMapId = 1;
        attacker.PixelX = 64;
        attacker.PixelY = 64;
        var victim = new Session { Id = Guid.NewGuid(), Username = "v" };
        victim.ApplyFromCharacter(victimChar);
        victim.CurrentMapId = 1;
        victim.PixelX = 64;
        victim.PixelY = 64;

        PlayerMeleeCombatResult? hit = null;
        for (var i = 0; i < 20; i++)
        {
            hit = await combat.TryMeleeAttackPlayerAsync(attacker, victim);
            if (hit.TargetKilled)
            {
                break;
            }

            attacker.LastMeleeUtc = DateTime.MinValue;
        }

        Assert.NotNull(hit);
        Assert.True(hit!.TargetKilled);
        var dropped = Assert.Single(hit.DroppedLoot);
        Assert.Equal(Phase7ContentSeed.DefaultItemId, dropped.ItemId);
        var victimBag = await inventory.GetInventoryAsync(victimChar.Id);
        Assert.Contains(victimBag.Slots, slot => slot.ItemId == Phase7ContentSeed.DefaultWeaponId);
    }

    [Fact]
    public async Task ExpiredGroundItem_IsHiddenAndNotPickupable()
    {
        var clock = new ManualClock();
        var ground = new InMemoryGroundItemRepository(clock);
        var dropped = await ground.DropAsync(1, 80, 80, Phase7ContentSeed.DefaultItemId, 1, null);
        Assert.Equal(GroundItemMutationStatus.Ok, dropped.Status);
        clock.UtcNow += GroundLootLifetime.TimeToLive;

        Assert.Empty(await ground.ListOnMapAsync(1));
        var pickup = await ground.TryPickupAsync(dropped.Item!.Id, Guid.NewGuid(), 80, 80, GameplayLimits.GroundPickupRangePixels);
        Assert.Equal(GroundItemMutationStatus.NotFound, pickup.Status);
        Assert.Equal(0, await ground.PurgeExpiredAsync(1));
    }

    [Fact]
    public async Task InteractRange_PicksNearestStack()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var ground = new InMemoryGroundItemRepository();
        var inventory = CreateInventory(invRepo, new InMemoryEquipmentRepository(), ground, content);
        var loot = CreateLoot(ground, content, inventory, (map, x, y) => x == 4 && y == 4);
        var dropped = await loot.DropMonsterLootAsync(1, 128, 128, Guid.NewGuid());
        Assert.Equal(2, dropped.Count);
        Assert.All(dropped, item =>
        {
            var tile = GroundLootPlacement.PixelToTile(item.PixelX, item.PixelY);
            Assert.False(tile.X == 4 && tile.Y == 4);
        });

        var created = await Phase7TestHelpers.CreateCharacterService(chars, content, invRepo)
            .CreateAsync(Guid.NewGuid(), "Near", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "near", CurrentMapId = 1 };
        session.ApplyFromCharacter(created.Character!);
        session.CurrentMapId = 1;
        session.PixelX = dropped[0].PixelX;
        session.PixelY = dropped[0].PixelY;
        var nearest = await loot.TryPickupNearestInRangeAsync(session);
        Assert.NotNull(nearest);
        Assert.True(nearest!.Success);
        Assert.Equal(Phase7ContentSeed.DefaultItemId, nearest.ItemId);
    }

    [Fact]
    public void Host_ResolvesGroundLoot_WithoutHelloBump()
    {
        var port = FreePort();
        using var host = FrogServerHostFactory
            .CreateHostBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();

        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.NotNull(host.Services.GetRequiredService<GroundLootService>());
        Assert.NotNull(host.Services.GetRequiredService<GroundItemObserverNotifier>());
        var combat = host.Services.GetRequiredService<CombatGameplayService>();
        var lootField = typeof(CombatGameplayService).GetField("_groundLoot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(lootField);
        Assert.NotNull(lootField!.GetValue(combat));
        Assert.NotNull(host.Services.GetRequiredService<PacketDispatcher>());
    }

    private static InventoryGameplayService CreateInventory(
        InMemoryInventoryRepository invRepo,
        InMemoryEquipmentRepository equip,
        InMemoryGroundItemRepository ground,
        Phase7PublishedContent content)
    {
        var transfers = new InMemoryInventoryTransferRepository(invRepo, equip, ground, content);
        return new InventoryGameplayService(invRepo, transfers, ground, content, equip);
    }

    private static GroundLootService CreateLoot(
        InMemoryGroundItemRepository ground,
        Phase7PublishedContent content,
        InventoryGameplayService inventory,
        Func<int, int, int, bool>? blocked)
    {
        return new GroundLootService(
            ground,
            content,
            inventory,
            GroundLootTable.CreateDefault(),
            blocked ?? ((_, _, _) => false),
            _ => (false, 0, 0));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
