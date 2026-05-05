using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Server.Database;
using Frog.Server.Logging;
using Frog.Server.Persistence;
using Frog.Server.Services;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Network;

public sealed class PacketDispatcher(
    AuthService authService,
    ConnectionManager connectionManager,
    ClientRegistry clientRegistry,
    MapService mapService,
    MovementService movementService,
    PacketSender packetSender,
    PlayerLifecycleNotifier playerLifecycleNotifier,
    ICharacterBootstrap characterBootstrap,
    ICharacterPayloadReader characterPayloadReader,
    IPlayerStateStore playerStateStore,
    ILogger<PacketDispatcher> logger)
{
    private readonly AuthService _authService = authService;
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly ClientRegistry _clientRegistry = clientRegistry;
    private readonly MapService _mapService = mapService;
    private readonly MovementService _movementService = movementService;
    private readonly PacketSender _packetSender = packetSender;
    private readonly PlayerLifecycleNotifier _playerLifecycleNotifier = playerLifecycleNotifier;
    private readonly ICharacterBootstrap _characterBootstrap = characterBootstrap;
    private readonly ICharacterPayloadReader _characterPayloadReader = characterPayloadReader;
    private readonly IPlayerStateStore _playerStateStore = playerStateStore;
    private readonly ILogger<PacketDispatcher> _logger = logger;

    public async Task DispatchAsync(ClientSession clientSession, byte[] framePayload, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(BuildLogScope(clientSession)))
        {
            await DispatchCoreAsync(clientSession, framePayload, cancellationToken);
        }
    }

    private static Dictionary<string, object?> BuildLogScope(ClientSession clientSession) => new()
    {
        ["ConnectionId"] = clientSession.ConnectionId,
        ["RemoteEndPoint"] = clientSession.RemoteEndPoint,
        ["Username"] = clientSession.Username ?? string.Empty
    };

    private async Task DispatchCoreAsync(ClientSession clientSession, byte[] framePayload, CancellationToken cancellationToken)
    {
        if (framePayload.Length == 0)
        {
            await _packetSender.SendErrorAsync(clientSession, "Paquet vide.", cancellationToken);
            return;
        }

        var packetId = (PacketId)framePayload[0];
        var payload = framePayload.AsMemory(1);
        ServerNetworkLogs.InboundPacket(_logger, packetId.ToString(), framePayload.Length - 1);

        switch (packetId)
        {
            case PacketId.LoginRequest:
                await HandleLoginRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.RegisterRequest:
                await HandleRegisterRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.MapRequest:
                await HandleMapRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.MoveRequest:
                await HandleMoveRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.HeartbeatRequest:
                await HandleHeartbeatRequestAsync(clientSession, cancellationToken);
                break;

            case PacketId.LogoutRequest:
                await HandleLogoutRequestAsync(clientSession, cancellationToken);
                break;

            case PacketId.ChatSend:
                await HandleChatSendAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.MeleeAttackRequest:
                await HandleMeleeAttackRequestAsync(clientSession, payload, cancellationToken);
                break;

            default:
                ServerNetworkLogs.UnknownPacket(_logger, (byte)packetId);
                await _packetSender.SendErrorAsync(clientSession, $"Packet non supporte: {(byte)packetId}", cancellationToken);
                break;
        }
    }

    private async Task HandleLoginRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryParseLoginPayload(payload.Span, out var username, out var password))
        {
            ServerNetworkLogs.LoginFailed(_logger, "invalid_payload");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Payload login invalide.", cancellationToken);
            return;
        }

        if (!_authService.ValidateCredentials(username, password))
        {
            ServerNetworkLogs.LoginFailed(_logger, "invalid_credentials");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Identifiants invalides.", cancellationToken);
            return;
        }

        if (!_connectionManager.TryCreateSession(username, out var session) || session is null)
        {
            ServerNetworkLogs.LoginFailed(_logger, "already_connected");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Compte deja connecte.", cancellationToken);
            return;
        }

        clientSession.AuthenticatedSession = session;
        session.CharacterId = _characterBootstrap.EnsureDefaultHero(username);
        if (_playerStateStore.TryGet(username, out var world))
        {
            session.PositionX = world.X;
            session.PositionY = world.Y;
            session.CurrentMapId = world.MapId;
        }
        else
        {
            session.PositionX = 0;
            session.PositionY = 0;
            session.CurrentMapId = MapService.DefaultWorldMapId;
        }

        if (!_mapService.TryEnsureMapLoaded(session.CurrentMapId))
        {
            session.CurrentMapId = MapService.DefaultWorldMapId;
            session.PositionX = 0;
            session.PositionY = 0;
            _mapService.TryEnsureMapLoaded(session.CurrentMapId);
        }

        _clientRegistry.Register(session.Id, clientSession);
        SessionPixelSync.SyncFromTileGrid(session);
        _connectionManager.TryTouchSession(session.Id);
        await _packetSender.SendLoginResultAsync(clientSession, true, "Connexion reussie.", cancellationToken);
        ServerNetworkLogs.LoginSucceeded(_logger, username);

        if (!string.IsNullOrWhiteSpace(session.CharacterId) &&
            _characterPayloadReader.TryGetPayloadJson(session.CharacterId!, out var payloadJson))
        {
            try
            {
                await _packetSender.SendCharacterPayloadAsync(clientSession, session.CharacterId!, payloadJson, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible d'envoyer CharacterPayload pour {CharacterId}", session.CharacterId);
            }
        }

        await SyncPositionsOnJoinAsync(clientSession, session, cancellationToken);
    }

    private async Task HandleRegisterRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryParseLoginPayload(payload.Span, out var username, out var password))
        {
            ServerNetworkLogs.RegisterFailed(_logger, "invalid_payload");
            await _packetSender.SendRegisterResultAsync(clientSession, false, "Payload inscription invalide.", cancellationToken);
            return;
        }

        var created = _authService.RegisterAccount(username, password);
        if (!created)
        {
            ServerNetworkLogs.RegisterFailed(_logger, "duplicate_or_invalid");
            await _packetSender.SendRegisterResultAsync(clientSession, false, "Compte deja existant ou invalide.", cancellationToken);
            return;
        }

        await _packetSender.SendRegisterResultAsync(clientSession, true, "Compte cree.", cancellationToken);
        ServerNetworkLogs.RegisterSucceeded(_logger, username);
    }

    private async Task HandleMapRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        var span = payload.Span;
        if (!(span.IsEmpty || span.Length == 40))
        {
            await _packetSender.SendErrorAsync(clientSession, "MapRequest: payload attendu (vide ou 40 octets).", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);

        var requestedMapId = session.CurrentMapId;
        if (!_mapService.TryEnsureMapLoaded(requestedMapId))
        {
            await _packetSender.SendErrorAsync(
                clientSession,
                $"Carte {requestedMapId} introuvable ou blob illisible.",
                cancellationToken);
            return;
        }

        if (span.Length == 40)
        {
            var rev = BinaryPrimitives.ReadInt64LittleEndian(span);
            var hintSha = span.Slice(sizeof(long), 32);
            if (_mapService.TryMatchMapFingerprint(requestedMapId, rev, hintSha))
            {
                await _packetSender.SendMapAlreadySyncedAsync(
                    clientSession,
                    requestedMapId,
                    _mapService.GetFingerprintRevision(requestedMapId),
                    _mapService.GetFingerprintSha256(requestedMapId),
                    cancellationToken);
                return;
            }
        }

        var mapData = _mapService.GetSerializedMapForSession(session.Id, requestedMapId);
        await _packetSender.SendMapDataAsync(
            clientSession,
            requestedMapId,
            mapData,
            _mapService.GetFingerprintRevision(requestedMapId),
            _mapService.GetFingerprintSha256(requestedMapId),
            cancellationToken);
        ServerNetworkLogs.MapDataSent(_logger, session.Username, requestedMapId, mapData.Length);
    }

    private async Task HandleMoveRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseMovePayload(payload.Span, out var deltaX, out var deltaY))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload mouvement invalide.", cancellationToken);
            return;
        }

        if (!_movementService.TryApplyMove(session, deltaX, deltaY, out var error))
        {
            await _packetSender.SendErrorAsync(clientSession, error, cancellationToken);
            return;
        }

        _movementService.TryApplyWarpAfterMove(session);
        _connectionManager.TryTouchSession(session.Id);
        ServerNetworkLogs.MoveApplied(_logger, session.Username, session.PositionX, session.PositionY);
        var clients = _clientRegistry.GetAllAuthenticatedClients();
        foreach (var targetClient in clients)
        {
            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                session.Username,
                session.CurrentMapId,
                session.PositionX,
                session.PositionY,
                cancellationToken);
        }
    }

    private async Task HandleHeartbeatRequestAsync(ClientSession clientSession, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        await _packetSender.SendHeartbeatAckAsync(clientSession, cancellationToken);
    }

    private async Task HandleLogoutRequestAsync(ClientSession clientSession, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        var sessionId = session.Id;
        var username = session.Username;
        _playerStateStore.Upsert(username, session.CurrentMapId, session.PositionX, session.PositionY, session.CharacterId);
        _clientRegistry.Unregister(sessionId);
        await _playerLifecycleNotifier.NotifyPlayerLeftAsync(username, cancellationToken);
        _connectionManager.RemoveSession(sessionId);
        clientSession.AuthenticatedSession = null;
        await _packetSender.SendLogoutAckAsync(clientSession, cancellationToken);
        clientSession.Disconnect();
    }

    private async Task HandleMeleeAttackRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var attacker))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseMeleeTargetPayload(payload.Span, out var targetName))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload attaque melee invalide.", cancellationToken);
            return;
        }

        if (!_connectionManager.TryGetSessionByUsername(targetName, out var defender) || defender is null)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                clientSession,
                false,
                targetName,
                "Cible hors ligne.",
                cancellationToken);
            return;
        }

        if (defender.Id == attacker.Id)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                clientSession,
                false,
                targetName,
                "Cible invalide.",
                cancellationToken);
            return;
        }

        if (defender.CurrentMapId != attacker.CurrentMapId)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                clientSession,
                false,
                targetName,
                "Pas sur la meme carte.",
                cancellationToken);
            return;
        }

        SessionPixelSync.SyncFromTileGrid(attacker);
        SessionPixelSync.SyncFromTileGrid(defender);
        var hit = MeleeCombat.IsWithinMeleeRange(attacker.PixelX, attacker.PixelY, defender.PixelX, defender.PixelY);
        var message = hit ? "Touche." : "Hors portee.";
        _connectionManager.TryTouchSession(attacker.Id);
        await _packetSender.SendMeleeAttackResultAsync(clientSession, hit, targetName, message, cancellationToken);
        if (hit && _clientRegistry.TryGet(defender.Id, out var defenderClient) && defenderClient is not null)
        {
            await _packetSender.SendMeleeAttackResultAsync(defenderClient, hit, attacker.Username, "Subi une attaque melee.", cancellationToken);
        }

        ServerNetworkLogs.MeleeResolved(_logger, attacker.Username, targetName, hit);
    }

    public static bool TryParseMeleeTargetPayload(ReadOnlySpan<byte> payload, out string targetUsername)
    {
        targetUsername = string.Empty;
        if (payload.Length < 1)
        {
            return false;
        }

        var len = payload[0];
        if (len is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != 1 + len)
        {
            return false;
        }

        targetUsername = Encoding.UTF8.GetString(payload.Slice(1, len));
        return !string.IsNullOrWhiteSpace(targetUsername);
    }

    private async Task HandleChatSendAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseChatSendPayload(payload.Span, out var channel, out var whisperTarget, out var message))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload chat invalide.", cancellationToken);
            return;
        }

        if (channel == ChatChannel.Whisper && string.IsNullOrWhiteSpace(whisperTarget))
        {
            await _packetSender.SendErrorAsync(clientSession, "Cible du chuchotement invalide.", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        var from = session.Username;
        var delivered = 0;

        switch (channel)
        {
            case ChatChannel.Global:
                foreach (var target in _clientRegistry.GetAllAuthenticatedClients())
                {
                    await _packetSender.SendChatMessageAsync(target, channel, from, string.Empty, message, cancellationToken);
                    delivered++;
                }

                break;

            case ChatChannel.Map:
                var mapId = session.CurrentMapId;
                foreach (var s in _connectionManager.GetActiveSessions())
                {
                    if (s.CurrentMapId != mapId)
                    {
                        continue;
                    }

                    if (!_clientRegistry.TryGet(s.Id, out var mapClient) || mapClient is null)
                    {
                        continue;
                    }

                    await _packetSender.SendChatMessageAsync(mapClient, channel, from, string.Empty, message, cancellationToken);
                    delivered++;
                }

                break;

            case ChatChannel.Whisper:
                if (!_connectionManager.TryGetSessionByUsername(whisperTarget, out var targetSession) || targetSession is null)
                {
                    await _packetSender.SendErrorAsync(clientSession, "Joueur hors ligne.", cancellationToken);
                    return;
                }

                if (!_clientRegistry.TryGet(targetSession.Id, out var targetClient) || targetClient is null)
                {
                    await _packetSender.SendErrorAsync(clientSession, "Joueur hors ligne.", cancellationToken);
                    return;
                }

                await _packetSender.SendChatMessageAsync(clientSession, channel, from, whisperTarget, message, cancellationToken);
                await _packetSender.SendChatMessageAsync(targetClient, channel, from, whisperTarget, message, cancellationToken);
                delivered = 2;
                break;

            default:
                await _packetSender.SendErrorAsync(clientSession, "Canal chat inconnu.", cancellationToken);
                return;
        }

        ServerNetworkLogs.ChatBroadcast(_logger, channel.ToString(), from, delivered);
    }

    public static bool TryParseChatSendPayload(ReadOnlySpan<byte> payload, out ChatChannel channel, out string whisperTarget, out string message)
    {
        channel = default;
        whisperTarget = string.Empty;
        message = string.Empty;
        if (payload.Length < 1 + sizeof(ushort))
        {
            return false;
        }

        channel = (ChatChannel)payload[0];
        if (channel is not (ChatChannel.Global or ChatChannel.Map or ChatChannel.Whisper))
        {
            return false;
        }

        var o = 1;
        if (channel == ChatChannel.Whisper)
        {
            if (payload.Length < o + 1)
            {
                return false;
            }

            var targetLen = payload[o++];
            if (targetLen is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
            {
                return false;
            }

            if (payload.Length < o + targetLen + sizeof(ushort))
            {
                return false;
            }

            whisperTarget = Encoding.UTF8.GetString(payload.Slice(o, targetLen));
            o += targetLen;
        }

        if (payload.Length < o + sizeof(ushort))
        {
            return false;
        }

        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, sizeof(ushort)));
        o += sizeof(ushort);
        if (msgLen is 0 or > ChatProtocolLimits.MaxMessageUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != o + msgLen)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(payload.Slice(o, msgLen));
        return true;
    }

    public static bool TryParseLoginPayload(ReadOnlySpan<byte> payload, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        if (payload.Length < 2)
        {
            return false;
        }

        var usernameLength = payload[0];
        if (payload.Length < 1 + usernameLength + 1)
        {
            return false;
        }

        var usernameStart = 1;
        var passwordLengthOffset = usernameStart + usernameLength;
        var passwordLength = payload[passwordLengthOffset];
        var passwordStart = passwordLengthOffset + 1;
        if (payload.Length != passwordStart + passwordLength)
        {
            return false;
        }

        username = Encoding.UTF8.GetString(payload.Slice(usernameStart, usernameLength));
        password = Encoding.UTF8.GetString(payload.Slice(passwordStart, passwordLength));
        return !string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(password);
    }

    public static bool TryParseMovePayload(ReadOnlySpan<byte> payload, out sbyte deltaX, out sbyte deltaY)
    {
        deltaX = 0;
        deltaY = 0;
        if (payload.Length != 2)
        {
            return false;
        }

        deltaX = unchecked((sbyte)payload[0]);
        deltaY = unchecked((sbyte)payload[1]);
        return true;
    }

    private bool TryGetActiveSession(ClientSession clientSession, out Frog.Server.Models.Session session)
    {
        session = null!;
        if (clientSession.AuthenticatedSession is null)
        {
            return false;
        }

        var sessionId = clientSession.AuthenticatedSession.Id;
        if (!_connectionManager.IsSessionActive(sessionId))
        {
            _clientRegistry.Unregister(sessionId);
            clientSession.AuthenticatedSession = null;
            return false;
        }

        session = clientSession.AuthenticatedSession;
        return true;
    }

    private async Task SyncPositionsOnJoinAsync(ClientSession joiningClient, Frog.Server.Models.Session joiningSession, CancellationToken cancellationToken)
    {
        var activeSessions = _connectionManager.GetActiveSessions();
        var connectedClients = _clientRegistry.GetAllAuthenticatedClients();

        foreach (var existingSession in activeSessions)
        {
            if (existingSession.Id == joiningSession.Id)
            {
                continue;
            }

            if (existingSession.CurrentMapId != joiningSession.CurrentMapId)
            {
                continue;
            }

            await _packetSender.SendPositionUpdateAsync(
                joiningClient,
                existingSession.Username,
                existingSession.CurrentMapId,
                existingSession.PositionX,
                existingSession.PositionY,
                cancellationToken);
        }

        foreach (var targetClient in connectedClients)
        {
            if (ReferenceEquals(targetClient, joiningClient))
            {
                continue;
            }

            var peer = targetClient.AuthenticatedSession;
            if (peer is null || peer.CurrentMapId != joiningSession.CurrentMapId)
            {
                continue;
            }

            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                joiningSession.Username,
                joiningSession.CurrentMapId,
                joiningSession.PositionX,
                joiningSession.PositionY,
                cancellationToken);
        }
    }
}
