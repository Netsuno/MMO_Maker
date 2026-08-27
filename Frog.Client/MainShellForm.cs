using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Frog.Client.Assets;
using Frog.Client.Controls;
using Frog.Client.Network;
using Frog.Client.UI;
using Frog.Application.Playtest;
using Frog.Core.Character;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client;

public sealed class MainShellForm : Form
{
    private enum ClientUiPhase
    {
        Login,
        CharacterSelect,
        Playing,
    }

    private ClientUiPhase _phase = ClientUiPhase.Login;
    private bool _awaitingPlayingPhase;

    private readonly Panel _hostPages = new() { Dock = DockStyle.Fill };
    private readonly Panel _panelLogin = new() { Dock = DockStyle.Fill, Padding = new Padding(32), AutoScroll = true };
    private readonly Panel _panelCharacter = new() { Dock = DockStyle.Fill, Padding = new Padding(32), AutoScroll = true, Visible = false };
    private readonly Panel _panelGame = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Button _btnSwitchCharacter = new() { Text = "Changer de personnage", AutoSize = true };
    private readonly Button _btnBackDisconnect = new() { Text = "Retour à la connexion (fermer la session)", AutoSize = true, Enabled = false };

    private FrogGameClient? _client;
    private Map? _map;
    private string? _username;
    private int _srvPixelX;
    private int _srvPixelY;
    private readonly ConcurrentDictionary<string, OtherPlayerView> _others = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Position affichée locale : prédiction continue + réconciliation avec la position réseau autoritaire.</summary>
    private float _visLocalCx;

    private float _visLocalCy;

    private bool _localVisualInitialized;

    /// <summary>Dernier échantillon UTC pour le lissage mouvement (client seulement).</summary>
    private DateTime _motionSmoothLastUtc;

    private readonly System.Windows.Forms.Timer _smoothTimer = new() { Interval = 16 };

    /// <summary>Fond carte (aligné <see cref="MapViewRenderer"/> secours) pour éviter flash si le bitmap est court.</summary>
    private static readonly Color MapSurfaceBackColor = Color.FromArgb(60, 90, 60);

    private sealed class OtherPlayerView
    {
        public int ServerPixelX;

        public int ServerPixelY;

        public float VisCx;

        public float VisCy;

        public bool Initialized;
    }
    private int _sessionDisplayedMapId;
    private DateTime _lastAutoMapRequestUtc = DateTime.MinValue;
    private static readonly TimeSpan AutoMapRequestDebounce = TimeSpan.FromMilliseconds(300);
    private readonly Dictionary<int, Bitmap> _tilesetBitmaps = new();
    /// <summary>Envoi périodique <see cref="FrogGameClient.SendPositionSyncAsync"/> (protocole ≥ 8) : centre prédit en pixels.</summary>
    private DateTime _lastMoveSendUtc = DateTime.MinValue;
    private bool _pendingIdlePositionSync;
    private DateTime _lastInteractUtc = DateTime.MinValue;

    /// <summary>Touches direction maintenues (prédiction client + boucle réseau).</summary>
    private bool _holdLeft;

    private bool _holdRight;

    private bool _holdUp;

    private bool _holdDown;

    private HashSet<(int X, int Y)>? _mapBlockedTiles;

