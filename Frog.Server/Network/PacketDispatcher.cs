using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Application.Playtest;
using Frog.Core;
using Frog.Core.Character;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Models;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Logging;
using Frog.Server.Persistence;
using Frog.Server.Security;
using Frog.Server.Services;
using Frog.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher(
    AuthService authService,
    IAccountRepository accountRepository,
    IAuthSessionRepository authSessions,
    ConnectionManager connectionManager,
    ClientRegistry clientRegistry,
    MapService mapService,
    MovementService movementService,
    PacketSender packetSender,
    PlayerLifecycleNotifier playerLifecycleNotifier,
    ICharacterBootstrap characterBootstrap,
    ICharacterPayloadReader characterPayloadReader,
    ICharacterPayloadWriter characterPayloadWriter,
    IPlayerStateStore playerStateStore,
    IMapEventStore mapEventStore,
    CharacterGameplayService characterGameplay,
    InventoryGameplayService inventoryGameplay,
    CombatGameplayService combatGameplay,
    ShopBankGameplayService shopBankGameplay,
    PublishedCatalogService publishedCatalog,
    ChatRateLimiter chatRateLimiter,
    IOptions<PlaytestRuntimeOptions> playtestOptions,
    PlaytestAuthTokenGate playtestAuthTokenGate,
    ILogger<PacketDispatcher> logger)
{
    private readonly AuthService _authService = authService;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IAuthSessionRepository _authSessions = authSessions;
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly ClientRegistry _clientRegistry = clientRegistry;
    private readonly MapService _mapService = mapService;
    private readonly MovementService _movementService = movementService;
    private readonly PacketSender _packetSender = packetSender;
    private readonly PlayerLifecycleNotifier _playerLifecycleNotifier = playerLifecycleNotifier;
    private readonly ICharacterBootstrap _characterBootstrap = characterBootstrap;
    private readonly ICharacterPayloadReader _characterPayloadReader = characterPayloadReader;
    private readonly ICharacterPayloadWriter _characterPayloadWriter = characterPayloadWriter;
    private readonly IPlayerStateStore _playerStateStore = playerStateStore;
    private readonly IMapEventStore _mapEventStore = mapEventStore;
    private readonly CharacterGameplayService _characterGameplay = characterGameplay;
    private readonly InventoryGameplayService _inventoryGameplay = inventoryGameplay;
    private readonly CombatGameplayService _combatGameplay = combatGameplay;
    private readonly ShopBankGameplayService _shopBankGameplay = shopBankGameplay;
    private readonly PublishedCatalogService _publishedCatalog = publishedCatalog;
    private readonly ChatRateLimiter _chatRateLimiter = chatRateLimiter;
    private readonly PlaytestRuntimeOptions _playtest = playtestOptions.Value;
    private readonly PlaytestAuthTokenGate _playtestAuthTokenGate = playtestAuthTokenGate;
    private readonly ILogger<PacketDispatcher> _logger = logger;

    public async Task DispatchAsync(ClientSession clientSession, byte[] framePayload, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(BuildLogScope(clientSession)))
        {
            await DispatchCoreAsync(clientSession, framePayload, cancellationToken);
        }
    }

    private Dictionary<string, object?> BuildLogScope(ClientSession clientSession)
    {
        var scope = new Dictionary<string, object?>
        {
            ["ConnectionId"] = clientSession.ConnectionId,
            ["RemoteEndPoint"] = clientSession.RemoteEndPoint,
            ["Username"] = clientSession.Username ?? string.Empty
        };
        if (_playtest.Enabled)
        {
            scope["PlaytestCorrelationId"] = _playtest.CorrelationId;
            scope["PlaytestMapId"] = _playtest.PrimaryCanonicalMapId;
            scope["PlaytestPublishedRevision"] = _playtest.PrimaryPublishedRevision;
        }

        return scope;
    }

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

            case PacketId.PositionSyncRequest:
                await HandlePositionSyncRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.HeartbeatRequest:
                await HandleHeartbeatRequestAsync(clientSession, cancellationToken);
                break;

            case PacketId.LogoutRequest:
                await HandleLogoutRequestAsync(clientSession, cancellationToken);
                break;

            case PacketId.ReconnectRequest:
                await HandleReconnectRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.ChatSend:
                await HandleChatSendAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.MeleeAttackRequest:
                await HandleMeleeAttackRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.CharacterListRequest:
                await HandleCharacterListRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.CharacterSelectRequest:
                await HandleCharacterSelectRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.CharacterCreateRequest:
                await HandleCharacterCreateRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.CharacterStatsUpdateRequest:
                await HandleCharacterStatsUpdateRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.MapEventsRequest:
                await HandleMapEventsRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.InteractRequest:
                await HandleInteractRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.WorldFlagsPatchRequest:
                await HandleWorldFlagsPatchRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.EquipRequest:
                await HandleEquipRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.UnequipRequest:
                await HandleUnequipRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.DropItemRequest:
                await HandleDropItemRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.PickupItemRequest:
                await HandlePickupItemRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.SpellCastRequest:
                await HandleSpellCastRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.ShopBuyRequest:
                await HandleShopBuyRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.ShopSellRequest:
                await HandleShopSellRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.BankDepositRequest:
                await HandleBankDepositRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.BankWithdrawRequest:
                await HandleBankWithdrawRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.RespawnRequest:
                await HandleRespawnRequestAsync(clientSession, payload, cancellationToken);
                break;

            case PacketId.PublishedCatalogRequest:
                await HandlePublishedCatalogRequestAsync(clientSession, payload, cancellationToken);
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

        var isPlaytestUser = PlaytestAuthToken.IsReservedUsername(username);
        if (isPlaytestUser)
        {
            await HandlePlaytestLoginAsync(clientSession, password, cancellationToken).ConfigureAwait(false);
            return;
        }

        var authResult = await _authService.TryAuthenticateAsync(
            username,
            password,
            clientSession.RemoteEndPoint,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.Success || authResult.Account is null)
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

        session.AccountId = authResult.Account.Id;
        var issued = await _authSessions.IssueAsync(
            authResult.Account.Id,
            TimeSpan.FromHours(12),
            cancellationToken).ConfigureAwait(false);
        if (issued.Status != AuthSessionIssueStatus.Issued || issued.Session is null)
        {
            _connectionManager.RemoveSession(session.Id);
            ServerNetworkLogs.LoginFailed(_logger, "session_issue_failed");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Identifiants invalides.", cancellationToken);
            return;
        }

        session.AuthSessionId = issued.Session.Id;

        await CompleteLoginAsync(
            clientSession,
            username,
            playtestSpawn: false,
            successMessage: issued.Token ?? string.Empty,
            sendReconnectResult: false,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleReconnectRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (_playtest.Enabled)
        {
            await _packetSender.SendReconnectResultAsync(
                clientSession,
                false,
                "Reconnexion indisponible.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryParseReconnectPayload(payload.Span, out var token))
        {
            ServerNetworkLogs.LoginFailed(_logger, "invalid_reconnect_payload");
            await _packetSender.SendReconnectResultAsync(
                clientSession,
                false,
                "Session invalide.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_authService.TryAllowReconnect(clientSession.RemoteEndPoint))
        {
            ServerNetworkLogs.LoginFailed(_logger, "reconnect_rate_limited");
            await _packetSender.SendReconnectResultAsync(
                clientSession,
                false,
                "Session invalide.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var validation = await _authSessions.ValidateTokenAsync(token, cancellationToken).ConfigureAwait(false);
        if (validation.Status != AuthSessionValidationStatus.Valid || validation.Session is null)
        {
            _authService.RegisterReconnectFailure(clientSession.RemoteEndPoint);
            ServerNetworkLogs.LoginFailed(_logger, "invalid_reconnect_token");
            await _packetSender.SendReconnectResultAsync(
                clientSession,
                false,
                "Session invalide.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var account = await _accountRepository.FindByIdAsync(validation.Session.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            _authService.RegisterReconnectFailure(clientSession.RemoteEndPoint);
            ServerNetworkLogs.LoginFailed(_logger, "invalid_reconnect_token");
            await _packetSender.SendReconnectResultAsync(
                clientSession,
                false,
                "Session invalide.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        // Drop any still-registered TCP for this account before creating the new session.
        // Must Unregister: nulling AuthenticatedSession alone makes the old HandleClientAsync
        // skip cleanup, leaving a zombie in ClientRegistry. Later broadcasts then throw on the
        // disposed stream and tear down the *new* connection (gameplay smoke reconnect failure).
        if (_connectionManager.TryGetSessionByUsername(account.Username, out var existing)
            && existing is not null)
        {
            if (_clientRegistry.TryGet(existing.Id, out var oldClient) && oldClient is not null)
            {
                _clientRegistry.Unregister(existing.Id);
                oldClient.AuthenticatedSession = null;
                oldClient.Disconnect();
            }
        }

        if (!_connectionManager.TryDisplaceAndCreateSession(account.Username, out var session, out _)
            || session is null)
        {
            ServerNetworkLogs.LoginFailed(_logger, "already_connected");
            await _packetSender.SendReconnectResultAsync(
                clientSession,
                false,
                "Compte deja connecte.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        session.AccountId = account.Id;
        session.AuthSessionId = validation.Session.Id;
        await _authSessions.TouchAsync(validation.Session.Id, cancellationToken).ConfigureAwait(false);
        _authService.RegisterReconnectSuccess(clientSession.RemoteEndPoint);

        await CompleteLoginAsync(
            clientSession,
            account.Username,
            playtestSpawn: false,
            successMessage: token,
            sendReconnectResult: true,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandlePlaytestLoginAsync(
        ClientSession clientSession,
        string password,
        CancellationToken cancellationToken)
    {
        if (!_playtest.Enabled)
        {
            ServerNetworkLogs.LoginFailed(_logger, "invalid_credentials");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Identifiants invalides.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!_playtestAuthTokenGate.TryClaim(password))
        {
            ServerNetworkLogs.LoginFailed(_logger, "invalid_credentials");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Identifiants invalides.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!_connectionManager.TryCreateSession(PlaytestAuthToken.Username, out var session) || session is null)
        {
            _playtestAuthTokenGate.ReleaseClaim();
            ServerNetworkLogs.LoginFailed(_logger, "already_connected");
            await _packetSender.SendLoginResultAsync(clientSession, false, "Compte deja connecte.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var claimCommitted = false;
        try
        {
            await CompleteLoginAsync(
                    clientSession,
                    PlaytestAuthToken.Username,
                    playtestSpawn: true,
                    beforeSuccessfulLoginResult: () =>
                    {
                        _playtestAuthTokenGate.CommitClaim();
                        claimCommitted = true;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Après CommitClaim, ne jamais restaurer le jeton — nettoyer seulement la session.
            if (!claimCommitted)
            {
                _playtestAuthTokenGate.ReleaseClaim();
            }

            _clientRegistry.Unregister(session.Id);
            _connectionManager.RemoveSession(session.Id);
            clientSession.AuthenticatedSession = null;
            throw;
        }
    }

    private Task CompleteLoginAsync(
        ClientSession clientSession,
        string sessionName,
        bool playtestSpawn,
        CancellationToken cancellationToken)
        => CompleteLoginAsync(
            clientSession,
            sessionName,
            playtestSpawn,
            successMessage: "Connexion reussie.",
            sendReconnectResult: false,
            beforeSuccessfulLoginResult: null,
            cancellationToken);

    private async Task CompleteLoginAsync(
        ClientSession clientSession,
        string sessionName,
        bool playtestSpawn,
        string successMessage,
        bool sendReconnectResult,
        CancellationToken cancellationToken)
        => await CompleteLoginAsync(
            clientSession,
            sessionName,
            playtestSpawn,
            successMessage,
            sendReconnectResult,
            beforeSuccessfulLoginResult: null,
            cancellationToken).ConfigureAwait(false);

    private async Task CompleteLoginAsync(
        ClientSession clientSession,
        string sessionName,
        bool playtestSpawn,
        Action? beforeSuccessfulLoginResult,
        CancellationToken cancellationToken)
        => await CompleteLoginAsync(
            clientSession,
            sessionName,
            playtestSpawn,
            successMessage: "Connexion reussie.",
            sendReconnectResult: false,
            beforeSuccessfulLoginResult,
            cancellationToken).ConfigureAwait(false);

    private async Task CompleteLoginAsync(
        ClientSession clientSession,
        string sessionName,
        bool playtestSpawn,
        string successMessage,
        bool sendReconnectResult,
        Action? beforeSuccessfulLoginResult,
        CancellationToken cancellationToken)
    {
        if (!_connectionManager.TryGetSessionByUsername(sessionName, out var session) || session is null)
        {
            throw new InvalidOperationException("Session playtest/login introuvable après création.");
        }

        clientSession.AuthenticatedSession = session;
        var isPlaytestAccount = playtestSpawn
            || (_playtest.Enabled && PlaytestAuthToken.IsReservedUsername(sessionName));
        if (isPlaytestAccount)
        {
            session.CharacterId = _characterBootstrap.EnsureDefaultHero(sessionName);
        }
        else if (session.AccountId != Guid.Empty)
        {
            session.CharacterId = null;
            session.CharacterGuid = null;
        }
        else
        {
            session.CharacterId = _characterBootstrap.EnsureDefaultHero(sessionName);
        }

        var mapAtLoginStart = session.CurrentMapId;

        PlayerWorldState world = default;
        var persistOk = !string.IsNullOrWhiteSpace(session.CharacterId) &&
            _playerStateStore.TryGetForCharacter(session.CharacterId, out world);

        if (playtestSpawn)
        {
            session.CurrentMapId = _playtest.SpawnRuntimeMapId > 0
                ? _playtest.SpawnRuntimeMapId
                : MapService.DefaultWorldMapId;
            _mapService.TryEnsureMapLoaded(session.CurrentMapId);
            SessionPixelSync.SetTileCenter(session, _playtest.SpawnTileX, _playtest.SpawnTileY);
            _logger.LogInformation(
                "Playtest spawn correlation={CorrelationId} mapRuntime={MapId} tile=({X},{Y}) publishedRev={Revision}",
                _playtest.CorrelationId,
                session.CurrentMapId,
                _playtest.SpawnTileX,
                _playtest.SpawnTileY,
                _playtest.PrimaryPublishedRevision);
        }
        else
        {
            session.CurrentMapId = persistOk ? world.MapId : MapService.DefaultWorldMapId;

            var usePersistedPose = persistOk;

            if (!_mapService.TryEnsureMapLoaded(session.CurrentMapId))
            {
                session.CurrentMapId = MapService.DefaultWorldMapId;
                _mapService.TryEnsureMapLoaded(session.CurrentMapId);
                usePersistedPose = false;
            }

            if (usePersistedPose)
            {
                session.PixelX = world.X;
                session.PixelY = world.Y;
            }
            else
            {
                SessionPixelSync.SetTileCenter(session, 0, 0);
            }
        }

        ClampSessionPixelsAndSyncTiles(session);

        _clientRegistry.Register(session.Id, clientSession);
        _connectionManager.TryTouchSession(session.Id);

        // Consommer le jeton avant d’exposer un LoginResult positif (évite réutilisation si déconnexion / échec post-login).
        beforeSuccessfulLoginResult?.Invoke();

        if (sendReconnectResult)
        {
            await _packetSender.SendReconnectResultAsync(clientSession, true, successMessage, cancellationToken);
        }
        else
        {
            await _packetSender.SendLoginResultAsync(clientSession, true, successMessage, cancellationToken);
        }
        ServerNetworkLogs.LoginSucceeded(_logger, sessionName);

        if (playtestSpawn && _playtest.FailAfterSuccessfulLoginResult)
        {
            throw new InvalidOperationException("playtest-injected-fail-after-login-result");
        }

        await TrySendPublishedCatalogAsync(clientSession, cancellationToken).ConfigureAwait(false);
        await TrySendCharacterPayloadAsync(clientSession, session, cancellationToken);

        await SyncPositionsOnJoinAsync(clientSession, session, cancellationToken);

        if (session.CurrentMapId != mapAtLoginStart)
        {
            ReleasePageTriggerForPreviousMap(session, mapAtLoginStart);
        }

        await TryFirePageMapEventsAsync(clientSession, session, cancellationToken);
    }

    private async Task HandleRegisterRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryParseLoginPayload(payload.Span, out var username, out var password))
        {
            ServerNetworkLogs.RegisterFailed(_logger, "invalid_payload");
            await _packetSender.SendRegisterResultAsync(clientSession, false, "Payload inscription invalide.", cancellationToken);
            return;
        }

        if (PlaytestAuthToken.IsReservedUsername(username))
        {
            ServerNetworkLogs.RegisterFailed(_logger, "reserved_username");
            await _packetSender.SendRegisterResultAsync(
                clientSession,
                false,
                "Nom d'utilisateur réservé au playtest.",
                cancellationToken);
            return;
        }

        var created = await _authService.RegisterAccountAsync(username, password, cancellationToken).ConfigureAwait(false);
        if (created.Status != AccountCreateStatus.Created)
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

        // Copier les hints avant tout await : ReadOnlySpan ne peut pas vivre dans une méthode async (C# 12).
        if (!TryReadMapRequestFingerprint(payload, out var hasFingerprint, out var clientRevision, out var clientSha256))
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

        if (hasFingerprint &&
            _mapService.TryMatchMapFingerprint(requestedMapId, clientRevision, clientSha256))
        {
            await _packetSender.SendMapAlreadySyncedAsync(
                clientSession,
                requestedMapId,
                _mapService.GetFingerprintRevision(requestedMapId),
                _mapService.GetFingerprintSha256(requestedMapId),
                cancellationToken);
            return;
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

    private async Task HandleMapEventsRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!payload.IsEmpty)
        {
            await _packetSender.SendErrorAsync(clientSession, "MapEventsRequest: corps vide attendu.", cancellationToken);
            return;
        }

        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        var mapId = session.CurrentMapId;
        if (!_mapEventStore.TryGetEventsWireJson(mapId, out var json) || string.IsNullOrWhiteSpace(json))
        {
            json = "[]";
        }

        await _packetSender.SendMapEventsResultAsync(clientSession, mapId, json, cancellationToken);
    }

    private async Task HandleInteractRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!payload.IsEmpty)
        {
            await _packetSender.SendErrorAsync(clientSession, "InteractRequest: corps vide attendu.", cancellationToken);
            return;
        }

        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);

        if (!_mapEventStore.TryGetPlacements(session.CurrentMapId, out var placements))
        {
            placements = Array.Empty<MapEventWireEntry>();
        }

        var here = placements
            .Where(p => p.TileX == session.PositionX && p.TileY == session.PositionY)
            .Where(p => MapEventTriggerNormalization.NormalizeTriggerKind(p.TriggerKind) == MapEventTriggerKinds.Interact)
            .ToList();
        if (here.Count == 0)
        {
            await _packetSender.SendInteractResultAsync(clientSession, false, "Rien a interagir ici.", cancellationToken);
            return;
        }

        var ev = here.OrderBy(p => p.CatalogId).ThenBy(p => p.PlacementId).First();
        ServerNetworkLogs.MapEventInteractFired(
            _logger,
            session.Username,
            session.CurrentMapId,
            session.PositionX,
            session.PositionY,
            ev.Slug,
            ev.PlacementId);
        await _packetSender.SendInteractResultAsync(
            clientSession,
            true,
            $"{ev.DisplayName} ({ev.Slug})",
            cancellationToken);
    }

    private async Task HandleMoveRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!session.MovementPacketRateGate.TryConsume(DateTime.UtcNow))
        {
            ServerNetworkLogs.MovementRateLimited(_logger, session.Username);
            await _packetSender.SendErrorAsync(clientSession, "Trop de mouvements.", cancellationToken);
            return;
        }

        if (!TryParseMovePayload(payload.Span, out var deltaX, out var deltaY))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload mouvement invalide.", cancellationToken);
            return;
        }

        var cellBefore = (session.CurrentMapId, session.PositionX, session.PositionY);

        if (!_movementService.TryApplyMove(session, deltaX, deltaY, out var error))
        {
            await _packetSender.SendErrorAsync(clientSession, error, cancellationToken);
            return;
        }

        _movementService.TryApplyWarpAfterMove(session);
        _connectionManager.TryTouchSession(session.Id);
        ServerNetworkLogs.MoveApplied(_logger, session.Username, session.PixelX, session.PixelY);
        var clients = _clientRegistry.GetAllAuthenticatedClients();
        foreach (var targetClient in clients)
        {
            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                session.Username,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY,
                cancellationToken);
        }

        var cellAfter = (session.CurrentMapId, session.PositionX, session.PositionY);
        if (cellAfter != cellBefore)
        {
            session.MapEventAutoTileLastFiredUtc.Clear();
        }

        if (cellBefore.CurrentMapId != cellAfter.CurrentMapId)
        {
            _combatGameplay.CancelForMapChange(session);
            ReleasePageTriggerForPreviousMap(session, cellBefore.CurrentMapId);
            await TryFirePageMapEventsAsync(clientSession, session, cancellationToken);
        }

        if (cellAfter != cellBefore)
        {
            await TryFireStepOnMapEventsAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandlePositionSyncRequestAsync(ClientSession clientSession, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!session.MovementPacketRateGate.TryConsume(DateTime.UtcNow))
        {
            ServerNetworkLogs.MovementRateLimited(_logger, session.Username);
            await _packetSender.SendErrorAsync(clientSession, "Trop de mouvements.", cancellationToken);
            return;
        }

        if (!TryParsePositionSyncPayload(payload.Span, out var px, out var py))
        {
            await _packetSender.SendErrorAsync(clientSession, "PositionSyncRequest: 8 octets attendus (Int32 centre X,Y LE).", cancellationToken);
            return;
        }

        var cellBefore = (session.CurrentMapId, session.PositionX, session.PositionY);

        if (!_movementService.TryApplyReportedPixelPosition(session, px, py, out var error))
        {
            await _packetSender.SendErrorAsync(clientSession, error, cancellationToken);
            return;
        }

        _movementService.TryApplyWarpAfterMove(session);
        _connectionManager.TryTouchSession(session.Id);
        ServerNetworkLogs.MoveApplied(_logger, session.Username, session.PixelX, session.PixelY);
        foreach (var targetClient in _clientRegistry.GetAllAuthenticatedClients())
        {
            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                session.Username,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY,
                cancellationToken);
        }

        var cellAfter = (session.CurrentMapId, session.PositionX, session.PositionY);
        if (cellAfter != cellBefore)
        {
            session.MapEventAutoTileLastFiredUtc.Clear();
        }

        if (cellBefore.CurrentMapId != cellAfter.CurrentMapId)
        {
            _combatGameplay.CancelForMapChange(session);
            ReleasePageTriggerForPreviousMap(session, cellBefore.CurrentMapId);
            await TryFirePageMapEventsAsync(clientSession, session, cancellationToken);
        }

        if (cellAfter != cellBefore)
        {
            await TryFireStepOnMapEventsAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task TryFireStepOnMapEventsAsync(ClientSession clientSession, Session session, CancellationToken cancellationToken)
    {
        if (!_mapEventStore.TryGetPlacements(session.CurrentMapId, out var placements))
        {
            placements = Array.Empty<MapEventWireEntry>();
        }

        var here = placements
            .Where(p => p.TileX == session.PositionX && p.TileY == session.PositionY)
            .Where(p => MapEventTriggerNormalization.NormalizeTriggerKind(p.TriggerKind) == MapEventTriggerKinds.StepOn)
            .ToList();
        if (here.Count == 0)
        {
            return;
        }

        var ev = here.OrderBy(p => p.CatalogId).ThenBy(p => p.PlacementId).First();
        ServerNetworkLogs.MapEventStepOnFired(
            _logger,
            session.Username,
            session.CurrentMapId,
            session.PositionX,
            session.PositionY,
            ev.Slug,
            ev.PlacementId);
        await _packetSender.SendInteractResultAsync(
            clientSession,
            true,
            $"[Marche] {ev.DisplayName} ({ev.Slug})",
            cancellationToken);
    }

    private static void ReleasePageTriggerForPreviousMap(Session session, int previousMapId)
    {
        if (previousMapId == session.CurrentMapId)
        {
            return;
        }

        session.PageTriggerSatisfiedMapIds.Remove(previousMapId);
    }

    private async Task TryFirePageMapEventsAsync(ClientSession clientSession, Session session, CancellationToken cancellationToken)
    {
        if (session.PageTriggerSatisfiedMapIds.Contains(session.CurrentMapId))
        {
            return;
        }

        if (!_mapEventStore.TryGetPlacements(session.CurrentMapId, out var placements))
        {
            placements = Array.Empty<MapEventWireEntry>();
        }

        var here = placements
            .Where(p => p.TileX == session.PositionX && p.TileY == session.PositionY)
            .Where(p => MapEventTriggerNormalization.NormalizeTriggerKind(p.TriggerKind) == MapEventTriggerKinds.Page)
            .ToList();
        if (here.Count == 0)
        {
            session.PageTriggerSatisfiedMapIds.Add(session.CurrentMapId);
            return;
        }

        var ev = here.OrderBy(p => p.CatalogId).ThenBy(p => p.PlacementId).First();
        ServerNetworkLogs.MapEventPageFired(
            _logger,
            session.Username,
            session.CurrentMapId,
            session.PositionX,
            session.PositionY,
            ev.Slug,
            ev.PlacementId);
        await _packetSender.SendInteractResultAsync(
            clientSession,
            true,
            $"[Page] {ev.DisplayName} ({ev.Slug})",
            cancellationToken);
        session.PageTriggerSatisfiedMapIds.Add(session.CurrentMapId);
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
        await TryFireAutoTileMapEventsOnHeartbeatAsync(clientSession, session, cancellationToken);
    }

    private async Task TryFireAutoTileMapEventsOnHeartbeatAsync(
        ClientSession clientSession,
        Session session,
        CancellationToken cancellationToken)
    {
        if (!_mapEventStore.TryGetPlacements(session.CurrentMapId, out var placements))
        {
            placements = Array.Empty<MapEventWireEntry>();
        }

        var candidates = placements
            .Where(p => p.TileX == session.PositionX && p.TileY == session.PositionY)
            .Where(p => MapEventTriggerNormalization.NormalizeTriggerKind(p.TriggerKind) == MapEventTriggerKinds.AutoTile)
            .OrderBy(p => p.CatalogId)
            .ThenBy(p => p.PlacementId)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var ev in candidates)
        {
            if (session.MapEventAutoTileLastFiredUtc.TryGetValue(ev.PlacementId, out var last) &&
                now - last < MapEventAutoTileConstants.Cooldown)
            {
                continue;
            }

            session.MapEventAutoTileLastFiredUtc[ev.PlacementId] = now;
            ServerNetworkLogs.MapEventAutoTileFired(
                _logger,
                session.Username,
                session.CurrentMapId,
                session.PositionX,
                session.PositionY,
                ev.Slug,
                ev.PlacementId);
            await _packetSender.SendInteractResultAsync(
                clientSession,
                true,
                $"[Auto-tuile] {ev.DisplayName} ({ev.Slug})",
                cancellationToken);
            return;
        }
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
        if (!string.IsNullOrWhiteSpace(session.CharacterId))
        {
            _playerStateStore.UpsertForCharacter(
                session.CharacterId,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY);
        }

        if (session.AuthSessionId is Guid authSessionId)
        {
            await _authSessions.RevokeAsync(authSessionId, cancellationToken).ConfigureAwait(false);
        }

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

        if (attacker.IsDead)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                clientSession,
                false,
                string.Empty,
                "Personnage mort.",
                cancellationToken);
            return;
        }

        if (!TryParseMeleeTargetPayload(payload.Span, out var targetName))
        {
            await _packetSender.SendErrorAsync(clientSession, "Payload attaque melee invalide.", cancellationToken);
            return;
        }

        var monsterResult = await _combatGameplay.TryMeleeAttackMonsterAsync(attacker, targetName, cancellationToken)
            .ConfigureAwait(false);
        if (monsterResult.Success)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                clientSession,
                true,
                monsterResult.TargetName,
                monsterResult.Message,
                cancellationToken);
            if (monsterResult.MonsterKilled && monsterResult.ExperienceGained > 0)
            {
                await _packetSender.SendExperienceGainAsync(
                    clientSession,
                    monsterResult.ExperienceGained,
                    attacker.Level,
                    attacker.Experience,
                    cancellationToken);
            }

            await SendCombatStateAsync(clientSession, attacker, cancellationToken);
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

        var hit = MeleeCombat.IsWithinMeleeRange(attacker.PixelX, attacker.PixelY, defender.PixelX, defender.PixelY);
        if (!hit)
        {
            await _packetSender.SendMeleeAttackResultAsync(clientSession, false, targetName, "Hors portee.", cancellationToken);
            return;
        }

        var pvp = await _combatGameplay.TryMeleeAttackPlayerAsync(attacker, defender, cancellationToken)
            .ConfigureAwait(false);
        if (!pvp.Success)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                clientSession,
                false,
                targetName,
                pvp.Message,
                cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(attacker.Id);
        await _packetSender.SendMeleeAttackResultAsync(clientSession, true, targetName, pvp.Message, cancellationToken);
        if (_clientRegistry.TryGet(defender.Id, out var defenderClient) && defenderClient is not null)
        {
            await _packetSender.SendMeleeAttackResultAsync(
                defenderClient,
                true,
                attacker.Username,
                "Subi une attaque melee.",
                cancellationToken);
            await SendCombatStateAsync(defenderClient, defender, cancellationToken);
            if (pvp.TargetKilled)
            {
                await _packetSender.SendDeathNotifyAsync(defenderClient, cancellationToken);
            }
        }

        ServerNetworkLogs.MeleeResolved(_logger, attacker.Username, targetName, true);
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

        if (string.IsNullOrWhiteSpace(message))
        {
            await _packetSender.SendErrorAsync(clientSession, "Message vide.", cancellationToken);
            return;
        }

        if (!_chatRateLimiter.TryAllow(session.Id))
        {
            await _packetSender.SendErrorAsync(clientSession, "Trop de messages.", cancellationToken);
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
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        return AccountInputRules.IsValidUsername(username)
               && AccountInputRules.IsValidLoginPassword(password);
    }

    public static bool TryParseReconnectPayload(ReadOnlySpan<byte> payload, out string token)
    {
        token = string.Empty;
        if (payload.Length < sizeof(ushort))
        {
            return false;
        }

        var tokenLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (tokenLen is 0 or > AuthProtocolLimits.MaxAuthTokenUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != sizeof(ushort) + tokenLen)
        {
            return false;
        }

        token = Encoding.UTF8.GetString(payload.Slice(sizeof(ushort), tokenLen));
        return !string.IsNullOrWhiteSpace(token);
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

    /// <summary>2 × Int32 LE — centre joueur en pixels monde (<see cref="Frog.Core.Constants.WorldMetrics"/>).</summary>
    public static bool TryParsePositionSyncPayload(ReadOnlySpan<byte> payload, out int pixelX, out int pixelY)
    {
        pixelX = pixelY = 0;
        if (payload.Length != WorldMetrics.PositionSyncPayloadByteCount)
        {
            return false;
        }

        pixelX = BinaryPrimitives.ReadInt32LittleEndian(payload);
        pixelY = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(sizeof(int)));
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

    private async Task TrySendCharacterPayloadAsync(
        ClientSession clientSession,
        Frog.Server.Models.Session session,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.CharacterId))
        {
            return;
        }

        if (!_characterPayloadReader.TryGetPayloadJson(session.CharacterId!, out var payloadJson))
        {
            return;
        }

        try
        {
            await _packetSender.SendCharacterPayloadAsync(clientSession, session.CharacterId!, payloadJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'envoyer CharacterPayload pour {CharacterId}", session.CharacterId);
        }
    }

    private async Task HandleCharacterListRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!payload.IsEmpty)
        {
            await _packetSender.SendErrorAsync(clientSession, "CharacterListRequest: corps vide attendu.", cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        if (UsesAccountGameplay(session))
        {
            var characters = await _characterGameplay.ListAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
            var accountWire = characters
                .Select(static c => new CharacterListWireEntry { Id = c.Id.ToString(), Name = c.DisplayName })
                .ToArray();
            var accountJson = JsonSerializer.Serialize(accountWire);
            await _packetSender.SendCharacterListResultAsync(clientSession, accountJson, cancellationToken);
            return;
        }

        var list = _characterBootstrap.ListCharacters(session.Username);
        var wire = list.Select(static c => new CharacterListWireEntry { Id = c.Id, Name = c.DisplayName }).ToArray();
        var json = JsonSerializer.Serialize(wire);
        await _packetSender.SendCharacterListResultAsync(clientSession, json, cancellationToken);
    }

    private async Task HandleCharacterSelectRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        var mapIdBeforeSelect = session.CurrentMapId;

        if (!TryParseCharacterSelectRequest(payload.Span, out var newCharacterId))
        {
            await _packetSender.SendCharacterSelectResultAsync(
                clientSession,
                false,
                "CharacterSelectRequest: UUID perso invalide.",
                cancellationToken);
            return;
        }

        if (UsesAccountGameplay(session))
        {
            if (!Guid.TryParse(newCharacterId, out var characterGuid))
            {
                await _packetSender.SendCharacterSelectResultAsync(
                    clientSession,
                    false,
                    "UUID perso invalide.",
                    cancellationToken);
                return;
            }

            if (!await _characterGameplay.IsOwnedAsync(session.AccountId, characterGuid, cancellationToken).ConfigureAwait(false))
            {
                await _packetSender.SendCharacterSelectResultAsync(
                    clientSession,
                    false,
                    "Personnage inconnu pour ce compte.",
                    cancellationToken);
                return;
            }

            var record = await _characterGameplay.FindAsync(characterGuid, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                await _packetSender.SendCharacterSelectResultAsync(
                    clientSession,
                    false,
                    "Personnage introuvable.",
                    cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(session.CharacterId))
            {
                _playerStateStore.UpsertForCharacter(
                    session.CharacterId,
                    session.CurrentMapId,
                    session.PixelX,
                    session.PixelY);
            }

            session.ApplyFromCharacter(record);
            _mapService.TryEnsureMapLoaded(session.CurrentMapId);
            ClampSessionPixelsAndSyncTiles(session);
            _connectionManager.TryTouchSession(session.Id);
            await _packetSender.SendCharacterSelectResultAsync(clientSession, true, "Personnage actif.", cancellationToken);
            await SendGameplaySnapshotsAsync(clientSession, session, cancellationToken);
            await BroadcastPositionAfterSelectAsync(clientSession, session, mapIdBeforeSelect, cancellationToken);
            return;
        }

        if (!_characterBootstrap.IsCharacterOwned(session.Username, newCharacterId))
        {
            await _packetSender.SendCharacterSelectResultAsync(
                clientSession,
                false,
                "Personnage inconnu pour ce compte.",
                cancellationToken);
            return;
        }

        if (string.Equals(session.CharacterId, newCharacterId, StringComparison.OrdinalIgnoreCase))
        {
            await _packetSender.SendCharacterSelectResultAsync(
                clientSession,
                true,
                "Personnage deja actif.",
                cancellationToken);
            await TrySendCharacterPayloadAsync(clientSession, session, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(session.CharacterId))
        {
            _playerStateStore.UpsertForCharacter(
                session.CharacterId,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY);
        }

        session.CharacterId = newCharacterId;
        var persistOkCs = _playerStateStore.TryGetForCharacter(newCharacterId, out var worldCs);
        session.CurrentMapId = persistOkCs ? worldCs.MapId : MapService.DefaultWorldMapId;

        var usePersistedPoseCs = persistOkCs;

        if (!_mapService.TryEnsureMapLoaded(session.CurrentMapId))
        {
            session.CurrentMapId = MapService.DefaultWorldMapId;
            _mapService.TryEnsureMapLoaded(session.CurrentMapId);
            usePersistedPoseCs = false;
        }

        if (usePersistedPoseCs)
        {
            session.PixelX = worldCs.X;
            session.PixelY = worldCs.Y;
        }
        else
        {
            SessionPixelSync.SetTileCenter(session, 0, 0);
        }

        ClampSessionPixelsAndSyncTiles(session);
        _connectionManager.TryTouchSession(session.Id);
        await _packetSender.SendCharacterSelectResultAsync(clientSession, true, "Personnage actif.", cancellationToken);
        await TrySendCharacterPayloadAsync(clientSession, session, cancellationToken);

        foreach (var targetClient in _clientRegistry.GetAllAuthenticatedClients())
        {
            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                session.Username,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY,
                cancellationToken);
        }

        if (session.CurrentMapId != mapIdBeforeSelect)
        {
            ReleasePageTriggerForPreviousMap(session, mapIdBeforeSelect);
        }

        await TryFirePageMapEventsAsync(clientSession, session, cancellationToken);
    }

    /// <summary>Corps : longueur UUID UTF‑8 (1 octet) + identifiant (≤ <see cref="ChatProtocolLimits.MaxUsernameUtf8Bytes"/>).</summary>
    public static bool TryParseCharacterSelectRequest(ReadOnlySpan<byte> payload, out string characterId)
    {
        characterId = string.Empty;
        if (payload.Length < 2)
        {
            return false;
        }

        var len = payload[0];
        if (len is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes || payload.Length != 1 + len)
        {
            return false;
        }

        characterId = Encoding.UTF8.GetString(payload.Slice(1, len));
        return !string.IsNullOrWhiteSpace(characterId);
    }

    private async Task HandleCharacterCreateRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseCharacterCreateRequest(payload.Span, out var rawDisplayName, out var classId))
        {
            await _packetSender.SendCharacterCreateResultAsync(
                clientSession,
                false,
                "CharacterCreateRequest: nom invalide.",
                cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        if (UsesAccountGameplay(session))
        {
            var createClassId = classId ?? Phase7ContentSeed.DefaultClassId;
            var created = await _characterGameplay
                .CreateAsync(session.AccountId, rawDisplayName, createClassId, cancellationToken)
                .ConfigureAwait(false);
            if (created.Status != CharacterCreateStatus.Created || created.Character is null)
            {
                await _packetSender.SendCharacterCreateResultAsync(
                    clientSession,
                    false,
                    created.ErrorMessage ?? "Creation echouee.",
                    cancellationToken);
                return;
            }

            await _packetSender.SendCharacterCreateResultAsync(
                clientSession,
                true,
                created.Character.Id.ToString(),
                cancellationToken);
            return;
        }

        if (!_characterBootstrap.TryCreateCharacter(session.Username, rawDisplayName, out var newId, out var err))
        {
            await _packetSender.SendCharacterCreateResultAsync(clientSession, false, err, cancellationToken);
            return;
        }

        await _packetSender.SendCharacterCreateResultAsync(clientSession, true, newId, cancellationToken);
    }

    private async Task HandleCharacterStatsUpdateRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.CharacterId))
        {
            await _packetSender.SendCharacterStatsUpdateResultAsync(
                clientSession,
                false,
                "Aucun personnage actif.",
                cancellationToken);
            return;
        }

        if (!TryCopyCharacterStatsUpdateRequest(payload, out var packedBytes))
        {
            await _packetSender.SendCharacterStatsUpdateResultAsync(
                clientSession,
                false,
                "CharacterStatsUpdateRequest: 6 octets STR..LUCK (1-99) attendus.",
                cancellationToken);
            return;
        }

        if (!_characterBootstrap.IsCharacterOwned(session.Username, session.CharacterId))
        {
            await _packetSender.SendCharacterStatsUpdateResultAsync(
                clientSession,
                false,
                "Personnage non autorise.",
                cancellationToken);
            return;
        }

        if (!_characterPayloadReader.TryGetPayloadJson(session.CharacterId!, out var currentJson))
        {
            currentJson = CharacterPayloadDefaults.NewHeroJson;
        }

        if (!CharacterStatsWire.TryMergeIntoPayload(currentJson, packedBytes, out var newJson, out var mergeErr))
        {
            await _packetSender.SendCharacterStatsUpdateResultAsync(clientSession, false, mergeErr, cancellationToken);
            return;
        }

        if (!_characterPayloadWriter.TryUpdatePayloadJson(session.CharacterId!, newJson))
        {
            await _packetSender.SendCharacterStatsUpdateResultAsync(
                clientSession,
                false,
                "Sauvegarde stats refusee.",
                cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        await _packetSender.SendCharacterStatsUpdateResultAsync(clientSession, true, "Stats mises a jour.", cancellationToken);
        await TrySendCharacterPayloadAsync(clientSession, session, cancellationToken);
    }

    /// <summary>
    /// MapRequest : vide (resync complet) ou 40 octets (Int64 LE révision + SHA-256 sur 32 octets).
    /// </summary>
    public static bool TryReadMapRequestFingerprint(
        ReadOnlyMemory<byte> payload,
        out bool hasFingerprint,
        out long clientRevision,
        out byte[] clientSha256)
    {
        hasFingerprint = false;
        clientRevision = 0;
        clientSha256 = Array.Empty<byte>();

        if (payload.IsEmpty)
        {
            return true;
        }

        if (payload.Length != 40)
        {
            return false;
        }

        var span = payload.Span;
        clientRevision = BinaryPrimitives.ReadInt64LittleEndian(span);
        clientSha256 = span.Slice(sizeof(long), 32).ToArray();
        hasFingerprint = true;
        return true;
    }

    /// <summary>Copie validée des 6 octets de stats (safe pour méthodes async).</summary>
    public static bool TryCopyCharacterStatsUpdateRequest(ReadOnlyMemory<byte> payload, out byte[] packedStats)
    {
        packedStats = Array.Empty<byte>();
        if (!TryParseCharacterStatsUpdateRequest(payload.Span, out var span))
        {
            return false;
        }

        packedStats = span.ToArray();
        return true;
    }

    /// <summary>Corps : exactement 6 octets STR, AGI, DEX, INT, VIT, LUCK (valeurs 1–99).</summary>
    public static bool TryParseCharacterStatsUpdateRequest(ReadOnlySpan<byte> payload, out ReadOnlySpan<byte> packedStats)
    {
        packedStats = ReadOnlySpan<byte>.Empty;
        if (payload.Length != CharacterStatsWire.PackedByteCount)
        {
            return false;
        }

        if (!CharacterStatsWire.TryValidatePacked(payload, out _))
        {
            return false;
        }

        packedStats = payload;
        return true;
    }

    /// <summary>Corps : longueur nom UTF‑8 (1 octet) + nom (+ Guid classe 16 octets optionnel).</summary>
    public static bool TryParseCharacterCreateRequest(ReadOnlySpan<byte> payload, out string displayName)
        => TryParseCharacterCreateRequest(payload, out displayName, out _);

    public static bool TryParseCharacterCreateRequest(ReadOnlySpan<byte> payload, out string displayName, out Guid? classId)
    {
        displayName = string.Empty;
        classId = null;
        if (payload.Length < 2)
        {
            return false;
        }

        var len = payload[0];
        if (len is 0 or > CharacterDisplayNameRules.MaxWireUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != 1 + len && payload.Length != 1 + len + 16)
        {
            return false;
        }

        displayName = Encoding.UTF8.GetString(payload.Slice(1, len));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        if (payload.Length == 1 + len + 16)
        {
            classId = new Guid(payload.Slice(1 + len, 16));
        }

        return true;
    }

    /// <summary>Corps : longueur JSON UTF‑8 (<see cref="ushort"/> LE) + objet (booléens uniquement, ≤ <see cref="CharacterPayloadWorldFlags.MaxPatchUtf8Bytes"/> octets).</summary>
    public static bool TryParseWorldFlagsPatchPayload(ReadOnlySpan<byte> payload, out string patchJson)
    {
        patchJson = string.Empty;
        if (payload.Length < 2)
        {
            return false;
        }

        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (len is 0 or > CharacterPayloadWorldFlags.MaxPatchUtf8Bytes || payload.Length != 2 + len)
        {
            return false;
        }

        patchJson = Encoding.UTF8.GetString(payload.Slice(2, len));
        return true;
    }

    private async Task HandleWorldFlagsPatchRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.CharacterId))
        {
            await _packetSender.SendWorldFlagsPatchResultAsync(
                clientSession,
                false,
                "Aucun personnage actif.",
                cancellationToken);
            return;
        }

        if (!TryParseWorldFlagsPatchPayload(payload.Span, out var patchJson))
        {
            await _packetSender.SendWorldFlagsPatchResultAsync(
                clientSession,
                false,
                "WorldFlagsPatchRequest: UInt16 LE + UTF-8 JSON attendu.",
                cancellationToken);
            return;
        }

        if (!_characterBootstrap.IsCharacterOwned(session.Username, session.CharacterId!))
        {
            await _packetSender.SendWorldFlagsPatchResultAsync(
                clientSession,
                false,
                "Personnage non autorise.",
                cancellationToken);
            return;
        }

        _connectionManager.TryTouchSession(session.Id);
        if (!_characterPayloadReader.TryGetPayloadJson(session.CharacterId!, out var currentJson))
        {
            currentJson = CharacterPayloadDefaults.NewHeroJson;
        }

        if (!CharacterPayloadWorldFlags.TryMergeWorldFlags(currentJson, patchJson, out var merged, out var mergeErr))
        {
            await _packetSender.SendWorldFlagsPatchResultAsync(clientSession, false, mergeErr, cancellationToken);
            return;
        }

        if (!_characterPayloadWriter.TryUpdatePayloadJson(session.CharacterId!, merged))
        {
            await _packetSender.SendWorldFlagsPatchResultAsync(
                clientSession,
                false,
                "Sauvegarde drapeaux refusee.",
                cancellationToken);
            return;
        }

        await _packetSender.SendWorldFlagsPatchResultAsync(clientSession, true, "worldFlags mis a jour.", cancellationToken);
        ServerNetworkLogs.WorldFlagsPatched(_logger, session.Username, session.CharacterId!);
        await TrySendCharacterPayloadAsync(clientSession, session, cancellationToken);
    }

    private async Task SyncPositionsOnJoinAsync(ClientSession joiningClient, Frog.Server.Models.Session joiningSession, CancellationToken cancellationToken)
    {
        // Le client initialise sa position locale depuis ce paquet (sans attendre CharacterSelect).
        await _packetSender.SendPositionUpdateAsync(
            joiningClient,
            joiningSession.Username,
            joiningSession.CurrentMapId,
            joiningSession.PixelX,
            joiningSession.PixelY,
            cancellationToken);

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
                existingSession.PixelX,
                existingSession.PixelY,
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
                joiningSession.PixelX,
                joiningSession.PixelY,
                cancellationToken);
        }
    }

    private void ClampSessionPixelsAndSyncTiles(Session session)
    {
        var ts = WorldMetrics.DefaultTileSizePixels;
        if (_mapService.TryGetMapBounds(session.CurrentMapId, out var mw, out var mh))
        {
            var maxPx = mw * ts - 1;
            var maxPy = mh * ts - 1;
            session.PixelX = Math.Clamp(session.PixelX, 0, maxPx);
            session.PixelY = Math.Clamp(session.PixelY, 0, maxPy);
        }

        SessionPixelSync.SyncTileFromPixels(session, ts);
        session.LastPositionSyncUtc = DateTime.UtcNow;
    }
}
