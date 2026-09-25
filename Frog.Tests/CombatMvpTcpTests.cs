using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Combat;
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

public sealed class CombatMvpTcpTests
{
    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_DummyMelee_AppliesDamage_RateLimits_AndBroadcasts()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var attacker = new TcpProbe();
            await using var observer = new TcpProbe();
            _ = await RegisterLoginSelectAsync(attacker, port, UniqueUser("ca"), password, "CaHero");
            _ = await RegisterLoginSelectAsync(observer, port, UniqueUser("co"), password, "CoHero");

            var reqId = Guid.NewGuid();
            _ = reqId;
            await attacker.SendFrameAsync(BuildMelee(CombatMvpLimits.DummyName, CombatTargetKind.Dummy, Direction.Down));
            var result = DecodeMelee(await attacker.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.True(result.hit);
            Assert.Equal(CombatMvpLimits.DummyName, result.target);
            Assert.Equal("Touche.", result.message);
            Assert.True(result.damage.HasValue);
            Assert.True(result.damage!.Value.Hit);
            Assert.Equal(CombatTargetKind.Dummy, result.damage.Value.TargetKind);
            Assert.True(result.damage.Value.Damage >= 1);
            Assert.Equal(
                CombatMvpLimits.DummyMaxHp - result.damage.Value.Damage,
                result.damage.Value.RemainingHp);

            var observed = DecodeMelee(await observer.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.True(observed.damage.HasValue);
            Assert.Equal(result.damage.Value.Damage, observed.damage!.Value.Damage);

            await attacker.SendFrameAsync(BuildMelee(CombatMvpLimits.DummyName, CombatTargetKind.Dummy, Direction.Down));
            var recharge = DecodeMelee(await attacker.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.False(recharge.hit);
            Assert.Equal("Attaque en recharge.", recharge.message);
            Assert.False(recharge.damage.HasValue);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_RangedDummy_SetsRangedFlagOnExistingTrailer()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            await using var client = new TcpProbe();
            _ = await RegisterLoginSelectAsync(client, port, UniqueUser("cr"), "password123", "CrHero");
            await client.SendFrameAsync(BuildMelee(
                CombatMvpLimits.DummyName,
                CombatTargetKind.Dummy,
                Direction.Down,
                AttackStyle.Ranged));
            var result = DecodeMelee(await client.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.True(result.hit);
            Assert.Equal("Touche.", result.message);
            Assert.True(result.damage.HasValue);
            Assert.True(result.damage!.Value.Ranged);
            Assert.True(result.damage.Value.Hit);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_LegacyNameOnly_StillHitsDummy()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            await using var client = new TcpProbe();
            _ = await RegisterLoginSelectAsync(client, port, UniqueUser("cl"), "password123", "ClHero");
            var name = Encoding.UTF8.GetBytes(CombatMvpLimits.DummyName);
            var payload = new byte[1 + 1 + name.Length];
            payload[0] = (byte)PacketId.MeleeAttackRequest;
            payload[1] = (byte)name.Length;
            name.CopyTo(payload.AsSpan(2));
            await client.SendFrameAsync(payload);
            var result = DecodeMelee(await client.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.True(result.hit);
            Assert.True(result.damage.HasValue);
            Assert.Equal(CombatTargetKind.Dummy, result.damage!.Value.TargetKind);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_PoisonDummy_AppliesTrailer_Ticks_AndBroadcasts()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port, statusTickMs: 120);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var attacker = new TcpProbe();
            await using var observer = new TcpProbe();
            _ = await RegisterLoginSelectAsync(attacker, port, UniqueUser("sp"), password, "SpHero");
            _ = await RegisterLoginSelectAsync(observer, port, UniqueUser("so"), password, "SoHero");

            await attacker.SendFrameAsync(BuildMelee(
                CombatMvpLimits.DummyName,
                CombatTargetKind.Dummy,
                Direction.Down,
                apply: StatusEffectKind.Poison));
            var result = DecodeMeleeFull(await attacker.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.True(result.hit);
            Assert.Equal("Touche.", result.message);
            Assert.True(result.damage.HasValue);
            Assert.Equal(
                CombatMvpLimits.DummyMaxHp - result.damage!.Value.Damage,
                result.damage.Value.RemainingHp);
            Assert.True(result.status.HasValue);
            Assert.Equal(StatusEffectOp.Apply, result.status!.Value.Op);
            Assert.Equal(StatusEffectKind.Poison, result.status.Value.Kind);
            Assert.Equal(StatusEffectLimits.PoisonTicks, result.status.Value.RemainingTicks);
            Assert.Equal(StatusEffectLimits.PoisonPotency, result.status.Value.Potency);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);

            var observed = DecodeMeleeFull(await observer.ReadUntilAsync(PacketId.MeleeAttackResult));
            Assert.True(observed.status.HasValue);
            Assert.Equal(result.status.Value.EffectId, observed.status!.Value.EffectId);
            Assert.Equal(StatusEffectKind.Poison, observed.status.Value.Kind);

            var tick = DecodeMeleeFull(await attacker.ReadUntilAsync(PacketId.MeleeAttackResult, TimeSpan.FromSeconds(5)));
            Assert.True(tick.status.HasValue);
            Assert.Equal(StatusEffectOp.Tick, tick.status!.Value.Op);
            Assert.Equal(StatusEffectKind.Poison, tick.status.Value.Kind);
            Assert.Equal(StatusEffectLimits.PoisonTicks - 1, tick.status.Value.RemainingTicks);
            Assert.Equal(StatusEffectLimits.PoisonPotency, tick.damage!.Value.Damage);
            Assert.Equal(result.damage.Value.RemainingHp - StatusEffectLimits.PoisonPotency, tick.damage.Value.RemainingHp);
            Assert.False(tick.damage.Value.Killed);
            Assert.Equal("Poison.", tick.message);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_MonsterAi_BroadcastsSlimePositionWithKindTrailer()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port, monsterAi: true);
        await host.StartAsync();
        try
        {
            await using var client = new TcpProbe();
            _ = await RegisterLoginSelectAsync(client, port, UniqueUser("ma"), "password123", "MaHero");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            PositionUpdateWire? slime = null;
            while (DateTime.UtcNow < deadline && slime is null)
            {
                var frame = await client.ReadUntilAsync(PacketId.PositionUpdate, TimeSpan.FromSeconds(2));
                if (Phase7PacketCodec.TryParsePositionUpdate(frame.AsSpan(1), out var pos)
                    && pos.Kind == CombatTargetKind.Monster
                    && string.Equals(pos.Username, "Slime", StringComparison.Ordinal))
                {
                    slime = pos;
                }
            }

            Assert.NotNull(slime);
            Assert.Equal(1, slime!.MapId);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHost CreateInMemoryHost(int port, int? statusTickMs = null, bool monsterAi = false)
        => FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                    [MonsterAiLimits.EnabledConfigKey] = monsterAi ? "true" : "false",
                };
                if (statusTickMs is int ms)
                {
                    settings[StatusEffectLimits.TickIntervalConfigKey] = ms.ToString();
                }

                config.AddInMemoryCollection(settings);
            })
            .Build();

    private static async Task<Guid> RegisterLoginSelectAsync(
        TcpProbe client,
        int port,
        string user,
        string password,
        string characterName)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(BuildLogin(user, password, PacketId.RegisterRequest));
        Assert.True(DecodeStatus(await client.ReadUntilAsync(PacketId.RegisterResult)).ok);
        await client.SendFrameAsync(BuildLogin(user, password));
        Assert.True(DecodeStatus(await client.ReadUntilAsync(PacketId.LoginResult)).ok);
        await client.SendFrameAsync(BuildCharacterCreate(characterName, Phase7ContentSeed.DefaultClassId));
        var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
        Assert.True(create.Length > 3 && create[1] != 0);
        var characterId = Encoding.UTF8.GetString(create, 3, create[2]);
        await client.SendFrameAsync(BuildCharacterSelect(characterId));
        Assert.True(DecodeStatus(await client.ReadUntilAsync(PacketId.CharacterSelectResult)).ok);
        await client.DrainPendingAsync();
        return Guid.Parse(characterId);
    }