    private readonly List<MapEventWireEntry> _mapEvents = new();
    private readonly TextBox _txtHost = new() { Text = "127.0.0.1", Width = 120 };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 6000, Width = 70 };
    private readonly TextBox _txtUser = new() { Text = "demo", Width = 100 };
    private readonly TextBox _txtPass = new() { Text = "demo", Width = 100, UseSystemPasswordChar = true };
    private readonly Button _btnConnect = new() { Text = "Connecter" };
    private readonly Button _btnDisconnect = new() { Text = "Déconnecter", Enabled = false };
    private readonly Button _btnLogin = new() { Text = "Login", Enabled = false };
    private readonly Button _btnRegister = new() { Text = "Inscription", Enabled = false };
    private readonly Button _btnReconnect = new() { Text = "Reconnecter (jeton)", Enabled = false };
    private readonly Label _lblAuthStatus = new() { AutoSize = true, Text = "Jeton: aucun", Margin = new Padding(4, 12, 4, 4) };
    private string? _storedAuthToken;
    private readonly Button _btnMap = new() { Text = "Demander map", Enabled = false };
    private readonly Button _btnLogout = new() { Text = "Logout", Enabled = false };
    private readonly ComboBox _cmbCharacters = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Enabled = false };
    private readonly Button _btnCharRefresh = new() { Text = "Liste persos", Width = 95, Enabled = false };
    private readonly Button _btnEnterGame = new() { Text = "Entrer dans le jeu", Width = 220, Enabled = false };
    private readonly TextBox _txtNewCharName = new() { Width = 100, PlaceholderText = "Nouveau perso" };
    private readonly Button _btnCharCreate = new() { Text = "Créer perso", Width = 95, Enabled = false };
    private readonly ComboBox _cmbClass = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Enabled = false };
    private readonly ComboBox _cmbShop = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Enabled = false };
    private readonly ComboBox _cmbShopItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Enabled = false };
    private readonly ComboBox _cmbSpell = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Enabled = false };
    private PublishedCatalogWire? _publishedCatalog;
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 72, Dock = DockStyle.Bottom };
    private readonly Panel _mapScroll = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = MapSurfaceBackColor };
    private readonly PictureBox _picMap = new()
    {
        Location = new Point(0, 0),
        SizeMode = PictureBoxSizeMode.AutoSize,
        BackColor = MapSurfaceBackColor,
    };
    private readonly TextBox _txtChat = new() { Dock = DockStyle.Fill };
    private readonly Button _btnSendChat = new() { Text = "Envoyer chat", Dock = DockStyle.Bottom, Height = 28 };
    private readonly ComboBox _cmbChannel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly TextBox _txtWhisperTo = new() { PlaceholderText = "Cible whisper", Width = 120 };
    /// <summary>Cible mêlée/sort : ComboBox éditable (P7-G5) peuplée des noms PNJ/monstre publiés (défaut « Slime » si présent), texte libre toujours possible.</summary>
    private readonly ComboBox _cmbMeleeTarget = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 120 };
    private readonly Button _btnMelee = new() { Text = "Mêlée", Enabled = false };
    private readonly Button _btnSpell = new() { Text = "Sort", Enabled = false };
    private readonly Button _btnRespawn = new() { Text = "Respawn", Enabled = false, Visible = false };
    private readonly Label _lblCombat = new() { AutoSize = true, Text = "Combat: —", Margin = new Padding(4, 8, 4, 4) };
    private readonly InventoryPanel _inventoryPanel = new() { Dock = DockStyle.Fill, MinimumSize = new Size(200, 80) };
    private readonly EquipmentPanel _equipmentPanel = new() { Dock = DockStyle.Top, MinimumSize = new Size(200, 72) };
    private readonly TabControl _gameplayTabs = new() { Dock = DockStyle.Fill, MinimumSize = new Size(300, 0) };
    private readonly TabPage _tabChat = new("Chat") { Padding = new Padding(4) };
    private readonly TabPage _tabGameplay = new("Gameplay") { Padding = new Padding(4) };
    private readonly TextBox _txtShopId = new() { Width = 220, PlaceholderText = "Shop Guid (secours)", Visible = false };
    private readonly TextBox _txtShopItemId = new() { Width = 220, PlaceholderText = "Item Guid (secours)", Visible = false };
    private readonly NumericUpDown _numShopQty = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 48 };
    private readonly Button _btnShopBuy = new() { Text = "Acheter", AutoSize = true, Enabled = false };
    private readonly Button _btnShopSell = new() { Text = "Vendre slot", AutoSize = true, Enabled = false };
    /// <summary>Emplacement banque interne (P7-G5) : piloté par la sélection dans <see cref="_lstBank"/>, plus affiché en brut.</summary>
    private readonly NumericUpDown _numBankSlot = new() { Minimum = 0, Maximum = 39, Width = 48, Visible = false };
    private readonly NumericUpDown _numBankQty = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 48 };
    private readonly Button _btnBankDepositItem = new() { Text = "Banque dépôt", AutoSize = true, Enabled = false };
    private readonly Button _btnBankWithdrawItem = new() { Text = "Banque retrait", AutoSize = true, Enabled = false };
    private readonly NumericUpDown _numBankGold = new() { Minimum = 1, Maximum = 999999, Value = 10, Width = 64 };
    private readonly Button _btnBankDepositGold = new() { Text = "Dépôt or", AutoSize = true, Enabled = false };
    private readonly Button _btnBankWithdrawGold = new() { Text = "Retrait or", AutoSize = true, Enabled = false };
    private readonly Label _lblBank = new() { AutoSize = true, Text = "Banque: —", Margin = new Padding(4, 4, 4, 4) };
    /// <summary>Liste banque nommée (P7-G5) : sélectionner une ligne fixe <see cref="_numBankSlot"/> pour retrait.</summary>
    private readonly ListBox _lstBank = new() { Dock = DockStyle.Fill, IntegralHeight = false, Height = 70 };
    private BankSnapshotWire? _bankSnapshot;
    /// <summary>Liste objets au sol nommée (P7-G5) + ramassage.</summary>
    private readonly ListBox _lstGround = new() { Dock = DockStyle.Fill, IntegralHeight = false, Height = 70 };
    private readonly Button _btnPickup = new() { Text = "Ramasser", AutoSize = true, Enabled = false };
    private GroundItemsSnapshotWire? _groundSnapshot;
    private readonly Button _btnWorldFlagsDemo = new() { Text = "Drapeau démo (worldFlags)", Enabled = false };
    private readonly NumericUpDown[] _numStats = new NumericUpDown[CharacterStatsWire.PackedByteCount];
    private readonly Button _btnStatsApply = new() { Text = "Appliquer stats", AutoSize = true, Enabled = false };
    private readonly System.Windows.Forms.Timer _heartbeatTimer = new() { Interval = 45_000 };

    /// <summary>Fréquence d’envoi position au serveur (aligné prédiction locale ~52 ms).</summary>
    private const int MoveNetworkPulseMs = 52;

    private readonly ClientPlaytestOptions? _playtestOptions;
    private readonly PlaytestClientReadyState _playtestReady = new();
    private bool _playtestLoginOk;

    /// <summary>Dernier <see cref="CombatStateWire"/> reçu (ForTest : HP/mort observables sans re-parcourir le log).</summary>
    private CombatStateWire? _lastCombatState;

    public MainShellForm()
        : this(null)
    {
    }

    internal MainShellForm(ClientPlaytestOptions? playtestOptions)
    {
        _playtestOptions = playtestOptions;
        AutoScaleMode = AutoScaleMode.Font;
        Text = "FRoG — Frog Isle";
        ClientSize = new Size(1040, 720);
        MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;
        BuildLayout();
        _inventoryPanel.ItemNameLookup = ResolveItemName;
        _equipmentPanel.ItemNameLookup = ResolveItemName;
        if (_playtestOptions is { IsPlaytest: true })
        {
            _txtHost.Text = _playtestOptions.Host;
            _numPort.Value = Math.Clamp(_playtestOptions.Port, 1, 65535);
            Text = string.IsNullOrWhiteSpace(_playtestOptions.CorrelationId)
                ? "FRoG — Playtest"
                : $"FRoG — Playtest [{_playtestOptions.CorrelationId}]";
        }

        EnableDoubleBuffer(_mapScroll);
        EnableDoubleBuffer(_picMap);
        _smoothTimer.Tick += SmoothTimer_OnTick;
        _smoothTimer.Start();
        _cmbChannel.Items.AddRange(new object[] { "Global", "Map", "Whisper" });
        _cmbChannel.SelectedIndex = 1;
        _cmbShop.SelectedIndexChanged += (_, _) => RefreshShopItemCombo();
        _cmbShopItem.SelectedIndexChanged += (_, _) => SyncShopGuidTextBoxes();
        _heartbeatTimer.Tick += async (_, _) => await SendHeartbeatSafeAsync();
        Load += MainShell_Load;
        FormClosing += async (_, _) => await MainShell_FormClosingAsync();
        KeyDown += MainShell_KeyDown;
        KeyUp += MainShell_KeyUp;
        Deactivate += (_, _) =>
        {
            ReleaseAllMoveKeys();
            ScheduleIdlePositionSyncIfAllReleased();
        };

        ApplyPhaseUi();
    }

    private static Label TitleLbl(string text, float emSize = 14f)
    {
        var family = SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily;
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(family, emSize, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
        };
    }

    private void ApplyPhaseUi()
    {
        _panelLogin.Visible = _phase == ClientUiPhase.Login;
        _panelCharacter.Visible = _phase == ClientUiPhase.CharacterSelect;
        _panelGame.Visible = _phase == ClientUiPhase.Playing;

        if (_phase == ClientUiPhase.Login)
        {
            _panelLogin.BringToFront();
        }
        else if (_phase == ClientUiPhase.CharacterSelect)
        {
            _panelCharacter.BringToFront();
        }
        else
        {
            _panelGame.BringToFront();
        }
    }

    private void SetPhase(ClientUiPhase p)
    {
        _phase = p;
        ApplyPhaseUi();
        if (InvokeRequired)
        {
            return;
        }

        Activate();
        Focus();
    }

    private void GoToCharacterSelectPhase()
    {
        ReleaseAllMoveKeys();
        _awaitingPlayingPhase = false;
        _btnMelee.Enabled = false;
        _btnWorldFlagsDemo.Enabled = false;
        SetGameplayControlsEnabled(false);
        SetPhase(ClientUiPhase.CharacterSelect);
        _ = RefreshCharacterListAsync();
    }

    private void TryEnterPlayingPhaseAfterMapReady()
    {
        if (!_awaitingPlayingPhase || _map is null)
        {
            return;
        }

        _awaitingPlayingPhase = false;
        _btnMelee.Enabled = true;
        _btnWorldFlagsDemo.Enabled = true;
        SetGameplayControlsEnabled(true);
        _gameplayTabs.SelectedTab = _tabGameplay;
        SetPhase(ClientUiPhase.Playing);
    }

    private void MainShell_Load(object? sender, EventArgs e)
    {
        var ctx = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _client = new FrogGameClient(ctx);
        WireClient();
        if (_playtestOptions is { IsPlaytest: true })
        {
            AppendLog(
                $"[playtest] host={_playtestOptions.Host} port={_playtestOptions.Port} correlation={_playtestOptions.CorrelationId ?? "-"}");
            BeginInvoke(async () => await RunPlaytestAutoStartAsync().ConfigureAwait(true));
        }
    }

    /// <summary>
    /// Playtest : connecte, authentifie via jeton éphémère (jamais loggé), charge la carte, signale readiness stdout.
    /// </summary>
    private async Task RunPlaytestAutoStartAsync()
    {
        if (_playtestOptions is not { IsPlaytest: true } || _client is null)
        {
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(_playtestOptions.PlaytestToken))
            {
                EmitPlaytestFailure("jeton playtest manquant");
                return;
            }

            await ConnectAsync().ConfigureAwait(true);
            if (!_client.IsConnected)
            {
                EmitPlaytestFailure("connexion TCP échouée");
                return;
            }

            // Attendre Hello (envoyé par le serveur à la connexion) avant login.
            for (var i = 0; i < 50 && _client.IsConnected; i++)
            {
                await Task.Delay(40).ConfigureAwait(true);
            }

            if (!_client.IsConnected)
            {
                EmitPlaytestFailure("déconnecté pendant Hello (version protocole ?)");
                return;
            }

            await _client.SendLoginAsync("__frog_playtest__", _playtestOptions.PlaytestToken!)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            EmitPlaytestFailure(ex.Message);
        }
    }

    private void EmitPlaytestFailure(string message)
    {
        var safe = message.Replace('\r', ' ').Replace('\n', ' ');
        if (!string.IsNullOrEmpty(_playtestOptions?.PlaytestToken))
        {
            safe = safe.Replace(_playtestOptions.PlaytestToken, "***", StringComparison.Ordinal);
        }

        AppendLog("[playtest] FAIL: " + safe);
        try
        {
            Console.Error.WriteLine("FROG_PLAYTEST_FAIL " + safe);
            Console.Error.Flush();
        }
        catch
        {
            // ignore
        }
    }

    private void TryEmitPlaytestReady()
    {
        if (_playtestReady.ReadyEmitted || _playtestOptions is not { IsPlaytest: true })
        {
            return;
        }

        _playtestReady.LoginOk = _playtestLoginOk;
        _playtestReady.MapLoaded = _map is not null;

        if (!Guid.TryParseExact(_playtestOptions.CorrelationId ?? string.Empty, "N", out var corr)
            && !Guid.TryParse(_playtestOptions.CorrelationId, out corr))
        {
            EmitPlaytestFailure("correlation manquante pour READY");
            return;
        }

        if (!_playtestReady.TryBuildReadyLine(corr, out var line, out var failureReason))
        {
            if (!string.IsNullOrEmpty(failureReason)
                && failureReason.Contains("map-mismatch", StringComparison.OrdinalIgnoreCase))
            {
                EmitPlaytestFailure(failureReason);
            }

            return;
        }

        try
        {
            Console.Out.WriteLine(line);
            Console.Out.Flush();
        }
        catch
        {
            // ignore
        }

        AppendLog(line!);
        _playtestReady.ReadyEmitted = true;
    }

    private async Task MainShell_FormClosingAsync()
    {
        _smoothTimer.Stop();
        _heartbeatTimer.Stop();
        if (_client is not null)
        {
            await _client.DisconnectAsync().ConfigureAwait(true);
            _client.Dispose();
        }

        _smoothTimer.Dispose();
    }

    private void SmoothTimer_OnTick(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SmoothTimer_OnTick(sender, e));
            return;
        }

        if (_phase != ClientUiPhase.Playing || _map is null)
        {
            return;
        }

        if (AdvanceMovementSmoothing())
        {
            RedrawMap();
        }

        TrySendHeldMoveNetwork();
    }

    private void ResetLocalMotionState()
    {
        _localVisualInitialized = false;
        _motionSmoothLastUtc = default;
        _pendingIdlePositionSync = false;
        ReleaseAllMoveKeys();
    }

    private void ReleaseAllMoveKeys()
    {
        _holdLeft = _holdRight = _holdUp = _holdDown = false;
    }

    private bool TryGetHeldMoveNormalized(out float vx, out float vy)
    {
        vx = (_holdRight ? 1 : 0) - (_holdLeft ? 1 : 0);
        vy = (_holdDown ? 1 : 0) - (_holdUp ? 1 : 0);
        if (vx == 0 && vy == 0)
        {
            return false;
        }

        var len = MathF.Sqrt(vx * vx + vy * vy);
        vx /= len;
        vy /= len;
        return true;
    }

    private bool TryGetHeldMoveDiscrete(out sbyte dx, out sbyte dy)
    {
        dx = (sbyte)((_holdRight ? 1 : 0) - (_holdLeft ? 1 : 0));
        dy = (sbyte)((_holdDown ? 1 : 0) - (_holdUp ? 1 : 0));
        return dx != 0 || dy != 0;
    }

    private void ClampLocalVisToMap()
    {
        if (_map is null)
        {
            return;
        }

        var tw = WorldMetrics.DefaultTileSizePixels;
        var maxX = _map.Width * tw - 1f;
        var maxY = _map.Height * tw - 1f;
        _visLocalCx = Math.Clamp(_visLocalCx, 0f, maxX);
        _visLocalCy = Math.Clamp(_visLocalCy, 0f, maxY);
    }

    private bool IsPredictedCenterBlocked(float cx, float cy)
    {
        if (_mapBlockedTiles is null || _map is null || _mapBlockedTiles.Count == 0)
        {
            return false;
        }

        var ix = (int)MathF.Round(cx);
        var iy = (int)MathF.Round(cy);
        return MapCollision.IsBlockedForPlayerCircle(
            _map,
            _mapBlockedTiles,
            ix,
            iy,
            WorldMetrics.PlayerCollisionRadiusPixels);
    }

    /// <summary>Avance la prédiction avec la même cible que le serveur (~8 px par « tick » réseau) et glissement le long des murs.</summary>
    private void TryApplyLocalPredictStep(float pvx, float pvy, float dt)
    {
        var speedPxPerSec = WorldMetrics.PlayerMovePixelsPerRequest / (MoveNetworkPulseMs / 1000f);
        var dx = pvx * speedPxPerSec * dt;
        var dy = pvy * speedPxPerSec * dt;

        var nx = _visLocalCx + dx;
        var ny = _visLocalCy + dy;
        if (!IsPredictedCenterBlocked(nx, ny))
        {
            _visLocalCx = nx;
            _visLocalCy = ny;
            return;
        }

        nx = _visLocalCx + dx;
        ny = _visLocalCy;
        if (!IsPredictedCenterBlocked(nx, ny))
        {
            _visLocalCx = nx;
            return;
        }

        nx = _visLocalCx;
        ny = _visLocalCy + dy;
        if (!IsPredictedCenterBlocked(nx, ny))
        {
            _visLocalCy = ny;
        }
    }

    private void TrySendHeldMoveNetwork()
    {
        if (_client is null || !_client.IsConnected || string.IsNullOrEmpty(_username) || !_localVisualInitialized)
        {
            return;
        }

        var holding = TryGetHeldMoveDiscrete(out _, out _);
        if (!holding && !_pendingIdlePositionSync)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastMoveSendUtc).TotalMilliseconds < MoveNetworkPulseMs)
        {
            return;
        }

        _lastMoveSendUtc = now;
        if (_pendingIdlePositionSync && !holding)
        {
            _pendingIdlePositionSync = false;
        }

        var px = (int)Math.Round(_visLocalCx);
        var py = (int)Math.Round(_visLocalCy);
        _ = SendPositionSyncBurstAsync(px, py);
    }

    private async Task SendPositionSyncBurstAsync(int pixelCenterX, int pixelCenterY)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendPositionSyncAsync(pixelCenterX, pixelCenterY).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("PositionSync: " + ex.Message);
        }
    }

    private void ScheduleIdlePositionSyncIfAllReleased()
    {
        if (_holdLeft || _holdRight || _holdUp || _holdDown)
        {
            return;
        }

        _pendingIdlePositionSync = true;
        PrimeMoveNetworkPulse();
    }

    private static void EnableDoubleBuffer(Control control)
    {
        typeof(Control).InvokeMember(
            "DoubleBuffered",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty,
            null,
            control,
            [true]);
    }

    private bool AdvanceMovementSmoothing()
    {
        if (_map is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (_motionSmoothLastUtc == default)
        {
            _motionSmoothLastUtc = now;
        }

        var dt = (float)(now - _motionSmoothLastUtc).TotalSeconds;
        _motionSmoothLastUtc = now;
        if (dt <= 0 || dt > 0.25f)
        {
            dt = 1f / 60f;
        }

        const float convergencePerSec = 17f;
        var alpha = 1f - MathF.Exp(-convergencePerSec * dt);
        const float snapEps = 0.18f;
        const float moveEps = 0.006f;

        var dirty = false;

        if (_localVisualInitialized && !string.IsNullOrEmpty(_username) && _map is not null)
        {
            var hasMove = TryGetHeldMoveNormalized(out var pvx, out var pvy);
            if (hasMove)
            {
                TryApplyLocalPredictStep(pvx, pvy, dt);
                ClampLocalVisToMap();
                dirty = true;
            }

            var ex = _visLocalCx - _srvPixelX;
            var ey = _visLocalCy - _srvPixelY;
            var errMag = MathF.Sqrt(ex * ex + ey * ey);
            // Référence serveur mise à jour par PositionUpdate ; ne pas tirer le joueur local vers elle (évite rollback).
            const float snapDesyncPx = 256f;
            if (errMag > snapDesyncPx)
            {
                _visLocalCx = _srvPixelX;
                _visLocalCy = _srvPixelY;
                dirty = true;
            }
        }

        foreach (var kv in _others.ToArray())
        {
            var o = kv.Value;
            if (!o.Initialized)
            {
                continue;
            }

            var tx = (float)o.ServerPixelX;
            var ty = (float)o.ServerPixelY;
            var nx = o.VisCx + (tx - o.VisCx) * alpha;
            var ny = o.VisCy + (ty - o.VisCy) * alpha;
            if (MathF.Abs(tx - nx) <= snapEps && MathF.Abs(ty - ny) <= snapEps)
            {
                nx = tx;
                ny = ty;
            }

            if (MathF.Abs(nx - o.VisCx) > moveEps || MathF.Abs(ny - o.VisCy) > moveEps)
            {
                o.VisCx = nx;
                o.VisCy = ny;
                dirty = true;
            }
        }

        return dirty;
    }

    private static void StyleToolbarButton(Button b)
    {
        b.AutoSize = true;
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.MinimumSize = new Size(96, 30);
        b.Padding = new Padding(10, 4, 10, 4);
        b.Margin = new Padding(4, 4, 4, 4);
    }

    private static FlowLayoutPanel CreateToolbarRow()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 2),
            Padding = new Padding(0),
        };
    }

    private static Label Lbl(string text, int topPad = 8)
        => new()
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(4, topPad, 4, 4),
        };

    /// <summary>Empile des contrôles en colonne dans un panneau (écran login / perso).</summary>
    private static void AddStackToPanel(Panel panel, params Control[] sections)
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = panel.BackColor,
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 560));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            BackColor = panel.BackColor,
        };
        foreach (var section in sections)
        {
            section.Margin = new Padding(0, 0, 0, 12);
            flow.Controls.Add(section);
        }

        var host = new Panel { Dock = DockStyle.Fill, AutoSize = false, BackColor = panel.BackColor };
        host.Controls.Add(flow);
        outer.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        outer.Controls.Add(host, 1, 0);
        outer.Controls.Add(new Panel { Dock = DockStyle.Fill }, 2, 0);

        panel.Controls.Clear();
        panel.Controls.Add(outer);
    }

    private void BuildLayout()
    {
        StyleToolbarButton(_btnConnect);
        StyleToolbarButton(_btnDisconnect);
        StyleToolbarButton(_btnLogin);
        StyleToolbarButton(_btnRegister);
        StyleToolbarButton(_btnReconnect);
        StyleToolbarButton(_btnMap);
        StyleToolbarButton(_btnLogout);
        StyleToolbarButton(_btnCharRefresh);
        StyleToolbarButton(_btnEnterGame);
        StyleToolbarButton(_btnCharCreate);
        StyleToolbarButton(_btnMelee);
        StyleToolbarButton(_btnSpell);
        StyleToolbarButton(_btnRespawn);
        StyleToolbarButton(_btnShopBuy);
        StyleToolbarButton(_btnShopSell);
        StyleToolbarButton(_btnBankDepositItem);
        StyleToolbarButton(_btnBankWithdrawItem);
        StyleToolbarButton(_btnBankDepositGold);
        StyleToolbarButton(_btnBankWithdrawGold);
        StyleToolbarButton(_btnPickup);
        StyleToolbarButton(_btnWorldFlagsDemo);
        StyleToolbarButton(_btnStatsApply);
        StyleToolbarButton(_btnBackDisconnect);
        StyleToolbarButton(_btnSwitchCharacter);
        BackColor = SystemColors.Control;
        _panelLogin.BackColor = Color.FromArgb(245, 248, 252);
        _panelCharacter.BackColor = Color.FromArgb(245, 248, 252);
        _panelGame.BackColor = SystemColors.Control;
        foreach (Panel p in new[] { _panelLogin, _panelCharacter })
        {
            p.AutoScroll = true;
        }

        _cmbCharacters.MinimumSize = new Size(220, 0);
        _cmbCharacters.Width = Math.Max(_cmbCharacters.Width, 320);
        _txtNewCharName.MinimumSize = new Size(120, 0);
        _txtNewCharName.Width = Math.Max(_txtNewCharName.Width, 140);
        foreach (Control c in new Control[] { _txtHost, _txtUser, _txtPass })
        {
            c.Margin = new Padding(2, 4, 12, 4);
        }

        _numPort.Margin = new Padding(2, 4, 12, 4);
        _cmbMeleeTarget.Margin = new Padding(2, 4, 8, 4);

        var loginFields = CreateToolbarRow();
        loginFields.FlowDirection = FlowDirection.LeftToRight;
        loginFields.WrapContents = true;
        loginFields.AutoSize = true;
        loginFields.Controls.Add(Lbl("Hôte", topPad: 16));
        loginFields.Controls.Add(_txtHost);
        loginFields.Controls.Add(Lbl("Port", topPad: 16));
        loginFields.Controls.Add(_numPort);
        loginFields.Controls.Add(Lbl("Compte", topPad: 16));
        loginFields.Controls.Add(_txtUser);
        loginFields.Controls.Add(Lbl("Mot de passe", topPad: 16));
        loginFields.Controls.Add(_txtPass);

        var loginBtns = CreateToolbarRow();
        loginBtns.WrapContents = true;
        loginBtns.Controls.Add(_btnConnect);
        loginBtns.Controls.Add(_btnDisconnect);
        loginBtns.Controls.Add(_btnLogin);
        loginBtns.Controls.Add(_btnRegister);
        loginBtns.Controls.Add(_btnReconnect);

        AddStackToPanel(_panelLogin, TitleLbl("Connexion"), loginFields, loginBtns, _lblAuthStatus);

        var rowCharPick = CreateToolbarRow();
        rowCharPick.WrapContents = true;
        rowCharPick.Controls.Add(Lbl("Personnage", topPad: 16));
        rowCharPick.Controls.Add(_cmbCharacters);
        rowCharPick.Controls.Add(_btnCharRefresh);
        rowCharPick.Controls.Add(_btnEnterGame);

        var rowCreate = CreateToolbarRow();
        rowCreate.WrapContents = true;
        rowCreate.Controls.Add(Lbl("Nouveau personnage", topPad: 16));
        rowCreate.Controls.Add(_txtNewCharName);
        rowCreate.Controls.Add(Lbl("Classe", topPad: 16));
        rowCreate.Controls.Add(_cmbClass);
        rowCreate.Controls.Add(_btnCharCreate);

        var rowStats = CreateToolbarRow();
        var statLabels = new[] { "STR", "AGI", "DEX", "INT", "VIT", "LUCK" };
        rowStats.WrapContents = true;
        rowStats.Controls.Add(Lbl("Stats", topPad: 16));
        for (var i = 0; i < _numStats.Length; i++)
        {
            _numStats[i] = new NumericUpDown
            {
                Minimum = CharacterStatsWire.MinStat,
                Maximum = CharacterStatsWire.MaxStat,
                Value = 10,
                Width = 48,
                Enabled = false,
                Margin = new Padding(2, 4, 8, 4),
            };
            rowStats.Controls.Add(Lbl(statLabels[i], topPad: 16));
            rowStats.Controls.Add(_numStats[i]);
        }

        rowStats.Controls.Add(_btnStatsApply);

        // Sécurité (P7-G1) : édition de stats en direct par le client retirée de l'UI Phase 7 —
        // le serveur rejette désormais CharacterStatsUpdateRequest hors playtest/AllowInMemoryFallback.
        rowStats.Visible = false;

        var rowCharNav = CreateToolbarRow();
        rowCharNav.WrapContents = true;
        rowCharNav.Controls.Add(_btnBackDisconnect);

        AddStackToPanel(_panelCharacter, TitleLbl("Choisir votre personnage"), rowCharPick, rowCreate, rowStats, rowCharNav);

        _mapScroll.Controls.Add(_picMap);

        var gameplayTab = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(4),
        };
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        gameplayTab.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        gameplayTab.Controls.Add(_lblCombat, 0, 0);
        gameplayTab.Controls.Add(_equipmentPanel, 0, 1);
        gameplayTab.Controls.Add(_inventoryPanel, 0, 2);
        var shopRow = CreateToolbarRow();
        shopRow.Controls.Add(Lbl("Shop"));
        shopRow.Controls.Add(_cmbShop);
        shopRow.Controls.Add(Lbl("Article"));
        shopRow.Controls.Add(_cmbShopItem);
        shopRow.Controls.Add(_numShopQty);
        shopRow.Controls.Add(_btnShopBuy);
        shopRow.Controls.Add(_btnShopSell);
        shopRow.Controls.Add(_txtShopId);
        shopRow.Controls.Add(_txtShopItemId);
        gameplayTab.Controls.Add(shopRow, 0, 3);
        var bankRow = CreateToolbarRow();
        bankRow.Controls.Add(Lbl("Slot"));
        bankRow.Controls.Add(_numBankSlot);
        bankRow.Controls.Add(_numBankQty);
        bankRow.Controls.Add(_btnBankDepositItem);
        bankRow.Controls.Add(_btnBankWithdrawItem);
        bankRow.Controls.Add(_numBankGold);
        bankRow.Controls.Add(_btnBankDepositGold);
        bankRow.Controls.Add(_btnBankWithdrawGold);
        gameplayTab.Controls.Add(bankRow, 0, 4);
        gameplayTab.Controls.Add(_lblBank, 0, 5);
        var bankListPanel = new Panel { Dock = DockStyle.Fill };
        bankListPanel.Controls.Add(_lstBank);
        gameplayTab.Controls.Add(bankListPanel, 0, 6);
        var groundPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        groundPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        groundPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var groundTop = CreateToolbarRow();
        groundTop.Controls.Add(Lbl("Objets au sol", topPad: 0));
        groundTop.Controls.Add(_btnPickup);
        groundPanel.Controls.Add(groundTop, 0, 0);
        groundPanel.Controls.Add(_lstGround, 0, 1);
        gameplayTab.Controls.Add(groundPanel, 0, 7);

        var tabRight = _gameplayTabs;
        tabRight.TabPages.Clear();
        tabRight.TabPages.Add(_tabChat);
        tabRight.TabPages.Add(_tabGameplay);
        var tabChat = _tabChat;
        var tabGameplay = _tabGameplay;

        var rightChat = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(6),
            MinimumSize = new Size(300, 0),
        };
        rightChat.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightChat.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightChat.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var chatTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
        };
        chatTop.Controls.Add(_cmbChannel);
        _txtWhisperTo.MinimumSize = new Size(140, 0);
        chatTop.Controls.Add(_txtWhisperTo);
        rightChat.Controls.Add(chatTop, 0, 0);
        _txtChat.Dock = DockStyle.Fill;
        _txtChat.MinimumSize = new Size(160, 60);
        rightChat.Controls.Add(_txtChat, 0, 1);
        _btnSendChat.AutoSize = false;
        _btnSendChat.Dock = DockStyle.Fill;
        _btnSendChat.MinimumSize = new Size(160, 32);
        _btnSendChat.Padding = new Padding(12, 6, 12, 6);
        _btnSendChat.Margin = new Padding(0, 4, 0, 0);
        rightChat.Controls.Add(_btnSendChat, 0, 2);
        tabChat.Controls.Add(rightChat);
        tabGameplay.Controls.Add(gameplayTab);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        center.Controls.Add(_mapScroll, 0, 0);
        center.Controls.Add(tabRight, 1, 0);

        var gameTop = CreateToolbarRow();
        gameTop.WrapContents = true;
        gameTop.Controls.Add(_btnMap);
        gameTop.Controls.Add(_btnSwitchCharacter);
        gameTop.Controls.Add(_btnLogout);
        gameTop.Controls.Add(Lbl("Cible"));
        gameTop.Controls.Add(_cmbMeleeTarget);
        gameTop.Controls.Add(_btnMelee);
        gameTop.Controls.Add(_cmbSpell);
        gameTop.Controls.Add(_btnSpell);
        gameTop.Controls.Add(_btnRespawn);
        gameTop.Controls.Add(_btnWorldFlagsDemo);
        gameTop.Controls.Add(Lbl("Flèches = déplacement · E = interagir", topPad: 14));

        var gameLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6, 4, 6, 6),
            BackColor = SystemColors.Control,
        };
        gameLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gameLayout.Controls.Add(gameTop, 0, 0);
        gameLayout.Controls.Add(center, 0, 1);
        _panelGame.Controls.Clear();
        _panelGame.Controls.Add(gameLayout);

        _hostPages.BackColor = SystemColors.Control;
        _hostPages.Controls.Add(_panelGame);
        _hostPages.Controls.Add(_panelCharacter);
        _hostPages.Controls.Add(_panelLogin);

        _txtLog.Dock = DockStyle.Bottom;
        _txtLog.MinimumSize = new Size(120, 88);
        _txtLog.Height = 100;

        ClientSize = new Size(Math.Max(ClientSize.Width, 1040), Math.Max(ClientSize.Height, 720));

        Controls.Add(_hostPages);
        Controls.Add(_txtLog);
    }

    private void WireClient()
    {
        if (_client is null)
        {
            return;
        }

        _btnConnect.Click += async (_, _) => await ConnectAsync();
        _btnDisconnect.Click += async (_, _) => await DisconnectAsync();
        _btnLogin.Click += async (_, _) => await LoginAsync();
        _btnRegister.Click += async (_, _) => await RegisterAsync();
        _btnReconnect.Click += async (_, _) => await ReconnectAsync();
        _btnMap.Click += async (_, _) => await MapRequestAsync();
        _btnLogout.Click += async (_, _) => await LogoutAsync();
        _btnSendChat.Click += async (_, _) => await SendChatAsync();
        _btnMelee.Click += async (_, _) => await MeleeAsync();
        _btnSpell.Click += async (_, _) => await SpellCastAsync();
        _btnRespawn.Click += async (_, _) => await RespawnAsync();
        _btnShopBuy.Click += async (_, _) => await ShopBuyAsync();
        _btnShopSell.Click += async (_, _) => await ShopSellAsync();
        _btnBankDepositItem.Click += async (_, _) => await BankDepositItemAsync();
        _btnBankWithdrawItem.Click += async (_, _) => await BankWithdrawItemAsync();
        _btnBankDepositGold.Click += async (_, _) => await BankDepositGoldAsync();
        _btnBankWithdrawGold.Click += async (_, _) => await BankWithdrawGoldAsync();
        _btnPickup.Click += async (_, _) => await PickupSelectedGroundItemAsync();
        _lstBank.SelectedIndexChanged += (_, _) =>
        {
            if (_lstBank.SelectedItem is BankRow row)
            {
                _numBankSlot.Value = Math.Clamp(row.SlotIndex, (int)_numBankSlot.Minimum, (int)_numBankSlot.Maximum);
            }
        };
        _inventoryPanel.EquipRequested += slot => _ = EquipSlotAsync(slot);
        _inventoryPanel.DropRequested += (slot, qty) => _ = DropItemAsync(slot, qty);
        _equipmentPanel.UnequipRequested += slot => _ = UnequipSlotAsync(slot);
        _btnWorldFlagsDemo.Click += async (_, _) => await SendWorldFlagsDemoPatchAsync();
        _btnSwitchCharacter.Click += (_, _) => GoToCharacterSelectPhase();
        _btnBackDisconnect.Click += async (_, _) => await DisconnectAsync();

        _client.HelloReceived += msg => AppendLog("Hello: " + msg);
        _client.LoginResultReceived += OnLoginResult;
        _client.RegisterResultReceived += (ok, msg) => AppendLog(ok ? "Inscription OK: " + msg : "Inscription: " + msg);
        _client.MapDataReceived += OnMapData;
        _client.MapAlreadySyncedReceived += OnMapAlreadySynced;
        _client.CharacterPayloadReceived += OnCharacterPayload;
        _client.PositionUpdateReceived += OnPositionUpdate;
        _client.PlayerLeaveReceived += OnPlayerLeave;
        _client.ErrorReceived += err =>
        {
            AppendLog("Erreur: " + err);
            if (_playtestOptions is { IsPlaytest: true } && !_playtestReady.ReadyEmitted)
            {
                EmitPlaytestFailure(err);
            }
        };
        _client.HeartbeatAckReceived += () => { };
        _client.LogoutAckReceived += OnLogoutAck;
        _client.ChatMessageReceived += OnChatMessage;
        _client.MeleeAttackResultReceived += (hit, tgt, msg) =>
            AppendLog($"Mêlée → {tgt}: {(hit ? "touche" : "rate")} — {msg}");
        _client.CharacterListReceived += OnCharacterListJson;
        _client.CharacterSelectResultReceived += OnCharacterSelectResult;
        _client.CharacterCreateResultReceived += OnCharacterCreateResult;
        _client.CharacterStatsUpdateResultReceived += OnCharacterStatsUpdateResult;
        _client.MapEventsResultReceived += OnMapEventsResult;
        _client.InteractResultReceived += OnInteractResult;
        _client.WorldFlagsPatchResultReceived += OnWorldFlagsPatchResult;
        _client.ReconnectResultReceived += OnReconnectResult;
        _client.InventorySnapshotReceived += OnInventorySnapshot;
        _client.EquipResultReceived += (ok, msg) => AppendLog(ok ? "Équipement: " + msg : "Équipement refusé: " + msg);
        _client.UnequipResultReceived += (ok, msg) => AppendLog(ok ? "Déséquipement: " + msg : "Déséquipement refusé: " + msg);
        _client.DropItemResultReceived += (ok, msg) => AppendLog(ok ? "Drop: " + msg : "Drop refusé: " + msg);
        _client.PickupItemResultReceived += (ok, msg) => AppendLog(ok ? "Ramassé: " + msg : "Ramassé refusé: " + msg);
        _client.GroundItemsSnapshotReceived += OnGroundItemsSnapshot;
        _client.SpellCastResultReceived += (ok, msg) => AppendLog(ok ? "Sort: " + msg : "Sort refusé: " + msg);
        _client.CombatStateReceived += OnCombatState;
        _client.ShopBuyResultReceived += (ok, msg) => AppendLog(ok ? "Achat: " + msg : "Achat refusé: " + msg);
        _client.ShopSellResultReceived += (ok, msg) => AppendLog(ok ? "Vente: " + msg : "Vente refusée: " + msg);
        _client.BankDepositResultReceived += (ok, msg) => AppendLog(ok ? "Banque dépôt: " + msg : "Banque dépôt refusé: " + msg);
        _client.BankWithdrawResultReceived += (ok, msg) => AppendLog(ok ? "Banque retrait: " + msg : "Banque retrait refusé: " + msg);
        _client.BankSnapshotReceived += OnBankSnapshot;
        _client.RespawnResultReceived += (ok, msg) => AppendLog(ok ? "Respawn: " + msg : "Respawn refusé: " + msg);
        _client.ExperienceGainReceived += gain =>
            AppendLog($"XP +{gain.Amount} (niv {gain.Level}, total {gain.Experience})");
        _client.DeathNotifyReceived += () => AppendLog("Mort signalée par le serveur.");
        _client.PublishedCatalogReceived += OnPublishedCatalogReceived;
        _client.ConnectionClosed += OnConnectionClosed;
        _btnCharRefresh.Click += async (_, _) => await RefreshCharacterListAsync();
        _btnEnterGame.Click += async (_, _) => await ApplySelectedCharacterAsync();
        _btnCharCreate.Click += async (_, _) => await CreateCharacterAsync();
        _btnStatsApply.Click += async (_, _) => await ApplyCharacterStatsAsync();
    }

    private void UpdateAuthTokenUi()
    {
        var hasToken = !string.IsNullOrWhiteSpace(_storedAuthToken);
        _btnReconnect.Enabled = hasToken && _client is { IsConnected: true };
        _lblAuthStatus.Text = hasToken ? "Jeton: stocké (reconnect possible)" : "Jeton: aucun";
    }


    private void OnPublishedCatalogReceived(PublishedCatalogWire catalog)
    {
        _publishedCatalog = catalog;
        ApplyCatalogToUi(catalog);
        AppendLog(
            $"Catalogue: {catalog.Classes.Count} classe(s), {catalog.Items.Count} objet(s), {catalog.Spells.Count} sort(s), {catalog.Shops.Count} boutique(s).");
    }

    private void ApplyCatalogToUi(PublishedCatalogWire catalog)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ApplyCatalogToUi(catalog));
            return;
        }

        _cmbClass.Items.Clear();
        foreach (var entry in catalog.Classes)
        {
            if (Guid.TryParse(entry.Id, out var classId))
            {
                _cmbClass.Items.Add(new ClassPickRow(classId, entry.Name));
            }
        }

        if (_cmbClass.Items.Count > 0)
        {
            _cmbClass.SelectedIndex = 0;
        }

        _cmbShop.Items.Clear();
        foreach (var entry in catalog.Shops)
        {
            if (Guid.TryParse(entry.Id, out var shopId))
            {
                _cmbShop.Items.Add(new ShopPickRow(shopId, entry.Name, entry.ItemIds));
            }
        }

        if (_cmbShop.Items.Count > 0)
        {
            _cmbShop.SelectedIndex = 0;
        }

        RefreshShopItemCombo();

        _cmbSpell.Items.Clear();
        foreach (var entry in catalog.Spells)
        {
            if (Guid.TryParse(entry.Id, out var spellId))
            {
                _cmbSpell.Items.Add(new SpellPickRow(spellId, entry.Name));
            }
        }

        if (_cmbSpell.Items.Count > 0)
        {
            _cmbSpell.SelectedIndex = 0;
        }

        _cmbMeleeTarget.Items.Clear();
        foreach (var npc in catalog.Npcs)
        {
            if (!string.IsNullOrWhiteSpace(npc.Name) && !_cmbMeleeTarget.Items.Contains(npc.Name))
            {
                _cmbMeleeTarget.Items.Add(npc.Name);
            }
        }

        if (string.IsNullOrWhiteSpace(_cmbMeleeTarget.Text))
        {
            var defaultTarget = catalog.Npcs.FirstOrDefault(n =>
                    string.Equals(n.Name, "Slime", StringComparison.OrdinalIgnoreCase))?.Name
                ?? catalog.Npcs.FirstOrDefault()?.Name;
            if (!string.IsNullOrWhiteSpace(defaultTarget))
            {
                _cmbMeleeTarget.Text = defaultTarget;
            }
        }

        if (_btnCharCreate.Enabled)
        {
            _cmbClass.Enabled = _cmbClass.Items.Count > 0;
        }

        if (_btnShopBuy.Enabled)
        {
            _cmbShop.Enabled = _cmbShop.Items.Count > 0;
            _cmbShopItem.Enabled = _cmbShopItem.Items.Count > 0;
            _cmbSpell.Enabled = _cmbSpell.Items.Count > 0;
        }
    }

    private void RefreshShopItemCombo()
    {
        _cmbShopItem.Items.Clear();
        if (_cmbShop.SelectedItem is not ShopPickRow shop || _publishedCatalog is null)
        {
            SyncShopGuidTextBoxes();
            return;
        }

        var itemsById = _publishedCatalog.Items.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var itemId in shop.ItemIds)
        {
            if (!itemsById.TryGetValue(itemId, out var item))
            {
                continue;
            }

            if (Guid.TryParse(item.Id, out var parsedId))
            {
                _cmbShopItem.Items.Add(new ItemPickRow(parsedId, item.Name, item.Type));
            }
        }

        if (_cmbShopItem.Items.Count > 0)
        {
            _cmbShopItem.SelectedIndex = 0;
        }

        SyncShopGuidTextBoxes();
    }

    private void SyncShopGuidTextBoxes()
    {
        if (_cmbShop.SelectedItem is ShopPickRow shop)
        {
            _txtShopId.Text = shop.Id.ToString("D");
        }

        if (_cmbShopItem.SelectedItem is ItemPickRow item)
        {
            _txtShopItemId.Text = item.Id.ToString("D");
        }
    }

    private bool TryResolveShopSelection(out Guid shopId, out Guid itemId)
    {
        shopId = Guid.Empty;
        itemId = Guid.Empty;
        if (_cmbShop.SelectedItem is ShopPickRow shop && _cmbShopItem.SelectedItem is ItemPickRow item)
        {
            shopId = shop.Id;
            itemId = item.Id;
            return true;
        }

        return Guid.TryParse(_txtShopId.Text.Trim(), out shopId)
               && Guid.TryParse(_txtShopItemId.Text.Trim(), out itemId);
    }

    private bool TryResolveSpellSelection(out Guid spellId)
    {
        spellId = Guid.Empty;
        if (_cmbSpell.SelectedItem is SpellPickRow spell)
        {
            spellId = spell.Id;
            return true;
        }

        return false;
    }

    private void SetGameplayControlsEnabled(bool enabled)
    {
        _btnShopBuy.Enabled = enabled;
        _btnShopSell.Enabled = enabled;
        _btnBankDepositItem.Enabled = enabled;
        _btnBankWithdrawItem.Enabled = enabled;
        _btnBankDepositGold.Enabled = enabled;
        _btnBankWithdrawGold.Enabled = enabled;
        _btnSpell.Enabled = enabled;
        _btnPickup.Enabled = enabled && _lstGround.Items.Count > 0;
        _cmbShop.Enabled = enabled && _cmbShop.Items.Count > 0;
        _cmbShopItem.Enabled = enabled && _cmbShopItem.Items.Count > 0;
        _cmbSpell.Enabled = enabled && _cmbSpell.Items.Count > 0;
    }

    private void OnCombatState(CombatStateWire state)
    {
        _lastCombatState = state;
        _lblCombat.Text =
            $"Niv {state.Level} · XP {state.Experience} · HP {state.Hp}/{state.MaxHp} · MP {state.Mp}/{state.MaxMp} · Or {state.Gold}";
        _btnRespawn.Visible = state.IsDead;
        _btnRespawn.Enabled = state.IsDead;
    }

    private void OnInventorySnapshot(InventorySnapshotWire snapshot)
    {
        _inventoryPanel.ApplySnapshot(snapshot);
        _equipmentPanel.ApplySnapshot(snapshot);
        AppendLog($"Inventaire: {snapshot.Slots.Count(s => s.ItemId is not null && s.Quantity > 0)} slot(s) rempli(s).");
    }

    /// <summary>Nom publié (catalogue) pour un ItemId ; secours GUID court si catalogue absent/objet inconnu.</summary>
    private string ResolveItemName(Guid itemId)
    {
        var match = _publishedCatalog?.Items.FirstOrDefault(i =>
            Guid.TryParse(i.Id, out var parsed) && parsed == itemId);
        return match is not null ? match.Name : itemId.ToString("N")[..8];
    }

    private void OnBankSnapshot(BankSnapshotWire snapshot)
    {
        _bankSnapshot = snapshot;
        var filled = snapshot.Slots.Count(s => s.ItemId is not null && s.Quantity > 0);
        _lblBank.Text = $"Banque: or {snapshot.BankGold} · {filled} slot(s)";

        var previouslySelectedSlot = (_lstBank.SelectedItem as BankRow)?.SlotIndex;
        _lstBank.Items.Clear();
        foreach (var slot in snapshot.Slots.OrderBy(s => s.SlotIndex))
        {
            if (slot.ItemId is Guid id && slot.Quantity > 0)
            {
                _lstBank.Items.Add(new BankRow(slot.SlotIndex, id, slot.Quantity, ResolveItemName(id)));
            }
        }

        if (_lstBank.Items.Count > 0)
        {
            var restoreIndex = previouslySelectedSlot is int prev
                ? _lstBank.Items.Cast<BankRow>().ToList().FindIndex(r => r.SlotIndex == prev)
                : -1;
            _lstBank.SelectedIndex = restoreIndex >= 0 ? restoreIndex : 0;
        }
    }

    private void OnGroundItemsSnapshot(GroundItemsSnapshotWire snapshot)
    {
        _groundSnapshot = snapshot;
        _lstGround.Items.Clear();
        foreach (var item in snapshot.Items)
        {
            _lstGround.Items.Add(new GroundRow(item.GroundItemId, item.ItemId, item.Quantity, ResolveItemName(item.ItemId)));
        }

        if (_lstGround.Items.Count > 0 && _lstGround.SelectedIndex < 0)
        {
            _lstGround.SelectedIndex = 0;
        }

        _btnPickup.Enabled = _lstGround.Items.Count > 0;
        AppendLog($"Sol map={snapshot.MapId}: {snapshot.Items.Count} objet(s)");
    }

    private async Task PickupSelectedGroundItemAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        if (_lstGround.SelectedItem is not GroundRow row)
        {
            return;
        }

        try
        {
            await _client.SendPickupItemAsync(row.GroundItemId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Ramasser: " + ex.Message);
        }
    }

    private void OnReconnectResult(bool ok, string message)
    {
        if (!ok)
        {
            // Échec : message serveur générique ("Session invalide.") — jamais de jeton, mais
            // on sanitize quand même par défense en profondeur.
            AppendLog("Reconnect refusé: " + SanitizeSecrets(message));
            return;
        }

        // Succès : `message` est le jeton de session lui-même (echo du ReconnectRequest) —
        // ne jamais l'écrire dans le log (fenêtre UI ou stdout playtest).
        AppendLog("Reconnect OK");
        _username = _txtUser.Text.Trim();
        _btnMap.Enabled = true;
        _btnLogout.Enabled = true;
        _cmbCharacters.Enabled = true;
        _btnCharRefresh.Enabled = true;
        _btnEnterGame.Enabled = true;
        _txtNewCharName.Enabled = true;
        _btnCharCreate.Enabled = true;
        _cmbClass.Enabled = true;
        _btnBackDisconnect.Enabled = true;
        _heartbeatTimer.Start();
        // Mirror login: preload map so Enter Game can reach Playing via MapAlreadySynced.
        SetGameplayControlsEnabled(false);
        SetPhase(ClientUiPhase.CharacterSelect);
        _ = RefreshCharacterListAsync();
        _ = MapRequestAsync();
    }

    /// <summary>Jeton de session base64url générés par <c>InMemoryAuthSessionRepository</c>/PostgreSQL (32 octets → ~43 caractères).</summary>
    private static readonly Regex SessionTokenLikePattern = new("[A-Za-z0-9_-]{40,}", RegexOptions.Compiled);

    private static string SanitizeSecrets(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Masque tout ce qui ressemble à un jeton de session (base64url ~40+ caractères)
        // afin qu'un jeton reflété par erreur dans un message serveur ne fuite jamais dans les logs UI.
        return SessionTokenLikePattern.Replace(text, "***");
    }

    private async Task ReconnectAsync()
    {
        if (_client is null || !_client.IsConnected || string.IsNullOrWhiteSpace(_storedAuthToken))
        {
            AppendLog("Reconnect: connectez d'abord TCP et assurez un jeton stocké.");
            return;
        }

        try
        {
            await _client.SendReconnectAsync(_storedAuthToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Reconnect: " + ex.Message);
        }
    }

    private async Task EquipSlotAsync(byte slot)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendEquipAsync(slot).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Équiper: " + ex.Message);
        }
    }

    private async Task UnequipSlotAsync(EquipmentSlotKind slot)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendUnequipAsync(slot).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Déséquiper: " + ex.Message);
        }
    }

    private async Task DropItemAsync(byte slot, int quantity)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendDropItemAsync(slot, quantity).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Drop: " + ex.Message);
        }
    }

    private async Task SpellCastAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        var target = _cmbMeleeTarget.Text.Trim();
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        try
        {
            if (!TryResolveSpellSelection(out var spellId))
            {
                AppendLog("Sort: sélectionnez un sort dans le catalogue.");
                return;
            }

            await _client.SendSpellCastAsync(spellId, target).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Sort: " + ex.Message);
        }
    }

    private async Task RespawnAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendRespawnAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Respawn: " + ex.Message);
        }
    }

    private async Task ShopBuyAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        if (!TryResolveShopSelection(out var shopId, out var itemId))
        {
            AppendLog("Shop: sélectionnez boutique et article (catalogue) ou saisissez des Guids valides.");
            return;
        }

        try
        {
            await _client.SendShopBuyAsync(shopId, itemId, (int)_numShopQty.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Achat: " + ex.Message);
        }
    }

    private async Task ShopSellAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        if (_inventoryPanel is null)
        {
            return;
        }

        try
        {
            var slot = (byte)Math.Clamp(_numBankSlot.Value, 0, 255);
            await _client.SendShopSellAsync(slot, (int)_numShopQty.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Vente: " + ex.Message);
        }
    }

    private async Task BankDepositItemAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendBankDepositItemAsync((byte)_numBankSlot.Value, (int)_numBankQty.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Banque dépôt: " + ex.Message);
        }
    }

    private async Task BankWithdrawItemAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendBankWithdrawItemAsync((byte)_numBankSlot.Value, (int)_numBankQty.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Banque retrait: " + ex.Message);
        }
    }

    private async Task BankDepositGoldAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendBankDepositGoldAsync((int)_numBankGold.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Dépôt or: " + ex.Message);
        }
    }

    private async Task BankWithdrawGoldAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendBankWithdrawGoldAsync((int)_numBankGold.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Retrait or: " + ex.Message);
        }
    }

    private async Task ConnectAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            _btnConnect.Enabled = false;
            var host = _txtHost.Text.Trim();
            var port = (int)_numPort.Value;
            await _client.ConnectAsync(host, port).ConfigureAwait(true);
            AppendLog($"TCP connecté {host}:{port}");
            _btnDisconnect.Enabled = true;
            _btnLogin.Enabled = true;
            _btnRegister.Enabled = true;
            UpdateAuthTokenUi();
        }
        catch (Exception ex)
        {
            AppendLog("Connexion: " + ex.Message);
            _btnConnect.Enabled = true;
        }
    }

    private async Task DisconnectAsync()
    {
        _heartbeatTimer.Stop();
        if (_client is not null)
        {
            await _client.DisconnectAsync().ConfigureAwait(true);
        }

        ResetUiAfterDisconnect();
    }

    private void ResetUiAfterDisconnect()
    {
        _btnConnect.Enabled = true;
        _btnDisconnect.Enabled = false;
        _btnLogin.Enabled = false;
        _btnRegister.Enabled = false;
        _btnMap.Enabled = false;
        _btnMelee.Enabled = false;
        _btnWorldFlagsDemo.Enabled = false;
        _btnLogout.Enabled = false;
        ResetCharacterPickUi();
        ClearPublishedCatalogUi();
        SetGameplayControlsEnabled(false);
        _map = null;
        _mapBlockedTiles = null;
        _username = null;
        _sessionDisplayedMapId = 0;
        _others.Clear();
        ResetLocalMotionState();
        ClearMapImage();
        DisposeTilesetBitmaps();
        _mapEvents.Clear();
        _awaitingPlayingPhase = false;
        _btnBackDisconnect.Enabled = false;
        SetPhase(ClientUiPhase.Login);
    }

    private void ClearPublishedCatalogUi()
    {
        _publishedCatalog = null;
        _cmbClass.Items.Clear();
        _cmbShop.Items.Clear();
        _cmbShopItem.Items.Clear();
        _cmbSpell.Items.Clear();
        _cmbMeleeTarget.Items.Clear();
        _lstBank.Items.Clear();
        _lstGround.Items.Clear();
        // Keep ItemNameLookup wired to ResolveItemName (handles null catalog).
    }

    private void OnConnectionClosed()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnConnectionClosed);
            return;
        }

        AppendLog("Connexion fermée.");
        ResetUiAfterDisconnect();
    }

    private async Task LoginAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendLoginAsync(_txtUser.Text.Trim(), _txtPass.Text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Login send: " + ex.Message);
        }
    }

    private void OnLoginResult(bool ok, string message)
    {
        if (!ok)
        {
            // Échec : message serveur générique ("Identifiants invalides.") — jamais de jeton,
            // mais on sanitize quand même par défense en profondeur.
            AppendLog("Login refusé: " + SanitizeSecrets(message));
            if (_playtestOptions is { IsPlaytest: true })
            {
                EmitPlaytestFailure("login refusé: " + message);
            }

            return;
        }

        _playtestLoginOk = true;
        // Stocker le jeton AVANT tout log : `message` EST le jeton de session (successMessage du
        // LoginResult serveur) et ne doit jamais apparaître dans le log UI / stdout playtest.
        if (!string.IsNullOrWhiteSpace(message))
        {
            _storedAuthToken = message.Trim();
        }

        AppendLog("Login OK");

        UpdateAuthTokenUi();
        _username = _playtestOptions is { IsPlaytest: true }
            ? "__frog_playtest__"
            : _txtUser.Text.Trim();
        _btnMap.Enabled = true;
        _btnLogout.Enabled = true;
        _btnMelee.Enabled = false;
        _btnWorldFlagsDemo.Enabled = false;
        _cmbCharacters.Enabled = true;
        _btnCharRefresh.Enabled = true;
        _btnEnterGame.Enabled = true;
        _txtNewCharName.Enabled = true;
        _btnCharCreate.Enabled = true;
        _cmbClass.Enabled = true;
        _btnBackDisconnect.Enabled = true;
        _heartbeatTimer.Start();
        _ = RefreshCharacterListAsync();
        _ = MapRequestAsync();
        SetPhase(ClientUiPhase.CharacterSelect);
        if (_playtestOptions is { IsPlaytest: true })
        {
            _ = AutoEnterPlaytestCharacterAsync();
        }
    }

    private async Task AutoEnterPlaytestCharacterAsync()
    {
        try
        {
            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(50).ConfigureAwait(true);
                if (_cmbCharacters.Items.Count > 0)
                {
                    _cmbCharacters.SelectedIndex = 0;
                    await ApplySelectedCharacterAsync().ConfigureAwait(true);
                    return;
                }
            }

            // Pas de perso listé : la carte + spawn playtest suffisent pour la readiness.
            TryEmitPlaytestReady();
        }
        catch (Exception ex)
        {
            EmitPlaytestFailure("auto perso: " + ex.Message);
        }
    }

    private async Task RegisterAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendRegisterAsync(_txtUser.Text.Trim(), _txtPass.Text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Register send: " + ex.Message);
        }
    }

    private async Task MapRequestAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendMapRequestAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("MapRequest: " + ex.Message);
        }
    }

    private async Task LogoutAsync()
    {
        if (_client is null || !_client.IsConnected || string.IsNullOrWhiteSpace(_username))
        {
            return;
        }

        try
        {
            AppendLog("Envoi LogoutRequest…");
            await _client.SendLogoutAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Logout: " + ex.Message);
        }
    }

    private void OnLogoutAck()
    {
        _heartbeatTimer.Stop();
        ApplyLoggedOutSessionUi();
        AppendLog("LogoutAck reçu — la session est terminée (le serveur ferme la connexion).");
    }

    /// <summary>Réinitialise l’état « en jeu » après logout serveur ; la socket se ferme ensuite.</summary>
    private void ApplyLoggedOutSessionUi()
    {
        _username = null;
        _map = null;
        _mapBlockedTiles = null;
        _sessionDisplayedMapId = 0;
        _others.Clear();
        ResetLocalMotionState();
        ClearMapImage();
        DisposeTilesetBitmaps();
        _mapEvents.Clear();
        _btnMap.Enabled = false;
        _btnMelee.Enabled = false;
        _btnWorldFlagsDemo.Enabled = false;
        _btnLogout.Enabled = false;
        ResetCharacterPickUi();
        _awaitingPlayingPhase = false;
        SetPhase(ClientUiPhase.Login);
    }

    private void ResetCharacterPickUi()
    {
        _cmbCharacters.Items.Clear();
        _cmbCharacters.Enabled = false;
        _btnCharRefresh.Enabled = false;
        _btnEnterGame.Enabled = false;
        _txtNewCharName.Enabled = false;
        _btnCharCreate.Enabled = false;
        _cmbClass.Enabled = false;
        SetStatsControlsEnabled(false);
    }

    private void SetStatsControlsEnabled(bool enabled)
    {
        _btnStatsApply.Enabled = enabled;
        foreach (var n in _numStats)
        {
            n.Enabled = enabled;
        }
    }

    private void OnMapData(int mapId, Map map)
    {
        AppendLog($"Map reçue id={mapId} {map.Name} {map.Width}x{map.Height}");
        _mapEvents.Clear();
        _sessionDisplayedMapId = mapId;
        _map = map;
        _mapBlockedTiles = MapCollision.IndexBlockedTiles(map);
        _others.Clear();
        // Ne pas ResetLocalMotionState() ici : un second MapRequest (ex. après CharacterSelectResult)
        // recevrait MapData après PositionUpdate ; la remise à zéro coupait tout envoi PositionSync.
        _motionSmoothLastUtc = DateTime.UtcNow;
        _pendingIdlePositionSync = false;
        if (_localVisualInitialized && !string.IsNullOrEmpty(_username))
        {
            _visLocalCx = _srvPixelX;
            _visLocalCy = _srvPixelY;
            ClampLocalVisToMap();
        }

        ReloadTilesetBitmaps();
        RedrawMap();
        _ = RequestMapEventsFromServerAsync();
        TryEnterPlayingPhaseAfterMapReady();
        if (_playtestOptions is { IsPlaytest: true })
        {
            _playtestReady.ObserveLoadedMap(mapId);
            TryEmitPlaytestReady();
        }
    }

    private void OnMapAlreadySynced(int mapId, long revision)
    {
        AppendLog($"Carte id={mapId} déjà à jour (révision serveur {revision}).");
        _sessionDisplayedMapId = mapId;
        if (_playtestOptions is { IsPlaytest: true })
        {
            _playtestReady.ObserveLoadedMap(mapId);
        }

        if (_map is null && _client is { IsConnected: true } && !string.IsNullOrEmpty(_username))
        {
            AppendLog("Carte absente en local — re-demande du blob complet.");
            _ = RequestFullMapBlobAsync();
        }

        _ = RequestMapEventsFromServerAsync();
        TryEnterPlayingPhaseAfterMapReady();
        if (_playtestOptions is { IsPlaytest: true })
        {
            TryEmitPlaytestReady();
        }
    }

    private async Task RequestFullMapBlobAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendMapRequestIgnoringFingerprintAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("MapRequest (blob complet): " + ex.Message);
        }
    }

    private async Task RequestMapEventsFromServerAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendMapEventsRequestAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("MapEventsRequest: " + ex.Message);
        }
    }

    private void OnMapEventsResult(int mapId, string json)
    {
        if (mapId != _sessionDisplayedMapId)
        {
            return;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<MapEventWireEntry>>(json);
            var n = list?.Count ?? 0;
            AppendLog($"Événements carte id={mapId}: {n} placement(s)");
            _mapEvents.Clear();
            if (list is { Count: > 0 })
            {
                _mapEvents.AddRange(list);
            }

            RedrawMap();
        }
        catch
        {
            AppendLog($"Événements carte id={mapId}: réponse JSON non analysée.");
        }
    }

    private void OnInteractResult(bool ok, string message)
    {
        AppendLog(ok ? "Interaction: " + message : "Interaction refusée: " + message);
    }

    private void OnWorldFlagsPatchResult(bool ok, string message)
    {
        AppendLog(ok ? "worldFlags: " + message : "worldFlags refusé: " + message);
    }

    private async Task SendWorldFlagsDemoPatchAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendWorldFlagsPatchAsync("{\"demo_story\":true}", CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("WorldFlagsPatch: " + ex.Message);
        }
    }

    private async Task SendInteractAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendInteractRequestAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Interaction: " + ex.Message);
        }
    }

    private void OnCharacterPayload(string characterId, string payloadJson)
    {
        var cid = characterId.Length <= 12 ? characterId : characterId[..12] + "…";
        var j = payloadJson.Length <= 200 ? payloadJson : payloadJson[..200] + "…";
        AppendLog($"Perso {cid} : {j}");
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                AppendLog("Stats: " + stats.ToString());
                ApplyStatsUiFromJson(stats);
            }
        }
        catch
        {
            // JSON optionnel / évolutif
        }
    }

    private void ApplyStatsUiFromJson(JsonElement stats)
    {
        var keys = new[] { "STR", "AGI", "DEX", "INT", "VIT", "LUCK" };
        for (var i = 0; i < keys.Length && i < _numStats.Length; i++)
        {
            if (!stats.TryGetProperty(keys[i], out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
            {
                v = Math.Clamp(v, (int)CharacterStatsWire.MinStat, (int)CharacterStatsWire.MaxStat);
                _numStats[i].Value = v;
            }
        }
    }

    private void OnCharacterStatsUpdateResult(bool ok, string message)
    {
        AppendLog(ok ? "Stats: " + message : "Stats refusées: " + message);
    }

    private async Task ApplyCharacterStatsAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        var buf = new byte[CharacterStatsWire.PackedByteCount];
        for (var i = 0; i < buf.Length; i++)
        {
            buf[i] = (byte)_numStats[i].Value;
        }

        try
        {
            await _client.SendCharacterStatsUpdateAsync(buf).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("CharacterStatsUpdate: " + ex.Message);
        }
    }

    private async Task RefreshCharacterListAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendCharacterListRequestAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("CharacterListRequest: " + ex.Message);
        }
    }

    private void OnCharacterListJson(string json)
    {
        _cmbCharacters.Items.Clear();
        try
        {
            var entries = JsonSerializer.Deserialize<List<CharacterListWireEntry>>(json);
            if (entries is null || entries.Count == 0)
            {
                AppendLog("Liste persos vide.");
                return;
            }

            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.Id))
                {
                    continue;
                }

                var name = string.IsNullOrEmpty(e.Name) ? e.Id : e.Name;
                _cmbCharacters.Items.Add(new CharacterPickRow(e.Id, name));
            }

            if (_cmbCharacters.Items.Count > 0)
            {
                _cmbCharacters.SelectedIndex = 0;
            }

            AppendLog($"{_cmbCharacters.Items.Count} perso(s) listé(s).");
        }
        catch (Exception ex)
        {
            AppendLog("Liste persos JSON: " + ex.Message);
        }
    }

    private void OnCharacterSelectResult(bool ok, string message)
    {
        AppendLog(ok ? "Perso: " + message : "Perso refusé: " + message);
        if (ok)
        {
            _awaitingPlayingPhase = true;
            if (_map is not null)
            {
                TryEnterPlayingPhaseAfterMapReady();
            }
            else if (_client is { IsConnected: true })
            {
                // Force full blob when local map was cleared (disconnect) even if a stale
                // fingerprint somehow remained.
                _ = RequestFullMapBlobAsync();
            }
        }
    }

    private void OnCharacterCreateResult(bool ok, string message)
    {
        if (ok)
        {
            AppendLog("Perso créé — id: " + message);
            _ = RefreshCharacterListAsync();
        }
        else
        {
            AppendLog("Création perso refusée: " + message);
        }
    }

    private async Task CreateCharacterAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        var name = _txtNewCharName.Text.Trim();
        if (name.Length == 0)
        {
            AppendLog("Saisir un nom pour le nouveau perso.");
            return;
        }

        try
        {
            if (_cmbClass.SelectedItem is not ClassPickRow row)
            {
                AppendLog("Catalogue classes non chargé — attendez après login.");
                return;
            }

            await _client.SendCharacterCreateAsync(name, row.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("CharacterCreate: " + ex.Message);
        }
    }

    private async Task ApplySelectedCharacterAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        if (_cmbCharacters.SelectedItem is not CharacterPickRow row)
        {
            AppendLog("Choisir un personnage dans la liste.");
            return;
        }

        try
        {
            await _client.SendCharacterSelectAsync(row.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("CharacterSelect: " + ex.Message);
        }
    }

    private void OnPositionUpdate(string user, int mapId, int x, int y)
    {
        var isLocal = _username is not null && string.Equals(user, _username, StringComparison.OrdinalIgnoreCase);
        if (isLocal && _playtestOptions is { IsPlaytest: true })
        {
            _playtestReady.ObservePosition(mapId, x, y);
            TryEmitPlaytestReady();
        }

        if (!isLocal && _sessionDisplayedMapId != 0 && mapId != _sessionDisplayedMapId)
        {
            return;
        }

        var needImmediateRedraw = false;
        if (isLocal)
        {
            if (_sessionDisplayedMapId != 0 && mapId != _sessionDisplayedMapId)
            {
                TryScheduleMapRequestAfterWarp(mapId);
            }

            if (!_localVisualInitialized)
            {
                _srvPixelX = x;
                _srvPixelY = y;
                _visLocalCx = x;
                _visLocalCy = y;
                _motionSmoothLastUtc = DateTime.UtcNow;
                _localVisualInitialized = true;
                needImmediateRedraw = true;
            }
            else if (x != _srvPixelX || y != _srvPixelY)
            {
                _srvPixelX = x;
                _srvPixelY = y;
            }
        }
        else
        {
            var ov = _others.GetOrAdd(user, _ => new OtherPlayerView());
            if (!ov.Initialized)
            {
                ov.ServerPixelX = x;
                ov.ServerPixelY = y;
                ov.VisCx = x;
                ov.VisCy = y;
                ov.Initialized = true;
                needImmediateRedraw = true;
            }
            else if (ov.ServerPixelX != x || ov.ServerPixelY != y)
            {
                ov.ServerPixelX = x;
                ov.ServerPixelY = y;
            }
        }

        if (needImmediateRedraw)
        {
            RedrawMap();
        }
    }

    private void TryScheduleMapRequestAfterWarp(int serverMapId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        if (_sessionDisplayedMapId == 0 || serverMapId == _sessionDisplayedMapId)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastAutoMapRequestUtc < AutoMapRequestDebounce)
        {
            return;
        }

        _lastAutoMapRequestUtc = now;
        _ = MapRequestForMapAsync(serverMapId);
    }

    private async Task MapRequestForMapAsync(int mapId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendMapRequestAsync(mapId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("MapRequest (changement de carte): " + ex.Message);
        }
    }

    private void OnPlayerLeave(string user)
    {
        _others.TryRemove(user, out _);
        AppendLog("Parti: " + user);
        RedrawMap();
    }

    private void OnChatMessage(ChatChannel ch, string from, string to, string message)
    {
        var prefix = ch switch
        {
            ChatChannel.Global => "[G]",
            ChatChannel.Map => "[M]",
            ChatChannel.Whisper => "[W]",
            _ => "[?]"
        };
        var target = string.IsNullOrEmpty(to) ? string.Empty : $"→{to} ";
        AppendLog($"{prefix} {from} {target}: {message}");
    }

    private async Task SendChatAsync()
    {
        if (_client is null || !_client.IsConnected || string.IsNullOrWhiteSpace(_username))
        {
            return;
        }

        var text = _txtChat.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var ch = _cmbChannel.SelectedIndex switch
        {
            0 => ChatChannel.Global,
            1 => ChatChannel.Map,
            _ => ChatChannel.Whisper
        };

        try
        {
            await _client.SendChatAsync(ch, _txtWhisperTo.Text.Trim(), text).ConfigureAwait(true);
            _txtChat.Clear();
        }
        catch (Exception ex)
        {
            AppendLog("Chat: " + ex.Message);
        }
    }

    private async Task MeleeAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        var t = _cmbMeleeTarget.Text.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        try
        {
            await _client.SendMeleeAttackAsync(t).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Mêlée: " + ex.Message);
        }
    }

    private async Task SendHeartbeatSafeAsync()
    {
        if (_client is null || !_client.IsConnected || string.IsNullOrEmpty(_username))
        {
            return;
        }

        try
        {
            await _client.SendHeartbeatAsync().ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }
    }

    private void MainShell_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_phase != ClientUiPhase.Playing)
        {
            return;
        }

        if (_client is null || !_client.IsConnected || string.IsNullOrEmpty(_username))
        {
            return;
        }

        if (e.KeyCode == Keys.E)
        {
            if (_map is null)
            {
                return;
            }

            e.Handled = true;
            var nowE = DateTime.UtcNow;
            if ((nowE - _lastInteractUtc).TotalMilliseconds < 400)
            {
                return;
            }

            _lastInteractUtc = nowE;
            _ = SendInteractAsync();
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Left:
                if (!_holdLeft)
                {
                    PrimeMoveNetworkPulse();
                }

                _holdLeft = true;
                break;
            case Keys.Right:
                if (!_holdRight)
                {
                    PrimeMoveNetworkPulse();
                }

                _holdRight = true;
                break;
            case Keys.Up:
                if (!_holdUp)
                {
                    PrimeMoveNetworkPulse();
                }

                _holdUp = true;
                break;
            case Keys.Down:
                if (!_holdDown)
                {
                    PrimeMoveNetworkPulse();
                }

                _holdDown = true;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void PrimeMoveNetworkPulse()
    {
        _lastMoveSendUtc = DateTime.UtcNow.AddMilliseconds(-MoveNetworkPulseMs - 1);
        TrySendHeldMoveNetwork();
    }

    private void MainShell_KeyUp(object? sender, KeyEventArgs e)
    {
        if (_phase != ClientUiPhase.Playing)
        {
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Left:
                _holdLeft = false;
                ScheduleIdlePositionSyncIfAllReleased();
                e.Handled = true;
                break;
            case Keys.Right:
                _holdRight = false;
                ScheduleIdlePositionSyncIfAllReleased();
                e.Handled = true;
                break;
            case Keys.Up:
                _holdUp = false;
                ScheduleIdlePositionSyncIfAllReleased();
                e.Handled = true;
                break;
            case Keys.Down:
                _holdDown = false;
                ScheduleIdlePositionSyncIfAllReleased();
                e.Handled = true;
                break;
        }
    }

    private void RedrawMap()
    {
        if (_map is null)
        {
            return;
        }

        var lcx = (float)_srvPixelX;
        var lcy = (float)_srvPixelY;
        if (_localVisualInitialized)
        {
            lcx = _visLocalCx;
            lcy = _visLocalCy;
        }

        var otherPx = new Dictionary<string, (float CxPx, float CyPx)>(_others.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _others)
        {
            otherPx[kv.Key] = (kv.Value.VisCx, kv.Value.VisCy);
        }

        var bmp = MapViewRenderer.Render(_map, otherPx, _username, lcx, lcy, _tilesetBitmaps, _mapEvents);
        var previous = _picMap.Image;
        _picMap.Image = bmp;
        previous?.Dispose();
    }

    private void ReloadTilesetBitmaps()
    {
        DisposeTilesetBitmaps();
        if (_map is null)
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        foreach (var kv in ClientTilesetLoader.LoadForMap(_map, baseDir))
        {
            _tilesetBitmaps[kv.Key] = kv.Value;
        }

        if (_tilesetBitmaps.Count > 0)
        {
            AppendLog($"Tilesets chargés : {string.Join(", ", _tilesetBitmaps.Keys.OrderBy(k => k))} (dossiers Maps/ ou Tilesets/ — voir Docs/premier-monde.md).");
        }
    }

    private void DisposeTilesetBitmaps()
    {
        foreach (var b in _tilesetBitmaps.Values)
        {
            b.Dispose();
        }

        _tilesetBitmaps.Clear();
    }

    private void ClearMapImage()
    {
        var old = _picMap.Image;
        _picMap.Image = null;
        old?.Dispose();
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line));
            return;
        }

        if (!string.IsNullOrEmpty(_playtestOptions?.PlaytestToken))
        {
            line = line.Replace(_playtestOptions.PlaytestToken, "***", StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(_storedAuthToken))
        {
            line = line.Replace(_storedAuthToken, "***", StringComparison.Ordinal);
        }

        var t = DateTime.Now.ToString("HH:mm:ss");
        var stamped = $"[{t}] {line}";
        _txtLog.AppendText(stamped + Environment.NewLine);
        if (_playtestOptions is { IsPlaytest: true })
        {
            try
            {
                Console.Out.WriteLine(stamped);
                Console.Out.Flush();
            }
            catch
            {
                // ignore
            }
        }
    }

    private sealed class CharacterPickRow(string id, string displayName)
    {
        public string Id { get; } = id;

        public string DisplayName { get; } = displayName;

        public override string ToString() => DisplayName;
    }

    private sealed class ClassPickRow(Guid id, string label)
    {
        public Guid Id { get; } = id;

        public string Label { get; } = label;

        public override string ToString() => Label;
    }

    private sealed class ShopPickRow(Guid id, string label, IReadOnlyList<string> itemIds)
    {
        public Guid Id { get; } = id;

        public string Label { get; } = label;

        public IReadOnlyList<string> ItemIds { get; } = itemIds;

        public override string ToString() => Label;
    }

    private sealed class ItemPickRow(Guid id, string label, string type)
    {
        public Guid Id { get; } = id;

        public string Label { get; } = label;

        public string Type { get; } = type;

        public override string ToString() => Label;
    }

    private sealed class SpellPickRow(Guid id, string label)
    {
        public Guid Id { get; } = id;

        public string Label { get; } = label;

        public override string ToString() => Label;
    }

    private sealed class BankRow(int slotIndex, Guid itemId, int quantity, string name)
    {
        public int SlotIndex { get; } = slotIndex;

        public Guid ItemId { get; } = itemId;

        public int Quantity { get; } = quantity;

        public string Name { get; } = name;

        public override string ToString() => $"[{SlotIndex}] {Name} ×{Quantity}";
    }

    private sealed class GroundRow(Guid groundItemId, Guid itemId, int quantity, string name)
    {
        public Guid GroundItemId { get; } = groundItemId;

        public Guid ItemId { get; } = itemId;

        public int Quantity { get; } = quantity;

        public string Name { get; } = name;

        public override string ToString() => $"{Name} ×{Quantity}";
    }

    internal TextBox HostTextBoxForTest => _txtHost;

    internal NumericUpDown PortNumericForTest => _numPort;

    internal TextBox UserTextBoxForTest => _txtUser;

    internal TextBox PassTextBoxForTest => _txtPass;

    internal Button ConnectButtonForTest => _btnConnect;

    internal Button DisconnectButtonForTest => _btnDisconnect;

    internal Button BackDisconnectButtonForTest => _btnBackDisconnect;

    internal Button SwitchCharacterButtonForTest => _btnSwitchCharacter;

    /// <summary>Disconnect regardless of which panel hosts the visible Disconnect control.</summary>
    internal void DisconnectForTest()
    {
        // Prefer visible controls; fall back to direct disconnect for smoke reliability.
        if (_btnDisconnect.Visible && _btnDisconnect.Enabled)
        {
            _btnDisconnect.PerformClick();
            return;
        }

        if (_btnBackDisconnect.Visible && _btnBackDisconnect.Enabled)
        {
            _btnBackDisconnect.PerformClick();
            return;
        }

        // Playing phase: leave game to character select, then disconnect.
        if (_phase == ClientUiPhase.Playing)
        {
            GoToCharacterSelectPhase();
        }

        if (_btnBackDisconnect.Enabled)
        {
            _btnBackDisconnect.PerformClick();
            return;
        }

        _ = DisconnectAsync();
    }

    internal Button LoginButtonForTest => _btnLogin;

    internal Button RegisterButtonForTest => _btnRegister;

    internal Button ReconnectButtonForTest => _btnReconnect;

    internal Button CharCreateButtonForTest => _btnCharCreate;

    internal Button EnterGameButtonForTest => _btnEnterGame;

    internal TextBox NewCharNameTextBoxForTest => _txtNewCharName;

    internal ComboBox CharactersComboForTest => _cmbCharacters;

    internal InventoryPanel InventoryPanelForTest => _inventoryPanel;

    internal EquipmentPanel EquipmentPanelForTest => _equipmentPanel;

    internal bool IsPlayingPhaseForTest => _phase == ClientUiPhase.Playing;

    internal string? SelectedCharacterIdForTest =>
        _cmbCharacters.SelectedItem is CharacterPickRow row ? row.Id : null;

    internal string? StoredAuthTokenForTest => _storedAuthToken;

    internal ComboBox ClassesComboForTest => _cmbClass;

    internal ComboBox ShopComboForTest => _cmbShop;

    internal ComboBox ShopItemComboForTest => _cmbShopItem;

    internal ComboBox SpellComboForTest => _cmbSpell;

    internal bool CatalogClassesPopulatedForTest => _cmbClass.Items.Count > 0;

    internal bool TrySelectWeaponFromCatalogForTest()
        => TrySelectCatalogShopItemForTest("Weapon");

    internal bool TrySelectConsumableFromCatalogForTest()
        => TrySelectCatalogShopItemForTest("Consumable");

    private bool TrySelectCatalogShopItemForTest(string itemType)
    {
        if (_publishedCatalog is null)
        {
            return false;
        }

        var item = _publishedCatalog.Items.FirstOrDefault(i =>
            string.Equals(i.Type, itemType, StringComparison.OrdinalIgnoreCase));
        if (item is null || !Guid.TryParse(item.Id, out var itemId))
        {
            return false;
        }

        var shop = _publishedCatalog.Shops.FirstOrDefault(s =>
            s.ItemIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase));
        if (shop is null || !Guid.TryParse(shop.Id, out var shopId))
        {
            return false;
        }

        for (var i = 0; i < _cmbShop.Items.Count; i++)
        {
            if (_cmbShop.Items[i] is ShopPickRow row && row.Id == shopId)
            {
                _cmbShop.SelectedIndex = i;
                break;
            }
        }

        RefreshShopItemCombo();
        for (var i = 0; i < _cmbShopItem.Items.Count; i++)
        {
            if (_cmbShopItem.Items[i] is ItemPickRow row && row.Id == itemId)
            {
                _cmbShopItem.SelectedIndex = i;
                SyncShopGuidTextBoxes();
                return true;
            }
        }

        return false;
    }

    internal Guid? SelectedCatalogWeaponIdForTest =>
        _cmbShopItem.SelectedItem is ItemPickRow row
        && string.Equals(row.Type, "Weapon", StringComparison.OrdinalIgnoreCase)
            ? row.Id
            : null;

    internal Button SpellButtonForTest => _btnSpell;

    internal Button SendChatButtonForTest => _btnSendChat;

    internal TextBox ChatTextBoxForTest => _txtChat;

    internal Button BankDepositGoldButtonForTest => _btnBankDepositGold;

    internal Button BankWithdrawGoldButtonForTest => _btnBankWithdrawGold;

    internal Button ShopSellButtonForTest => _btnShopSell;

    internal NumericUpDown BankGoldNumericForTest => _numBankGold;

    internal ComboBox MeleeTargetComboForTest => _cmbMeleeTarget;

    internal Button MeleeButtonForTest => _btnMelee;

    internal TextBox ShopItemIdTextBoxForTest => _txtShopItemId;

    internal Button ShopBuyButtonForTest => _btnShopBuy;

    internal Button BankDepositItemButtonForTest => _btnBankDepositItem;

    internal Button BankWithdrawItemButtonForTest => _btnBankWithdrawItem;

    internal NumericUpDown BankSlotNumericForTest => _numBankSlot;

    internal NumericUpDown BankQtyNumericForTest => _numBankQty;

    internal ListBox BankItemsListForTest => _lstBank;

    internal int BankItemsCountForTest => _lstBank.Items.Count;

    internal ListBox GroundItemsListForTest => _lstGround;

    internal int GroundItemsCountForTest => _lstGround.Items.Count;

    internal Button PickupButtonForTest => _btnPickup;

    internal void SelectFirstGroundItemForTest()
    {
        if (_lstGround.Items.Count > 0)
        {
            _lstGround.SelectedIndex = 0;
        }
    }

    internal void ClickPickupForTest() => _btnPickup.PerformClick();

    /// <summary>Dernier HP connu (dernier <see cref="Frog.Core.Protocol.CombatStateWire"/>) : null si aucun reçu encore.</summary>
    internal int? CombatHpForTest => _lastCombatState?.Hp;

    internal int? CombatMaxHpForTest => _lastCombatState?.MaxHp;

    internal bool CombatIsDeadForTest => _lastCombatState?.IsDead ?? false;

    /// <summary>Force l'envoi RespawnRequest sans passer par la visibilité/activation du bouton UI.</summary>
    internal void RespawnForTest() => _ = RespawnAsync();

    internal void SelectGameplayTabForTest() => _gameplayTabs.SelectedTab = _tabGameplay;

    internal void SelectChatTabForTest() => _gameplayTabs.SelectedTab = _tabChat;

    internal void SelectChatChannelForTest(int channelIndex) => _cmbChannel.SelectedIndex = channelIndex;

    internal bool LogContainsForTest(string fragment) =>
        _txtLog.Text.Contains(fragment, StringComparison.Ordinal);

    internal string LogTextForTest => _txtLog.Text;
}
