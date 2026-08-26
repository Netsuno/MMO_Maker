using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

/// <summary>Gate E2E Phase 7 : auth, perso, inventaire, combat, shop, banque, chat, reconnexion.</summary>
public sealed class Phase7E2EGameplayTests
{
    [Fact]
    public async Task FullGameplayFlow_RegisterLoginCreateSelectFightShopReconnect()
    {
        var port = GetFreePort();
        using var host = FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                });
            })
            .Build();
        await host.StartAsync();
        try
        {
            var user = $"p7-{Guid.NewGuid():N}"[..20];
            const string password = "password123";
            await using var client = new Phase7TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

            await client.SendFrameAsync(BuildRegister(user, password));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);

            await client.SendFrameAsync(BuildLogin(user, password));
            var login = await client.ReadUntilAsync(PacketId.LoginResult);
            Assert.NotEqual(0, login[1]);
            var token = Encoding.UTF8.GetString(login, 3, login[2]);
            await client.DrainPendingAsync();

            await client.SendFrameAsync(BuildCharacterCreate("E2EHero", Phase7ContentSeed.DefaultClassId));
            var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
            Assert.NotEqual(0, create[1]);
            var characterId = Encoding.UTF8.GetString(create, 3, create[2]);

            await client.SendFrameAsync(BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            await client.DrainPendingAsync();

            await client.SendFrameAsync(BuildMapRequest());
            _ = await client.ReadUntilAnyAsync(new[] { PacketId.MapData, PacketId.MapAlreadySynced });

            var services = host.Services;
            var invSvc = services.GetRequiredService<InventoryGameplayService>();
            var combatSvc = services.GetRequiredService<CombatGameplayService>();
            var charGuid = Guid.Parse(characterId);
            await invSvc.TryAddItemAsync(charGuid, Phase7ContentSeed.DefaultWeaponId, 1);

            await client.SendFrameAsync(BuildEquip(0));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.EquipResult))[1]);
            _ = await client.ReadUntilAsync(PacketId.InventorySnapshot);

            await client.DisconnectAsync();
            await Task.Delay(100);
            await using var client2 = new Phase7TcpClient();
            await client2.ConnectAsync("127.0.0.1", port);
            _ = await client2.ReadFrameAsync();
            await client2.SendFrameAsync(BuildReconnect(token));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client2.SendFrameAsync(BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            await client2.DrainPendingAsync();

            var (spawnPx, spawnPy) = WorldMetrics.TileCenterToPixels(
                GameplayLimits.DefaultSpawnTileX,
                GameplayLimits.DefaultSpawnTileY);
            combatSvc.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, spawnPx, spawnPy);
            await client2.SendFrameAsync(BuildMelee("Slime"));
            var melee = await client2.ReadUntilAsync(PacketId.MeleeAttackResult);
            Assert.NotEqual(0, melee[1]);
            _ = await client2.ReadUntilAnyAsync(new[] { PacketId.ExperienceGain, PacketId.CombatState });

            await client2.SendFrameAsync(BuildSpellCast(Phase7ContentSeed.DefaultSpellId, "Slime"));
            _ = await client2.ReadUntilAsync(PacketId.SpellCastResult);

            await client2.SendFrameAsync(BuildSpellCast(Guid.NewGuid(), "Slime"));
            var badSpell = await client2.ReadUntilAsync(PacketId.SpellCastResult);
            Assert.Equal(0, badSpell[1]);

            await client2.SendFrameAsync(BuildChat(ChatChannel.Map, "hello map"));
            await client2.DrainPendingAsync(TimeSpan.FromMilliseconds(50));
            await client2.SendFrameAsync(BuildChat(ChatChannel.Global, "hello world"));
            await client2.DrainPendingAsync(TimeSpan.FromMilliseconds(50));

            sessionGold(host, charGuid, 500);
            await client2.DisconnectAsync();
            await using var clientShop = new Phase7TcpClient();
            await clientShop.ConnectAsync("127.0.0.1", port);
            _ = await clientShop.ReadFrameAsync();
            await clientShop.SendFrameAsync(BuildReconnect(token));
            Assert.NotEqual(0, (await clientShop.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await clientShop.SendFrameAsync(BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await clientShop.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            _ = await clientShop.ReadUntilAsync(PacketId.CombatState);

            await clientShop.SendFrameAsync(BuildShopBuy(Phase7ContentSeed.DefaultShopId, Phase7ContentSeed.DefaultItemId, 1));
            Assert.NotEqual(0, (await clientShop.ReadUntilAsync(PacketId.ShopBuyResult))[1]);

            await clientShop.SendFrameAsync(BuildBankDepositItem(0, 1));
            Assert.NotEqual(0, (await clientShop.ReadUntilAsync(PacketId.BankDepositResult))[1]);

            await clientShop.SendFrameAsync(BuildBankWithdrawItem(0, 1));
            Assert.NotEqual(0, (await clientShop.ReadUntilAsync(PacketId.BankWithdrawResult))[1]);

            await clientShop.DisconnectAsync();
            await Task.Delay(200);
            await using var client3 = new Phase7TcpClient();
            await client3.ConnectAsync("127.0.0.1", port);
            _ = await client3.ReadFrameAsync();
            await client3.SendFrameAsync(BuildReconnect(token));
            var reconnect3 = await client3.ReadUntilAsync(PacketId.ReconnectResult);
            Assert.True(reconnect3.Length >= 2 && reconnect3[1] != 0,
                "Reconnect after shop should succeed; payload=" + Encoding.UTF8.GetString(reconnect3));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task PickupRace_ExactlyOneWinner()
    {
        var port = GetFreePort();
        using var host = FrogServerHostFactory
            .CreateHostBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                });
            })
            .Build();
        await host.StartAsync();
        var actualPort = port;
        try
        {
            var userA = $"a-{Guid.NewGuid():N}"[..18];
            var userB = $"b-{Guid.NewGuid():N}"[..18];
            const string password = "password123";
            var charA = await RegisterLoginCreateSelect(host, actualPort, userA, password, "Alpha");
            var charB = await RegisterLoginCreateSelect(host, actualPort, userB, password, "Beta");

            var ground = host.Services.GetRequiredService<IGroundItemRepository>();
            var spawn = WorldMetrics.TileCenterToPixels(GameplayLimits.DefaultSpawnTileX, GameplayLimits.DefaultSpawnTileY);
            var dropped = await ground.DropAsync(
                1,
                spawn.PixelX,
                spawn.PixelY,
                Phase7ContentSeed.DefaultItemId,
                1,
                null);

            await using var tcpA = new Phase7TcpClient();
            await using var tcpB = new Phase7TcpClient();
            await tcpA.ConnectAsync("127.0.0.1", actualPort);
            await tcpB.ConnectAsync("127.0.0.1", actualPort);
            _ = await tcpA.ReadFrameAsync();
            _ = await tcpB.ReadFrameAsync();
            await tcpA.SendFrameAsync(BuildLogin(userA, password));
            await tcpB.SendFrameAsync(BuildLogin(userB, password));
            _ = await tcpA.ReadUntilAsync(PacketId.LoginResult);
            _ = await tcpB.ReadUntilAsync(PacketId.LoginResult);
            await tcpA.SendFrameAsync(BuildCharacterSelect(charA));
            await tcpB.SendFrameAsync(BuildCharacterSelect(charB));
            _ = await tcpA.ReadUntilAsync(PacketId.CharacterSelectResult);
            _ = await tcpB.ReadUntilAsync(PacketId.CharacterSelectResult);
            DrainUntil(tcpA, PacketId.InventorySnapshot);
            DrainUntil(tcpB, PacketId.InventorySnapshot);

            await tcpA.SendFrameAsync(BuildPickup(dropped.Item!.Id));
            await tcpB.SendFrameAsync(BuildPickup(dropped.Item!.Id));
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

    private static async Task<string> RegisterLoginCreateSelect(
        IHost host,
        int port,
        string user,
        string password,
        string charName)
    {
        await using var tcp = new Phase7TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(BuildLogin(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.LoginResult);
        await tcp.DrainPendingAsync();
        await tcp.SendFrameAsync(BuildCharacterCreate(charName, Phase7ContentSeed.DefaultClassId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Encoding.UTF8.GetString(create, 3, create[2]);
        await tcp.SendFrameAsync(BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        await tcp.DrainPendingAsync();
        return id;
    }

    private static void sessionGold(IHost host, Guid charGuid, int gold)
    {
        var chars = host.Services.GetRequiredService<ICharacterRepository>();
        var record = chars.FindByIdAsync(charGuid).GetAwaiter().GetResult()!;
        var patched = record with { Gold = gold };
        chars.SaveAsync(patched).GetAwaiter().GetResult();
    }


    private static void DrainUntil(Phase7TcpClient tcp, PacketId id)
    {
        tcp.ReadUntilAsync(id).GetAwaiter().GetResult();
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static byte[] BuildRegister(string user, string pass) => BuildLogin(user, pass, PacketId.RegisterRequest);

    private static byte[] BuildLogin(string user, string pass, PacketId id = PacketId.LoginRequest)
    {
        var u = Encoding.UTF8.GetBytes(user);
        var p = Encoding.UTF8.GetBytes(pass);
        var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
        payload[0] = (byte)id;
        payload[1] = (byte)u.Length;
        u.CopyTo(payload, 2);
        payload[2 + u.Length] = (byte)p.Length;
        p.CopyTo(payload, 3 + u.Length);
        return payload;
    }

    private static byte[] BuildReconnect(string token)
    {
        var t = Encoding.UTF8.GetBytes(token);
        var payload = new byte[1 + 2 + t.Length];
        payload[0] = (byte)PacketId.ReconnectRequest;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)t.Length);
        t.CopyTo(payload, 3);
        return payload;
    }

    private static byte[] BuildCharacterCreate(string name, Guid classId)
    {
        var n = Encoding.UTF8.GetBytes(name);
        var payload = new byte[1 + 1 + n.Length + 16];
        payload[0] = (byte)PacketId.CharacterCreateRequest;
        payload[1] = (byte)n.Length;
        n.CopyTo(payload, 2);
        classId.TryWriteBytes(payload.AsSpan(2 + n.Length));
        return payload;
    }

    private static byte[] BuildCharacterSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildMapRequest()
    {
        return [(byte)PacketId.MapRequest];
    }

    private static byte[] BuildEquip(byte slot)
    {
        return [(byte)PacketId.EquipRequest, slot];
    }

    private static byte[] BuildMelee(string target)
    {
        var t = Encoding.UTF8.GetBytes(target);
        var payload = new byte[1 + 1 + t.Length];
        payload[0] = (byte)PacketId.MeleeAttackRequest;
        payload[1] = (byte)t.Length;
        t.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildSpellCast(Guid spellId, string target)
    {
        var t = Encoding.UTF8.GetBytes(target);
        var payload = new byte[1 + 16 + 1 + t.Length];
        payload[0] = (byte)PacketId.SpellCastRequest;
        spellId.TryWriteBytes(payload.AsSpan(1));
        payload[17] = (byte)t.Length;
        t.CopyTo(payload, 18);
        return payload;
    }

    private static byte[] BuildChat(ChatChannel channel, string message)
    {
        var m = Encoding.UTF8.GetBytes(message);
        var payload = new byte[1 + 1 + 1 + sizeof(ushort) + m.Length];
        payload[0] = (byte)PacketId.ChatSend;
        payload[1] = (byte)channel;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3), (ushort)m.Length);
        m.CopyTo(payload, 5);
        return payload;
    }

    private static byte[] BuildShopBuy(Guid shopId, Guid itemId, int qty)
    {
        var payload = new byte[1 + 16 + 16 + 4];
        payload[0] = (byte)PacketId.ShopBuyRequest;
        shopId.TryWriteBytes(payload.AsSpan(1));
        itemId.TryWriteBytes(payload.AsSpan(17));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(33), qty);
        return payload;
    }

    private static byte[] BuildBankDepositItem(byte slot, int qty)
    {
        var payload = new byte[1 + 1 + 4];
        payload[0] = (byte)PacketId.BankDepositRequest;
        payload[1] = slot;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), qty);
        return payload;
    }

    private static byte[] BuildBankWithdrawItem(byte slot, int qty)
    {
        var payload = new byte[1 + 1 + 4];
        payload[0] = (byte)PacketId.BankWithdrawRequest;
        payload[1] = slot;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), qty);
        return payload;
    }

    private static byte[] BuildPickup(Guid groundId)
    {
        var payload = new byte[1 + 16];
        payload[0] = (byte)PacketId.PickupItemRequest;
        groundId.TryWriteBytes(payload.AsSpan(1));
        return payload;
    }

    internal sealed class Phase7TcpClient : IAsyncDisposable
    {
        private TcpClient? _tcp;
        private NetworkStream? _stream;

        public async Task ConnectAsync(string host, int port)
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(host, port);
            _stream = _tcp.GetStream();
        }

        public async Task SendFrameAsync(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame);
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task DrainPendingAsync(TimeSpan? budget = null)
        {
            var deadline = DateTime.UtcNow + (budget ?? TimeSpan.FromMilliseconds(300));
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    using var cts = new CancellationTokenSource(remaining);
                    var lenBuf = new byte[4];
                    await ReadExactAsync(lenBuf, cts.Token);
                    var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                    var payload = new byte[len];
                    await ReadExactAsync(payload, cts.Token);
                }
                catch
                {
                    break;
                }
            }
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
            => await ReadUntilAnyAsync(new[] { id }, timeout);

        public async Task<byte[]> ReadUntilAnyAsync(PacketId[] ids, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
            while (DateTime.UtcNow < deadline)
            {
                var frame = await ReadFrameAsync(deadline - DateTime.UtcNow);
                if (ids.Any(i => frame[0] == (byte)i))
                {
                    return frame;
                }
            }

            throw new TimeoutException($"expected packet not received: {string.Join(',', ids)}");
        }

        public Task DisconnectAsync()
        {
            _tcp?.Close();
            return Task.CompletedTask;
        }

        private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                read += n;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            _stream?.Dispose();
            _tcp?.Dispose();
        }
    }
}