    private static string UniqueUser(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

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

    private static byte[] BuildCharacterSelect(string characterId)
    {
        var b = Encoding.UTF8.GetBytes(characterId);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildMelee(
        string target,
        CombatTargetKind kind,
        Direction facing,
        AttackStyle style = AttackStyle.Melee,
        StatusEffectKind apply = StatusEffectKind.None)
    {
        var body = CombatMvpWire.BuildAttackRequest(
            new AttackRequest(target, kind, facing, CombatMvpLimits.DummyId, style, apply));
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.MeleeAttackRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    private static (bool hit, string target, string message, DamageEvent? damage) DecodeMelee(byte[] frame)
    {
        var full = DecodeMeleeFull(frame);
        return (full.hit, full.target, full.message, full.damage);
    }

    private static (bool hit, string target, string message, DamageEvent? damage, StatusEffectEvent? status) DecodeMeleeFull(byte[] frame)
    {
        Assert.Equal((byte)PacketId.MeleeAttackResult, frame[0]);
        Assert.True(CombatMvpWire.TryParseMeleeResult(
            frame.AsSpan(1),
            out var hit,
            out var target,
            out var message,
            out var damage,
            out var status));
        return (hit, target, message, damage, status);
    }

    private static (bool ok, string message) DecodeStatus(byte[] payload)
    {
        var ok = payload.Length > 1 && payload[1] != 0;
        if (payload.Length < 3)
        {
            return (ok, string.Empty);
        }

        var len = payload[2];
        var message = payload.Length >= 3 + len ? Encoding.UTF8.GetString(payload, 3, len) : string.Empty;
        return (ok, message);
    }

    private sealed class TcpProbe : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
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
            await _sendLock.WaitAsync();
            try
            {
                var frame = new byte[4 + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
                payload.CopyTo(frame, 4);
                await _stream!.WriteAsync(frame);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(8));
            var header = new byte[4];
            await ReadExactAsync(header, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(header);
            Assert.True(len > 0 && len < 1_000_000);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
            while (DateTime.UtcNow < deadline)
            {
                var frame = await ReadFrameAsync(deadline - DateTime.UtcNow);
                if (frame.Length > 0 && frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("expected packet not received: " + id);
        }

        public async Task DrainPendingAsync(TimeSpan? window = null)
        {
            var until = DateTime.UtcNow + (window ?? TimeSpan.FromMilliseconds(250));
            while (DateTime.UtcNow < until)
            {
                var remain = until - DateTime.UtcNow;
                if (remain <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    _ = await ReadFrameAsync(remain);
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
                if (n == 0)
                {
                    throw new System.IO.EndOfStreamException();
                }

                read += n;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _tcp?.Close();
            _stream?.Dispose();
            _tcp?.Dispose();
            await Task.CompletedTask;
        }
    }
}
