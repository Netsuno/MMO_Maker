using System.Text;
using Frog.Core.Enums;
using Frog.Server.Services;

namespace Frog.Server.Network;

public sealed class PacketDispatcher(
    AuthService authService,
    ConnectionManager connectionManager,
    ClientRegistry clientRegistry,
    MapService mapService,
    MovementService movementService,
    PacketSender packetSender,
    PlayerLifecycleNotifier playerLifecycleNotifier)
{
    private readonly AuthService _authService = authService;
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly ClientRegistry _clientRegistry = clientRegistry;
    private readonly MapService _mapService = mapService;
    private readonly MovementService _movementService = movementService;
    private readonly PacketSender _packetSender = packetSender;
    private readonly PlayerLifecycleNotifier _playerLifecycleNotifier = playerLifecycleNotifier;

    public async Task DispatchAsync(ClientSession clientSession, byte[] framePayload, CancellationToken cancellationToken)
    {
        if (framePayload.Length == 0)
        {
            await _packetSender.SendErrorAsync(clientSession, "Paquet vide.", cancellationToken);
            return;
        }

        var packetId = (PacketId)framePayload[0];
        var payload = framePayload.AsMemory(1);

        switch (packetId)
        {
            case PacketId.LoginRequest:
                await HandleLoginRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.RegisterRequest:
                await HandleRegisterRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.MapRequest:
                await HandleMapRequestAsync(clientSession, cancellationToken);
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

            default:
                await _packetSender.SendErrorAsync(clientSession, $"Packet non supporte: {(byte)packetId}", cancellationToken);
                break;
        }
    }

    private async Task HandleLoginRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryParseLoginPayload(payload.Span, out var username, out var password))
        {
            await _packetSender.SendLoginResultAsync(clientSession, false, "Payload login invalide.", cancellationToken);
            return;
        }

        if (!_authService.ValidateCredentials(username, password))
        {
            await _packetSender.SendLoginResultAsync(clientSession, false, "Identifiants invalides.", cancellationToken);
            return;
        }

        if (!_connectionManager.TryCreateSession(username, out var session) || session is null)
        {
            await _packetSender.SendLoginResultAsync(clientSession, false, "Compte deja connecte.", cancellationToken);
            return;
        }

        clientSession.AuthenticatedSession = session;
        session.PositionX = 0;
        session.PositionY = 0;
        _clientRegistry.Register(session.Id, clientSession);
        _connectionManager.TryTouchSession(session.Id);
        await _packetSender.SendLoginResultAsync(clientSession, true, "Connexion reussie.", cancellationToken);
        await SyncPositionsOnJoinAsync(clientSession, session, cancellationToken);
    }

    private async Task HandleRegisterRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryParseLoginPayload(payload.Span, out var username, out var password))
        {
            await _packetSender.SendRegisterResultAsync(clientSession, false, "Payload inscription invalide.", cancellationToken);
            return;
        }

        var created = _authService.RegisterAccount(username, password);
        if (!created)
        {
            await _packetSender.SendRegisterResultAsync(clientSession, false, "Compte deja existant ou invalide.", cancellationToken);
            return;
        }

        await _packetSender.SendRegisterResultAsync(clientSession, true, "Compte cree.", cancellationToken);
    }

    private async Task HandleMapRequestAsync(ClientSession clientSession, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        var mapData = _mapService.GetSerializedMapForSession(session.Id);
        await _packetSender.SendMapDataAsync(clientSession, 1, mapData, cancellationToken);
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

        _connectionManager.TryTouchSession(session.Id);
        var clients = _clientRegistry.GetAllAuthenticatedClients();
        foreach (var targetClient in clients)
        {
            await _packetSender.SendPositionUpdateAsync(targetClient, session.Username, session.PositionX, session.PositionY, cancellationToken);
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
        _clientRegistry.Unregister(sessionId);
        await _playerLifecycleNotifier.NotifyPlayerLeftAsync(username, cancellationToken);
        _connectionManager.RemoveSession(sessionId);
        clientSession.AuthenticatedSession = null;
        await _packetSender.SendLogoutAckAsync(clientSession, cancellationToken);
        clientSession.Disconnect();
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

            await _packetSender.SendPositionUpdateAsync(
                joiningClient,
                existingSession.Username,
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

            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                joiningSession.Username,
                joiningSession.PositionX,
                joiningSession.PositionY,
                cancellationToken);
        }
    }
}
