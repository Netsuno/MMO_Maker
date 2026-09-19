using System.Buffers.Binary;
using System.Text;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;

namespace Frog.LoadHarness;

internal static class LoadPackets
{
    public static byte[] Register(string user, string pass) => Login(user, pass, PacketId.RegisterRequest);

    public static byte[] Login(string user, string pass, PacketId id = PacketId.LoginRequest)
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

    public static byte[] CharacterCreate(string name, Guid classId)
    {
        var n = Encoding.UTF8.GetBytes(name);
        var payload = new byte[1 + 1 + n.Length + 16];
        payload[0] = (byte)PacketId.CharacterCreateRequest;
        payload[1] = (byte)n.Length;
        n.CopyTo(payload, 2);
        classId.TryWriteBytes(payload.AsSpan(2 + n.Length));
        return payload;
    }

    public static byte[] CharacterSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    public static byte[] Chat(ChatChannel channel, string message)
    {
        var m = Encoding.UTF8.GetBytes(message);
        var payload = new byte[1 + 1 + sizeof(ushort) + m.Length];
        payload[0] = (byte)PacketId.ChatSend;
        payload[1] = (byte)channel;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), (ushort)m.Length);
        m.CopyTo(payload, 2 + sizeof(ushort));
        return payload;
    }

    public static byte[] Move(sbyte deltaX, sbyte deltaY)
        => [(byte)PacketId.MoveRequest, (byte)deltaX, (byte)deltaY];

    public static byte[] Heartbeat() => [(byte)PacketId.HeartbeatRequest];

    public static byte[] Melee(string target)
    {
        var t = Encoding.UTF8.GetBytes(target);
        var payload = new byte[1 + 1 + t.Length];
        payload[0] = (byte)PacketId.MeleeAttackRequest;
        payload[1] = (byte)t.Length;
        t.CopyTo(payload, 2);
        return payload;
    }

    public static byte[] Interact(Guid activationId)
    {
        var body = Phase8Wire.BuildInteractRequest(activationId);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.InteractRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    public static Guid DefaultClassId => Phase7ContentSeed.DefaultClassId;

    public static bool IsHello(ReadOnlySpan<byte> frame)
        => frame.Length > 0 && frame[0] == (byte)PacketId.Hello && WireHello.TryParse(frame, out _, out _);

    public static bool StatusOk(ReadOnlySpan<byte> frame)
        => frame.Length >= 2 && frame[1] != 0;

    public static string StatusMessage(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3)
        {
            return string.Empty;
        }

        var len = frame[2];
        if (frame.Length < 3 + len)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(frame.Slice(3, len));
    }

    public static string ErrorMessage(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2 || frame[0] != (byte)PacketId.Error)
        {
            return string.Empty;
        }

        var len = frame[1];
        if (frame.Length != 2 + len)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(frame.Slice(2, len));
    }
}
