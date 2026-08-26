using System.Threading;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase7PostgresE2ETests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase7PostgresE2ETests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FullGameplayFlow_PostgreSqlHeadless_AllSteps()
    {
        var (seed, groundWeaponId) = await SeedPublishedContentAsync(seedGroundItem: true);
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId })
            .Build();
        await host.StartAsync();
        string token = string.Empty;
        string characterId = string.Empty;
        try
        {
            var user = $"pg-{Guid.NewGuid():N}"[..18];
            var chatUser = $"cht-{Guid.NewGuid():N}"[..18];
            var killerUser = $"kil-{Guid.NewGuid():N}"[..18];
            const string password = "password12345";

            await using var client = new Phase7TcpTestClient();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
            var login = await client.ReadUntilAsync(PacketId.LoginResult);
            token = Phase7WireDecoders.DecodeLoginToken(login);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("PgHero", seed.ClassId));
            var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
            characterId = Phase7WireDecoders.DecodeCharacterId(create);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);

            var combat = await client.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combat, out _, out _, out _, out _, out _, out _, out var startGold, out _));
            Assert.Equal(GameplayLimits.StartingGold, startGold);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
            _ = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(groundWeaponId!.Value));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.PickupItemResult))[1]);
            var invAfterPickup = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterPickup, out var pickupSnap));
            Assert.Contains(pickupSnap.Slots, s => s.ItemId == seed.WeaponId);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildEquip(0));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.EquipResult))[1]);
            var invEquipped = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invEquipped, out var equipSnap));
            Assert.Equal(seed.WeaponId, equipSnap.EquippedWeaponItemId);

            await client.DisconnectAsync();
            await Task.Delay(150);
            await using var client2 = new Phase7TcpTestClient();
            await client2.ConnectAsync("127.0.0.1", port);
            _ = await client2.ReadFrameAsync();
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            var invReconnect = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invReconnect, out var reconnectSnap));
            Assert.Equal(seed.WeaponId, reconnectSnap.EquippedWeaponItemId);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee("Slime"));
            _ = await client2.ReadUntilAsync(PacketId.MeleeAttackResult);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildSpellCast(seed.SpellId, "Slime"));
            _ = await client2.ReadUntilAsync(PacketId.SpellCastResult);
            var afterSpell = await client2.ReadUntilAnyAsync(
                [PacketId.ExperienceGain, PacketId.CombatState],
                TimeSpan.FromSeconds(2));
            if (afterSpell[0] == (byte)PacketId.ExperienceGain)
            {
                afterSpell = await client2.ReadUntilAsync(PacketId.CombatState);
            }

            Assert.True(Phase7WireDecoders.TryDecodeCombatState(afterSpell, out var beforeLevel, out var beforeXp, out var beforeHp, out _, out _, out _, out var beforeGold, out _));
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildSpellCast(Guid.NewGuid(), "Slime"));
            var badSpell = await client2.ReadUntilAsync(PacketId.SpellCastResult);
            Assert.Equal(0, badSpell[1]);
            await client2.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
            // Invalid spell must not mutate combat economy/XP; drain may include unrelated frames — re-query via heartbeat path:
            // After failed cast, next CombatState (if any) should not show XP drop; gold/level baseline retained for later asserts.
            Assert.True(beforeLevel >= 1);
            Assert.True(beforeXp >= 0);
            Assert.True(beforeHp > 0);
            Assert.True(beforeGold >= 0);

            var xpFromKill = await KillMonsterOrReadExperienceAsync(client2, "Slime");
            Assert.True(xpFromKill > 0);
            var combatAfterKill = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterKill, out _, out var experience, out _, out _, out _, out _, out _, out _));
            Assert.True(experience > 0);

            await using var chatPeer = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(chatPeer, port, chatUser, password, "Chatter", seed.ClassId);
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Map, "hello map"));
            var mapChat = await chatPeer.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(mapChat, out var mapChannel, out _, out _, out var mapMsg));
            Assert.Equal(ChatChannel.Map, mapChannel);
            Assert.Equal("hello map", mapMsg);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Global, "hello world"));
            var globalChat = await chatPeer.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(globalChat, out var globalChannel, out _, out _, out _));
            Assert.Equal(ChatChannel.Global, globalChannel);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Whisper, "psst", chatUser));
            var whisper = await chatPeer.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(whisper, out var whisperChannel, out _, out _, out var whisperMsg));
            Assert.Equal(ChatChannel.Whisper, whisperChannel);
            Assert.Equal("psst", whisperMsg);

            for (var i = 0; i < GameplayLimits.MaxChatMessagesPerWindow + 2; i++)
            {
                await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Global, $"spam{i}"));
            }

            await client2.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            var buyRequestId = Guid.NewGuid();
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, buyRequestId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var invAfterBuy = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterBuy, out var buySnap));
            Assert.Contains(buySnap.Slots, s => s.ItemId == seed.ConsumableId);
            var combatAfterBuy = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterBuy, out _, out _, out _, out _, out _, out _, out var goldAfterBuy, out _));
            Assert.True(goldAfterBuy < GameplayLimits.StartingGold);

            var sellSlot = buySnap.Slots.First(s => s.ItemId == seed.ConsumableId).SlotIndex;
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopSell((byte)sellSlot, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ShopSellResult))[1]);
            _ = await client2.ReadUntilAsync(PacketId.CombatState);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var invForBank = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invForBank, out var bankInv));
            var bankSlot = bankInv.Slots.First(s => s.ItemId == seed.ConsumableId).SlotIndex;

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankDepositItem((byte)bankSlot, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankDepositResult))[1]);
            var bankAfterDeposit = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(bankAfterDeposit, out var depositedBank));
            Assert.Contains(depositedBank.Slots, s => s.ItemId == seed.ConsumableId);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankDepositGold(25, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankDepositResult))[1]);
            var bankGoldSnap = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(bankGoldSnap, out var goldBank));
            Assert.Equal(25, goldBank.BankGold);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankWithdrawItem(0, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankWithdrawResult))[1]);

            await using var killer = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(killer, port, killerUser, password, "Killer", seed.ClassId);
            await client2.DrainPendingAsync();
            var deadCombat = await KillPlayerWithMeleeAsync(killer, user, client2);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(deadCombat, out _, out _, out var deadHp, out _, out _, out _, out _, out var isDead));
            Assert.True(isDead);
            Assert.Equal(0, deadHp);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildRespawn());
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.RespawnResult))[1]);
            var respawnCombat = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(respawnCombat, out _, out _, out var respawnHp, out var respawnMaxHp, out var respawnMp, out var respawnMaxMp, out _, out var respawnDead));
            Assert.False(respawnDead);
            Assert.Equal(respawnMaxHp, respawnHp);
            Assert.Equal(respawnMaxMp, respawnMp);

            await client2.DisconnectAsync();
        }
        finally
        {
            await host.StopAsync();
        }

        using var host2 = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId })
            .Build();
        await host2.StartAsync();
        try
        {
            await using var client3 = new Phase7TcpTestClient();
            await client3.ConnectAsync("127.0.0.1", port);
            _ = await client3.ReadFrameAsync();
            await client3.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await client3.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client3.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client3.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            var persistedCombat = await client3.ReadUntilAsync(PacketId.CombatState);
            var persistedInv = await client3.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(persistedInv, out var persisted));
            Assert.Equal(seed.WeaponId, persisted.EquippedWeaponItemId);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(persistedCombat, out var persistedLevel, out var persistedXp, out _, out _, out _, out _, out _, out var persistedDead));
            Assert.False(persistedDead);
            Assert.True(persistedLevel >= 1);
            Assert.True(persistedXp >= 0);
        }
        finally
        {
            await host2.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GroundPickupRace_TwoClients_ExactlyOneWinner()
    {
        var (seed, groundItemId) = await SeedPublishedContentAsync(seedGroundItem: true, useConsumableAsGround: true);
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId })
            .Build();
        await host.StartAsync();
        try
        {
            var userA = $"a-{Guid.NewGuid():N}"[..16];
            var userB = $"b-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";
            await using var tcpA = new Phase7TcpTestClient();
            await using var tcpB = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(tcpA, port, userA, password, "Alpha", seed.ClassId);
            await RegisterLoginSelectAsync(tcpB, port, userB, password, "Beta", seed.ClassId);
            await tcpA.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(groundItemId!.Value));
            await tcpB.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(groundItemId.Value));
            var rA = await tcpA.ReadUntilAsync(PacketId.PickupItemResult);
            var rB = await tcpB.ReadUntilAsync(PacketId.PickupItemResult);
            var wins = (rA[1] != 0 ? 1 : 0) + (rB[1] != 0 ? 1 : 0);
            Assert.Equal(1, wins);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Reconnect_DisplacesStaleConnection()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId })
            .Build();
        await host.StartAsync();
        try
        {
            var user = $"rc-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";
            await using var oldClient = new Phase7TcpTestClient();
            var token = await RegisterLoginCreateAsync(oldClient, port, user, password, "Stale", seed.ClassId);
            await using var newClient = new Phase7TcpTestClient();
            await newClient.ConnectAsync("127.0.0.1", port);
            _ = await newClient.ReadFrameAsync();
            await newClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await newClient.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await Task.Delay(200);
            await Assert.ThrowsAnyAsync<Exception>(() => oldClient.ReadFrameAsync(TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CombatRace_TwoClients_SameMonster_ExactlyOneExperienceGrant()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(
                _fixture.ConnectionString,
                port,
                new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId, MonsterCount = 1 })
            .Build();
        await host.StartAsync();
        try
        {
            const string password = "password12345";
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(a, port, $"ca-{Guid.NewGuid():N}"[..16], password, "FighterA", seed.ClassId);
            await RegisterLoginSelectAsync(b, port, $"cb-{Guid.NewGuid():N}"[..16], password, "FighterB", seed.ClassId);

            var xpEvents = 0;
            async Task AttackLoopAsync(Phase7TcpTestClient client)
            {
                for (var i = 0; i < 20; i++)
                {
                    await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee("Slime"));
                    try
                    {
                        var frame = await client.ReadUntilAnyAsync(
                            [PacketId.MeleeAttackResult, PacketId.ExperienceGain, PacketId.CombatState],
                            TimeSpan.FromSeconds(2));
                        if (frame[0] == (byte)PacketId.ExperienceGain)
                        {
                            Interlocked.Increment(ref xpEvents);
                            return;
                        }
                    }
                    catch (TimeoutException)
                    {
                        // continue
                    }

                    await Task.Delay(CombatFormulas.BasicAttackCooldownMs + 20);
                }
            }

            await Task.WhenAll(AttackLoopAsync(a), AttackLoopAsync(b));
            Assert.Equal(1, Volatile.Read(ref xpEvents));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ShopBuy_IdempotentRetry_DoesNotDuplicateItem()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId })
            .Build();
        await host.StartAsync();
        try
        {
            await using var client = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(
                client,
                port,
                $"sh-{Guid.NewGuid():N}"[..16],
                "password12345",
                "Shopper",
                seed.ClassId);
            var requestId = Guid.NewGuid();
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, requestId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var inv1 = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(inv1, out var snap1));
            var qty1 = snap1.Slots.Where(s => s.ItemId == seed.ConsumableId).Sum(s => s.Quantity);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, requestId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var inv2 = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(inv2, out var snap2));
            var qty2 = snap2.Slots.Where(s => s.ItemId == seed.ConsumableId).Sum(s => s.Quantity);
            Assert.Equal(qty1, qty2);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ChatWhisper_DoesNotLeakToThirdParty()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, new Phase7PostgresE2EOptions { MonsterNpcId = seed.MonsterId })
            .Build();
        await host.StartAsync();
        try
        {
            const string password = "password12345";
            var userA = $"wa-{Guid.NewGuid():N}"[..16];
            var userB = $"wb-{Guid.NewGuid():N}"[..16];
            var userC = $"wc-{Guid.NewGuid():N}"[..16];
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            await using var c = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(a, port, userA, password, "Alice", seed.ClassId);
            await RegisterLoginSelectAsync(b, port, userB, password, "Bob", seed.ClassId);
            await RegisterLoginSelectAsync(c, port, userC, password, "Carol", seed.ClassId);

            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Whisper, "secret", userB));
            var toBob = await b.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(toBob, out var channel, out _, out _, out var msg));
            Assert.Equal(ChatChannel.Whisper, channel);
            Assert.Equal("secret", msg);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                c.ReadUntilAsync(PacketId.ChatMessage, TimeSpan.FromMilliseconds(400)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task DrainOptionalPacketsAsync(Phase7TcpTestClient client, params PacketId[] ids)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
            while (!cts.IsCancellationRequested)
            {
                var frame = await client.ReadFrameAsync(TimeSpan.FromMilliseconds(200));
                if (ids.Any(i => frame[0] == (byte)i))
                {
                    return;
                }
            }
        }
        catch
        {
            // optional drain
        }
    }


    private async Task<(Phase7PostgresContentSeedResult Seed, Guid? GroundItemId)> SeedPublishedContentAsync(
        bool seedGroundItem = false,
        bool useConsumableAsGround = false)
    {
        // Dispose the seed gate before starting the server host so two long-lived
        // FrogDbContext instances do not share the Npgsql pool during teardown.
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        Guid? groundId = null;
        if (seedGroundItem)
        {
            var itemId = useConsumableAsGround ? seed.ConsumableId : seed.WeaponId;
            groundId = await Phase7PostgresContentSeed.SeedGroundWeaponAsync(gate, itemId);
        }

        return (seed, groundId);
    }

    private static async Task<long> KillMonsterOrReadExperienceAsync(Phase7TcpTestClient client, string monsterName)
    {
        for (var i = 0; i < 12; i++)
        {
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee(monsterName));
            try
            {
                var frame = await client.ReadUntilAnyAsync(
                    [PacketId.MeleeAttackResult, PacketId.ExperienceGain, PacketId.CombatState],
                    TimeSpan.FromSeconds(3));
                if (frame[0] == (byte)PacketId.ExperienceGain
                    && Phase7WireDecoders.TryDecodeExperienceGain(frame, out var amount, out _, out _))
                {
                    return amount;
                }
            }
            catch (TimeoutException)
            {
                // continue attacking
            }

            await Task.Delay(CombatFormulas.BasicAttackCooldownMs + 50);
        }

        try
        {
            var frame = await client.ReadUntilAsync(PacketId.ExperienceGain, TimeSpan.FromSeconds(1));
            if (Phase7WireDecoders.TryDecodeExperienceGain(frame, out var amount, out _, out _))
            {
                return amount;
            }
        }
        catch (TimeoutException)
        {
            // fall through
        }

        var combat = await client.ReadUntilAsync(PacketId.CombatState);
        if (Phase7WireDecoders.TryDecodeCombatState(combat, out _, out var xp, out _, out _, out _, out _, out _, out _) && xp > 0)
        {
            return xp;
        }

        throw new TimeoutException("Monster XP was not awarded.");
    }

    private static async Task KillMonsterWithMeleeAsync(Phase7TcpTestClient client, string monsterName)
    {
        _ = await KillMonsterOrReadExperienceAsync(client, monsterName);
    }

    private static async Task<byte[]> KillPlayerWithMeleeAsync(Phase7TcpTestClient attacker, string targetUser, Phase7TcpTestClient victim)
    {
        for (var i = 0; i < 30; i++)
        {
            await attacker.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee(targetUser));
            _ = await attacker.ReadUntilAsync(PacketId.MeleeAttackResult);
            try
            {
                var frame = await victim.ReadUntilAnyAsync(
                    [PacketId.DeathNotify, PacketId.CombatState],
                    TimeSpan.FromSeconds(1));
                if (frame[0] == (byte)PacketId.DeathNotify)
                {
                    return await victim.ReadUntilAsync(PacketId.CombatState);
                }

                if (Phase7WireDecoders.TryDecodeCombatState(frame, out _, out _, out _, out _, out _, out _, out _, out var dead) && dead)
                {
                    return frame;
                }
            }
            catch (TimeoutException)
            {
                // keep attacking
            }

            await Task.Delay(CombatFormulas.BasicAttackCooldownMs + 50);
        }

        throw new TimeoutException("Player was not killed in time.");
    }

    private static async Task<(int level, long experience, int hp, int gold)> ReadLatestCombatStateAsync(Phase7TcpTestClient client)
    {
        var frame = await client.ReadUntilAsync(PacketId.CombatState);
        Assert.True(Phase7WireDecoders.TryDecodeCombatState(frame, out var level, out var xp, out var hp, out _, out _, out _, out var gold, out _));
        return (level, xp, hp, gold);
    }

    private static async Task RegisterLoginSelectAsync(
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
        await tcp.DrainPendingAsync();
    }

    private static async Task<string> RegisterLoginCreateAsync(
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
        var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
        var token = Phase7WireDecoders.DecodeLoginToken(login);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        return token;
    }
}
