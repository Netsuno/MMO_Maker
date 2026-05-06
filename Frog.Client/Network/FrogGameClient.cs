using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client.Network;

/// <summary>Client TCP minimal : Hello, login/register, map, mouvements, heartbeat, chat (voir protocol_login_map.md).</summary>
public sealed class FrogGameClient : IDisposable
{
    private readonly SynchronizationContext _ui;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _mapFingerprintLock = new();
    private readonly Dictionary<int, (long Revision, byte[] Sha32)> _mapFingerprints = new();
    /// <summary>Carte dont on enverra l’empreinte avec le prochain <see cref="PacketId.MapRequest"/> (défaut 1).</summary>
    private int _mapRequestHintMapId = 1;
    private volatile bool _intentionalDisconnect;
    private readonly MapSerializer _mapSerializer = new();

    public FrogGameClient(SynchronizationContext uiContext)
    {
        _ui = uiContext;
    }

    public bool IsConnected => _tcp?.Connected == true;

    public event Action<string>? HelloReceived;
    public event Action<bool, string>? LoginResultReceived;
    public event Action<bool, string>? RegisterResultReceived;
    public event Action<int, Map>? MapDataReceived;
    /// <summary>Émis lorsque le serveur répond que le blob carte est déjà à jour (hint <see cref="PacketId.MapRequest"/>).</summary>
    public event Action<int, long>? MapAlreadySyncedReceived;
    public event Action<string, int, int, int>? PositionUpdateReceived;
    public event Action<string, string>? CharacterPayloadReceived;
    public event Action<string>? PlayerLeaveReceived;
    public event Action<string>? ErrorReceived;
    public event Action? HeartbeatAckReceived;
    public event Action? LogoutAckReceived;
    public event Action<ChatChannel, string, string, string>? ChatMessageReceived;
    public event Action<bool, string, string>? MeleeAttackResultReceived;
    public event Action? ConnectionClosed;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync().ConfigureAwait(false);
        var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        _tcp = tcp;
        _stream = tcp.GetStream();
        _receiveCts = new CancellationTokenSource();
        var loopCt = _receiveCts.Token;
        _receiveTask = Task.Run(() => ReceiveLoopAsync(loopCt), CancellationToken.None);
    }

    public async Task DisconnectAsync()
    {
        _intentionalDisconnect = true;
        try
        {
            try
            {
                _receiveCts?.Cancel();
            }
            catch
            {
                // ignore
            }

            if (_receiveTask is not null)
            {
                try
                {
                    await _receiveTask.ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            }

            _receiveTask = null;
            _receiveCts?.Dispose();
            _receiveCts = null;
            _stream = null;
            if (_tcp is not null)
            {
                try
                {
                    _tcp.Close();
                }
                catch
                {
                    // ignore
                }

                _tcp.Dispose();
                _tcp = null;
            }
        }
        finally
        {
            ClearWorldMapFingerprint();
            _intentionalDisconnect = false;
        }
    }

    private void ClearWorldMapFingerprint()
    {
        lock (_mapFingerprintLock)
        {
            _mapFingerprints.Clear();
            _mapRequestHintMapId = 1;
        }
    }

    private void CaptureMapFingerprint(int mapId, long revision, ReadOnlySpan<byte> sha256)
    {
        if (sha256.Length != 32)
        {
            return;
        }

        var copy = new byte[32];
        sha256.CopyTo(copy);
        lock (_mapFingerprintLock)
        {
            _mapFingerprints[mapId] = (revision, copy);
            _mapRequestHintMapId = mapId;
        }
    }

    /// <summary>Indique quelle entrée d’empreinte utiliser pour le prochain <c>MapRequest</c> (ex. après warp avant réception du blob).</summary>
    public void SetMapRequestHintMapId(int mapId)
    {
        lock (_mapFingerprintLock)
        {
            _mapRequestHintMapId = mapId;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream is not null)
            {
                var payload = await TcpFrameCodec.ReadFramePayloadAsync(_stream, cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    break;
                }

                ProcessPayload(payload);
            }
        }
        catch (OperationCanceledException)
        {
            // normal
        }
        catch
        {
            Post(() => ErrorReceived?.Invoke("Connexion interrompue."));
        }
        finally
        {
            if (!_intentionalDisconnect)
            {
                Post(() => ConnectionClosed?.Invoke());
            }
        }
    }

    private void ProcessPayload(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        var id = (PacketId)payload[0];
        var body = payload.AsMemory(1);
        switch (id)
        {
            case PacketId.Hello:
                if (WireHello.TryParse(payload, out var helloMsg, out var helloVer))
                {
                    if (helloVer != FrogWireProtocol.Version)
                    {
                        Post(() =>
                        {
                            ErrorReceived?.Invoke(
                                $"Version protocole incompatible (serveur indique {helloVer}, ce client attend {FrogWireProtocol.Version}). Mettez à jour client et serveur ensemble.");
                            _ = DisconnectAsync();
                        });
                    }
                    else
                    {
                        Post(() => HelloReceived?.Invoke(helloMsg));
                    }
                }
                else
                {
                    Post(() =>
                    {
                        ErrorReceived?.Invoke(
                            "Hello serveur incomplet ou obsolète — mettez Frog.Server à jour (même dépôt que le client).");
                        _ = DisconnectAsync();
                    });
                }

                break;

            case PacketId.LoginResult:
                if (TryReadStatusMessage(body.Span, out var okLogin, out var loginMsg))
                {
                    Post(() => LoginResultReceived?.Invoke(okLogin, loginMsg));
                }

                break;

            case PacketId.RegisterResult:
                if (TryReadStatusMessage(body.Span, out var okReg, out var regMsg))
                {
                    Post(() => RegisterResultReceived?.Invoke(okReg, regMsg));
                }

                break;

            case PacketId.MapData:
                if (!TryReadMapDataWithSyncFooter(
                        body.Span,
                        out var mapId,
                        out var mapBytes,
                        out var mapRev,
                        out var mapSha))
                {
                    Post(() => ErrorReceived?.Invoke("MapData: format protocole invalide (empreinte carte attendue)."));
                }
                else
                {
                    try
                    {
                        var map = _mapSerializer.Deserialize(mapBytes);
                        CaptureMapFingerprint(mapId, mapRev, mapSha);
                        Post(() => MapDataReceived?.Invoke(mapId, map));
                    }
                    catch (Exception ex)
                    {
                        Post(() => ErrorReceived?.Invoke("Map invalide: " + ex.Message));
                    }
                }

                break;

            case PacketId.MapAlreadySynced:
                if (!TryReadMapAlreadySynced(body.Span, out var unchangedId, out var unchangedRev, out var unchangedSha))
                {
                    Post(() => ErrorReceived?.Invoke("MapAlreadySynced: payload invalide."));
                }
                else
                {
                    CaptureMapFingerprint(unchangedId, unchangedRev, unchangedSha);
                    Post(() => MapAlreadySyncedReceived?.Invoke(unchangedId, unchangedRev));
                }

                break;

            case PacketId.PositionUpdate:
                if (TryReadPositionUpdate(body.Span, out var user, out var mapIdPu, out var px, out var py))
                {
                    Post(() => PositionUpdateReceived?.Invoke(user, mapIdPu, px, py));
                }

                break;

            case PacketId.CharacterPayload:
                if (TryReadCharacterPayload(body.Span, out var charIdUtf, out var jsonUtf))
                {
                    Post(() => CharacterPayloadReceived?.Invoke(charIdUtf, jsonUtf));
                }

                break;

            case PacketId.PlayerLeave:
                if (TryReadUsername(body.Span, out var leftUser))
                {
                    Post(() => PlayerLeaveReceived?.Invoke(leftUser));
                }

                break;

            case PacketId.HeartbeatAck:
                Post(() => HeartbeatAckReceived?.Invoke());
                break;

            case PacketId.LogoutAck:
                Post(() => LogoutAckReceived?.Invoke());
                break;

            case PacketId.ChatMessage:
                if (TryReadChatMessage(body.Span, out var ch, out var from, out var to, out var chatText))
                {
                    Post(() => ChatMessageReceived?.Invoke(ch, from, to, chatText));
                }

                break;

            case PacketId.MeleeAttackResult:
                if (TryReadMeleeResult(body.Span, out var hit, out var tgt, out var meleeMsg))
                {
                    Post(() => MeleeAttackResultReceived?.Invoke(hit, tgt, meleeMsg));
                }

                break;

            case PacketId.Error:
                if (TryReadUtf8PrefixedByteLength(body.Span, out var err))
                {
                    Post(() => ErrorReceived?.Invoke(err));
                }

                break;

            default:
                Post(() => ErrorReceived?.Invoke($"Paquet serveur inconnu: {(byte)id}"));
                break;
        }
    }

    private void Post(Action action)
    {
        _ui.Post(_ => action(), null);
    }

    public async Task SendLoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var u = Encoding.UTF8.GetBytes(username);
        var p = Encoding.UTF8.GetBytes(password);
        if (u.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes ||
            p.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentException("Identifiants trop longs ou vides.");
        }

        var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
        var o = 0;
        payload[o++] = (byte)PacketId.LoginRequest;
        payload[o++] = (byte)u.Length;
        u.CopyTo(payload.AsSpan(o));
        o += u.Length;
        payload[o++] = (byte)p.Length;
        p.CopyTo(payload.AsSpan(o));
        await SendRawAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendRegisterAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var u = Encoding.UTF8.GetBytes(username);
        var p = Encoding.UTF8.GetBytes(password);
        if (u.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes ||
            p.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentException("Identifiants trop longs ou vides.");
        }

        var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
        var o = 0;
        payload[o++] = (byte)PacketId.RegisterRequest;
        payload[o++] = (byte)u.Length;
        u.CopyTo(payload.AsSpan(o));
        o += u.Length;
        payload[o++] = (byte)p.Length;
        p.CopyTo(payload.AsSpan(o));
        await SendRawAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public Task SendMapRequestAsync(CancellationToken cancellationToken = default)
        => SendMapRequestAsync(null, cancellationToken);

    /// <param name="hintMapId">Si non null, cherche l’empreinte pour cette carte ; sinon utilise la dernière carte connue pour les hints.</param>
    public Task SendMapRequestAsync(int? hintMapId, CancellationToken cancellationToken = default)
    {
        int mapIdForHint;
        long revision;
        byte[]? sha;
        lock (_mapFingerprintLock)
        {
            if (hintMapId is { } hid)
            {
                _mapRequestHintMapId = hid;
            }

            mapIdForHint = hintMapId ?? _mapRequestHintMapId;
            if (!_mapFingerprints.TryGetValue(mapIdForHint, out var entry))
            {
                return SendRawAsync([(byte)PacketId.MapRequest], cancellationToken);
            }

            revision = entry.Revision;
            sha = entry.Sha32;
        }

        if (sha is null || sha.Length != 32)
        {
            return SendRawAsync([(byte)PacketId.MapRequest], cancellationToken);
        }

        var payload = new byte[1 + sizeof(long) + 32];
        payload[0] = (byte)PacketId.MapRequest;
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(1), revision);
        sha.CopyTo(payload.AsSpan(1 + sizeof(long)));
        return SendRawAsync(payload, cancellationToken);
    }

    public Task SendMoveAsync(sbyte dx, sbyte dy, CancellationToken cancellationToken = default)
    {
        var payload = new byte[] { (byte)PacketId.MoveRequest, (byte)dx, (byte)dy };
        return SendRawAsync(payload, cancellationToken);
    }

    public Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
        => SendRawAsync(new[] { (byte)PacketId.HeartbeatRequest }, cancellationToken);

    public Task SendLogoutAsync(CancellationToken cancellationToken = default)
        => SendRawAsync(new[] { (byte)PacketId.LogoutRequest }, cancellationToken);

    public async Task SendChatAsync(ChatChannel channel, string whisperTarget, string message, CancellationToken cancellationToken = default)
    {
        var msgBytes = Encoding.UTF8.GetBytes(message);
        if (msgBytes.Length is 0 or > ChatProtocolLimits.MaxMessageUtf8Bytes)
        {
            throw new ArgumentException("Message vide ou trop long.");
        }

        var whisperBytes = Encoding.UTF8.GetBytes(whisperTarget ?? string.Empty);
        if (channel == ChatChannel.Whisper && whisperBytes.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentException("Cible whisper invalide.");
        }

        int size = 1 + 1 + sizeof(ushort) + msgBytes.Length;
        if (channel == ChatChannel.Whisper)
        {
            size += 1 + whisperBytes.Length;
        }

        var payload = new byte[size];
        var o = 0;
        payload[o++] = (byte)PacketId.ChatSend;
        payload[o++] = (byte)channel;
        if (channel == ChatChannel.Whisper)
        {
            payload[o++] = (byte)whisperBytes.Length;
            whisperBytes.CopyTo(payload.AsSpan(o));
            o += whisperBytes.Length;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)msgBytes.Length);
        o += sizeof(ushort);
        msgBytes.CopyTo(payload.AsSpan(o));
        await SendRawAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendMeleeAttackAsync(string targetUsername, CancellationToken cancellationToken = default)
    {
        var t = Encoding.UTF8.GetBytes(targetUsername);
        if (t.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentException("Cible invalide.");
        }

        var payload = new byte[1 + 1 + t.Length];
        payload[0] = (byte)PacketId.MeleeAttackRequest;
        payload[1] = (byte)t.Length;
        t.CopyTo(payload.AsSpan(2));
        await SendRawAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendRawAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Non connecte.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await TcpFrameCodec.WriteFrameAsync(_stream, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static bool TryReadUtf8PrefixedByteLength(ReadOnlySpan<byte> span, out string text)
    {
        text = string.Empty;
        if (span.Length < 1)
        {
            return false;
        }

        var len = span[0];
        if (len > span.Length - 1)
        {
            return false;
        }

        text = Encoding.UTF8.GetString(span.Slice(1, len));
        return true;
    }

    private static bool TryReadStatusMessage(ReadOnlySpan<byte> span, out bool success, out string message)
    {
        success = false;
        message = string.Empty;
        if (span.Length < 2)
        {
            return false;
        }

        success = span[0] != 0;
        return TryReadUtf8PrefixedByteLength(span.Slice(1), out message);
    }

    private static bool TryReadMapDataWithSyncFooter(
        ReadOnlySpan<byte> span,
        out int mapId,
        out ReadOnlySpan<byte> mapBytes,
        out long fingerprintRevision,
        out ReadOnlySpan<byte> fingerprintSha256)
    {
        mapId = 0;
        mapBytes = ReadOnlySpan<byte>.Empty;
        fingerprintRevision = 0;
        fingerprintSha256 = ReadOnlySpan<byte>.Empty;
        if (span.Length < sizeof(int) * 2)
        {
            return false;
        }

        mapId = BinaryPrimitives.ReadInt32LittleEndian(span);
        var len = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(sizeof(int)));
        if (len < 0 || len > span.Length - sizeof(int) * 2)
        {
            return false;
        }

        var footerStart = sizeof(int) * 2 + len;
        if (span.Length != footerStart + sizeof(long) + 32)
        {
            return false;
        }

        mapBytes = span.Slice(sizeof(int) * 2, len);
        fingerprintRevision = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(footerStart));
        fingerprintSha256 = span.Slice(footerStart + sizeof(long), 32);
        return true;
    }

    private static bool TryReadMapAlreadySynced(
        ReadOnlySpan<byte> span,
        out int mapId,
        out long fingerprintRevision,
        out ReadOnlySpan<byte> fingerprintSha256)
    {
        mapId = 0;
        fingerprintRevision = 0;
        fingerprintSha256 = ReadOnlySpan<byte>.Empty;
        if (span.Length != sizeof(int) + sizeof(long) + 32)
        {
            return false;
        }

        mapId = BinaryPrimitives.ReadInt32LittleEndian(span);
        fingerprintRevision = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(sizeof(int)));
        fingerprintSha256 = span.Slice(sizeof(int) + sizeof(long), 32);
        return true;
    }

    private static bool TryReadUsername(ReadOnlySpan<byte> span, out string username)
    {
        username = string.Empty;
        return TryReadUtf8PrefixedByteLength(span, out username);
    }

    private static bool TryReadPositionUpdate(ReadOnlySpan<byte> span, out string username, out int mapId, out int x, out int y)
    {
        username = string.Empty;
        mapId = x = y = 0;
        if (span.Length < 1)
        {
            return false;
        }

        var ulen = span[0];
        var need = 1 + ulen + sizeof(int) * 3;
        if (ulen is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes || span.Length < need)
        {
            return false;
        }

        username = Encoding.UTF8.GetString(span.Slice(1, ulen));
        var o = 1 + ulen;
        mapId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(o));
        x = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(o + sizeof(int)));
        y = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(o + sizeof(int) * 2));
        return true;
    }

    private static bool TryReadCharacterPayload(ReadOnlySpan<byte> span, out string characterId, out string jsonPayload)
    {
        characterId = jsonPayload = string.Empty;
        if (span.Length < 3)
        {
            return false;
        }

        var idLen = span[0];
        if (idLen is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            return false;
        }

        var jsonOffset = 1 + idLen;
        if (span.Length < jsonOffset + sizeof(ushort))
        {
            return false;
        }

        characterId = Encoding.UTF8.GetString(span.Slice(1, idLen));
        var jl = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(jsonOffset));
        jsonOffset += sizeof(ushort);
        if (jl is 0 || span.Length < jsonOffset + jl)
        {
            return false;
        }

        jsonPayload = Encoding.UTF8.GetString(span.Slice(jsonOffset, jl));
        return true;
    }

    private static bool TryReadChatMessage(ReadOnlySpan<byte> span, out ChatChannel channel, out string from, out string to, out string message)
    {
        channel = default;
        from = to = message = string.Empty;
        if (span.Length < 3)
        {
            return false;
        }

        channel = (ChatChannel)span[0];
        var o = 1;
        if (span.Length < o + 1)
        {
            return false;
        }

        var fromLen = span[o++];
        if (fromLen is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes || span.Length < o + fromLen)
        {
            return false;
        }

        from = Encoding.UTF8.GetString(span.Slice(o, fromLen));
        o += fromLen;
        if (span.Length < o + 1)
        {
            return false;
        }

        var toLen = span[o++];
        if (toLen > ChatProtocolLimits.MaxUsernameUtf8Bytes || span.Length < o + toLen + sizeof(ushort))
        {
            return false;
        }

        to = toLen > 0 ? Encoding.UTF8.GetString(span.Slice(o, toLen)) : string.Empty;
        o += toLen;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(o));
        o += sizeof(ushort);
        if (msgLen is 0 or > ChatProtocolLimits.MaxMessageUtf8Bytes || span.Length < o + msgLen)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(span.Slice(o, msgLen));
        return true;
    }

    private static bool TryReadMeleeResult(ReadOnlySpan<byte> span, out bool hit, out string target, out string message)
    {
        hit = false;
        target = message = string.Empty;
        if (span.Length < 2)
        {
            return false;
        }

        hit = span[0] != 0;
        if (!TryReadUtf8PrefixedByteLength(span.Slice(1), out target))
        {
            return false;
        }

        var consumed = 1 + 1 + Encoding.UTF8.GetByteCount(target);
        if (span.Length < consumed + sizeof(ushort))
        {
            return false;
        }

        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(consumed));
        consumed += sizeof(ushort);
        if (msgLen > ChatProtocolLimits.MaxMessageUtf8Bytes || span.Length < consumed + msgLen)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(span.Slice(consumed, msgLen));
        return true;
    }

    public void Dispose()
    {
        try
        {
            _receiveCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _tcp?.Close();
        }
        catch
        {
            // ignore
        }

        _sendLock.Dispose();
    }
}
