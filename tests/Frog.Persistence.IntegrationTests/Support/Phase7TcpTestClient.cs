using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Persistence.IntegrationTests.Support;

internal static class Phase7TcpPacketBuilder
{
    public static byte[] BuildRegister(string user, string pass) => BuildLogin(user, pass, PacketId.RegisterRequest);

    public static byte[] BuildLogin(string user, string pass, PacketId id = PacketId.LoginRequest)
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

    public static byte[] BuildReconnect(string token)
    {
        var t = Encoding.UTF8.GetBytes(token);
        var payload = new byte[1 + 2 + t.Length];
        payload[0] = (byte)PacketId.ReconnectRequest;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)t.Length);
        t.CopyTo(payload, 3);
        return payload;
    }

    public static byte[] BuildCharacterCreate(string name, Guid classId)
    {
        var n = Encoding.UTF8.GetBytes(name);
        var payload = new byte[1 + 1 + n.Length + 16];
        payload[0] = (byte)PacketId.CharacterCreateRequest;
        payload[1] = (byte)n.Length;
        n.CopyTo(payload, 2);
        classId.TryWriteBytes(payload.AsSpan(2 + n.Length));
        return payload;
    }

    public static byte[] BuildCharacterList() => [(byte)PacketId.CharacterListRequest];

    public static byte[] BuildCharacterSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    public static byte[] BuildMapRequest() => [(byte)PacketId.MapRequest];

    public static byte[] BuildEquip(byte slot) => [(byte)PacketId.EquipRequest, slot];

    public static byte[] BuildMelee(string target)
    {
        var t = Encoding.UTF8.GetBytes(target);
        var payload = new byte[1 + 1 + t.Length];
        payload[0] = (byte)PacketId.MeleeAttackRequest;
        payload[1] = (byte)t.Length;
        t.CopyTo(payload, 2);
        return payload;
    }

    public static byte[] BuildSpellCast(Guid spellId, string target)
    {
        var t = Encoding.UTF8.GetBytes(target);
        var payload = new byte[1 + 16 + 1 + t.Length];
        payload[0] = (byte)PacketId.SpellCastRequest;
        spellId.TryWriteBytes(payload.AsSpan(1));
        payload[17] = (byte)t.Length;
        t.CopyTo(payload, 18);
        return payload;
    }

    public static byte[] BuildChat(ChatChannel channel, string message, string whisperTarget = "")
    {
        var m = Encoding.UTF8.GetBytes(message);
        if (channel == ChatChannel.Whisper)
        {
            var target = Encoding.UTF8.GetBytes(whisperTarget);
            var payload = new byte[1 + 1 + 1 + target.Length + sizeof(ushort) + m.Length];
            payload[0] = (byte)PacketId.ChatSend;
            payload[1] = (byte)channel;
            payload[2] = (byte)target.Length;
            target.CopyTo(payload, 3);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3 + target.Length), (ushort)m.Length);
            m.CopyTo(payload, 3 + target.Length + sizeof(ushort));
            return payload;
        }

        var plain = new byte[1 + 1 + sizeof(ushort) + m.Length];
        plain[0] = (byte)PacketId.ChatSend;
        plain[1] = (byte)channel;
        BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(2), (ushort)m.Length);
        m.CopyTo(plain, 2 + sizeof(ushort));
        return plain;
    }

    public static byte[] BuildShopBuy(Guid shopId, Guid itemId, int qty, Guid requestId)
    {
        var payload = new byte[1 + 16 + 16 + 4 + 16];
        payload[0] = (byte)PacketId.ShopBuyRequest;
        shopId.TryWriteBytes(payload.AsSpan(1));
        itemId.TryWriteBytes(payload.AsSpan(17));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(33), qty);
        requestId.TryWriteBytes(payload.AsSpan(37));
        return payload;
    }

    public static byte[] BuildShopSell(byte slot, int qty, Guid requestId)
    {
        var payload = new byte[1 + 1 + 4 + 16];
        payload[0] = (byte)PacketId.ShopSellRequest;
        payload[1] = slot;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), qty);
        requestId.TryWriteBytes(payload.AsSpan(6));
        return payload;
    }

    public static byte[] BuildBankDepositItem(byte slot, int qty, Guid requestId)
    {
        var payload = new byte[1 + 1 + 4 + 16];
        payload[0] = (byte)PacketId.BankDepositRequest;
        payload[1] = slot;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), qty);
        requestId.TryWriteBytes(payload.AsSpan(6));
        return payload;
    }

    public static byte[] BuildBankWithdrawItem(byte slot, int qty, Guid requestId)
    {
        var payload = new byte[1 + 1 + 4 + 16];
        payload[0] = (byte)PacketId.BankWithdrawRequest;
        payload[1] = slot;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), qty);
        requestId.TryWriteBytes(payload.AsSpan(6));
        return payload;
    }

    public static byte[] BuildBankDepositGold(int gold, Guid requestId)
    {
        var payload = new byte[1 + 4 + 16];
        payload[0] = (byte)PacketId.BankDepositRequest;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), gold);
        requestId.TryWriteBytes(payload.AsSpan(5));
        return payload;
    }

    public static byte[] BuildBankWithdrawGold(int gold, Guid requestId)
    {
        var payload = new byte[1 + 4 + 16];
        payload[0] = (byte)PacketId.BankWithdrawRequest;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), gold);
        requestId.TryWriteBytes(payload.AsSpan(5));
        return payload;
    }

    public static byte[] BuildPickup(Guid groundId)
    {
        var payload = new byte[1 + 16];
        payload[0] = (byte)PacketId.PickupItemRequest;
        groundId.TryWriteBytes(payload.AsSpan(1));
        return payload;
    }

    public static byte[] BuildRespawn() => [(byte)PacketId.RespawnRequest];

    public static byte[] BuildMove(sbyte deltaX, sbyte deltaY) =>
        [(byte)PacketId.MoveRequest, (byte)deltaX, (byte)deltaY];

    public static byte[] BuildPositionSync(int pixelX, int pixelY)
    {
        var payload = new byte[1 + 8];
        payload[0] = (byte)PacketId.PositionSyncRequest;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), pixelX);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(5), pixelY);
        return payload;
    }

    public static byte[] BuildInteract() => [(byte)PacketId.InteractRequest];

    public static byte[] BuildMapEventsRequest() => [(byte)PacketId.MapEventsRequest];

    public static byte[] BuildPublishedCatalogRequest() => [(byte)PacketId.PublishedCatalogRequest];

    public static byte[] BuildDialogueChoice(byte[] sessionToken, string choiceId)
    {
        var body = Phase8Wire.BuildDialogueChoiceRequest(sessionToken, choiceId);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.DialogueChoiceRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    public static byte[] BuildQuestTurnIn(Guid questId, Guid requestId)
    {
        var body = Phase8Wire.BuildQuestTurnInRequest(questId, requestId);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.QuestTurnInRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    public static byte[] BuildCraft(Guid recipeId, Guid requestId)
    {
        var body = Phase8Wire.BuildCraftRequest(recipeId, requestId);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.CraftRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    public static byte[] BuildAcquireProfession(Guid professionId)
    {
        var body = Phase8Wire.BuildAcquireProfessionRequest(professionId);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.AcquireProfessionRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    public static byte[] BuildHeartbeat() => [(byte)PacketId.HeartbeatRequest];
}

internal sealed class Phase7TcpTestClient : IAsyncDisposable
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
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));
        var lenBuf = new byte[4];
        await ReadExactAsync(lenBuf, cts.Token);
        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        var payload = new byte[len];
        await ReadExactAsync(payload, cts.Token);
        return payload;
    }

    public async Task DrainPendingAsync(TimeSpan? budget = null)
    {
        var deadline = DateTime.UtcNow + (budget ?? TimeSpan.FromMilliseconds(400));
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

    public Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
        => ReadUntilAnyAsync([id], timeout);

    public async Task<byte[]> ReadUntilAnyAsync(PacketId[] ids, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
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

internal static class Phase7TcpTestPorts
{
    public static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
