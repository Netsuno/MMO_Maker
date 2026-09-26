using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Frog.Client.Assets;
using Frog.Client.Config;
using Frog.Client.Controls;
using Frog.Client.Forms;
using Frog.Client.Models;
using Frog.Client.Network;
using Frog.Client.Services;
using Frog.Client.UI;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Chat;
using Frog.Core.Events;
using Frog.Core.Character;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Observability;
using Frog.Core.Protocol;
using Frog.Core.Security;
using Frog.Core.Combat;
using Frog.Core.Economy;
using Frog.Core.Instances;
using Frog.Core.Social;
using Frog.Core.Shop;
using Frog.Core.Trade;
using Frog.Core.Weather;

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
    private readonly Panel _panelLogin = new() { Dock = DockStyle.Fill, Padding = new Padding(0), AutoScroll = true };
    private readonly LoginShell _loginShell = new();
    private readonly Panel _panelCharacter = new() { Dock = DockStyle.Fill, Padding = new Padding(32), AutoScroll = true, Visible = false };
    private readonly Panel _panelGame = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Button _btnSwitchCharacter = new() { Text = "Changer de personnage", AutoSize = true };
    private readonly Button _btnBackDisconnect = new() { Text = "Retour à la connexion (fermer la session)", AutoSize = true, Enabled = false };
    private readonly Button _btnHelp = new() { Text = "Aide", AutoSize = true };
    private readonly Button _btnOptions = new() { Text = "Options", AutoSize = true };
    private readonly Button _btnCopyDiagnostics = new() { Text = "Copier diagnostics", AutoSize = true };
    private readonly Label _lblVersion = new() { AutoSize = true, Text = "v—", Margin = new Padding(8, 10, 4, 4) };
    private readonly Label _lblPlayerStatus = new()
    {
        AutoSize = false,
        Height = 24,
        Text = "Prêt.",
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 8, 0),
        Dock = DockStyle.Top,
    };
    private readonly FlowLayoutPanel _topChrome = new()
    {
        AutoSize = true,
        Dock = DockStyle.Top,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Padding = new Padding(6, 4, 6, 2),
    };
    private readonly Label _lblMoveHint = new() { AutoSize = true, Margin = new Padding(8, 14, 4, 4) };
    private HelpForm? _helpForm;

    private FrogGameClient? _client;
    private Map? _map;
    private string? _username;
    private string? _lastWhisperTarget;
    private bool _whisperAwaitingResult;
    private int _srvPixelX;
    private int _srvPixelY;
    private readonly ConcurrentDictionary<string, OtherPlayerView> _others = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Position affichée locale : prédiction continue + réconciliation avec la position réseau autoritaire.</summary>
    private float _visLocalCx;

    private float _visLocalCy;

    private bool _localVisualInitialized;

    /// <summary>Focus caméra lissé (snap au premier paint / warp ; damp ensuite).</summary>
    private float _camFocusX;

    private float _camFocusY;

    private bool _camFocusInitialized;

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

        public Direction Facing = Direction.Down;

        public bool Walking;

        public int WalkElapsedMs;

        public SpriteAction Action;

        public int ActionElapsedMs;
    }

    /// <summary>Client-only NPC / monster visual (walk / attack / death). Combat / fil inchangés.</summary>
    private sealed class WorldEntityView
    {
        public int ServerPixelX = 0;

        public int ServerPixelY = 0;

        public float VisCx;

        public float VisCy;

        public Direction Facing = Direction.Down;

        public bool Walking;

        public int WalkElapsedMs;

        public SpriteAction Action;

        public int ActionElapsedMs;
    }

    private readonly ConcurrentDictionary<string, WorldEntityView> _worldNpcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WorldEntityView> _worldMonsters = new(StringComparer.OrdinalIgnoreCase);
    private int _sessionDisplayedMapId;
    private DateTime _lastAutoMapRequestUtc = DateTime.MinValue;
    private static readonly TimeSpan AutoMapRequestDebounce = TimeSpan.FromMilliseconds(300);
    private readonly Dictionary<int, Bitmap> _tilesetBitmaps = new();
    private readonly List<PrefabPlacement> _prefabPlacements = new();
    private readonly List<MapPlacedEntity> _playtestPlacedEntities = new();
    private readonly Dictionary<string, Bitmap> _prefabBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private PrefabCatalog? _prefabCatalog;
    /// <summary>Envoi périodique <see cref="FrogGameClient.SendPositionSyncAsync"/> (protocole ≥ 8) : centre prédit en pixels.</summary>
    private DateTime _lastMoveSendUtc = DateTime.MinValue;
    private bool _pendingIdlePositionSync;
    private DateTime _lastInteractUtc = DateTime.MinValue;

    private readonly ClientSettingsStore _settingsStore = new();
    private UserSettings _settings;
    private int _uiScalePercent = ClientUiScale.DefaultPercent;
    private int _appliedUiScalePercent = ClientUiScale.DefaultPercent;
    private readonly TilePackClientService _tilePacks;
    private readonly Dictionary<TileAssetId, Bitmap> _tileAssetBitmaps = new();
    private string? _tileAssetBitmapSha;
    private readonly InputService _input = new();
    private readonly SoundService _sound = new();
    private readonly HashSet<Keys> _keysDown = new();

    /// <summary>Facing + walk / attack / death clock for the local world sprite (client-only, no protocol field).</summary>
    private Direction _localFacing = Direction.Down;

    private int _localWalkElapsedMs;

    private SpriteAction _localAction;

    private int _localActionElapsedMs;

    /// <summary>Touches direction maintenues (prédiction client + boucle réseau).</summary>
    private bool _holdLeft;

    private bool _holdRight;

    private bool _holdUp;

    private bool _holdDown;

    private HashSet<(int X, int Y)>? _mapBlockedTiles;

    private readonly List<MapEventWireEntry> _mapEvents = new();

    private readonly Dictionary<int, ShownEventPicture> _eventPictures = new();

    private WeatherOverlayPlan _weatherPlan = WeatherCatalog.Clear;

    private WeatherDebugOverride _weatherDebug;

    private int _weatherTickMs;

    private EnvironmentStateWire? _lastEnvironment;
    private readonly TextBox _txtHost = new() { Text = "127.0.0.1", Width = 120 };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 6000, Width = 70 };
    private readonly ComboBox _cmbServers = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = LoginShell.FieldWidth,
    };
    private readonly TextBox _txtServerName = new() { Width = 120, PlaceholderText = "Nom" };
    private readonly Button _btnAddServer = new() { Text = "Ajouter" };
    private readonly Button _btnRetry = new() { Text = "Réessayer", Enabled = false };
    private bool _syncingServerList;
    private ConnectionFailureKind _lastFailureKind;
    private string? _lastFailureText;
    private readonly TextBox _txtUser = new() { Text = "demo", Width = LoginShell.FieldWidth };
    private readonly TextBox _txtPass = new() { Text = "demo", Width = LoginShell.FieldWidth, UseSystemPasswordChar = true };
    private readonly Button _btnConnect = new() { Text = "Connecter" };
    private readonly Button _btnDisconnect = new() { Text = "Déconnecter", Enabled = false };
    private readonly Button _btnLogin = new() { Text = "Connexion", Enabled = false };
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
    private readonly ComboBox _cmbShop = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Enabled = false };
    private readonly ComboBox _cmbShopItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, Enabled = false };
    private readonly ComboBox _cmbSpell = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Enabled = false };
    private PublishedCatalogWire? _publishedCatalog;
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 72, Dock = DockStyle.Bottom };
    /// <summary>
    /// Viewport carte. <see cref="Panel.AutoScroll"/> reste faux : le coin (0,0) de la carte
    /// n’est plus collé au coin client — <see cref="ApplyMapViewportCamera"/> pose <see cref="_picMap"/>.
    /// </summary>
    private readonly Panel _mapScroll = new() { Dock = DockStyle.Fill, AutoScroll = false, BackColor = MapSurfaceBackColor };
    private readonly PictureBox _picMap = new()
    {
        Location = new Point(0, 0),
        SizeMode = PictureBoxSizeMode.AutoSize,
        BackColor = MapSurfaceBackColor,
        TabStop = true,
    };
    private readonly TextBox _txtChat = new() { Dock = DockStyle.Fill };
    private readonly Button _btnSendChat = new() { Text = "Envoyer chat", Dock = DockStyle.Bottom, Height = 28 };
    private readonly ComboBox _cmbChannel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly TextBox _txtWhisperTo = new() { PlaceholderText = "Nom, ami ou cible", Width = 140 };
    /// <summary>Cible mêlée/sort : ComboBox éditable (P7-G5) peuplée des noms PNJ/monstre publiés (défaut « Slime » si présent), texte libre toujours possible.</summary>
    private readonly ComboBox _cmbMeleeTarget = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 120 };
    private readonly Button _btnMelee = new() { Text = "Mêlée", Enabled = false };
    private readonly Button _btnRanged = new() { Text = "Distance", Enabled = false };
    private readonly Button _btnSpell = new() { Text = "Sort", Enabled = false };
    private readonly Button _btnRespawn = new() { Text = "Respawn", Enabled = false, Visible = false };
    private readonly Label _lblCombat = new() { AutoSize = true, Text = "Combat: —", Margin = new Padding(4, 8, 4, 4) };
    private readonly InventoryPanel _inventoryPanel = new() { Dock = DockStyle.Fill, MinimumSize = new Size(200, 80) };
    private readonly EquipmentPanel _equipmentPanel = new() { Dock = DockStyle.Top, MinimumSize = new Size(200, 240) };
    private readonly CharacterSheetPanel _characterSheet = new() { Dock = DockStyle.Fill };
    private readonly AppearancePickerPanel _appearancePicker = new();
    private Equipment _paperdoll = Equipment.Empty;
    private CharacterLook _activeLook = CharacterLook.Default;
    private string? _activeCharacterId;
    private string? _activeCharacterName;
    private string? _pendingLookName;
    /// <summary>
    /// Overlay TabControl is 360 (DA). Pre-overlay the tab filled a 360 TLP cell with
    /// default Margin 3+3, so Dialogue/Quest/Environment exact-sha crops stayed 324 wide.
    /// </summary>
    internal const int Phase8ExactShaPanelWidth = 324;

    private readonly DialoguePanel _dialoguePanel = new() { Dock = DockStyle.Top, MinimumSize = new Size(200, 96) };
    private readonly QuestJournalPanel _questJournalPanel = new() { Dock = DockStyle.Fill, MinimumSize = new Size(200, 80) };
    private readonly CraftPanel _craftPanel = new() { Dock = DockStyle.Top, MinimumSize = new Size(200, 56) };
    private readonly TradeForm _tradeForm = new();
    private readonly ClientShopBankSession _shopBank = new();
    private readonly ShopForm _shopForm = new();
    private bool _shopChromeLock;
    private readonly EnvironmentPanel _environmentPanel = new() { Dock = DockStyle.Top, MinimumSize = new Size(200, 72) };
    private readonly TabControl _gameplayTabs = new() { Dock = DockStyle.None, Width = 360, Height = 480, MinimumSize = new Size(300, 250), MaximumSize = new Size(360, 700) };
    private readonly TabPage _tabChat = new("Chat") { Padding = new Padding(4) };
    private readonly TabPage _tabGameplay = new("Inventaire") { Padding = new Padding(4) };
    private readonly TabPage _tabCharacter = new("Fiche") { Padding = new Padding(4) };
    private readonly TabPage _tabPhase8 = new("Quêtes") { Padding = new Padding(4) };
    private readonly TabPage _tabSocial = new("Social") { Padding = new Padding(4) };
    private readonly ClientSocialRoster _socialRoster = new();
    private readonly ClientEconomyHub _economyHub = new();
    private readonly ClientInstanceHub _instanceHub = new();
    private readonly ClientCombatHud _combatHud = new();
    private readonly ToolTip _statusTips = new() { ShowAlways = true };
    private readonly SocialHubPanel _socialHub = new() { Dock = DockStyle.Fill };
    private readonly HudWindowChrome _windowChrome = new("Inventaire");
    private string _windowTitleHint = "Inventaire";
    private readonly Panel _worldHost = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _gameToolbar = new()
    {
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Padding = new Padding(4),
        BackColor = UiTheme.BgPanel,
    };
    private readonly HudStatusModule _hudStatus = new();
    private readonly HudMinimapModule _hudMinimap = new();
    private readonly HudQuestTrackerModule _hudQuest = new();
    private readonly HudChatDock _hudChat = new();
    private readonly FriendsSticky _friendsSticky = new();
    private readonly HudFriendsDock _hudFriends = new();
    private readonly HudHotbar _hudHotbar = new();
    private readonly HudMenuRing _hudMenu = new();
    private readonly InteractHintBadge _interactHint = new();
    private readonly EventMessageBox _eventMessage = new();
    /// <summary>Dialogue poussé tant que le joueur reste sur la tuile où il a commencé.</summary>
    private bool _dialogueSessionOpen;
    private int _dialogueAnchorTileX;
    private int _dialogueAnchorTileY;
    private bool _windowLayerVisible;
    private readonly TextBox _txtShopId = new() { Width = 220, PlaceholderText = "Shop Guid (secours)", Visible = false };
    private readonly TextBox _txtShopItemId = new() { Width = 220, PlaceholderText = "Item Guid (secours)", Visible = false };
    private readonly NumericUpDown _numShopQty = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 48 };
    private readonly Button _btnShopToggle = new() { Text = "Ouvrir la boutique", AutoSize = true, Enabled = false };
    private readonly Button _btnShopBuy = new() { Text = "Acheter", AutoSize = true, Enabled = false };
    private readonly Button _btnShopSell = new() { Text = "Vendre", AutoSize = true, Enabled = false };
    private readonly Label _lblShopListing = new() { AutoSize = true, Text = "—", Margin = new Padding(4, 6, 4, 0) };
    /// <summary>Emplacement banque interne (P7-G5) : piloté par la sélection dans <see cref="_lstBank"/>, plus affiché en brut.</summary>
    private readonly NumericUpDown _numBankSlot = new() { Minimum = 0, Maximum = 39, Width = 48, Visible = false };
    private readonly NumericUpDown _numBankQty = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 48 };
    private readonly Button _btnBankDepositItem = new() { Text = "Déposer l'objet", AutoSize = true, Enabled = false };
    private readonly Button _btnBankWithdrawItem = new() { Text = "Retirer l'objet", AutoSize = true, Enabled = false };
    private readonly NumericUpDown _numBankGold = new() { Minimum = 1, Maximum = 999999, Value = 10, Width = 64 };
    private readonly Button _btnBankDepositGold = new() { Text = "Déposer l'or", AutoSize = true, Enabled = false };
    private readonly Button _btnBankWithdrawGold = new() { Text = "Retirer l'or", AutoSize = true, Enabled = false };
    private readonly Label _lblBank = new() { AutoSize = true, Text = "Banque : —", Margin = new Padding(4, 4, 4, 4) };
    /// <summary>Liste banque nommée (P7-G5) : sélectionner une ligne fixe <see cref="_numBankSlot"/> pour retrait.</summary>
    private readonly ListBox _lstBank = new() { Dock = DockStyle.Fill, IntegralHeight = false, Height = 70 };
    private BankSnapshotWire? _bankSnapshot;
    /// <summary>Liste objets au sol nommée (P7-G5) + ramassage.</summary>
    private readonly ListBox _lstGround = new() { Dock = DockStyle.Fill, IntegralHeight = false, Height = 70 };
    private readonly Button _btnPickup = new() { Text = "Ramasser", AutoSize = true, Enabled = false };
    private GroundItemsSnapshotWire? _groundSnapshot;
    private readonly HashSet<Guid> _walkOnPickupSent = new();
    private (int MapId, int TileX, int TileY)? _walkOnScannedTile;
    private readonly NumericUpDown[] _numStats = new NumericUpDown[CharacterStatsWire.PackedByteCount];
    private readonly Button _btnStatsApply = new() { Text = "Appliquer stats", AutoSize = true, Enabled = false };
    private readonly System.Windows.Forms.Timer _heartbeatTimer = new() { Interval = 45_000 };
    private readonly System.Windows.Forms.Timer _diagnosticRttTimer = new() { Interval = 2_000 };
    private readonly DiagnosticOverlayPanel _diagnosticOverlay = new();
    private readonly HeartbeatRttProbe _rtt = new();
    private bool _connectInFlight;
    private string? _diagnosticLastError;
    private TilePackSyncResult? _lastTilePack;

    /// <summary>Fréquence d’envoi position au serveur (aligné prédiction locale ~52 ms).</summary>
    private const int MoveNetworkPulseMs = 52;

    /// <summary>Opt-in movement baseline (off unless <c>FROG_MOVEMENT_MEASURE=1</c>).</summary>
    private readonly MovementMeasureProbe _movementMeasure = new(MovementMeasureOptions.IsEnabledFromEnvironment());

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
        _settings = _settingsStore.Load();
        _uiScalePercent = _settings.UiScalePercent;
        ClientUiScale.SetActive(_uiScalePercent);
        _tilePacks = new TilePackClientService(TilePackClientOptions.Resolve(_settings));
        _input.Apply(_settings);
        _sound.Apply(_settings);
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Text = "FRoG — Frog Isle";
        ClientSize = new Size(1040, 720);
        MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;
        ApplyWindowSettings(_settings.Window);
        if (_playtestOptions is not { IsPlaytest: true })
        {
            _txtHost.Text = _settings.LastHost;
            _numPort.Value = Math.Clamp(_settings.LastPort, 1, 65535);
        }

        BuildLayout();
        if (_settings.AppearanceDraft is { } draft)
        {
            _appearancePicker.SetLook(draft.ToLook());
        }

        ApplyRememberedAccount();
        _movementMeasure.Log = AppendLog;
        if (_movementMeasure.Enabled)
        {
            AppendLog("[measure] FROG_MOVEMENT_MEASURE=1 — baseline on (docs/progress/movement/MEASURE-BASELINE.md)");
        }

        ApplyDaTheme();
        ApplyUiScale(_settings.UiScalePercent);
        ApplyVersionChrome();
        ApplyPlayerStatusLayout();
        RefreshMoveHint();
        UpdateVersionBadge();
        ShowPlayerStatus("Prêt.");
        _btnHelp.Click += (_, _) => OpenHelp();
        _btnOptions.Click += (_, _) => OpenOptions();
        _btnCopyDiagnostics.Click += (_, _) => CopyDiagnosticsToClipboard();
        ApplyOptionsHelpChrome();
        ApplyCatalogRecipesToCraft(_publishedCatalog);
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

        _cmbServers.SelectedIndexChanged += (_, _) => ApplyPickedServer();
        _btnAddServer.Click += (_, _) => AddServerFromFields();
        RefreshServerListUi();

        EnableDoubleBuffer(_mapScroll);
        EnableDoubleBuffer(_picMap);
        _smoothTimer.Tick += SmoothTimer_OnTick;
        _smoothTimer.Start();
        _cmbChannel.Items.AddRange(new object[] { "Général", "Local", "Chuchoter", "Groupe", "Guilde" });
        _cmbChannel.SelectedIndex = 1;
        _cmbShop.SelectedIndexChanged += (_, _) => OnShopComboChanged();
        _cmbShopItem.SelectedIndexChanged += (_, _) => OnShopItemChanged();
        _numShopQty.ValueChanged += (_, _) => OnEconomyQuantityChanged();
        _numBankQty.ValueChanged += (_, _) => OnEconomyQuantityChanged();
        _numBankGold.ValueChanged += (_, _) => OnEconomyQuantityChanged();
        _heartbeatTimer.Tick += async (_, _) => await SendHeartbeatSafeAsync();
        _diagnosticOverlay.Dismissed += HideDiagnosticOverlay;
        _diagnosticRttTimer.Tick += async (_, _) =>
        {
            RefreshDiagnosticOverlay();
            await SendHeartbeatSafeAsync();
        };
        _txtHost.TextChanged += (_, _) => RefreshDiagnosticOverlay();
        _numPort.ValueChanged += (_, _) => RefreshDiagnosticOverlay();
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
            LayoutGameHud();
        }

        if (_phase != ClientUiPhase.Playing)
        {
            _dialogueSessionOpen = false;
        }

        RefreshInteractHint();
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

    private void ResetPaperdoll()
    {
        _paperdoll = Equipment.Empty;
        _activeLook = CharacterLook.Default;
        _activeCharacterId = null;
        _activeCharacterName = null;
        _tradeForm.SetLocalCharacter(Guid.Empty);
        _equipmentPanel.ResetLocalHeadwear();
        _equipmentPanel.ResetLocalTunic();
        SyncStatusPortrait();
    }

    private void SyncStatusPortrait()
    {
        _hudStatus.ApplyLook(_activeLook);
        _hudStatus.ApplyPortrait(EquipmentService.ToOverlaySet(_paperdoll));
        _characterSheet.ApplyLook(_activeLook);
        _characterSheet.ApplyLoadout(_paperdoll, ResolveItemName, _username, _lastCombatState?.Level);
    }

    private bool TryAppearanceArrow(KeyEventArgs e)
    {
        if (e.KeyCode is not (Keys.Left or Keys.Right or Keys.Up or Keys.Down))
        {
            return false;
        }

        if (ActiveControl is TextBoxBase or ComboBox or NumericUpDown)
        {
            return false;
        }

        if (_appearancePicker.ContainsFocus)
        {
            return false;
        }

        _appearancePicker.Nudge(e.KeyCode);
        return true;
    }

    private void RememberNamedLook(string displayName, CharacterLook look)
    {
        _settings.CharacterLooks ??= new List<CharacterLookRecord>();
        CharacterLookBook.Remember(
            _settings.CharacterLooks,
            characterId: null,
            displayName,
            look,
            tunicWorn: look.WearsTunic);
        _settings.AppearanceDraft = CharacterLookRecord.FromLook(look, look.WearsTunic);
        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            // le look reste en mémoire jusqu'au prochain essai
        }
    }

    private void PersistActiveLook()
    {
        if (string.IsNullOrWhiteSpace(_activeCharacterId) && string.IsNullOrWhiteSpace(_activeCharacterName))
        {
            return;
        }

        var worn = _paperdoll.TunicItemId == Equipment.LocalTunicItemId;
        _settings.CharacterLooks ??= new List<CharacterLookRecord>();
        CharacterLookBook.Remember(
            _settings.CharacterLooks,
            _activeCharacterId,
            _activeCharacterName,
            _activeLook,
            worn);
        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            // visuel local : le prochain enregistrement reprendra
        }
    }

    private void ApplySavedLook(string? characterId, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            _activeCharacterId = characterId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            _activeCharacterName = displayName.Trim();
        }

        SyncTradeIdentity();
        if (!CharacterLookBook.TryGet(_settings.CharacterLooks, _activeCharacterId, _activeCharacterName, out var record))
        {
            _activeLook = CharacterLook.Default;
            _paperdoll = _paperdoll with { TunicItemId = null };
            _equipmentPanel.SetLocalTunic(false);
            SyncStatusPortrait();
            return;
        }

        _activeLook = record.ToLook();
        var worn = record.TunicWorn && _activeLook.WearsTunic;
        _paperdoll = _paperdoll with
        {
            TunicItemId = worn ? Equipment.LocalTunicItemId : null,
        };
        _equipmentPanel.SetLocalTunic(worn);
        SyncStatusPortrait();
    }

    private void GoToCharacterSelectPhase()
    {
        ResetPaperdoll();
        ReleaseAllMoveKeys();
        _awaitingPlayingPhase = false;
        _btnMelee.Enabled = false;
        _btnRanged.Enabled = false;
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
        _btnRanged.Enabled = true;
        SetGameplayControlsEnabled(true);
        _gameplayTabs.SelectedTab = _tabGameplay;
        SetPhase(ClientUiPhase.Playing);
        SyncStatusPortrait();
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
        PersistWindowSettings();
        _smoothTimer.Stop();
        _heartbeatTimer.Stop();
        _diagnosticRttTimer.Stop();
        if (_client is not null)
        {
            await _client.DisconnectAsync().ConfigureAwait(true);
            _client.Dispose();
        }

        _smoothTimer.Dispose();
        _sound.Dispose();
        DisposeTileAssetBitmaps();
        _tilePacks.Dispose();
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
        else if (_weatherPlan.ParticleCount > 0 || _combatHud.HasFloats || _combatHud.SparksVisible(DateTime.UtcNow))
        {
            if (_weatherPlan.ParticleCount > 0)
            {
                _weatherTickMs += _smoothTimer.Interval;
            }

            RedrawMap();
        }

        TrySendHeldMoveNetwork();
        RefreshInteractHint();
    }

    private void ResetLocalMotionState()
    {
        _localVisualInitialized = false;
        _camFocusInitialized = false;
        _motionSmoothLastUtc = default;
        _pendingIdlePositionSync = false;
        _localFacing = Direction.Down;
        _localWalkElapsedMs = 0;
        _localAction = SpriteAction.Walk;
        _localActionElapsedMs = 0;
        ReleaseAllMoveKeys();
    }

    private void ReleaseAllMoveKeys()
    {
        _keysDown.Clear();
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

    private void SnapCameraToLocalVisual()
    {
        _camFocusX = _visLocalCx;
        _camFocusY = _visLocalCy;
        _camFocusInitialized = _localVisualInitialized;
    }

    private bool AdvanceCameraFocus(float visualDt)
    {
        if (!_localVisualInitialized)
        {
            return false;
        }

        if (!_camFocusInitialized)
        {
            SnapCameraToLocalVisual();
            return false;
        }

        var (nx, ny) = MapViewportCamera.DampFocus(
            _camFocusX,
            _camFocusY,
            _visLocalCx,
            _visLocalCy,
            visualDt);
        if (MathF.Abs(nx - _camFocusX) <= 0.006f && MathF.Abs(ny - _camFocusY) <= 0.006f)
        {
            return false;
        }

        _camFocusX = nx;
        _camFocusY = ny;
        return true;
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
            WorldMetrics.PlayerCollisionRadiusPixels,
            PredictionTileSize());
    }

    /// <summary>Avance la prédiction avec la même cible que le serveur (~8 px par « tick » réseau) et glissement le long des murs.</summary>
    private void TryApplyLocalPredictStep(float pvx, float pvy, float dt)
    {
        var speedPxPerSec = WorldMetrics.PlayerMovePixelsPerRequest / (MoveNetworkPulseMs / 1000f);
        var dx = pvx * speedPxPerSec * dt;
        var dy = pvy * speedPxPerSec * dt;

        var nx = _visLocalCx + dx;
        var ny = _visLocalCy + dy;
        if (PredictedMoveAllowed(nx, ny))
        {
            _visLocalCx = nx;
            _visLocalCy = ny;
            return;
        }

        nx = _visLocalCx + dx;
        ny = _visLocalCy;
        if (PredictedMoveAllowed(nx, ny))
        {
            _visLocalCx = nx;
            return;
        }

        nx = _visLocalCx;
        ny = _visLocalCy + dy;
        if (PredictedMoveAllowed(nx, ny))
        {
            _visLocalCy = ny;
        }
    }

    private bool PredictedMoveAllowed(float nextX, float nextY)
    {
        if (IsPredictedCenterBlocked(nextX, nextY))
        {
            return false;
        }

        if (_map is null)
        {
            return true;
        }

        return MapCollision.AllowsPixelMove(
            _map,
            (int)MathF.Round(_visLocalCx),
            (int)MathF.Round(_visLocalCy),
            (int)MathF.Round(nextX),
            (int)MathF.Round(nextY),
            PredictionTileSize());
    }

    private static void AttachRuntimeTileFlags(Map map)
    {
        if (map.TileFlags is { Count: > 0 })
        {
            return;
        }

        foreach (var directory in new[] { TilePackClientOptions.DefaultCacheDirectory(), AppContext.BaseDirectory })
        {
            var loaded = TileAssetFlagTable.TryLoadOrNull(directory);
            if (loaded is not { Count: > 0 })
            {
                continue;
            }

            map.TileFlags = loaded;
            return;
        }
    }

    private int PredictionTileSize()
    {
        if (_map?.TileFlags is { Count: > 0 } && _map.TileSizePixels > 0)
        {
            return _map.TileSizePixels;
        }

        return WorldMetrics.DefaultTileSizePixels;
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
        _movementMeasure.NoteNetworkSend();
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

        var rawDt = MovementFluidity.SanitizeRawDt((float)(now - _motionSmoothLastUtc).TotalSeconds);
        _motionSmoothLastUtc = now;
        var visualDt = MovementFluidity.ClampVisualDt(rawDt);

        _movementMeasure.NoteFrameTime(rawDt * 1000f);

        var alpha = MovementFluidity.ExpAlpha(MovementFluidity.OtherConvergencePerSec, visualDt);
        var otherMaxStep = MovementFluidity.OtherMaxStepPixels(visualDt, MoveNetworkPulseMs);
        const float moveEps = 0.006f;

        var dirty = false;

        if (_localVisualInitialized && !string.IsNullOrEmpty(_username) && _map is not null)
        {
            var hasMove = TryGetHeldMoveNormalized(out var pvx, out var pvy);
            if (hasMove)
            {
                if (_localAction == SpriteAction.Walk)
                {
                    _localFacing = PlayerWalkClock.FacingFromVector(pvx, pvy, _localFacing);
                }

                // Walk sheet stays on raw dt (anim MVP unchanged). Predict uses capped visual dt.
                _localWalkElapsedMs += (int)(rawDt * 1000f);
                TryApplyLocalPredictStep(pvx, pvy, visualDt);
                ClampLocalVisToMap();
                dirty = true;
            }
            else if (_localWalkElapsedMs != 0)
            {
                _localWalkElapsedMs = 0;
                dirty = true;
            }

            var errMag = MovementFluidity.Distance(_visLocalCx, _visLocalCy, _srvPixelX, _srvPixelY);
            // Référence serveur mise à jour par PositionUpdate ; ne pas tirer le joueur local vers elle (évite rollback).
            if (errMag > MovementFluidity.SnapDesyncPx)
            {
                _visLocalCx = _srvPixelX;
                _visLocalCy = _srvPixelY;
                SnapCameraToLocalVisual();
                dirty = true;
            }

            if (AdvanceCameraFocus(visualDt))
            {
                dirty = true;
            }

            if (TickSpriteAction(ref _localAction, ref _localActionElapsedMs, rawDt))
            {
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
            var (nx, ny) = MovementFluidity.StepToward(
                o.VisCx,
                o.VisCy,
                tx,
                ty,
                alpha,
                otherMaxStep);
            if (MathF.Abs(nx - o.VisCx) > moveEps || MathF.Abs(ny - o.VisCy) > moveEps)
            {
                if (o.Action == SpriteAction.Walk)
                {
                    o.Facing = PlayerWalkClock.FacingFromVector(nx - o.VisCx, ny - o.VisCy, o.Facing);
                }

                o.Walking = true;
                o.WalkElapsedMs += (int)(rawDt * 1000f);
                o.VisCx = nx;
                o.VisCy = ny;
                dirty = true;
            }
            else if (o.Walking)
            {
                o.Walking = false;
                o.WalkElapsedMs = 0;
                dirty = true;
            }

            if (TickSpriteAction(ref o.Action, ref o.ActionElapsedMs, rawDt))
            {
                dirty = true;
            }
        }

        if (AdvanceWorldEntitySmoothing(_worldNpcs, rawDt, alpha, otherMaxStep, moveEps))
        {
            dirty = true;
        }

        if (AdvanceWorldEntitySmoothing(_worldMonsters, rawDt, alpha, otherMaxStep, moveEps))
        {
            dirty = true;
        }

        return dirty;
    }

    private static bool AdvanceWorldEntitySmoothing(
        ConcurrentDictionary<string, WorldEntityView> entities,
        float rawDt,
        float alpha,
        float otherMaxStep,
        float moveEps)
    {
        var dirty = false;
        foreach (var kv in entities.ToArray())
        {
            var e = kv.Value;
            var tx = (float)e.ServerPixelX;
            var ty = (float)e.ServerPixelY;
            var (nx, ny) = MovementFluidity.StepToward(
                e.VisCx,
                e.VisCy,
                tx,
                ty,
                alpha,
                otherMaxStep);
            if (MathF.Abs(nx - e.VisCx) > moveEps || MathF.Abs(ny - e.VisCy) > moveEps)
            {
                if (e.Action == SpriteAction.Walk)
                {
                    e.Facing = WalkClock.FacingFromVector(nx - e.VisCx, ny - e.VisCy, e.Facing);
                }

                e.Walking = true;
                e.WalkElapsedMs += (int)(rawDt * 1000f);
                e.VisCx = nx;
                e.VisCy = ny;
                dirty = true;
            }
            else if (e.Walking)
            {
                e.Walking = false;
                e.WalkElapsedMs = 0;
                dirty = true;
            }

            if (TickSpriteAction(ref e.Action, ref e.ActionElapsedMs, rawDt))
            {
                dirty = true;
            }
        }

        return dirty;
    }

    /// <summary>Avance attaque (retour à la marche) ou mort (fige sur la dernière case).</summary>
    private static bool TickSpriteAction(ref SpriteAction action, ref int elapsedMs, float rawDt)
    {
        if (action == SpriteAction.Walk)
        {
            return false;
        }

        var before = elapsedMs;
        var step = (int)(rawDt * 1000f);
        if (step < 0)
        {
            step = 0;
        }

        elapsedMs = before > int.MaxValue - step ? int.MaxValue : before + step;
        if (action == SpriteAction.Attack && ActionClock.AttackFinished(elapsedMs))
        {
            action = SpriteAction.Walk;
            elapsedMs = 0;
            return true;
        }

        if (action == SpriteAction.Death && ActionClock.DeathSettled(before))
        {
            return false;
        }

        return true;
    }

    private static void StyleToolbarButton(Button b)
    {
        b.AutoSize = true;
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.MinimumSize = new Size(96, 30);
        b.Padding = new Padding(10, 4, 10, 4);
        b.Margin = new Padding(4, 4, 4, 4);
        UiTheme.StyleButton(b);
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

    private void BuildLayout()
    {
        StyleToolbarButton(_btnConnect);
        StyleToolbarButton(_btnDisconnect);
        StyleToolbarButton(_btnRetry);
        StyleToolbarButton(_btnAddServer);
        StyleToolbarButton(_btnLogin);
        StyleToolbarButton(_btnRegister);
        StyleToolbarButton(_btnReconnect);
        StyleToolbarButton(_btnMap);
        StyleToolbarButton(_btnLogout);
        StyleToolbarButton(_btnCharRefresh);
        StyleToolbarButton(_btnEnterGame);
        StyleToolbarButton(_btnCharCreate);
        StyleToolbarButton(_btnMelee);
        StyleToolbarButton(_btnRanged);
        StyleToolbarButton(_btnSpell);
        StyleToolbarButton(_btnRespawn);
        StyleToolbarButton(_btnShopToggle);
        StyleToolbarButton(_btnShopBuy);
        StyleToolbarButton(_btnShopSell);
        StyleToolbarButton(_btnBankDepositItem);
        StyleToolbarButton(_btnBankWithdrawItem);
        StyleToolbarButton(_btnBankDepositGold);
        StyleToolbarButton(_btnBankWithdrawGold);
        StyleToolbarButton(_btnPickup);
        StyleToolbarButton(_btnStatsApply);
        StyleToolbarButton(_btnBackDisconnect);
        StyleToolbarButton(_btnSwitchCharacter);
        StyleToolbarButton(_btnHelp);
        StyleToolbarButton(_btnOptions);
        StyleToolbarButton(_btnCopyDiagnostics);
        BackColor = UiTheme.BgApp;
        _panelLogin.BackColor = UiTheme.BgApp;
        _panelCharacter.BackColor = UiTheme.BgApp;
        _panelGame.BackColor = UiTheme.BgApp;
        foreach (Panel p in new[] { _panelLogin, _panelCharacter })
        {
            p.AutoScroll = true;
        }

        // Keep the combo + « Liste persos » on one 520 px card row; CTAs go on their own rows
        // so they cannot overflow the gold frame (production clip: « on voit pas tout les boutons »).
        _cmbCharacters.MinimumSize = new Size(180, 0);
        _cmbCharacters.Width = 240;
        _txtNewCharName.MinimumSize = new Size(120, 0);
        _txtNewCharName.Width = Math.Max(_txtNewCharName.Width, 140);
        _cmbMeleeTarget.Margin = new Padding(2, 4, 8, 4);

        var rowCharPick = CreateToolbarRow();
        rowCharPick.WrapContents = true;
        rowCharPick.Dock = DockStyle.None;
        rowCharPick.Controls.Add(Lbl("Personnage", topPad: 8));
        rowCharPick.Controls.Add(_cmbCharacters);
        rowCharPick.Controls.Add(_btnCharRefresh);

        var rowEnter = CreateToolbarRow();
        rowEnter.WrapContents = true;
        rowEnter.Dock = DockStyle.None;
        _btnEnterGame.AutoSize = true;
        _btnEnterGame.MinimumSize = new Size(220, 34);
        rowEnter.Controls.Add(_btnEnterGame);

        var rowCreate = CreateToolbarRow();
        rowCreate.WrapContents = true;
        rowCreate.Dock = DockStyle.None;
        rowCreate.Controls.Add(Lbl("Nouveau personnage", topPad: 8));
        rowCreate.Controls.Add(_txtNewCharName);
        rowCreate.Controls.Add(Lbl("Classe", topPad: 8));
        rowCreate.Controls.Add(_cmbClass);

        var rowCreateAction = CreateToolbarRow();
        rowCreateAction.WrapContents = true;
        rowCreateAction.Dock = DockStyle.None;
        rowCreateAction.Controls.Add(_btnCharCreate);

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
        rowCharNav.Dock = DockStyle.None;
        rowCharNav.Controls.Add(_btnBackDisconnect);

        LoginShell.HostCenteredCard(
            _panelCharacter,
            TitleLbl("Choisir votre personnage"),
            rowCharPick,
            rowEnter,
            rowCreate,
            _appearancePicker,
            rowCreateAction,
            rowStats,
            rowCharNav);

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
        shopRow.Controls.Add(_btnShopToggle);
        shopRow.Controls.Add(Lbl("Boutique"));
        shopRow.Controls.Add(_cmbShop);
        shopRow.Controls.Add(Lbl("Article"));
        shopRow.Controls.Add(_cmbShopItem);
        shopRow.Controls.Add(Lbl("Qté"));
        shopRow.Controls.Add(_numShopQty);
        shopRow.Controls.Add(_btnShopBuy);
        shopRow.Controls.Add(_btnShopSell);
        shopRow.Controls.Add(_lblShopListing);
        shopRow.Controls.Add(_txtShopId);
        shopRow.Controls.Add(_txtShopItemId);
        gameplayTab.Controls.Add(shopRow, 0, 3);
        var bankRow = CreateToolbarRow();
        bankRow.Controls.Add(Lbl("Banque"));
        bankRow.Controls.Add(_numBankSlot);
        bankRow.Controls.Add(Lbl("Qté"));
        bankRow.Controls.Add(_numBankQty);
        bankRow.Controls.Add(_btnBankDepositItem);
        bankRow.Controls.Add(_btnBankWithdrawItem);
        bankRow.Controls.Add(Lbl("Or"));
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
        tabRight.TabPages.Add(_tabCharacter);
        tabRight.TabPages.Add(_tabPhase8);
        tabRight.TabPages.Add(_tabSocial);
        _tabCharacter.Controls.Add(_characterSheet);
        _tabSocial.Controls.Add(_socialHub);
        _socialHub.ActionRequested += OnSocialHubAction;
        _socialHub.EconomyQueryRequested += OnEconomyHubQuery;
        _socialHub.InstanceQueryRequested += OnInstanceHubQuery;
        _socialHub.InstanceEnterRequested += OnInstanceHubEnter;
        _socialHub.InstanceLeaveRequested += OnInstanceHubLeave;
        _socialHub.SurfaceChanged += RefreshWindowChromeTitle;
        _tabChat.Controls.Add(new Label
        {
            Text = "Saisie chat : dock bas-gauche (canaux réels Global / Map / Whisper / Party / Guild).",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.TextSecondary,
            Padding = new Padding(12),
        });
        _gameToolbar.Dock = DockStyle.Top;
        _gameToolbar.WrapContents = true;
        _tabGameplay.Controls.Add(gameplayTab);
        _tabGameplay.Controls.Add(_gameToolbar);

        var phase8Tab = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            // +3 L/R restores the 6 px lost when the tab left the 360 TLP cell (Margin 3+3).
            Padding = new Padding(7, 4, 7, 4),
        };
        phase8Tab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        phase8Tab.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        phase8Tab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        phase8Tab.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        phase8Tab.Controls.Add(_environmentPanel, 0, 0);
        phase8Tab.Controls.Add(_questJournalPanel, 0, 1);
        phase8Tab.Controls.Add(_dialoguePanel, 0, 2);
        phase8Tab.Controls.Add(_craftPanel, 0, 3);
        _tabPhase8.Controls.Add(phase8Tab);

        _gameToolbar.Controls.Add(_btnMap);
        _gameToolbar.Controls.Add(_btnSwitchCharacter);
        _gameToolbar.Controls.Add(_btnLogout);
        _gameToolbar.Controls.Add(Lbl("Cible"));
        _gameToolbar.Controls.Add(_cmbMeleeTarget);
        _gameToolbar.Controls.Add(_btnMelee);
        _gameToolbar.Controls.Add(_btnRanged);
        _gameToolbar.Controls.Add(_cmbSpell);
        _gameToolbar.Controls.Add(_btnSpell);
        _lblMoveHint.Margin = new Padding(8, 14, 4, 4);
        _gameToolbar.Controls.Add(_lblMoveHint);

        _worldHost.BackColor = MapSurfaceBackColor;
        _mapScroll.Dock = DockStyle.Fill;
        _worldHost.Controls.Add(_mapScroll);
        _hudStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _hudMinimap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _hudQuest.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _hudChat.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _hudFriends.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _hudFriends.Visible = false;
        _hudHotbar.Anchor = AnchorStyles.Bottom;
        _hudMenu.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        tabRight.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _windowChrome.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnRespawn.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        UiTheme.StyleGoldTabs(tabRight);
        _windowChrome.Host(tabRight);
        _windowChrome.CloseClicked += (_, _) => SetWindowLayerVisible(false);
        tabRight.SelectedIndexChanged += (_, _) => RefreshWindowChromeTitle();
        _worldHost.Controls.Add(_hudStatus);
        _worldHost.Controls.Add(_hudMinimap);
        _worldHost.Controls.Add(_hudQuest);
        _worldHost.Controls.Add(_hudChat);
        _worldHost.Controls.Add(_hudFriends);
        _hudFriends.PinToggled += (_, _) =>
        {
            _friendsSticky.TogglePin();
            ApplyFriendsDock();
        };
        _hudFriends.CloseRequested += (_, _) =>
        {
            _friendsSticky.Close();
            ApplyFriendsDock();
        };
        _hudFriends.FriendClicked += OnFriendsDockFriend;
        _worldHost.Controls.Add(_hudHotbar);
        _worldHost.Controls.Add(_hudMenu);
        _worldHost.Controls.Add(_interactHint);
        _worldHost.Controls.Add(_eventMessage);
        _txtChat.Enter += (_, _) =>
        {
            StopMovementForChat();
            RefreshInteractHint();
        };
        _txtChat.Leave += (_, _) => RefreshInteractHint();
        _txtWhisperTo.Enter += (_, _) =>
        {
            StopMovementForChat();
            RefreshInteractHint();
        };
        _txtWhisperTo.Leave += (_, _) => RefreshInteractHint();
        _worldHost.Controls.Add(_btnRespawn);
        _worldHost.Controls.Add(_windowChrome);
        _picMap.Click += (_, _) => OnWorldSurfaceClick();
        _mapScroll.Click += (_, _) => OnWorldSurfaceClick();
        _worldHost.Resize += (_, _) => LayoutGameHud();
        _hudChat.AttachInputs(_cmbChannel, _txtWhisperTo, _txtChat, _btnSendChat);
        _cmbChannel.SelectedIndexChanged += (_, _) => PrefillWhisperFromSelection();
        _hudChat.SocialPanelRequested += OpenSocialPanel;
        _hudHotbar.SlotActivated += OnHotbarSlotActivated;
        _hudMenu.Command += OnHudMenuCommand;
        _panelGame.Controls.Clear();
        _panelGame.Padding = new Padding(0);
        _panelGame.Controls.Add(_worldHost);
        LayoutGameHud();

        _hostPages.BackColor = UiTheme.BgApp;
        _hostPages.Controls.Add(_panelGame);
        _hostPages.Controls.Add(_panelCharacter);
        _hostPages.Controls.Add(_panelLogin);
        _hostPages.Controls.Add(_diagnosticOverlay);
        _hostPages.Resize += (_, _) => PlaceDiagnosticOverlay();

        _loginShell.Dock = DockStyle.Fill;
        _loginShell.Attach(
            _txtUser,
            _txtPass,
            _btnLogin,
            _btnRegister,
            _btnReconnect,
            _btnConnect,
            _btnDisconnect,
            _txtHost,
            _numPort,
            _lblAuthStatus,
            _cmbServers,
            _txtServerName,
            _btnAddServer,
            _btnRetry);
        _btnLogin.EnabledChanged += (_, _) => LoginShell.StylePrimaryCta(_btnLogin);
        _btnRegister.EnabledChanged += (_, _) => LoginShell.StyleSecondaryCta(_btnRegister);
        _btnReconnect.EnabledChanged += (_, _) => LoginShell.StyleSecondaryCta(_btnReconnect);
        _btnConnect.EnabledChanged += (_, _) => LoginShell.StyleSecondaryCta(_btnConnect);
        _btnDisconnect.EnabledChanged += (_, _) => LoginShell.StyleSecondaryCta(_btnDisconnect);
        _btnRetry.EnabledChanged += (_, _) => LoginShell.StyleSecondaryCta(_btnRetry);
        _btnAddServer.EnabledChanged += (_, _) => LoginShell.StyleSecondaryCta(_btnAddServer);
        _loginShell.RememberCheckBoxForTest.CheckedChanged += (_, _) => PersistRememberedAccount();
        _panelLogin.Controls.Clear();
        _panelLogin.Controls.Add(_loginShell);

        _txtLog.Dock = DockStyle.Bottom;
        _txtLog.MinimumSize = new Size(120, 88);
        _txtLog.Height = 100;

        ClientSize = new Size(Math.Max(ClientSize.Width, 1040), Math.Max(ClientSize.Height, 720));

        _topChrome.Controls.Add(_btnHelp);
        _topChrome.Controls.Add(_btnOptions);
        _topChrome.Controls.Add(_lblVersion);
        _topChrome.Controls.Add(_btnCopyDiagnostics);

        Controls.Add(_hostPages);
        Controls.Add(_txtLog);
        Controls.Add(_lblPlayerStatus);
        Controls.Add(_topChrome);
    }

    private void WireClient()
    {
        if (_client is null)
        {
            return;
        }

        _btnConnect.Click += async (_, _) => await ConnectAsync();
        _btnRetry.Click += async (_, _) => await RetryAsync();
        _btnDisconnect.Click += async (_, _) => await DisconnectAsync();
        _btnLogin.Click += async (_, _) => await LoginAsync();
        _btnRegister.Click += async (_, _) => await RegisterAsync();
        _btnReconnect.Click += async (_, _) => await ReconnectAsync();
        _btnMap.Click += async (_, _) => await MapRequestAsync();
        _btnLogout.Click += async (_, _) => await LogoutAsync();
        _btnSendChat.Click += async (_, _) => await SendChatAsync();
        _btnMelee.Click += async (_, _) => await MeleeAsync();
        _btnRanged.Click += async (_, _) => await RangedAsync();
        _btnSpell.Click += async (_, _) => await SpellCastAsync();
        _btnRespawn.Click += async (_, _) => await RespawnAsync();
        _btnShopToggle.Click += (_, _) => ToggleShopFromUi();
        _btnShopBuy.Click += async (_, _) => await ShopBuyAsync();
        _btnShopSell.Click += async (_, _) => await ShopSellAsync();
        _btnBankDepositItem.Click += async (_, _) => await BankDepositItemAsync();
        _btnBankWithdrawItem.Click += async (_, _) => await BankWithdrawItemAsync();
        _btnBankDepositGold.Click += async (_, _) => await BankDepositGoldAsync();
        _btnBankWithdrawGold.Click += async (_, _) => await BankWithdrawGoldAsync();
        _shopForm.BuyClicked += () => _ = ShopBuyAsync();
        _shopForm.SellClicked += () => _ = ShopSellAsync();
        _shopForm.CloseClicked += () => CloseShopWindow(announce: true);
        _shopForm.QuantityChanged += qty =>
        {
            if (_shopChromeLock)
            {
                return;
            }

            var clamped = Math.Clamp(qty, (int)_numShopQty.Minimum, (int)_numShopQty.Maximum);
            if (_numShopQty.Value != clamped)
            {
                _numShopQty.Value = clamped;
            }
        };
        _shopForm.SelectionChanged += index =>
        {
            if (_shopChromeLock || index < 0 || index >= _cmbShopItem.Items.Count)
            {
                return;
            }

            if (_cmbShopItem.SelectedIndex != index)
            {
                _cmbShopItem.SelectedIndex = index;
            }
        };
        _btnPickup.Click += async (_, _) => await PickupSelectedGroundItemAsync();
        _lstBank.SelectedIndexChanged += (_, _) =>
        {
            if (_lstBank.SelectedItem is BankRow row)
            {
                _numBankSlot.Value = Math.Clamp(row.SlotIndex, (int)_numBankSlot.Minimum, (int)_numBankSlot.Maximum);
            }

            UpdateBankWithdrawButtons();
        };
        _inventoryPanel.EquipRequested += slot => _ = EquipSlotAsync(slot);
        _inventoryPanel.DropRequested += (slot, qty) => _ = DropItemAsync(slot, qty);
        _inventoryPanel.SelectionChanged += UpdateInventoryActionButtons;
        _tradeForm.AcceptRequested += id => _ = SendTradeActionAsync((byte)TradeAction.Accept, id, []);
        _tradeForm.DeclineRequested += id => _ = SendTradeActionAsync((byte)TradeAction.Decline, id, []);
        _tradeForm.ConfirmRequested += (id, rev) => _ = SendTradeActionAsync(
            (byte)TradeAction.Confirm, id, TradeWire.BuildRevisionPayload(rev));
        _tradeForm.UnconfirmRequested += id => _ = SendTradeActionAsync((byte)TradeAction.Unconfirm, id, []);
        _tradeForm.CancelRequested += id => _ = SendTradeActionAsync((byte)TradeAction.Cancel, id, []);
        _tradeForm.SetOfferRequested += (id, rev, gold, stacks) => _ = SendTradeActionAsync(
            (byte)TradeAction.SetOffer, id, TradeWire.BuildSetOfferPayload(rev, gold, stacks));
        _tradeForm.PlayerNotice += ShowPlayerStatus;
        _tradeForm.VisibleChanged += (_, _) => RefreshInteractHint();
        _equipmentPanel.UnequipRequested += slot => _ = UnequipSlotAsync(slot);
        _equipmentPanel.LocalHeadwearChanged += worn =>
        {
            _paperdoll = _paperdoll with
            {
                HeadwearItemId = worn ? Equipment.LocalHeadwearItemId : null,
            };
            SyncStatusPortrait();
            if (_phase == ClientUiPhase.Playing && _map is not null)
            {
                RedrawMap();
            }
        };
        _equipmentPanel.LocalTunicChanged += worn =>
        {
            if (worn && _activeLook.Tunic == 0)
            {
                _activeLook = _activeLook with { Tunic = 1 };
            }

            _paperdoll = _paperdoll with
            {
                TunicItemId = worn ? Equipment.LocalTunicItemId : null,
            };
            PersistActiveLook();
            SyncStatusPortrait();
            if (_phase == ClientUiPhase.Playing && _map is not null)
            {
                RedrawMap();
            }
        };
        _appearancePicker.LookChanged += look =>
        {
            _settings.AppearanceDraft = CharacterLookRecord.FromLook(look, look.WearsTunic);
            try
            {
                _settingsStore.Save(_settings);
            }
            catch
            {
                // brouillon optionnel
            }
        };
        _characterSheet.LookCycled += slot =>
        {
            _activeLook = _activeLook.Cycle(slot, +1);
            PersistActiveLook();
            SyncStatusPortrait();
            if (_phase == ClientUiPhase.Playing && _map is not null)
            {
                RedrawMap();
            }
        };
        _characterSheet.ToggleTunicRequested += () => _equipmentPanel.RequestToggleTunic();
        _characterSheet.ToggleHeadwearRequested += () => _equipmentPanel.RequestToggleHeadwear();
        _characterSheet.EquipRequested += slot => _ = EquipSlotAsync(slot);
        _characterSheet.UnequipRequested += slot => _ = UnequipSlotAsync(slot);
        _dialoguePanel.ChoiceRequested += (token, choiceId) => _ = SendDialogueChoiceAsync(token, choiceId);
        _questJournalPanel.TurnInRequested += questId => _ = QuestTurnInAsync(questId);
        _craftPanel.CraftRequested += recipeId => _ = CraftAsync(recipeId);
        _btnSwitchCharacter.Click += (_, _) => GoToCharacterSelectPhase();
        _btnBackDisconnect.Click += async (_, _) => await DisconnectAsync();

        _client.HelloReceived += msg => AppendLog("Hello: " + msg);
        _client.LoginResultReceived += OnLoginResult;
        _client.RegisterResultReceived += (ok, msg) =>
        {
            if (ok)
            {
                AppendLog("Inscription OK: " + msg);
                ShowPlayerStatus("Compte créé. Vous pouvez vous connecter.");
            }
            else
            {
                var human = PlayerFacingMessages.FromServerOrNetwork(msg);
                AppendLog("Inscription: " + human);
                ShowPlayerStatus(human);
            }
        };
        _client.MapDataReceived += OnMapData;
        _client.MapAlreadySyncedReceived += OnMapAlreadySynced;
        _client.CharacterPayloadReceived += OnCharacterPayload;
        _client.PositionUpdateReceived += OnPositionUpdate;
        _client.PlayerLeaveReceived += OnPlayerLeave;
        _client.ErrorReceived += err =>
        {
            if (_whisperAwaitingResult)
            {
                _whisperAwaitingResult = false;
                if (ChatWhisper.TryPresent(err, _socialRoster.IsKnownOffline(_lastWhisperTarget), out var chatLine))
                {
                    ShowChatNotice(chatLine);
                    return;
                }
            }

            var kind = PlayerFacingMessages.ClassifyServer(err);
            var human = PlayerFacingMessages.FromServerOrNetwork(err);
            AppendLog("Erreur: " + human);
            ShowPlayerStatus(human);
            NoteConnectFailure(kind, human);
            if (kind == ConnectionFailureKind.Version)
            {
                // Le message part avant la fermeture TCP : couper Login tout de suite,
                // réarmer Connecter / Réessayer. Le socket tombe dans le même tour.
                _btnConnect.Enabled = true;
                _btnDisconnect.Enabled = false;
                _btnLogin.Enabled = false;
                _btnRegister.Enabled = false;
                UpdateAuthTokenUi();
            }

            if (_playtestOptions is { IsPlaytest: true } && !_playtestReady.ReadyEmitted)
            {
                EmitPlaytestFailure(human);
            }
        };
        _client.HeartbeatAckReceived += OnHeartbeatAck;
        _client.LogoutAckReceived += OnLogoutAck;
        _client.ChatMessageReceived += OnChatMessage;
        _client.ModerateResultReceived += (ok, msg) =>
            AppendLog(ok ? "Modération: " + msg : "Modération refusée: " + msg);
        _client.SocialResultReceived += OnSocialResult;
        _client.SocialEventReceived += OnSocialEvent;
        _client.SocialSnapshotReceived += OnSocialSnapshot;
        _client.EconomyHubResultReceived += OnEconomyHubResult;
        _client.EconomyHubSnapshotReceived += OnEconomyHubSnapshot;
        _client.InstanceHubResultReceived += OnInstanceHubResult;
        _client.InstanceHubSnapshotReceived += OnInstanceHubSnapshot;
        _client.TradeResultReceived += r =>
        {
            var human = TradePlayerMessages.Present(r.Message);
            AppendLog(r.Success ? "Échange: " + human : "Échange refusé: " + human);
            _tradeForm.ApplyResult(r);
        };
        _client.TradeSnapshotReceived += OnTradeSnapshot;
        _client.MeleeAttackResultReceived += (hit, tgt, msg) =>
        {
            if (StatusEffectText.IsPulseMessage(msg))
            {
                return;
            }

            var label = _client.LastResolvedAttackStyle == AttackStyle.Ranged ? "Distance" : "Mêlée";
            AppendLog($"{label} → {tgt}: {(hit ? "touche" : "raté")} — {msg}");
            if (CombatFx.IsSwingMiss(hit, msg))
            {
                OnMeleeMiss();
            }

            if (ActionClock.IsIncomingAttack(msg))
            {
                BeginNamedAttack(tgt);
            }
        };
        _client.DamageEventReceived += OnDamageEvent;
        _client.StatusEffectReceived += OnStatusEffect;
        _client.CharacterListReceived += OnCharacterListJson;
        _client.CharacterSelectResultReceived += OnCharacterSelectResult;
        _client.CharacterCreateResultReceived += OnCharacterCreateResult;
        _client.CharacterStatsUpdateResultReceived += OnCharacterStatsUpdateResult;
        _client.MapEventsResultReceived += OnMapEventsResult;
        _client.InteractResultReceived += OnInteractResult;
        _client.ReconnectResultReceived += OnReconnectResult;
        _client.InventorySnapshotReceived += OnInventorySnapshot;
        _client.EquipResultReceived += (ok, msg) => AppendLog(ok ? "Équipement: " + msg : "Équipement refusé: " + msg);
        _client.UnequipResultReceived += (ok, msg) => AppendLog(ok ? "Déséquipement: " + msg : "Déséquipement refusé: " + msg);
        _client.DropItemResultReceived += (ok, msg) => AppendLog(ok ? "Drop: " + msg : "Drop refusé: " + msg);
        _client.PickupItemResultReceived += (ok, msg) => AppendLog(ok ? "Ramassé: " + msg : "Ramassé refusé: " + msg);
        _client.GroundItemsSnapshotReceived += OnGroundItemsSnapshot;
        _client.SpellCastResultReceived += (ok, msg) => AppendLog(ok ? "Sort: " + msg : "Sort refusé: " + msg);
        _client.CombatStateReceived += OnCombatState;
        _client.ShopBuyResultReceived += (ok, msg) => FinishEconomy(ShopBankAction.Buy, ok, msg, "Achat: ", "Achat refusé: ");
        _client.ShopSellResultReceived += (ok, msg) => FinishEconomy(ShopBankAction.Sell, ok, msg, "Vente: ", "Vente refusée: ");
        _client.BankDepositResultReceived += (ok, msg) => FinishEconomy(ShopBankAction.DepositItem, ok, msg, "Banque dépôt: ", "Banque dépôt refusé: ");
        _client.BankWithdrawResultReceived += (ok, msg) => FinishEconomy(ShopBankAction.WithdrawItem, ok, msg, "Banque retrait: ", "Banque retrait refusé: ");
        _client.BankSnapshotReceived += OnBankSnapshot;
        _client.RespawnResultReceived += (ok, msg) => AppendLog(ok ? "Respawn: " + msg : "Respawn refusé: " + msg);
        _client.ExperienceGainReceived += gain =>
            AppendLog($"XP +{gain.Amount} (niv {gain.Level}, total {gain.Experience})");
        _client.DeathNotifyReceived += () =>
        {
            AppendLog("Mort signalée par le serveur.");
            BeginLocalDeath();
        };
        _client.PublishedCatalogReceived += OnPublishedCatalogReceived;
        _client.DialogueStatePushReceived += OnDialogueStatePush;
        _client.DialogueChoiceResultReceived += (ok, msg) =>
        {
            AppendLog(ok ? "Dialogue: " + msg : "Dialogue refusé: " + msg);
            if (!ok)
            {
                _dialoguePanel.ClearDialogue();
                _dialogueSessionOpen = false;
                RefreshInteractHint();
            }
        };
        _client.QuestJournalSnapshotReceived += OnQuestJournalSnapshot;
        _client.QuestTurnInResultReceived += (ok, msg) => AppendLog(ok ? "Quête rendue: " + msg : "Turn-in refusé: " + msg);
        _client.CraftResultReceived += (ok, msg) =>
        {
            var human = ok
                ? (string.IsNullOrWhiteSpace(msg) ? "Fabrication réussie." : "Fabrication : " + PlayerFacingMessages.Redact(msg))
                : "Fabrication impossible : " + PlayerFacingMessages.FromServerOrNetwork(msg);
            AppendLog(ok ? "Craft: " + msg : "Craft refusé: " + msg);
            _craftPanel.SetStatus(human);
            ShowPlayerStatus(human);
        };
        _client.AcquireProfessionResultReceived += (ok, msg) =>
            AppendLog(ok ? "Métier: " + msg : "Métier refusé: " + msg);
        _client.EnvironmentStatePushReceived += OnEnvironmentStatePush;
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
        ApplyCatalogRecipesToCraft(catalog);
        _characterSheet.RefreshBag(ResolveItemName, ResolveItemType);
        SyncStatusPortrait();
        var tilesetFiles = ClientPublishedTilesetMaterializer.Materialize(catalog, AppContext.BaseDirectory, _map?.Name);
        var prefabFiles = ClientPublishedPrefabMaterializer.Materialize(catalog, AppContext.BaseDirectory);
        _ = SyncTilePackAsync(redrawIfReady: _map?.GraphicIdentity == TileGraphicIdentity.TileAsset);
        if (tilesetFiles > 0 || prefabFiles > 0 || _map is not null)
        {
            ReloadTilesetBitmaps();
            ReloadPrefabOverlays();
            ReloadPlaytestPlacedEntities();
            RedrawMap();
        }

        AppendLog(
            $"Catalogue: {catalog.Classes.Count} classe(s), {catalog.Items.Count} objet(s), {catalog.Spells.Count} sort(s), {catalog.Shops.Count} boutique(s), {catalog.Recipes.Count} recette(s), {catalog.Tilesets.Count} tileset(s), {catalog.Prefabs.Count} prefab(s), {catalog.PrefabMaps.Count} carte(s) prefab.");
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

        _shopChromeLock = true;
        _cmbShop.Items.Clear();
        foreach (var shop in ClientShopBankSession.ReadShops(catalog))
        {
            _cmbShop.Items.Add(new ShopPickRow(shop));
        }

        if (_cmbShop.Items.Count > 0)
        {
            _cmbShop.SelectedIndex = 0;
        }

        _shopChromeLock = false;
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
        _cmbMeleeTarget.Items.Add(CombatMvpLimits.DummyName);
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
                ?? CombatMvpLimits.DummyName;
            _cmbMeleeTarget.Text = defaultTarget;
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

        ApplyCatalogRecipesToCraft(catalog);
    }

    private void ApplyCatalogRecipesToCraft(PublishedCatalogWire? catalog)
    {
        if (catalog is null)
        {
            _craftPanel.ClearRecipes();
            return;
        }

        _craftPanel.BindRecipes(catalog.Recipes);
        _craftPanel.SetCraftEnabled(_phase == ClientUiPhase.Playing);
    }

    private void OnShopComboChanged()
    {
        RefreshShopItemCombo();
        if (_shopChromeLock)
        {
            return;
        }

        if (_shopBank.ShopOpen && _cmbShop.SelectedItem is ShopPickRow row)
        {
            _shopBank.Retarget(row.Id, row.Shop.Name);
        }

        _shopBank.Disarm();
        ApplyShopBankChrome();
    }

    private void OnShopItemChanged()
    {
        SyncShopGuidTextBoxes();
        if (_shopChromeLock)
        {
            return;
        }

        _shopBank.Disarm();
        ApplyShopBankChrome();
    }

    private void OnEconomyQuantityChanged()
    {
        if (_shopChromeLock)
        {
            return;
        }

        _shopBank.Disarm();
        ApplyShopBankChrome();
    }

    private void RefreshShopItemCombo()
    {
        var previous = _shopChromeLock;
        _shopChromeLock = true;
        try
        {
            _cmbShopItem.Items.Clear();
            if (_cmbShop.SelectedItem is ShopPickRow shop)
            {
                foreach (var listing in shop.Shop.Listings)
                {
                    _cmbShopItem.Items.Add(new ItemPickRow(listing));
                }
            }

            if (_cmbShopItem.Items.Count > 0)
            {
                _cmbShopItem.SelectedIndex = 0;
            }

            SyncShopGuidTextBoxes();
        }
        finally
        {
            _shopChromeLock = previous;
        }

        if (!previous)
        {
            ApplyShopBankChrome();
        }
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
            itemId = item.Listing.ItemId;
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
        if (!enabled)
        {
            _shopForm.HideShop();
            _shopBank.CloseSilent();
            _shopBank.Disarm();
        }

        _btnShopBuy.Enabled = enabled;
        _btnShopToggle.Enabled = enabled && _cmbShop.Items.Count > 0;
        UpdateInventoryActionButtons();
        UpdateBankWithdrawButtons();
        _btnBankDepositGold.Enabled = enabled;
        _btnBankWithdrawGold.Enabled = enabled;
        _btnSpell.Enabled = enabled;
        _btnPickup.Enabled = enabled && _lstGround.Items.Count > 0;
        _craftPanel.SetCraftEnabled(enabled);
        _cmbShop.Enabled = enabled && _cmbShop.Items.Count > 0;
        _cmbShopItem.Enabled = enabled && _cmbShopItem.Items.Count > 0;
        _cmbSpell.Enabled = enabled && _cmbSpell.Items.Count > 0;
        ApplyShopBankChrome();
    }

    private void UpdateInventoryActionButtons()
    {
        var enabled = _client is { IsConnected: true };
        var hasSelection = _inventoryPanel.SelectedInventorySlot is not null;
        _btnShopSell.Enabled = enabled && hasSelection;
        _btnBankDepositItem.Enabled = enabled && hasSelection;
    }

    private void UpdateBankWithdrawButtons()
    {
        var enabled = _client is { IsConnected: true };
        _btnBankWithdrawItem.Enabled = enabled && _lstBank.SelectedItem is BankRow;
    }

    private void OnMeleeMiss()
    {
        _combatHud.ApplyMiss(DateTime.UtcNow);
        if (_phase == ClientUiPhase.Playing)
        {
            RedrawMap();
        }
    }

    private void OnDamageEvent(DamageEvent ev)
    {
        _combatHud.Apply(ev, DateTime.UtcNow, _localFacing);
        if (ev.Killed)
        {
            BeginNamedDeath(ev.TargetName, ev.TargetKind);
        }

        var hpSuffix = ev.Killed ? " (vaincu)" : $" ({ev.RemainingHp}/{ev.MaxHp})";
        AppendLog(!ev.Hit
            ? $"Raté → {ev.TargetName}"
            : ev.Crit
                ? $"Critique {ev.Damage} → {ev.TargetName}{hpSuffix}"
                : $"Dégâts {ev.Damage} → {ev.TargetName}{hpSuffix}");
        if (_phase == ClientUiPhase.Playing)
        {
            RedrawMap();
        }
    }

    private void OnStatusEffect(StatusEffectEvent ev, DamageEvent? damage)
    {
        _combatHud.ApplyStatus(ev, DateTime.UtcNow, damage);
        _statusTips.SetToolTip(_picMap, _combatHud.StatusTooltip);
        var line = StatusEffectText.Log(ev, damage);
        if (line.Length > 0)
        {
            AppendLog(line);
        }

        if (_phase == ClientUiPhase.Playing)
        {
            RedrawMap();
        }
    }

    private void OnCombatState(CombatStateWire state)
    {
        _lastCombatState = state;
        _tradeForm.SetWallet(state.Gold);
        _shopBank.SetWallet(state.Gold);
        _lblBank.Text = _shopBank.BankLine;
        _lblCombat.Text =
            $"Niv {state.Level} · XP {state.Experience} · HP {state.Hp}/{state.MaxHp} · MP {state.Mp}/{state.MaxMp} · Or {state.Gold}";
        _btnRespawn.Visible = state.IsDead;
        _btnRespawn.Enabled = state.IsDead;
        _hudStatus.ApplyCombat(state, _username);
        SyncStatusPortrait();
        if (state.IsDead)
        {
            BeginLocalDeath();
            LayoutGameHud();
        }
        else if (_localAction == SpriteAction.Death)
        {
            ClearLocalAction();
        }
    }

    private void OnInventorySnapshot(InventorySnapshotWire snapshot)
    {
        _paperdoll = _paperdoll.WithServerLoadout(snapshot);
        SyncStatusPortrait();
        _inventoryPanel.ApplySnapshot(snapshot);
        _shopBank.SetBag(BuildShopBag(snapshot));
        _tradeForm.SetBag(BuildTradeBag(snapshot));
        _equipmentPanel.ApplySnapshot(snapshot);
        _characterSheet.ApplyBag(snapshot, ResolveItemName, ResolveItemType);
        UpdateInventoryActionButtons();
        AppendLog($"Inventaire: {snapshot.Slots.Count(s => s.ItemId is not null && s.Quantity > 0)} slot(s) rempli(s).");
        if (_phase == ClientUiPhase.Playing && _map is not null)
        {
            RedrawMap();
        }
    }

    private void OnTradeSnapshot(TradeSnapshotWire snapshot)
    {
        SyncTradeIdentity();
        _tradeForm.ApplySnapshot(snapshot, ResolveItemName);
        AppendLog(
            $"Échange rév {snapshot.Revision}: {snapshot.InitiatorName} ({snapshot.InitiatorOffer.Gold} or) ↔ {snapshot.PartnerName} ({snapshot.PartnerOffer.Gold} or)");
        if (snapshot.Status is TradeStatus.Inviting or TradeStatus.Open)
        {
            if (!_tradeForm.Visible)
            {
                _tradeForm.Show(this);
            }
        }
    }

    private void SyncTradeIdentity()
    {
        if (Guid.TryParse(_activeCharacterId, out var id))
        {
            _tradeForm.SetLocalCharacter(id);
        }
    }

    private List<ShopBagSlot> BuildShopBag(InventorySnapshotWire snapshot)
    {
        var bag = new List<ShopBagSlot>();
        foreach (var slot in snapshot.Slots)
        {
            if (slot.ItemId is not Guid id || slot.Quantity <= 0)
            {
                continue;
            }

            var item = FindPublishedItem(id);
            bag.Add(new ShopBagSlot(
                slot.SlotIndex,
                id,
                slot.Quantity,
                ResolveItemName(id),
                item?.SellPrice ?? 0,
                item?.MaxStack ?? 0,
                item?.Stackable ?? false));
        }

        return bag;
    }

    private List<TradeBagEntry> BuildTradeBag(InventorySnapshotWire snapshot)
    {
        var bag = new List<TradeBagEntry>();
        foreach (var slot in snapshot.Slots)
        {
            if (slot.ItemId is not Guid id || slot.Quantity <= 0)
            {
                continue;
            }

            var index = bag.FindIndex(entry => entry.ItemId == id);
            if (index >= 0)
            {
                var prev = bag[index];
                bag[index] = prev with { Quantity = prev.Quantity + slot.Quantity };
            }
            else
            {
                bag.Add(new TradeBagEntry(id, slot.Quantity, ResolveItemName(id)));
            }
        }

        return bag;
    }

    private async Task SendTradeActionAsync(byte action, Guid tradeId, byte[] extra)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendTradeAsync(action, tradeId, Guid.NewGuid(), extra).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Échange: " + ex.Message);
        }
    }

    /// <summary>Nom publié (catalogue) pour un ItemId ; secours GUID court si catalogue absent/objet inconnu.</summary>
    private string ResolveItemName(Guid itemId)
    {
        var match = FindPublishedItem(itemId);
        return match is not null ? match.Name : itemId.ToString("N")[..8];
    }

    /// <summary>Type publié (Weapon / Armor / …). Null si le catalogue ne le connaît pas.</summary>
    private ItemType? ResolveItemType(Guid itemId)
    {
        var match = FindPublishedItem(itemId);
        return match is not null && CharacterSheetGear.TryParseItemType(match.Type, out var type)
            ? type
            : null;
    }

    private PublishedItemWireEntry? FindPublishedItem(Guid itemId) =>
        _publishedCatalog?.Items.FirstOrDefault(i =>
            Guid.TryParse(i.Id, out var parsed) && parsed == itemId);

    private void OnBankSnapshot(BankSnapshotWire snapshot)
    {
        _bankSnapshot = snapshot;
        var bankSlots = new List<ShopBagSlot>();
        foreach (var slot in snapshot.Slots)
        {
            if (slot.ItemId is not Guid id || slot.Quantity <= 0)
            {
                continue;
            }

            var item = FindPublishedItem(id);
            bankSlots.Add(new ShopBagSlot(
                slot.SlotIndex,
                id,
                slot.Quantity,
                ResolveItemName(id),
                item?.SellPrice ?? 0,
                item?.MaxStack ?? 0,
                item?.Stackable ?? false));
        }

        _shopBank.SetBank(snapshot.BankGold, bankSlots);
        _lblBank.Text = _shopBank.BankLine;

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
        if (_map is not null)
        {
            RedrawMap();
        }

        TryRequestWalkOnPickup(steppedOntoTile: false);
    }

    /// <summary>
    /// Walk-on pickup when the local player steps onto a tile that already has loot.
    /// A drop or a kill that lands under a stationary player stays until they leave and come back, or use Ramasser.
    /// </summary>
    private void TryRequestWalkOnPickup(bool steppedOntoTile)
    {
        if (_client is null || !_client.IsConnected || _groundSnapshot is null)
        {
            return;
        }

        if (_sessionDisplayedMapId != 0 && _groundSnapshot.MapId != _sessionDisplayedMapId)
        {
            return;
        }

        var tile = GroundLootPlacement.PixelToTile(_srvPixelX, _srvPixelY);
        var here = (_groundSnapshot.MapId, tile.X, tile.Y);
        if (!steppedOntoTile && _walkOnScannedTile == here)
        {
            return;
        }

        _walkOnScannedTile = here;
        foreach (var item in _groundSnapshot.Items)
        {
            if (GroundLootPlacement.PixelToTile(item.PixelX, item.PixelY) != tile)
            {
                continue;
            }

            if (!_walkOnPickupSent.Add(item.GroundItemId))
            {
                continue;
            }

            var groundItemId = item.GroundItemId;
            _ = SendWalkOnPickupAsync(groundItemId);
        }
    }

    private async Task SendWalkOnPickupAsync(Guid groundItemId)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.SendPickupItemAsync(groundItemId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _walkOnPickupSent.Remove(groundItemId);
            AppendLog("Ramasser: " + ex.Message);
        }
    }

    private void OnDialogueStatePush(DialogueStateWire state)
    {
        _dialoguePanel.ApplyState(state);
        var tile = CurrentPlayerTile();
        _dialogueAnchorTileX = tile.TileX;
        _dialogueAnchorTileY = tile.TileY;
        _dialogueSessionOpen = true;
        RefreshInteractHint();
        AppendLog($"Dialogue: {state.Speaker} — {state.Choices.Count} choix");
    }

    private void OnQuestJournalSnapshot(IReadOnlyList<QuestJournalEntryWire> entries)
    {
        _questJournalPanel.ApplySnapshot(entries);
        _hudQuest.ApplySnapshot(entries);
        AppendLog($"Journal quêtes: {entries.Count} entrée(s)");
    }

    private void OnEnvironmentStatePush(EnvironmentStateWire state)
    {
        _lastEnvironment = state;
        _environmentPanel.ApplyState(state);
        ApplyPublishedWeather();
        AppendLog($"Environnement map={state.MapId} éclairage={state.LightingLevel}");
    }

    private void ApplyPublishedWeather()
    {
        var env = _lastEnvironment;
        _weatherPlan = WeatherResolver.Resolve(
            env?.WeatherProfileId,
            env?.LightingLevel ?? 255,
            env?.WeatherKind,
            _weatherDebug);
        _sound.ApplyWeather(_weatherPlan);
        if (_map is not null)
        {
            RedrawMap();
        }
    }

    private void CycleWeatherDebug()
    {
        _weatherDebug = WeatherResolver.Cycle(_weatherDebug);
        ApplyPublishedWeather();
        var label = _weatherDebug == WeatherDebugOverride.Auto
            ? $"auto ({_weatherPlan.DisplayName})"
            : _weatherPlan.DisplayName;
        AppendLog("Météo debug: " + label);
        ShowPlayerStatus("Météo: " + label);
    }

    private async Task SendDialogueChoiceAsync(byte[] sessionToken, string choiceId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendDialogueChoiceAsync(sessionToken, choiceId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Choix dialogue: " + ex.Message);
        }
    }

    private async Task QuestTurnInAsync(Guid questId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendQuestTurnInAsync(questId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Turn-in quête: " + ex.Message);
        }
    }

    private async Task CraftAsync(Guid recipeId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendCraftAsync(recipeId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Craft: " + PlayerFacingMessages.FromException(ex));
            _craftPanel.SetStatus(PlayerFacingMessages.FromException(ex));
            ShowPlayerStatus("Fabrication impossible.");
        }
    }

    private async Task AcquireProfessionAsync(Guid professionId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendAcquireProfessionAsync(professionId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Métier: " + ex.Message);
        }
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
            ShowPlayerStatus(PlayerFacingMessages.FromServerOrNetwork(message));
            return;
        }

        // Succès : `message` est le jeton de session lui-même (echo du ReconnectRequest) —
        // ne jamais l'écrire dans le log (fenêtre UI ou stdout playtest).
        AppendLog("Reconnect OK");
        ShowPlayerStatus("Session reprise.");
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

    private bool EnsureEconomyOnline()
    {
        if (_client is { IsConnected: true })
        {
            return true;
        }

        ShowPlayerStatus("Connexion interrompue.");
        return false;
    }

    private void FinishEconomy(ShopBankAction hinted, bool ok, string msg, string okPrefix, string failPrefix)
    {
        var action = hinted;
        if (hinted == ShopBankAction.DepositItem && _shopBank.InFlight == ShopBankAction.DepositGold)
        {
            action = ShopBankAction.DepositGold;
        }
        else if (hinted == ShopBankAction.WithdrawItem && _shopBank.InFlight == ShopBankAction.WithdrawGold)
        {
            action = ShopBankAction.WithdrawGold;
        }

        AppendLog(ok ? okPrefix + msg : failPrefix + msg);
        _shopBank.NoteResult(action, ok, msg);
        var human = _shopBank.ConsumeToast();
        if (!string.IsNullOrWhiteSpace(human))
        {
            ShowPlayerStatus(human);
        }

        ApplyShopBankChrome();
    }

    private async Task CommitEconomyAsync(bool send, string? blocked, ShopBankAction action, Func<Task> sendAsync, string failureLogPrefix)
    {
        ApplyShopBankChrome();
        if (!send)
        {
            _shopBank.ConsumeToast();
            ShowPlayerStatus(blocked ?? "Opération refusée.");
            return;
        }

        try
        {
            _shopBank.MarkInFlight(action);
            await sendAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _shopBank.ClearInFlight();
            AppendLog(failureLogPrefix + ex.Message);
            ShowPlayerStatus(PlayerFacingMessages.FromException(ex));
            ApplyShopBankChrome();
        }
    }

    private async Task ShopBuyAsync()
    {
        if (!EnsureEconomyOnline())
        {
            return;
        }

        if (!TryResolveShopSelection(out var shopId, out var itemId) || _cmbShopItem.SelectedItem is not ItemPickRow row)
        {
            ShowPlayerStatus("Choisissez une boutique et un article.");
            return;
        }

        var qty = (int)_numShopQty.Value;
        var send = _shopBank.TryConfirmBuy(shopId, row.Listing, qty, out var blocked);
        await CommitEconomyAsync(
            send,
            blocked,
            ShopBankAction.Buy,
            () => _client!.SendShopBuyAsync(shopId, itemId, qty),
            "Achat: ").ConfigureAwait(true);
    }

    private async Task ShopSellAsync()
    {
        if (!EnsureEconomyOnline())
        {
            return;
        }

        if (_inventoryPanel.SelectedInventorySlot is not byte slot)
        {
            ShowPlayerStatus("Sélectionnez un objet dans l'inventaire.");
            return;
        }

        var qty = (int)_numShopQty.Value;
        var send = _shopBank.TryConfirmSell(slot, qty, out var blocked);
        await CommitEconomyAsync(
            send,
            blocked,
            ShopBankAction.Sell,
            () => _client!.SendShopSellAsync(slot, qty),
            "Vente: ").ConfigureAwait(true);
    }

    private async Task BankDepositItemAsync()
    {
        if (!EnsureEconomyOnline())
        {
            return;
        }

        if (_inventoryPanel.SelectedInventorySlot is not byte slot)
        {
            ShowPlayerStatus("Sélectionnez un objet dans l'inventaire.");
            return;
        }

        var qty = (int)_numBankQty.Value;
        var send = _shopBank.TryConfirmDepositItem(slot, qty, out var blocked);
        await CommitEconomyAsync(
            send,
            blocked,
            ShopBankAction.DepositItem,
            () => _client!.SendBankDepositItemAsync(slot, qty),
            "Banque dépôt: ").ConfigureAwait(true);
    }

    private async Task BankWithdrawItemAsync()
    {
        if (!EnsureEconomyOnline())
        {
            return;
        }

        if (_lstBank.SelectedItem is not BankRow row)
        {
            ShowPlayerStatus("Sélectionnez un objet dans la banque.");
            return;
        }

        var qty = (int)_numBankQty.Value;
        var send = _shopBank.TryConfirmWithdrawItem(row.SlotIndex, qty, out var blocked);
        await CommitEconomyAsync(
            send,
            blocked,
            ShopBankAction.WithdrawItem,
            () => _client!.SendBankWithdrawItemAsync((byte)row.SlotIndex, qty),
            "Banque retrait: ").ConfigureAwait(true);
    }

    private async Task BankDepositGoldAsync()
    {
        if (!EnsureEconomyOnline())
        {
            return;
        }

        var amount = (int)_numBankGold.Value;
        var send = _shopBank.TryConfirmDepositGold(amount, out var blocked);
        await CommitEconomyAsync(
            send,
            blocked,
            ShopBankAction.DepositGold,
            () => _client!.SendBankDepositGoldAsync(amount),
            "Dépôt or: ").ConfigureAwait(true);
    }

    private async Task BankWithdrawGoldAsync()
    {
        if (!EnsureEconomyOnline())
        {
            return;
        }

        var amount = (int)_numBankGold.Value;
        var send = _shopBank.TryConfirmWithdrawGold(amount, out var blocked);
        await CommitEconomyAsync(
            send,
            blocked,
            ShopBankAction.WithdrawGold,
            () => _client!.SendBankWithdrawGoldAsync(amount),
            "Retrait or: ").ConfigureAwait(true);
    }

    private void ToggleShopFromUi()
    {
        if (_shopBank.ShopOpen)
        {
            CloseShopWindow(announce: true);
            return;
        }

        if (_cmbShop.SelectedItem is not ShopPickRow shop)
        {
            ShowPlayerStatus("Aucune boutique dans le catalogue.");
            return;
        }

        OpenShopWindow(shop.Id, showForm: true);
    }

    private void OpenShopWindow(Guid shopId, bool showForm)
    {
        ShopPickRow? match = null;
        for (var i = 0; i < _cmbShop.Items.Count; i++)
        {
            if (_cmbShop.Items[i] is ShopPickRow row && row.Id == shopId)
            {
                match = row;
                if (_cmbShop.SelectedIndex != i)
                {
                    _cmbShop.SelectedIndex = i;
                }

                break;
            }
        }

        if (match is null)
        {
            ShowPlayerStatus("Boutique inconnue.");
            return;
        }

        _shopBank.Open(shopId, match.Shop.Name);
        var status = _shopBank.ConsumeToast() ?? _shopBank.StatusLine;
        ApplyShopBankChrome();
        if (showForm)
        {
            if (!_shopForm.Visible)
            {
                _shopForm.Show(this);
            }

            ApplyShopBankChrome();
        }

        ShowPlayerStatus(status);
    }

    private void CloseShopWindow(bool announce)
    {
        var closed = _shopBank.Close(out var status);
        _shopForm.HideShop();
        ApplyShopBankChrome();
        if (announce && closed)
        {
            _shopBank.ConsumeToast();
            ShowPlayerStatus(status);
        }
        else
        {
            _shopBank.ConsumeToast();
        }
    }

    private void ApplyShopBankChrome()
    {
        _btnShopBuy.Text = _shopBank.BuyLabel;
        _btnShopSell.Text = _shopBank.SellLabel;
        _btnBankDepositItem.Text = _shopBank.DepositItemLabel;
        _btnBankWithdrawItem.Text = _shopBank.WithdrawItemLabel;
        _btnBankDepositGold.Text = _shopBank.DepositGoldLabel;
        _btnBankWithdrawGold.Text = _shopBank.WithdrawGoldLabel;
        _btnShopToggle.Text = _shopBank.ToggleLabel;
        _lblShopListing.Text = _cmbShopItem.SelectedItem is ItemPickRow row
            ? row.ToString()
            : "—";
        _lblBank.Text = _shopBank.BankLine;
        if (!_shopForm.Visible)
        {
            return;
        }

        var lines = new List<string>(_cmbShopItem.Items.Count);
        foreach (var item in _cmbShopItem.Items)
        {
            lines.Add(item?.ToString() ?? "—");
        }

        _shopForm.Bind(
            _shopBank.ShopTitle,
            lines,
            _cmbShopItem.SelectedIndex,
            _shopBank.StatusLine,
            _shopBank.BuyLabel,
            _shopBank.SellLabel,
            (int)_numShopQty.Value,
            _btnShopBuy.Enabled,
            _btnShopSell.Enabled);
    }

    private bool TryToggleNearbyShop()
    {
        if (_phase != ClientUiPhase.Playing || _map is null || _publishedCatalog is null)
        {
            return false;
        }

        var tile = CurrentPlayerTile();
        if (DialogueOpenForHint(tile.TileX, tile.TileY) || InteractInputBlocked())
        {
            return false;
        }

        var anchors = new List<ShopNpcAnchor>(_mapEvents.Count + _playtestPlacedEntities.Count);
        foreach (var ev in _mapEvents)
        {
            anchors.Add(new ShopNpcAnchor(ev.TileX, ev.TileY, ShopAnchorKind.Event, ev.ScriptKey, ev.DisplayName, null));
        }

        foreach (var entity in _playtestPlacedEntities)
        {
            if (entity.Kind != MapPlacedKind.Npc)
            {
                continue;
            }

            anchors.Add(new ShopNpcAnchor(entity.TileX, entity.TileY, ShopAnchorKind.Npc, null, entity.Name, entity.Notes));
        }

        if (!ShopNpcLink.TryResolve(tile.TileX, tile.TileY, anchors, _publishedCatalog, out var shopId, out _))
        {
            return false;
        }

        if (_shopBank.ShopOpen && _shopBank.OpenShopId == shopId)
        {
            CloseShopWindow(announce: true);
            return true;
        }

        OpenShopWindow(shopId, showForm: true);
        return true;
    }

    private bool ShopInReach(int tileX, int tileY)
    {
        if (_publishedCatalog is null)
        {
            return false;
        }

        var anchors = new List<ShopNpcAnchor>(_mapEvents.Count + _playtestPlacedEntities.Count);
        foreach (var ev in _mapEvents)
        {
            anchors.Add(new ShopNpcAnchor(ev.TileX, ev.TileY, ShopAnchorKind.Event, ev.ScriptKey, ev.DisplayName, null));
        }

        foreach (var entity in _playtestPlacedEntities)
        {
            if (entity.Kind != MapPlacedKind.Npc)
            {
                continue;
            }

            anchors.Add(new ShopNpcAnchor(entity.TileX, entity.TileY, ShopAnchorKind.Npc, null, entity.Name, entity.Notes));
        }

        return ShopNpcLink.TryResolve(tileX, tileY, anchors, _publishedCatalog, out _, out _);
    }

    private async Task ConnectAsync()
    {
        if (_client is null)
        {
            return;
        }

        _connectInFlight = true;
        RefreshDiagnosticOverlay();
        try
        {
            _btnConnect.Enabled = false;
            _btnRetry.Enabled = false;
            var host = _txtHost.Text.Trim();
            var port = (int)_numPort.Value;
            var tls = ClientTlsOptions.FromEnvironment(host);
            PersistLastEndpoint(host, port);
            await _client.ConnectAsync(host, port, tls).ConfigureAwait(true);
            var connected = tls.Mode == TlsTransportMode.Required
                ? $"TLS connecté {host}:{port} SNI={tls.TargetHost}"
                : $"TCP connecté {host}:{port}";
            AppendLog(connected);
            ClearConnectFailure();
            ShowPlayerStatus(PlayerFacingMessages.Connected);
            _ = SyncTilePackAsync(redrawIfReady: false);
            _btnDisconnect.Enabled = true;
            _btnLogin.Enabled = true;
            _btnRegister.Enabled = true;
            UpdateAuthTokenUi();
        }
        catch (Exception ex)
        {
            var kind = PlayerFacingMessages.ClassifyException(ex);
            var human = PlayerFacingMessages.FromException(ex);
            AppendLog("Connexion: " + human);
            ShowPlayerStatus(human);
            NoteConnectFailure(kind, human);
            _btnConnect.Enabled = true;
        }
        finally
        {
            _connectInFlight = false;
            RefreshDiagnosticOverlay();
        }
    }

    private async Task RetryAsync()
    {
        if (_client is { IsConnected: true })
        {
            await LoginAsync().ConfigureAwait(true);
            return;
        }

        await ConnectAsync().ConfigureAwait(true);
    }

    private async Task DisconnectAsync()
    {
        _heartbeatTimer.Stop();
        if (_client is not null)
        {
            await _client.DisconnectAsync().ConfigureAwait(true);
        }

        var tradeNote = _tradeForm.NotifyLocalDisconnect();
        var shopNote = _shopBank.NotifyLocalDisconnect();
        ClearConnectFailure();
        ResetUiAfterDisconnect();
        var note = CombinePlayerNotes(tradeNote, shopNote);
        if (note is not null)
        {
            ShowPlayerStatus(note);
        }
    }

    private static string? CombinePlayerNotes(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return first + " " + second;
    }

    private void ResetUiAfterDisconnect()
    {
        _btnConnect.Enabled = true;
        _btnDisconnect.Enabled = false;
        _btnLogin.Enabled = false;
        _btnRegister.Enabled = false;
        _btnMap.Enabled = false;
        _btnMelee.Enabled = false;
        _btnRanged.Enabled = false;
        _btnLogout.Enabled = false;
        ResetCharacterPickUi();
        ClearPublishedCatalogUi();
        SetGameplayControlsEnabled(false);
        _map = null;
        _mapBlockedTiles = null;
        _username = null;
        _rtt.Clear();
        _sessionDisplayedMapId = 0;
        _others.Clear();
        _worldMonsters.Clear();
        _worldNpcs.Clear();
        ResetLocalMotionState();
        ResetPaperdoll();
        ClearMapImage();
        DisposeTilesetBitmaps();
        DisposeTileAssetBitmaps();
        _tileAssetBitmapSha = null;
        DisposePrefabBitmaps();
        _prefabPlacements.Clear();
        _prefabCatalog = null;
        _playtestPlacedEntities.Clear();
        _mapEvents.Clear();
        _dialogueSessionOpen = false;
        ClearEventPictures();
        DismissEventMessage();
        _awaitingPlayingPhase = false;
        _btnBackDisconnect.Enabled = false;
        SetPhase(ClientUiPhase.Login);
    }

    private void ClearPublishedCatalogUi()
    {
        _publishedCatalog = null;
        _shopForm.HideShop();
        _shopBank.CloseSilent();
        _cmbClass.Items.Clear();
        _cmbShop.Items.Clear();
        _cmbShopItem.Items.Clear();
        _cmbSpell.Items.Clear();
        _cmbMeleeTarget.Items.Clear();
        _lstBank.Items.Clear();
        _lstGround.Items.Clear();
        _groundSnapshot = null;
        _walkOnPickupSent.Clear();
        _walkOnScannedTile = null;
        _craftPanel.ClearRecipes();
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
        var tradeNote = _tradeForm.NotifyLocalDisconnect();
        var shopNote = _shopBank.NotifyLocalDisconnect();
        var human = CombinePlayerNotes(tradeNote, shopNote) ?? PlayerFacingMessages.ConnectionLost;
        ShowPlayerStatus(human);
        ResetUiAfterDisconnect();
        NoteConnectFailure(ConnectionFailureKind.ConnectionLost, human);
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
            var human = PlayerFacingMessages.FromServerOrNetwork(message);
            ShowPlayerStatus(human);
            NoteConnectFailure(PlayerFacingMessages.ClassifyServer(message), human);
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
        ClearConnectFailure();
        ShowPlayerStatus(PlayerFacingMessages.LoggedIn);
        PersistRememberedAccount();
        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            // persistance optionnelle
        }

        UpdateAuthTokenUi();
        _username = _playtestOptions is { IsPlaytest: true }
            ? "__frog_playtest__"
            : _txtUser.Text.Trim();
        _btnMap.Enabled = true;
        _btnLogout.Enabled = true;
        _btnMelee.Enabled = false;
        _btnRanged.Enabled = false;
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
        _rtt.Clear();
        _map = null;
        _mapBlockedTiles = null;
        _sessionDisplayedMapId = 0;
        _others.Clear();
        _worldMonsters.Clear();
        _worldNpcs.Clear();
        ResetLocalMotionState();
        ResetPaperdoll();
        ClearMapImage();
        DisposeTilesetBitmaps();
        DisposeTileAssetBitmaps();
        _tileAssetBitmapSha = null;
        DisposePrefabBitmaps();
        _prefabPlacements.Clear();
        _prefabCatalog = null;
        _playtestPlacedEntities.Clear();
        _mapEvents.Clear();
        _dialogueSessionOpen = false;
        ClearEventPictures();
        _btnMap.Enabled = false;
        _btnMelee.Enabled = false;
        _btnRanged.Enabled = false;
        _btnLogout.Enabled = false;
        ResetCharacterPickUi();
        _awaitingPlayingPhase = false;
        SetPhase(ClientUiPhase.Login);
        RefreshDiagnosticOverlay();
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
        _dialogueSessionOpen = false;
        if (_groundSnapshot is null || _groundSnapshot.MapId != mapId)
        {
            _groundSnapshot = null;
            _lstGround.Items.Clear();
            _btnPickup.Enabled = false;
        }

        _sessionDisplayedMapId = mapId;
        _map = map;
        AttachRuntimeTileFlags(map);
        _mapBlockedTiles = MapCollision.IndexBlockedTiles(map);
        _others.Clear();
        _worldMonsters.Clear();
        _worldNpcs.Clear();
        // Ne pas ResetLocalMotionState() ici : un second MapRequest (ex. après CharacterSelectResult)
        // recevrait MapData après PositionUpdate ; la remise à zéro coupait tout envoi PositionSync.
        _motionSmoothLastUtc = DateTime.UtcNow;
        _pendingIdlePositionSync = false;
        if (_localVisualInitialized && !string.IsNullOrEmpty(_username))
        {
            _visLocalCx = _srvPixelX;
            _visLocalCy = _srvPixelY;
            ClampLocalVisToMap();
            SnapCameraToLocalVisual();
        }

        if (_publishedCatalog is not null)
        {
            ClientPublishedTilesetMaterializer.Materialize(_publishedCatalog, AppContext.BaseDirectory, map.Name);
            ClientPublishedPrefabMaterializer.Materialize(_publishedCatalog, AppContext.BaseDirectory);
        }

        ReloadTilesetBitmaps();
        ReloadPrefabOverlays();
        ReloadPlaytestPlacedEntities();
        if (map.GraphicIdentity == TileGraphicIdentity.TileAsset && !_tilePacks.HasVerifiedPack)
        {
            ClearMapImage();
            _ = SyncTilePackAsync(redrawIfReady: true);
        }
        else
        {
            RedrawMap();
        }

        _hudMinimap.RebuildCache(map);
        if (_localVisualInitialized)
        {
            _hudMinimap.SetPlayerPixel(_srvPixelX, _srvPixelY);
        }

        _ = RequestMapEventsFromServerAsync();
        TryEnterPlayingPhaseAfterMapReady();
        RefreshInteractHint();
        KeepFriendsDockAcrossMap(mapId);
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
        KeepFriendsDockAcrossMap(mapId);
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
            RefreshInteractHint();
        }
        catch
        {
            AppendLog($"Événements carte id={mapId}: réponse JSON non analysée.");
        }
    }

    private void OnInteractResult(bool ok, string message, Guid activationId)
    {
        _ = activationId;
        if (ok && MapEventPictureWire.TryTakeInteractMessage(message, out var pictures, out var afterPictures))
        {
            ApplyEventPictures(pictures);
            message = afterPictures;
        }

        if (ok && MapEventShopOpen.TryTakeInteractMessage(message, out var shopId, out var remainder))
        {
            OpenShopWindow(shopId, showForm: true);
            message = remainder;
        }

        AppendLog(ok ? "Interaction: " + message : "Interaction refusée: " + message);
        TryPresentEventMessage(ok, message);
    }

    private void ApplyEventPictures(IReadOnlyList<MapEventPictureOp> ops)
    {
        var changed = false;
        foreach (var op in ops)
        {
            if (op.Erase)
            {
                if (_eventPictures.Remove(op.PictureId, out var removed))
                {
                    removed.Dispose();
                    changed = true;
                }

                continue;
            }

            if (_eventPictures.Remove(op.PictureId, out var previous))
            {
                previous.Dispose();
            }

            _eventPictures[op.PictureId] = new ShownEventPicture(
                op.X,
                op.Y,
                op.Opacity,
                op.Blend,
                EventPictureDraw.Load(op.Asset));
            changed = true;
        }

        if (changed && _map is not null)
        {
            RedrawMap();
        }
    }

    private void ClearEventPictures()
    {
        if (_eventPictures.Count == 0)
        {
            return;
        }

        foreach (var picture in _eventPictures.Values)
        {
            picture.Dispose();
        }

        _eventPictures.Clear();
    }

    private bool TryPresentEventMessage(bool success, string? message)
    {
        var placements = _mapEvents.Select(entry =>
            new EventShowTextPresentation.Placement(entry.DisplayName, entry.Slug));
        if (!EventShowTextPresentation.ShouldOpen(
                success,
                message,
                _dialoguePanel.HasActiveDialogue,
                _dialoguePanel.ActiveSpeaker,
                _dialoguePanel.ActiveBody,
                placements)
            || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        _eventMessage.ShowMessage(message.Trim());
        LayoutGameHud();
        return true;
    }

    private void DismissEventMessage() => _eventMessage.Dismiss();

    private async Task SendInteractAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        TryToggleNearbyShop();
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
        ApplySavedLook(characterId, _activeCharacterName);
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
            if (!string.IsNullOrWhiteSpace(_pendingLookName))
            {
                CharacterLookBook.BindCreatedId(_settings.CharacterLooks, _pendingLookName, message.Trim());
                try
                {
                    _settingsStore.Save(_settings);
                }
                catch
                {
                    // le look nommé reste en mémoire
                }

                _pendingLookName = null;
            }

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

            RememberNamedLook(name, _appearancePicker.Look);
            _pendingLookName = name;
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
            _activeCharacterId = row.Id;
            _activeCharacterName = row.DisplayName;
            ApplySavedLook(row.Id, row.DisplayName);
            await _client.SendCharacterSelectAsync(row.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("CharacterSelect: " + ex.Message);
        }
    }

    private void OnPositionUpdate(string user, int mapId, int x, int y, CombatTargetKind kind)
    {
        if (MonsterAi.TracksAsMonsterSprite(kind))
        {
            NoteMonsterPosition(user, mapId, x, y);
            return;
        }

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
                DismissEventMessage();
                TryScheduleMapRequestAfterWarp(mapId);
            }

            var wasInitialized = _localVisualInitialized;
            var tileBefore = GroundLootPlacement.PixelToTile(_srvPixelX, _srvPixelY);
            if (!wasInitialized)
            {
                _srvPixelX = x;
                _srvPixelY = y;
                _visLocalCx = x;
                _visLocalCy = y;
                _motionSmoothLastUtc = DateTime.UtcNow;
                _localVisualInitialized = true;
                SnapCameraToLocalVisual();
                needImmediateRedraw = true;
                var spawnTile = GroundLootPlacement.PixelToTile(_srvPixelX, _srvPixelY);
                _walkOnScannedTile = (mapId, spawnTile.X, spawnTile.Y);
            }
            else if (x != _srvPixelX || y != _srvPixelY)
            {
                var mapChanged = _sessionDisplayedMapId != 0 && mapId != _sessionDisplayedMapId;
                var (sx, sy) = MovementFluidity.ResolveLocalServerSample(
                    _visLocalCx,
                    _visLocalCy,
                    _srvPixelX,
                    _srvPixelY,
                    x,
                    y,
                    mapChanged);
                _srvPixelX = (int)MathF.Round(sx);
                _srvPixelY = (int)MathF.Round(sy);
                _movementMeasure.NoteLocalCorrection();
                var tileAfter = GroundLootPlacement.PixelToTile(_srvPixelX, _srvPixelY);
                if (tileBefore != tileAfter || mapChanged)
                {
                    TryRequestWalkOnPickup(steppedOntoTile: true);
                }
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
                _movementMeasure.NoteOtherPlayerUpdate();
            }
        }

        if (needImmediateRedraw)
        {
            RedrawMap();
        }

        if (isLocal)
        {
            _hudMinimap.SetPlayerPixel(_srvPixelX, _srvPixelY);
        }
    }

    private void NoteMonsterPosition(string name, int mapId, int x, int y)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_sessionDisplayedMapId != 0 && mapId != _sessionDisplayedMapId)
        {
            return;
        }

        _others.TryRemove(name, out _);
        var isNew = !_worldMonsters.ContainsKey(name);
        var view = _worldMonsters.GetOrAdd(name, _ => new WorldEntityView());
        if (isNew)
        {
            view.ServerPixelX = x;
            view.ServerPixelY = y;
            view.VisCx = x;
            view.VisCy = y;
            RedrawMap();
            return;
        }

        if (view.ServerPixelX != x || view.ServerPixelY != y)
        {
            view.ServerPixelX = x;
            view.ServerPixelY = y;
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
            ChatChannel.Party => "[P]",
            ChatChannel.Guild => "[H]",
            _ => "[?]"
        };
        var target = string.IsNullOrEmpty(to) ? string.Empty : $"→{to} ";
        AppendLog($"{prefix} {from} {target}: {message}");
        _hudChat.AppendChat(ch, from, to, message);
        if (ch == ChatChannel.Whisper
            && !string.IsNullOrEmpty(_username)
            && string.Equals(from, _username, StringComparison.OrdinalIgnoreCase))
        {
            _whisperAwaitingResult = false;
            var who = string.IsNullOrWhiteSpace(to) ? _lastWhisperTarget : to;
            if (!string.IsNullOrWhiteSpace(who))
            {
                ShowPlayerStatus(ChatWhisper.SentTo(who));
            }
        }
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

        if (ModerateWire.TryParseSlashCommand(text, out var modAction, out var modTarget, out var modReason))
        {
            try
            {
                await _client.SendModerateAsync(modAction, modTarget, modReason).ConfigureAwait(true);
                _txtChat.Clear();
            }
            catch (Exception ex)
            {
                AppendLog("Modération: " + ex.Message);
            }

            return;
        }

        if (text.Equals("/trade", StringComparison.OrdinalIgnoreCase))
        {
            var whispered = _txtWhisperTo.Text.Trim();
            if (string.IsNullOrEmpty(whispered))
            {
                ShowPlayerStatus("Indiquez le joueur : /trade Nom, ou le nom au-dessus du chat.");
                return;
            }

            text = "/trade " + whispered;
        }

        if (TradeWire.TryParseSlashCommand(text, out var tradeAction, out var tradeId, out var tradeExtra))
        {
            try
            {
                if (tradeId == Guid.Empty && tradeAction != (byte)TradeAction.Invite)
                {
                    if (_tradeForm.ActiveTradeId is not Guid active)
                    {
                        ShowPlayerStatus("Aucun échange en cours.");
                        return;
                    }

                    tradeId = active;
                }

                if (tradeAction == (byte)TradeAction.Confirm)
                {
                    tradeExtra = TradeWire.BuildRevisionPayload(_tradeForm.DisplayedRevision);
                }

                await _client.SendTradeAsync(tradeAction, tradeId, Guid.NewGuid(), tradeExtra)
                    .ConfigureAwait(true);
                _txtChat.Clear();
            }
            catch (Exception ex)
            {
                AppendLog("Échange: " + ex.Message);
            }

            return;
        }

        if (SocialWire.TryParseSlashCommand(text, out var socialKind, out var socialAction, out var socialExtra))
        {
            try
            {
                await _client.SendSocialAsync(socialKind, socialAction, Guid.NewGuid(), socialExtra)
                    .ConfigureAwait(true);
                _txtChat.Clear();
            }
            catch (Exception ex)
            {
                AppendLog("Social: " + ex.Message);
            }

            return;
        }

        var slash = ChatWhisper.ParseSlash(text, out var slashTarget, out var slashBody);
        if (slash == ChatWhisper.Slash.Incomplete)
        {
            ShowChatNotice(ChatWhisper.SlashHint);
            return;
        }

        var ch = slash == ChatWhisper.Slash.Ready
            ? ChatChannel.Whisper
            : _cmbChannel.SelectedIndex switch
            {
                0 => ChatChannel.Global,
                1 => ChatChannel.Map,
                2 => ChatChannel.Whisper,
                3 => ChatChannel.Party,
                4 => ChatChannel.Guild,
                _ => ChatChannel.Whisper
            };
        var body = slash == ChatWhisper.Slash.Ready ? slashBody : text;
        var whisperTo = slash == ChatWhisper.Slash.Ready ? slashTarget : _txtWhisperTo.Text.Trim();
        if (ch == ChatChannel.Whisper)
        {
            if (!ChatWhisper.TryResolveTarget(
                    whisperTo,
                    SelectedFriendName(),
                    SelectedWorldTargetName(),
                    CombatMvpLimits.DummyName,
                    out whisperTo))
            {
                ShowChatNotice(ChatWhisper.EmptyTarget);
                return;
            }

            if (ChatWhisper.IsSelf(whisperTo, _username, _activeCharacterName))
            {
                ShowChatNotice(ChatWhisper.Self);
                return;
            }

            _txtWhisperTo.Text = whisperTo;
            if (_cmbChannel.SelectedIndex != 2)
            {
                _cmbChannel.SelectedIndex = 2;
            }

            _lastWhisperTarget = whisperTo;
            _whisperAwaitingResult = true;
        }

        try
        {
            await _client.SendChatAsync(ch, whisperTo, body).ConfigureAwait(true);
            _txtChat.Clear();
            FocusChatInput();
            if (ch == ChatChannel.Whisper)
            {
                ShowPlayerStatus(ChatWhisper.SentTo(whisperTo));
            }
        }
        catch (Exception ex)
        {
            _whisperAwaitingResult = false;
            AppendLog("Chat: " + ex.Message);
        }
    }

    private void BeginLocalAttack()
    {
        if (_localAction == SpriteAction.Death)
        {
            return;
        }

        _localAction = SpriteAction.Attack;
        _localActionElapsedMs = 0;
        if (_phase == ClientUiPhase.Playing && _map is not null)
        {
            RedrawMap();
        }
    }

    private void BeginLocalDeath()
    {
        if (_localAction == SpriteAction.Death)
        {
            return;
        }

        _localAction = SpriteAction.Death;
        _localActionElapsedMs = 0;
        if (_phase == ClientUiPhase.Playing && _map is not null)
        {
            RedrawMap();
        }
    }

    private void ClearLocalAction()
    {
        if (_localAction == SpriteAction.Walk && _localActionElapsedMs == 0)
        {
            return;
        }

        _localAction = SpriteAction.Walk;
        _localActionElapsedMs = 0;
        if (_phase == ClientUiPhase.Playing && _map is not null)
        {
            RedrawMap();
        }
    }

    private void BeginNamedAttack(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var played = false;
        if (_others.TryGetValue(name, out var other) && other.Action != SpriteAction.Death)
        {
            other.Action = SpriteAction.Attack;
            other.ActionElapsedMs = 0;
            played = true;
        }

        if (_worldMonsters.TryGetValue(name, out var monster) && monster.Action != SpriteAction.Death)
        {
            monster.Action = SpriteAction.Attack;
            monster.ActionElapsedMs = 0;
            played = true;
        }

        if (played && _phase == ClientUiPhase.Playing && _map is not null)
        {
            RedrawMap();
        }
    }

    private void BeginNamedDeath(string name, CombatTargetKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (kind == CombatTargetKind.Player)
        {
            if (string.Equals(name, _username, StringComparison.OrdinalIgnoreCase))
            {
                BeginLocalDeath();
                return;
            }

            if (_others.TryGetValue(name, out var other) && other.Action != SpriteAction.Death)
            {
                other.Action = SpriteAction.Death;
                other.ActionElapsedMs = 0;
            }

            return;
        }

        var table = kind switch
        {
            CombatTargetKind.Monster => _worldMonsters,
            CombatTargetKind.Npc => _worldNpcs,
            _ => null,
        };
        if (table is not null && table.TryGetValue(name, out var entity) && entity.Action != SpriteAction.Death)
        {
            entity.Action = SpriteAction.Death;
            entity.ActionElapsedMs = 0;
        }
    }

    private Task MeleeAsync() => SendAttackAsync(AttackStyle.Melee);

    private Task RangedAsync() => SendAttackAsync(AttackStyle.Ranged);

    private async Task SendAttackAsync(AttackStyle style)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        var t = _cmbMeleeTarget.Text.Trim();
        if (string.IsNullOrEmpty(t))
        {
            t = CombatMvpLimits.DummyName;
        }

        var kind = string.Equals(t, CombatMvpLimits.DummyName, StringComparison.OrdinalIgnoreCase)
            ? CombatTargetKind.Dummy
            : CombatTargetKind.None;
        var apply = kind == CombatTargetKind.Dummy
            ? style == AttackStyle.Ranged ? StatusEffectKind.Stun : StatusEffectKind.Poison
            : StatusEffectKind.None;
        var label = style == AttackStyle.Ranged ? "Distance" : "Mêlée";
        BeginLocalAttack();
        try
        {
            await _client.SendMeleeAttackAsync(
                t,
                kind,
                _localFacing,
                Guid.Empty,
                style: style,
                applyStatus: apply).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog(label + ": " + ex.Message);
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
            _rtt.NoteSent(DateTime.UtcNow);
            await _client.SendHeartbeatAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _rtt.CancelPending();
            if (_diagnosticOverlay.Visible)
            {
                _diagnosticLastError = PlayerFacingMessages.Redact(PlayerFacingMessages.FromException(ex));
                RefreshDiagnosticOverlay();
            }
        }
    }

    private void OnHeartbeatAck()
    {
        if (IsDisposed)
        {
            return;
        }

        _rtt.NoteAck(DateTime.UtcNow);
        RefreshDiagnosticOverlay();
    }

    private void MainShell_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F1)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            OpenHelp();
            return;
        }

        if (e.KeyCode == DiagnosticOverlayPanel.ToggleKey)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ToggleDiagnosticOverlay();
            return;
        }

        if (DiagnosticDismissRequested(e.KeyCode)
            && _diagnosticOverlay.Visible
            && !InputService.IsTextInputFocus(ActiveControl))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HideDiagnosticOverlay();
            return;
        }

        if (e.KeyCode == Keys.F9 && _phase == ClientUiPhase.Login)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _loginShell.ToggleOps();
            return;
        }

        if (_phase != ClientUiPhase.Playing)
        {
            if (_phase == ClientUiPhase.CharacterSelect && TryAppearanceArrow(e))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            return;
        }

        if (TryHandleEventMessageKey(e))
        {
            return;
        }

        if (e.KeyCode == Keys.F8)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CycleWeatherDebug();
            return;
        }

        if (e.KeyCode == Keys.C
            && !_input.IsAttack(e.KeyCode)
            && !_input.IsMovementOrInteract(e.KeyCode)
            && !InputService.IsTextInputFocus(ActiveControl))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ToggleCharacterSheet();
            return;
        }

        if (TryHandleChatComposeKey(e))
        {
            return;
        }

        if (_client is null || !_client.IsConnected || string.IsNullOrEmpty(_username))
        {
            return;
        }

        if (InputService.IsTextInputFocus(ActiveControl))
        {
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            var chatFocused = ChatComposeFocused();
            SetWindowLayerVisible(false);
            if (_friendsSticky.TryDismiss(chatFocused, mapChanged: false))
            {
                ApplyFriendsDock();
            }

            e.Handled = true;
            return;
        }

        if (_input.IsAttack(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = MeleeAsync();
            return;
        }

        if (TryActivateHotbarKey(e.KeyCode))
        {
            e.Handled = true;
            return;
        }

        if (_input.IsInteract(e.KeyCode))
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

        if (!_input.IsMoveLeft(e.KeyCode)
            && !_input.IsMoveRight(e.KeyCode)
            && !_input.IsMoveUp(e.KeyCode)
            && !_input.IsMoveDown(e.KeyCode))
        {
            return;
        }

        var isNewEdge = _keysDown.Add(e.KeyCode);
        if (isNewEdge)
        {
            _movementMeasure.NoteKeyPress();
        }

        RecomputeHeldMoveKeys();
        e.Handled = true;
    }

    private void PrimeMoveNetworkPulse()
    {
        _lastMoveSendUtc = DateTime.UtcNow.AddMilliseconds(-MoveNetworkPulseMs - 1);
        TrySendHeldMoveNetwork();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_phase == ClientUiPhase.Playing && ChatComposeFocused())
        {
            var key = keyData & Keys.KeyCode;
            if (key is Keys.Up or Keys.Down)
            {
                // Flèches haut/bas d'une zone mono-ligne : ne pas donner le focus au monde.
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void MainShell_KeyUp(object? sender, KeyEventArgs e)
    {
        if (_phase != ClientUiPhase.Playing)
        {
            _keysDown.Remove(e.KeyCode);
            return;
        }

        if (ChatComposeFocused() || InputService.IsTextInputFocus(ActiveControl))
        {
            if (_keysDown.Remove(e.KeyCode) || _holdLeft || _holdRight || _holdUp || _holdDown)
            {
                StopMovementForChat();
            }

            return;
        }

        if (!_keysDown.Remove(e.KeyCode)
            && !_input.IsMoveLeft(e.KeyCode)
            && !_input.IsMoveRight(e.KeyCode)
            && !_input.IsMoveUp(e.KeyCode)
            && !_input.IsMoveDown(e.KeyCode))
        {
            return;
        }

        RecomputeHeldMoveKeys();
        e.Handled = true;
    }

    private void RecomputeHeldMoveKeys()
    {
        var left = false;
        var right = false;
        var up = false;
        var down = false;
        foreach (var key in _keysDown)
        {
            left |= _input.IsMoveLeft(key);
            right |= _input.IsMoveRight(key);
            up |= _input.IsMoveUp(key);
            down |= _input.IsMoveDown(key);
        }

        var becameHeld = (left && !_holdLeft) || (right && !_holdRight) || (up && !_holdUp) || (down && !_holdDown);
        var wasHolding = _holdLeft || _holdRight || _holdUp || _holdDown;
        _holdLeft = left;
        _holdRight = right;
        _holdUp = up;
        _holdDown = down;
        if (becameHeld)
        {
            _movementMeasure.NoteMoveIntent();
            // First paint in the KeyDown stack — baseline intent→visible mean 20.3 ms waited on the 16 ms timer.
            AdvanceMovementSmoothing();
            RedrawMap();
            PrimeMoveNetworkPulse();
        }

        if (wasHolding && !left && !right && !up && !down)
        {
            ScheduleIdlePositionSyncIfAllReleased();
        }
    }

    private void RedrawMap()
    {
        if (_map is null)
        {
            return;
        }

        ResetTileAssetBitmapCache();
        var lcx = (float)_srvPixelX;
        var lcy = (float)_srvPixelY;
        if (_localVisualInitialized)
        {
            lcx = _visLocalCx;
            lcy = _visLocalCy;
        }

        var otherPx = new Dictionary<string, (float CxPx, float CyPx)>(_others.Count, StringComparer.OrdinalIgnoreCase);
        var otherPoses = new Dictionary<string, PlayerSpritePose>(_others.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _others)
        {
            otherPx[kv.Key] = (kv.Value.VisCx, kv.Value.VisCy);
            otherPoses[kv.Key] = new PlayerSpritePose(
                kv.Value.Facing,
                kv.Value.Walking,
                kv.Value.WalkElapsedMs,
                kv.Value.Action,
                kv.Value.ActionElapsedMs);
        }

        var npcPx = new Dictionary<string, (float CxPx, float CyPx)>(_worldNpcs.Count, StringComparer.OrdinalIgnoreCase);
        var npcPoses = new Dictionary<string, WorldSpritePose>(_worldNpcs.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _worldNpcs)
        {
            npcPx[kv.Key] = (kv.Value.VisCx, kv.Value.VisCy);
            npcPoses[kv.Key] = new WorldSpritePose(
                kv.Value.Facing,
                kv.Value.Walking,
                kv.Value.WalkElapsedMs,
                kv.Value.Action,
                kv.Value.ActionElapsedMs);
        }

        var monsterPx = new Dictionary<string, (float CxPx, float CyPx)>(_worldMonsters.Count, StringComparer.OrdinalIgnoreCase);
        var monsterPoses = new Dictionary<string, WorldSpritePose>(_worldMonsters.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _worldMonsters)
        {
            monsterPx[kv.Key] = (kv.Value.VisCx, kv.Value.VisCy);
            monsterPoses[kv.Key] = new WorldSpritePose(
                kv.Value.Facing,
                kv.Value.Walking,
                kv.Value.WalkElapsedMs,
                kv.Value.Action,
                kv.Value.ActionElapsedMs);
        }

        var localWalking = TryGetHeldMoveDiscrete(out _, out _);
        var localPose = new PlayerSpritePose(
            _localFacing,
            localWalking,
            _localWalkElapsedMs,
            _localAction,
            _localActionElapsedMs);
        IReadOnlyList<(int PixelX, int PixelY)>? groundLoot = null;
        if (_groundSnapshot is { } groundSnap
            && groundSnap.MapId == _sessionDisplayedMapId
            && groundSnap.Items.Count > 0)
        {
            groundLoot = groundSnap.Items.Select(item => (item.PixelX, item.PixelY)).ToArray();
        }

        var bmp = MapViewRenderer.Render(
            _map,
            otherPx,
            _username,
            lcx,
            lcy,
            _tilesetBitmaps,
            _mapEvents,
            localPose: localPose,
            otherPoses: otherPoses,
            prefabPlacements: _prefabPlacements,
            prefabCatalog: _prefabCatalog,
            prefabBitmaps: _prefabBitmaps,
            npcCentersPx: npcPx,
            npcPoses: npcPoses,
            monsterCentersPx: monsterPx,
            monsterPoses: monsterPoses,
            weatherPlan: _weatherPlan,
            weatherTickMs: _weatherTickMs,
            localAppearance: EquipmentService.ToOverlaySet(_paperdoll),
            localLook: _activeLook,
            tileAssets: _tilePacks.Lookup,
            tileAssetBitmaps: _tileAssetBitmaps,
            groundLootCentersPx: groundLoot,
            playtestPlacedEntities: _playtestPlacedEntities);
        _combatHud.Tick(DateTime.UtcNow);
        CombatEffect.Draw(
            bmp,
            _combatHud.Floats,
            DateTime.UtcNow,
            lcx,
            lcy,
            _localFacing,
            _combatHud.Sparks,
            _combatHud.Statuses);
        if (_eventPictures.Count > 0)
        {
            var (cameraX, cameraY) = PreviewMapCameraOffset(bmp.Width, bmp.Height);
            using var overlay = Graphics.FromImage(bmp);
            overlay.CompositingMode = CompositingMode.SourceOver;
            overlay.InterpolationMode = InterpolationMode.NearestNeighbor;
            overlay.PixelOffsetMode = PixelOffsetMode.Half;
            overlay.SmoothingMode = SmoothingMode.None;
            foreach (var pair in _eventPictures.OrderBy(pair => pair.Key))
            {
                var picture = pair.Value;
                EventPictureDraw.Paint(
                    overlay,
                    picture.Image,
                    picture.X,
                    picture.Y,
                    cameraX,
                    cameraY,
                    picture.Opacity,
                    picture.Blend);
            }
        }

        var previous = _picMap.Image;
        _picMap.Image = bmp;
        previous?.Dispose();
        ApplyMapViewportCamera();
        _movementMeasure.NoteVisibleUpdate();
    }

    private async Task SyncTilePackAsync(bool redrawIfReady)
    {
        try
        {
            var result = await _tilePacks.SyncAsync().ConfigureAwait(true);
            if (IsDisposed)
            {
                return;
            }

            ResetTileAssetBitmapCache();
            _lastTilePack = result;
            if (result.Kind is TilePackSyncKind.Cached or TilePackSyncKind.Downloaded)
            {
                if (_diagnosticLastError is not null
                    && _diagnosticLastError.StartsWith("Paquet de tuiles", StringComparison.Ordinal))
                {
                    _diagnosticLastError = null;
                }
            }
            else
            {
                _diagnosticLastError = PlayerFacingMessages.Redact(DescribeTilePack(result));
            }

            AppendLog(DescribeTilePack(result));
            RefreshDiagnosticOverlay();
            if (redrawIfReady && _map is not null)
            {
                RedrawMap();
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                _diagnosticLastError = PlayerFacingMessages.Redact("Paquet de tuiles : " + ex.Message);
                AppendLog("Paquet de tuiles : " + ex.Message);
                RefreshDiagnosticOverlay();
            }
            catch (Exception closed) when (closed is ObjectDisposedException or InvalidOperationException)
            {
                // Fermeture de la fenêtre pendant le sync.
            }
        }
    }

    private static string DescribeTilePack(TilePackSyncResult result)
    {
        return result.Kind switch
        {
            TilePackSyncKind.Cached =>
                $"Paquet de tuiles en cache : {result.TileCount} tuile(s), version {result.Version}. {result.Detail}".Trim(),
            TilePackSyncKind.Downloaded =>
                $"Paquet de tuiles téléchargé : {result.TileCount} tuile(s), version {result.Version}.",
            TilePackSyncKind.Rejected => "Paquet de tuiles refusé : " + result.Detail,
            _ => "Paquet de tuiles indisponible : " + result.Detail,
        };
    }

    private void ResetTileAssetBitmapCache()
    {
        if (string.Equals(_tileAssetBitmapSha, _tilePacks.VerifiedContentSha256, StringComparison.Ordinal))
        {
            return;
        }

        DisposeTileAssetBitmaps();
        _tileAssetBitmapSha = _tilePacks.VerifiedContentSha256;
    }

    private void DisposeTileAssetBitmaps()
    {
        foreach (var bitmap in _tileAssetBitmaps.Values)
        {
            bitmap.Dispose();
        }

        _tileAssetBitmaps.Clear();
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

    private void ReloadPrefabOverlays()
    {
        DisposePrefabBitmaps();
        _prefabPlacements.Clear();
        _prefabCatalog = null;
        if (_map is null)
        {
            return;
        }

        var loaded = ClientPrefabLoader.LoadForMap(
            _map,
            AppContext.BaseDirectory,
            _publishedCatalog,
            runtimeMapId: _sessionDisplayedMapId);
        _prefabCatalog = loaded.Catalog;
        _prefabPlacements.AddRange(loaded.Placements);
        foreach (var kv in loaded.Bitmaps)
        {
            _prefabBitmaps[kv.Key] = kv.Value;
        }

        if (_prefabPlacements.Count > 0)
        {
            AppendLog($"Prefabs posés : {_prefabPlacements.Count} (catalogue publié / sidecar Maps/*.prefabs.json).");
        }
    }

    private void ReloadPlaytestPlacedEntities()
    {
        _playtestPlacedEntities.Clear();
        if (_map is null || _sessionDisplayedMapId <= 0)
        {
            return;
        }

        var loaded = PlaytestPlacedEntityPackage.TryLoadForRuntimeMap(
            ClientTilesetLoader.ResolveSearchDirectories(AppContext.BaseDirectory),
            _sessionDisplayedMapId,
            _map);
        _playtestPlacedEntities.AddRange(loaded);
        if (loaded.Count > 0)
        {
            AppendLog($"Entités de test : {loaded.Count} (fichier Maps/runtime-{_sessionDisplayedMapId}.placed.json).");
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

    private void DisposePrefabBitmaps()
    {
        ClientPrefabLoader.DisposeBitmaps(_prefabBitmaps);
    }

    private void ClearMapImage()
    {
        var old = _picMap.Image;
        _picMap.Image = null;
        old?.Dispose();
        _picMap.Location = Point.Empty;
    }

    private (int X, int Y) PreviewMapCameraOffset(int mapW, int mapH)
    {
        var view = _mapScroll.ClientSize;
        float? focusX = null;
        float? focusY = null;
        if (_localVisualInitialized)
        {
            if (!_camFocusInitialized)
            {
                SnapCameraToLocalVisual();
            }

            focusX = _camFocusX;
            focusY = _camFocusY;
        }

        return MapViewportCamera.ComputeDrawOffset(view.Width, view.Height, mapW, mapH, focusX, focusY);
    }

    /// <summary>
    /// Centre le viewport sur le joueur local (monde) ou sur le rectangle carte (pas de focus).
    /// <c>offset = (client / 2) − focusMonde</c> — plus d’alignement coin-à-coin.
    /// </summary>
    private void ApplyMapViewportCamera()
    {
        if (_map is null || _picMap.Image is null)
        {
            if (_picMap.Location != Point.Empty)
            {
                _picMap.Location = Point.Empty;
            }

            return;
        }

        var view = _mapScroll.ClientSize;
        var mapW = _picMap.Image.Width;
        var mapH = _picMap.Image.Height;
        float? focusX = null;
        float? focusY = null;
        if (_localVisualInitialized)
        {
            if (!_camFocusInitialized)
            {
                SnapCameraToLocalVisual();
            }

            focusX = _camFocusX;
            focusY = _camFocusY;
        }

        var (ox, oy) = MapViewportCamera.ComputeDrawOffset(view.Width, view.Height, mapW, mapH, focusX, focusY);
        var next = new Point(ox, oy);
        if (_picMap.Location != next)
        {
            _picMap.Location = next;
        }
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

    private void ShowPlayerStatus(string message)
    {
        var safe = SanitizeSecrets(PlayerFacingMessages.Redact(message));
        if (!string.IsNullOrEmpty(_playtestOptions?.PlaytestToken))
        {
            safe = safe.Replace(_playtestOptions.PlaytestToken, "***", StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(_storedAuthToken))
        {
            safe = safe.Replace(_storedAuthToken, "***", StringComparison.Ordinal);
        }

        void Apply()
        {
            _lblPlayerStatus.Text = safe;
            AppendLog("[ui] " + safe);
        }

        if (InvokeRequired)
        {
            BeginInvoke(Apply);
            return;
        }

        Apply();
    }

    private void ApplyWindowSettings(WindowSettings window)
    {
        window.Normalize();
        if (window.FullScreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            return;
        }

        FormBorderStyle = FormBorderStyle.Sizable;
        Width = window.Width;
        Height = window.Height;
        WindowState = window.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
    }

    private void PersistWindowSettings()
    {
        try
        {
            if (WindowState == FormWindowState.Normal)
            {
                _settings.Window.Width = Width;
                _settings.Window.Height = Height;
            }

            _settings.Window.Maximized = WindowState == FormWindowState.Maximized
                && FormBorderStyle != FormBorderStyle.None;
            _settings.Window.FullScreen = FormBorderStyle == FormBorderStyle.None;
            _settings.VolumePercent = _sound.VolumePercent;
            _settings.AudioMuted = _sound.MuteRequested;
            _settings.MusicEnabled = _sound.MusicEnabled;
            _settings.LastHost = _txtHost.Text.Trim();
            _settings.LastPort = (int)_numPort.Value;
            PersistRememberedAccount();
            _settingsStore.Save(_settings);
        }
        catch
        {
            // persistance optionnelle
        }
    }

    private void ApplySettingsFromStore(UserSettings settings)
    {
        _settings = settings.Clone();
        _input.Apply(_settings);
        _sound.Apply(_settings);
        ApplyWindowSettings(_settings.Window);
        ApplyUiScale(_settings.UiScalePercent);
        if (_playtestOptions is not { IsPlaytest: true })
        {
            _txtHost.Text = _settings.LastHost;
            _numPort.Value = Math.Clamp(_settings.LastPort, 1, 65535);
        }

        RefreshServerListUi();
        RefreshMoveHint();
        ApplyRememberedAccount();
        _settingsStore.Save(_settings);
    }

    private void ApplyRememberedAccount()
    {
        var box = _loginShell.RememberCheckBoxForTest;
        box.Checked = _settings.RememberAccount;
        if (_playtestOptions is { IsPlaytest: true })
        {
            return;
        }

        if (_settings.RememberAccount && !string.IsNullOrWhiteSpace(_settings.LastUsername))
        {
            _txtUser.Text = _settings.LastUsername;
        }
    }

    private void PersistRememberedAccount()
    {
        _settings.RememberAccount = _loginShell.RememberCheckBoxForTest.Checked;
        _settings.LastUsername = _settings.RememberAccount ? _txtUser.Text.Trim() : string.Empty;
    }

    private void RefreshMoveHint()
    {
        var layout = _input.Preset == KeyboardLayoutPreset.Qwerty ? "WASD" : "ZQSD";
        _lblMoveHint.Text = $"{layout} + flèches = déplacement · {InputService.KeyDisplayName(_input.Interact)} = interagir · {InputService.KeyDisplayName(_input.Attack)} = mêlée · F1 = aide";
        RefreshInteractHint();
    }

    /// <summary>
    /// Indice [touche] Parler/Interagir. Tuile = centre affiché (même division que le serveur).
    /// Masqué hors jeu, hors de la tuile, pendant un dialogue sur cette tuile, ou quand
    /// un panneau / une saisie bloque l'interaction (fenêtre HUD, chat, échange, aide).
    /// </summary>
    private void RefreshInteractHint()
    {
        if (!IsHandleCreated && !Visible)
        {
            return;
        }

        var tile = CurrentPlayerTile();
        var playing = _phase == ClientUiPhase.Playing && _map is not null;
        var dialogueOpen = DialogueOpenForHint(tile.TileX, tile.TileY);
        var inputBlocked = InteractInputBlocked();
        var cue = MapEventInteractHint.Resolve(
            tile.TileX,
            tile.TileY,
            _mapEvents,
            InputService.KeyDisplayName(_input.Interact),
            playing,
            dialogueOpen,
            inputBlocked);
        if (cue is null && playing && !dialogueOpen && !inputBlocked && ShopInReach(tile.TileX, tile.TileY))
        {
            var key = InputService.KeyDisplayName(_input.Interact);
            cue = $"[{(string.IsNullOrWhiteSpace(key) ? "E" : key.Trim())}] Boutique";
        }
        if (_interactHint.ApplyCue(cue))
        {
            PositionInteractHint();
        }
    }

    private (int TileX, int TileY) CurrentPlayerTile()
    {
        int px;
        int py;
        if (_localVisualInitialized)
        {
            px = (int)MathF.Round(_visLocalCx);
            py = (int)MathF.Round(_visLocalCy);
        }
        else
        {
            px = _srvPixelX;
            py = _srvPixelY;
        }

        return MapEventInteractHint.TileOfCenter(px, py);
    }

    private bool DialogueOpenForHint(int tileX, int tileY)
    {
        if (!_dialogueSessionOpen)
        {
            return false;
        }

        if (tileX != _dialogueAnchorTileX || tileY != _dialogueAnchorTileY)
        {
            _dialogueSessionOpen = false;
            return false;
        }

        return true;
    }

    private bool InteractInputBlocked()
    {
        if (_windowLayerVisible)
        {
            return true;
        }

        if (ActiveControl is { Visible: true } focused && InputService.IsTextInputFocus(focused))
        {
            return true;
        }

        if (_helpForm is { IsDisposed: false, Visible: true })
        {
            return true;
        }

        return _tradeForm.Visible;
    }

    private void PositionInteractHint()
    {
        if (!_interactHint.Visible || _interactHint.Parent != _worldHost)
        {
            return;
        }

        var host = _worldHost;
        const int gap = 8;
        var x = Math.Max(gap, (host.Width - _interactHint.Width) / 2);
        var y = _hudHotbar.Top - _interactHint.Height - gap;
        if (y < gap)
        {
            y = gap;
        }

        _interactHint.Location = new Point(x, y);
        _interactHint.BringToFront();
        if (_windowLayerVisible)
        {
            _windowChrome.BringToFront();
        }
    }

    private void UpdateVersionBadge()
    {
        _lblVersion.Text = "v" + ClientVersion.Display;
    }

    private void ApplyVersionChrome() => UpdateVersionBadge();

    /// <summary>
    /// Échelle d’interface seulement. La carte peinte reste en tuiles 32
    /// (<see cref="WorldMetrics.DefaultTileSizePixels"/>). Le DPI système n’est pas relu.
    /// À 100 % la méthode sort sans retoucher le layout Hello ni le HUD.
    /// </summary>
    private void ApplyUiScale(int percent)
    {
        var clamped = ClientUiScale.ClampPercent(percent);
        _uiScalePercent = clamped;
        _settings.UiScalePercent = clamped;
        ClientUiScale.SetActive(clamped);
        if (clamped == _appliedUiScalePercent)
        {
            return;
        }

        _appliedUiScalePercent = clamped;
        UiScaleApplicator.ApplyFonts(this, clamped);
        _loginShell.ApplyUiScale(clamped);
        LoginShell.ApplyCharacterPageScale(_panelCharacter, clamped);
        _hudStatus.ApplyChromeScale(clamped);
        _hudMinimap.ApplyChromeScale(clamped);
        _hudQuest.ApplyChromeScale(clamped);
        _hudChat.ApplyChromeScale(clamped);
        _hudFriends.ApplyChromeScale(clamped);
        _hudStatus.Size = new Size(
            ClientUiScale.ScaleDip(HudStatusModule.ModuleWidth, clamped),
            ClientUiScale.ScaleDip(HudStatusModule.ModuleHeight, clamped));
        _hudStatus.MinimumSize = new Size(
            ClientUiScale.ScaleDip(200, clamped),
            ClientUiScale.ScaleDip(64, clamped));
        _hudMinimap.Size = new Size(ClientUiScale.ScaleDip(180, clamped), ClientUiScale.ScaleDip(180, clamped));
        _hudMinimap.MinimumSize = new Size(ClientUiScale.ScaleDip(140, clamped), ClientUiScale.ScaleDip(140, clamped));
        _hudQuest.Size = new Size(ClientUiScale.ScaleDip(180, clamped), ClientUiScale.ScaleDip(76, clamped));
        _hudQuest.MinimumSize = new Size(ClientUiScale.ScaleDip(140, clamped), ClientUiScale.ScaleDip(64, clamped));
        _hudChat.Size = new Size(ClientUiScale.ScaleDip(360, clamped), ClientUiScale.ScaleDip(200, clamped));
        _hudChat.MinimumSize = new Size(ClientUiScale.ScaleDip(280, clamped), ClientUiScale.ScaleDip(160, clamped));
        _hudFriends.Size = new Size(ClientUiScale.ScaleDip(220, clamped), ClientUiScale.ScaleDip(168, clamped));
        _hudFriends.MinimumSize = new Size(ClientUiScale.ScaleDip(180, clamped), ClientUiScale.ScaleDip(120, clamped));
        _diagnosticOverlay.ApplyChromeScale(clamped);
        var diagnosticWidth = ClientUiScale.ScaleDip(DiagnosticOverlayPanel.PanelWidth, clamped);
        var diagnosticHeight = ClientUiScale.ScaleDip(DiagnosticOverlayPanel.PanelHeight, clamped);
        _diagnosticOverlay.MinimumSize = new Size(diagnosticWidth, diagnosticHeight);
        _diagnosticOverlay.Size = new Size(diagnosticWidth, diagnosticHeight);
        PlaceDiagnosticOverlay();
        _hudHotbar.ApplyUiScale(clamped);
        _hudMenu.ApplyUiScale(clamped);
        LayoutGameHud();
    }

    private void ApplyDaTheme()
    {
        UiTheme.Apply(this);
        _hudStatus.ApplyDaColors();
        UiTheme.StyleGoldTabs(_gameplayTabs);
        _windowChrome.ApplyTheme();
        _loginShell.ApplyTheme();
        LoginShell.StylePrimaryCta(_btnLogin);
        LoginShell.StyleSecondaryCta(_btnRegister);
        LoginShell.StyleSecondaryCta(_btnReconnect);
        LoginShell.StyleSecondaryCta(_btnConnect);
        LoginShell.StyleSecondaryCta(_btnDisconnect);
        LoginShell.StyleSecondaryCta(_btnRetry);
        LoginShell.StyleSecondaryCta(_btnAddServer);
        _mapScroll.BackColor = MapSurfaceBackColor;
        _picMap.BackColor = MapSurfaceBackColor;
        _worldHost.BackColor = MapSurfaceBackColor;
    }

    private void LayoutGameHud()
    {
        var host = _worldHost;
        if (host.Width < 32 || host.Height < 32)
        {
            return;
        }

        var gap = ClientUiScale.ScaleDip(8, _uiScalePercent);
        var chromeExtraH = HudWindowChrome.TitleBarHeight + HudWindowChrome.ContentPadding;
        var tabH = Math.Clamp(host.Height - (gap * 2) - chromeExtraH, 250, 700);
        _windowChrome.Visible = _windowLayerVisible;
        _gameplayTabs.Visible = _windowLayerVisible;
        if (_windowLayerVisible)
        {
            _gameplayTabs.Size = new Size(ClientUiScale.ScaleDip(360, _uiScalePercent), tabH);
            _windowChrome.FitToContent();
            _windowChrome.Location = new Point(Math.Max(0, host.Width - _windowChrome.Width - gap), gap);
            _windowChrome.BringToFront();
        }

        var tabW = _windowLayerVisible ? _windowChrome.Width + gap : 0;

        _hudStatus.Location = new Point(gap, gap);
        var rightX = Math.Max(gap, host.Width - tabW - _hudMinimap.Width - gap);
        _hudMinimap.Location = new Point(rightX, gap);
        _hudQuest.Location = new Point(rightX, _hudMinimap.Bottom + gap);
        _hudChat.Location = new Point(gap, Math.Max(gap, host.Height - _hudChat.Height - gap));
        if (_friendsSticky.Visible)
        {
            var friendsY = _hudChat.Top - _hudFriends.Height - gap;
            var minY = _hudStatus.Bottom + gap;
            if (friendsY < minY)
            {
                friendsY = minY;
            }

            _hudFriends.Location = new Point(gap, friendsY);
        }
        _hudHotbar.Location = new Point(
            Math.Max(gap, (host.Width - tabW - _hudHotbar.Width) / 2),
            Math.Max(gap, host.Height - _hudHotbar.Height - gap));
        _hudMenu.Location = new Point(
            Math.Max(gap, host.Width - tabW - _hudMenu.Width - gap),
            Math.Max(gap, host.Height - _hudMenu.Height - gap));
        if (_btnRespawn.Visible)
        {
            _btnRespawn.Location = new Point(_hudStatus.Right + gap, gap);
            _btnRespawn.BringToFront();
        }

        _hudStatus.BringToFront();
        _hudMinimap.BringToFront();
        _hudQuest.BringToFront();
        if (_friendsSticky.Visible)
        {
            _hudFriends.BringToFront();
        }

        _hudChat.BringToFront();
        _hudHotbar.BringToFront();
        _hudMenu.BringToFront();
        _interactHint.BringToFront();
        if (_eventMessage.IsOpen)
        {
            var width = Math.Min(480, Math.Max(280, host.Width - tabW - (gap * 2)));
            _eventMessage.Width = width;
            _eventMessage.Reflow();
            var messageY = _hudHotbar.Top - _eventMessage.Height - gap;
            if (messageY < gap)
            {
                messageY = gap;
            }

            _eventMessage.Location = new Point(
                Math.Max(gap, (host.Width - tabW - width) / 2),
                messageY);
            _eventMessage.BringToFront();
        }

        if (_windowLayerVisible)
        {
            _windowChrome.BringToFront();
        }

        ApplyMapViewportCamera();
        PositionInteractHint();
        if (_eventMessage.IsOpen)
        {
            _eventMessage.BringToFront();
        }
    }

    private void SetWindowLayerVisible(bool visible)
    {
        _windowLayerVisible = visible;
        _windowChrome.Visible = visible;
        _gameplayTabs.Visible = visible;
        if (visible)
        {
            _gameplayTabs.Width = ClientUiScale.ScaleDip(360, _uiScalePercent);
            if (_gameplayTabs.Height < 250 || _gameplayTabs.Height > 700)
            {
                _gameplayTabs.Height = 480;
            }

            RefreshWindowChromeTitle();
        }

        LayoutGameHud();
        RefreshInteractHint();
    }

    private void RefreshWindowChromeTitle()
    {
        if (_gameplayTabs.SelectedTab == _tabCharacter)
        {
            _windowChrome.Title = "Fiche perso";
            return;
        }

        if (_gameplayTabs.SelectedTab == _tabPhase8)
        {
            _windowChrome.Title = "Quêtes";
            return;
        }

        if (_gameplayTabs.SelectedTab == _tabChat)
        {
            _windowChrome.Title = "Chat";
            return;
        }

        if (_gameplayTabs.SelectedTab == _tabSocial)
        {
            _windowChrome.Title = _socialHub.ChromeTitle;
            return;
        }

        _windowChrome.Title = _windowTitleHint;
    }

    private void OnWorldSurfaceClick()
    {
        var chatFocused = ChatComposeFocused();
        StopMovementForChat();
        if (ChatCompose.OnWorldClick(chatFocused).ReleaseFocus)
        {
            ReleaseChatFocus();
        }
        else
        {
            RefreshInteractHint();
        }

        DismissEventMessage();
        DismissWindowLayerFromMap();
        if (_friendsSticky.TryDismiss(chatFocused, mapChanged: false))
        {
            ApplyFriendsDock();
        }
    }

    private bool TryHandleEventMessageKey(KeyEventArgs e)
    {
        if (!_eventMessage.IsOpen || ChatComposeFocused() || InputService.IsTextInputFocus(ActiveControl))
        {
            return false;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Escape || _input.IsInteract(e.KeyCode))
        {
            DismissEventMessage();
        }

        return true;
    }

    private bool ChatComposeFocused() => _txtChat.ContainsFocus || _txtWhisperTo.ContainsFocus;

    private bool TryHandleChatComposeKey(KeyEventArgs e)
    {
        var chatFocused = ChatComposeFocused();
        var otherText = !chatFocused && InputService.IsTextInputFocus(ActiveControl);
        var decision = ChatCompose.Decide(chatFocused, otherText, ClassifyChatKey(e.KeyCode));
        if (!decision.BlockWorldInput && !decision.FocusChat && !decision.ReleaseFocus && !decision.Send)
        {
            return false;
        }

        if (decision.BlockWorldInput)
        {
            StopMovementForChat();
        }

        if (decision.ReleaseFocus)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ReleaseChatFocus();
            return true;
        }

        if (decision.Send)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SendChatAsync();
            KeepChatFocus();
            return true;
        }

        if (decision.FocusChat)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            StopMovementForChat();
            FocusChatInput();
            return true;
        }

        return true;
    }

    private ChatCompose.Key ClassifyChatKey(Keys key)
    {
        if (key == Keys.Enter)
        {
            return ChatCompose.Key.Enter;
        }

        if (key == Keys.Escape)
        {
            return ChatCompose.Key.Escape;
        }

        if (_input.IsMoveLeft(key)
            || _input.IsMoveRight(key)
            || _input.IsMoveUp(key)
            || _input.IsMoveDown(key)
            || _input.IsAttack(key)
            || _input.IsInteract(key))
        {
            return ChatCompose.Key.World;
        }

        return ChatCompose.Key.Text;
    }

    private void StopMovementForChat()
    {
        var moving = _holdLeft || _holdRight || _holdUp || _holdDown || _keysDown.Count > 0;
        if (!moving)
        {
            return;
        }

        ReleaseAllMoveKeys();
        ScheduleIdlePositionSyncIfAllReleased();
    }

    private void FocusChatInput()
    {
        if (_txtChat.IsDisposed)
        {
            return;
        }

        _txtChat.Focus();
    }

    private void KeepChatFocus()
    {
        if (ChatComposeFocused() || _txtChat.Focused)
        {
            FocusChatInput();
        }
    }

    private void ReleaseChatFocus()
    {
        StopMovementForChat();
        _picMap.TabStop = true;
        if (IsHandleCreated && !_picMap.Focus())
        {
            _mapScroll.TabStop = true;
            _mapScroll.Focus();
        }

        RefreshInteractHint();
    }

    private void PrefillWhisperFromSelection()
    {
        if (_cmbChannel.SelectedIndex != 2 || !string.IsNullOrWhiteSpace(_txtWhisperTo.Text))
        {
            return;
        }

        if (ChatWhisper.TryResolveTarget(
                null,
                SelectedFriendName(),
                SelectedWorldTargetName(),
                CombatMvpLimits.DummyName,
                out var name))
        {
            _txtWhisperTo.Text = name;
        }
    }

    private void KeepFriendsDockAcrossMap(int mapId)
    {
        if (mapId < 0 || !_friendsSticky.RetainOnMapChange())
        {
            return;
        }

        ApplyFriendsDock();
    }

    private void ApplyFriendsDock()
    {
        _hudFriends.Bind(FriendsSticky.Build(_socialRoster), _friendsSticky.PinLabel, _friendsSticky.TitleText);
        var show = _friendsSticky.Visible;
        var was = _hudFriends.Visible;
        _hudFriends.Visible = show;
        if (show || was)
        {
            LayoutGameHud();
        }
    }

    private void OnFriendsDockFriend(FriendsSticky.Row row)
    {
        if (!FriendsSticky.TryArmWhisper(
                row,
                _username,
                _activeCharacterName,
                out var target,
                out var notice,
                out var focusChat))
        {
            ShowPlayerStatus(notice);
            return;
        }

        _txtWhisperTo.Text = target;
        if (_cmbChannel.Items.Count > 2 && _cmbChannel.SelectedIndex != 2)
        {
            _cmbChannel.SelectedIndex = 2;
        }

        ShowPlayerStatus(notice);
        if (focusChat)
        {
            FocusChatInput();
        }
    }

    private string? SelectedFriendName() =>
        _socialHub.TryGetSelectedWhisperName(out var name) ? name : null;

    private string? SelectedWorldTargetName()
    {
        var text = _cmbMeleeTarget.Text.Trim();
        return text.Length == 0 ? null : text;
    }

    private void ShowChatNotice(string message)
    {
        AppendLog(message);
        _hudChat.AppendSystem(message);
        ShowPlayerStatus(message);
    }

    private void DismissWindowLayerFromMap()
    {
        if (_windowLayerVisible)
        {
            SetWindowLayerVisible(false);
        }
    }

    private void OnHudMenuCommand(HudMenuCommand command)
    {
        switch (command)
        {
            case HudMenuCommand.Character:
                ToggleCharacterSheet();
                break;
            case HudMenuCommand.Inventory:
                _windowTitleHint = "Inventaire";
                if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabGameplay)
                {
                    SetWindowLayerVisible(false);
                    break;
                }

                SetWindowLayerVisible(true);
                _gameplayTabs.SelectedTab = _tabGameplay;
                RefreshWindowChromeTitle();
                break;
            case HudMenuCommand.Quests:
                _windowTitleHint = "Quêtes";
                if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabPhase8)
                {
                    SetWindowLayerVisible(false);
                    break;
                }

                SetWindowLayerVisible(true);
                _gameplayTabs.SelectedTab = _tabPhase8;
                RefreshWindowChromeTitle();
                _tabPhase8.PerformLayout();
                break;
            case HudMenuCommand.Map:
                SetWindowLayerVisible(false);
                break;
            case HudMenuCommand.Options:
                OpenOptions();
                break;
        }
    }

    private void ToggleCharacterSheet()
    {
        _windowTitleHint = "Fiche perso";
        if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabCharacter)
        {
            SetWindowLayerVisible(false);
            return;
        }

        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabCharacter;
        SyncStatusPortrait();
        RefreshWindowChromeTitle();
    }

    private void OpenSocialPanel(SocialKind kind)
    {
        var title = ClientSocialRoster.KindLabel(kind);
        _windowTitleHint = title;
        if (_windowLayerVisible
            && _gameplayTabs.SelectedTab == _tabSocial
            && _socialHub.SelectedKind == kind)
        {
            SetWindowLayerVisible(false);
            return;
        }

        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabSocial;
        _socialHub.SelectKind(kind);
        RefreshWindowChromeTitle();
        if (kind == SocialKind.Friend)
        {
            _friendsSticky.Pin();
            ApplyFriendsDock();
        }
    }

    private void OpenEconomyPanel(EconomyHubKind kind)
    {
        var title = ClientEconomyHub.KindLabel(kind);
        _windowTitleHint = title;
        if (_windowLayerVisible
            && _gameplayTabs.SelectedTab == _tabSocial
            && _socialHub.TryGetSelectedEconomy(out var current)
            && current == kind)
        {
            SetWindowLayerVisible(false);
            return;
        }

        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabSocial;
        _socialHub.SelectEconomy(kind);
        RefreshWindowChromeTitle();
    }

    private void OnEconomyHubQuery(EconomyHubKind kind)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        _ = SendEconomyHubQueryAsync(kind);
    }

    private async Task SendEconomyHubQueryAsync(EconomyHubKind kind)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.SendEconomyHubAsync(
                    kind,
                    (byte)EconomyHubAction.Query,
                    Guid.NewGuid(),
                    ClientEconomyHub.QueryExtra())
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Économie: " + ex.Message);
        }
    }

    private void OnEconomyHubSnapshot(EconomyHubSnapshotWire snap)
    {
        _economyHub.ApplySnapshot(snap);
        _socialHub.ApplyEconomy(_economyHub);
        AppendLog($"{ClientEconomyHub.KindLabel(snap.Kind)}: {snap.Entries.Count} entrée(s)");
        if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabSocial)
        {
            RefreshWindowChromeTitle();
        }
    }

    private void OnEconomyHubResult(EconomyHubResultWire result)
    {
        _economyHub.ApplyResult(result);
        _socialHub.ApplyEconomy(_economyHub);
        AppendLog(result.Success ? "Économie: " + result.Message : "Économie refusée: " + result.Message);
    }

    private void OpenInstancePanel()
    {
        _windowTitleHint = string.IsNullOrEmpty(_instanceHub.CurrentInstanceName)
            ? "Instance"
            : _instanceHub.CurrentInstanceName;
        if (_windowLayerVisible
            && _gameplayTabs.SelectedTab == _tabSocial
            && _socialHub.IsInstanceTabSelected)
        {
            SetWindowLayerVisible(false);
            return;
        }

        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabSocial;
        _socialHub.SelectInstance();
        RefreshWindowChromeTitle();
    }

    private void OnInstanceHubQuery(InstanceHubKind kind)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        _ = SendInstanceHubAsync(kind, (byte)InstanceHubAction.Query, ClientInstanceHub.QueryExtra());
    }

    private void OnInstanceHubEnter(InstanceHubKind kind, Guid definitionId)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        _ = SendInstanceHubAsync(kind, (byte)InstanceHubAction.Enter, ClientInstanceHub.EnterExtra(definitionId));
    }

    private void OnInstanceHubLeave(InstanceHubKind kind)
    {
        if (_client is null || !_client.IsConnected)
        {
            return;
        }

        _ = SendInstanceHubAsync(kind, (byte)InstanceHubAction.Leave, ClientInstanceHub.LeaveExtra());
    }

    private async Task SendInstanceHubAsync(InstanceHubKind kind, byte action, byte[] extra)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.SendInstanceHubAsync(kind, action, Guid.NewGuid(), extra).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Instance: " + ex.Message);
        }
    }

    private void OnInstanceHubSnapshot(InstanceHubSnapshotWire snap)
    {
        _instanceHub.ApplySnapshot(snap);
        _socialHub.ApplyInstance(_instanceHub);
        AppendLog($"{ClientInstanceHub.KindLabel(snap.Kind)}: {snap.Entries.Count} entrée(s)"
                  + (string.IsNullOrEmpty(_instanceHub.CurrentInstanceName)
                      ? string.Empty
                      : " — " + _instanceHub.CurrentInstanceName));
        if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabSocial)
        {
            RefreshWindowChromeTitle();
        }
    }

    private void OnInstanceHubResult(InstanceHubResultWire result)
    {
        _instanceHub.ApplyResult(result);
        _socialHub.ApplyInstance(_instanceHub);
        AppendLog(result.Success ? "Instance: " + result.Message : "Instance refusée: " + result.Message);
        if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabSocial)
        {
            RefreshWindowChromeTitle();
        }
    }

    private void OnSocialSnapshot(SocialSnapshotWire snap)
    {
        _socialRoster.ApplySnapshot(snap);
        _socialHub.ApplyRoster(_socialRoster);
        ApplyFriendsDock();
        var label = ClientSocialRoster.KindLabel(snap.Kind);
        AppendLog($"{label}: {snap.Members.Count} entrée(s)"
                  + (string.IsNullOrEmpty(snap.Motd) ? string.Empty : " — " + snap.Motd));
        if (_windowLayerVisible && _gameplayTabs.SelectedTab == _tabSocial)
        {
            RefreshWindowChromeTitle();
        }
    }

    private void OnSocialEvent(SocialEventWire ev)
    {
        _socialRoster.ApplyEvent(ev);
        _socialHub.ApplyRoster(_socialRoster);
        ApplyFriendsDock();
        AppendLog("Social: " + ev.Message);
        if (!string.IsNullOrWhiteSpace(ev.Message))
        {
            _hudChat.AppendSystem(ev.Message);
        }
    }

    private void OnSocialResult(SocialResultWire result)
    {
        _socialRoster.ApplyResult(result);
        _socialHub.ApplyRoster(_socialRoster);
        ApplyFriendsDock();
        AppendLog(result.Success ? "Social: " + result.Message : "Social refusé: " + result.Message);
    }

    private void OnSocialHubAction(SocialClientRequest request)
    {
        if (_client is null || !_client.IsConnected)
        {
            _socialRoster.ApplyResult(new SocialResultWire(
                request.Kind,
                request.Action,
                Guid.Empty,
                false,
                "Non connecté.",
                Guid.Empty,
                Guid.Empty));
            _socialHub.ApplyRoster(_socialRoster);
            ApplyFriendsDock();
            return;
        }

        _ = SendSocialRequestAsync(request);
    }

    private async Task SendSocialRequestAsync(SocialClientRequest request)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.SendSocialAsync(request.Kind, request.Action, Guid.NewGuid(), request.Extra)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Social: " + ex.Message);
        }
    }

    private void OnHotbarSlotActivated(int index)
    {
        switch (index)
        {
            case 0:
                _ = MeleeAsync();
                break;
            case 1:
                _ = SpellCastAsync();
                break;
            case 3:
                _ = RangedAsync();
                break;
            case 2:
                if (_map is null || _phase != ClientUiPhase.Playing)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if ((now - _lastInteractUtc).TotalMilliseconds < 400)
                {
                    return;
                }

                _lastInteractUtc = now;
                _ = SendInteractAsync();
                break;
        }
    }

    private bool TryActivateHotbarKey(Keys key)
    {
        var index = key switch
        {
            Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1,
            Keys.D3 or Keys.NumPad3 => 2,
            Keys.D4 or Keys.NumPad4 => 3,
            Keys.D5 or Keys.NumPad5 => 4,
            Keys.D6 or Keys.NumPad6 => 5,
            Keys.D7 or Keys.NumPad7 => 6,
            Keys.D8 or Keys.NumPad8 => 7,
            Keys.D9 or Keys.NumPad9 => 8,
            Keys.D0 or Keys.NumPad0 => 9,
            _ => -1,
        };
        if (index < 0)
        {
            return false;
        }

        _hudHotbar.ActivateSlot(index);
        return index < 4;
    }

    private void PersistLastEndpoint(string host, int port)
    {
        var normalizedHost = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        var normalizedPort = Math.Clamp(port, 1, 65535);
        SavedServerList.TryRemember(_settings, normalizedHost, normalizedPort, name: null, out _);
        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            // persistance optionnelle
        }

        RefreshServerListUi();
    }

    private void ApplyPickedServer()
    {
        if (_syncingServerList || _cmbServers.SelectedItem is not SavedServerEndpoint endpoint)
        {
            return;
        }

        _txtHost.Text = endpoint.Host;
        _numPort.Value = Math.Clamp(endpoint.Port, 1, 65535);
        _settings.LastHost = endpoint.Host;
        _settings.LastPort = endpoint.Port;
        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            // persistance optionnelle
        }

        RefreshConnectDiagnostic();
    }

    private void AddServerFromFields()
    {
        if (!SavedServerList.TryRemember(
                _settings,
                _txtHost.Text,
                (int)_numPort.Value,
                _txtServerName.Text,
                out var error))
        {
            ShowPlayerStatus(error);
            return;
        }

        _txtServerName.Clear();
        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            // persistance optionnelle
        }

        RefreshServerListUi();
        ShowPlayerStatus("Serveur enregistré.");
    }

    private void RefreshServerListUi()
    {
        _syncingServerList = true;
        try
        {
            _cmbServers.BeginUpdate();
            _cmbServers.Items.Clear();
            foreach (var row in _settings.SavedServers)
            {
                _cmbServers.Items.Add(row);
            }

            _cmbServers.SelectedIndex = SavedServerList.IndexOf(_settings.SavedServers, _txtHost.Text, (int)_numPort.Value);
        }
        finally
        {
            _cmbServers.EndUpdate();
            _syncingServerList = false;
        }

        RefreshConnectDiagnostic();
    }

    private void NoteConnectFailure(ConnectionFailureKind kind, string human)
    {
        _lastFailureKind = kind == ConnectionFailureKind.None ? ConnectionFailureKind.Other : kind;
        _lastFailureText = PlayerFacingMessages.Redact(human);
        _diagnosticLastError = _lastFailureText;
        _btnRetry.Enabled = true;
        RefreshConnectDiagnostic();
        RefreshDiagnosticOverlay();
    }

    private void ClearConnectFailure()
    {
        _lastFailureKind = ConnectionFailureKind.None;
        _lastFailureText = null;
        _diagnosticLastError = null;
        _btnRetry.Enabled = false;
        RefreshConnectDiagnostic();
        RefreshDiagnosticOverlay();
    }

    private void ToggleDiagnosticOverlay()
    {
        if (_diagnosticOverlay.Visible)
        {
            HideDiagnosticOverlay();
            return;
        }

        ShowDiagnosticOverlay();
    }

    private void ShowDiagnosticOverlay()
    {
        RefreshDiagnosticOverlay();
        PlaceDiagnosticOverlay();
        _diagnosticOverlay.Visible = true;
        _diagnosticOverlay.BringToFront();
        _diagnosticRttTimer.Start();
        _ = SendHeartbeatSafeAsync();
    }

    private void HideDiagnosticOverlay()
    {
        _diagnosticRttTimer.Stop();
        _diagnosticOverlay.Visible = false;
    }

    private static bool DiagnosticDismissRequested(Keys key) => key == Keys.Escape;

    private void RefreshDiagnosticOverlay()
    {
        if (IsDisposed)
        {
            return;
        }

        _diagnosticOverlay.SetText(BuildDiagnosticOverlayText());
    }

    private void PlaceDiagnosticOverlay()
    {
        var host = _diagnosticOverlay.Parent;
        if (host is null || host.ClientSize.Width < 32)
        {
            return;
        }

        const int margin = 12;
        var x = Math.Max(margin, host.ClientSize.Width - _diagnosticOverlay.Width - margin);
        _diagnosticOverlay.Location = new Point(x, margin);
    }

    private string BuildDiagnosticOverlayText()
    {
        var snapshot = ClientDiagnosticLight.Create(
            _txtHost.Text,
            (int)_numPort.Value,
            _client is { IsConnected: true },
            _connectInFlight,
            _rtt.LastMilliseconds,
            _lastTilePack,
            _diagnosticLastError);
        return ClientDiagnosticLight.Format(
            snapshot,
            _storedAuthToken,
            _playtestOptions?.PlaytestToken,
            _txtPass.Text);
    }

    private void RefreshConnectDiagnostic()
    {
        var where = _txtHost.Text.Trim() + ":" + (int)_numPort.Value;
        if (_lastFailureKind == ConnectionFailureKind.None)
        {
            _loginShell.SetConnectDiagnostic(
                $"Protocole {FrogWireProtocol.Version} · {where} · prêt.",
                failure: false);
            return;
        }

        _loginShell.SetConnectDiagnostic(
            $"Échec : {PlayerFacingMessages.Headline(_lastFailureKind)} · {where} · protocole {FrogWireProtocol.Version}",
            failure: true);
    }

    private void ApplyPlayerStatusLayout()
    {
        _lblPlayerStatus.BackColor = UiTheme.BgPanelHeader;
        _lblPlayerStatus.ForeColor = UiTheme.TextPrimary;
    }

    private void ApplyOptionsHelpChrome()
    {
        // Boutons déjà dans _topChrome (BuildLayout).
    }

    private void OpenHelp()
    {
        if (_helpForm is { IsDisposed: false })
        {
            _helpForm.BringToFront();
            _helpForm.Focus();
            RefreshInteractHint();
            return;
        }

        _helpForm = new HelpForm();
        _helpForm.FormClosed += (_, _) =>
        {
            _helpForm = null;
            RefreshInteractHint();
        };
        _helpForm.Show(this);
        RefreshInteractHint();
    }

    private void OpenOptions()
    {
        _sound.PlayUiClick();
        ReleaseAllMoveKeys();
        _settings.LastHost = _txtHost.Text.Trim();
        _settings.LastPort = (int)_numPort.Value;
        using var dlg = new OptionsForm(_settings);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ApplySettingsFromStore(dlg.Settings);
        ShowPlayerStatus("Options enregistrées.");
    }

    private void CopyDiagnosticsToClipboard()
    {
        var report = BuildDiagnosticsText();
        try
        {
            Clipboard.SetText(report);
            ShowPlayerStatus(PlayerFacingMessages.DiagnosticsCopied);
        }
        catch
        {
            ShowPlayerStatus("Diagnostics prêts (presse-papiers indisponible).");
        }
    }

    internal string BuildDiagnosticsText()
    {
        var tls = ClientTlsOptions.FromEnvironment(_txtHost.Text.Trim());
        var raw = ClientDiagnostics.Build(
            ClientVersion.Display,
            _txtHost.Text.Trim(),
            (int)_numPort.Value,
            tls.Mode.ToString(),
            tls.TargetHost,
            _phase.ToString(),
            _client is { IsConnected: true },
            string.IsNullOrWhiteSpace(_username) ? _txtUser.Text.Trim() : _username,
            _lastFailureText);
        if (_movementMeasure.Enabled)
        {
            raw += Environment.NewLine + _movementMeasure.FormatSummary();
        }

        return ClientDiagnostics.RedactSecrets(
            raw,
            _storedAuthToken,
            _playtestOptions?.PlaytestToken,
            _txtPass.Text);
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

    private sealed class ShopPickRow
    {
        public ShopPickRow(ShopView shop) => Shop = shop;

        public ShopView Shop { get; }

        public Guid Id => Shop.Id;

        public override string ToString() => Shop.Name;
    }

    private sealed class ItemPickRow
    {
        public ItemPickRow(ShopListingView listing) => Listing = listing;

        public ShopListingView Listing { get; }

        public Guid Id => Listing.ItemId;

        public string Type => Listing.Type;

        public override string ToString() => ShopBankPlayerMessages.FormatListing(Listing);
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

    internal LoginShell LoginShellForTest => _loginShell;

    internal void ToggleLoginOpsForTest() => _loginShell.ToggleOps();

    internal void PressF9ForTest() => MainShell_KeyDown(this, new KeyEventArgs(Keys.F9));

    internal CheckBox RememberAccountCheckBoxForTest => _loginShell.RememberCheckBoxForTest;

    internal TextBox HostTextBoxForTest => _txtHost;

    internal NumericUpDown PortNumericForTest => _numPort;

    internal TextBox UserTextBoxForTest => _txtUser;

    internal TextBox PassTextBoxForTest => _txtPass;

    internal Button ConnectButtonForTest => _btnConnect;

    internal Button RetryConnectButtonForTest => _btnRetry;

    internal Button AddServerButtonForTest => _btnAddServer;

    internal ComboBox ServerListComboForTest => _cmbServers;

    internal TextBox ServerNameTextBoxForTest => _txtServerName;

    internal string ConnectDiagnosticTextForTest => _loginShell.ConnectDiagnosticTextForTest;

    internal void NoteConnectFailureForTest(string raw)
    {
        var human = PlayerFacingMessages.FromServerOrNetwork(raw);
        NoteConnectFailure(PlayerFacingMessages.ClassifyServer(raw), human);
        ShowPlayerStatus(human);
    }

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

    internal AppearancePickerPanel AppearancePickerForTest => _appearancePicker;

    internal CharacterLook ActiveLookForTest => _activeLook;

    internal void PressAppearanceArrowForTest(Keys key) => MainShell_KeyDown(this, new KeyEventArgs(key));

    internal void RememberAppearanceForTest()
    {
        var name = _txtNewCharName.Text.Trim();
        RememberNamedLook(name, _appearancePicker.Look);
        _pendingLookName = name;
    }

    internal void BindAppearanceIdForTest(string name, string id)
    {
        CharacterLookBook.BindCreatedId(_settings.CharacterLooks, name, id);
        _settingsStore.Save(_settings);
    }

    internal void ApplySavedAppearanceForTest(string id, string name) => ApplySavedLook(id, name);

    internal Button EnterGameButtonForTest => _btnEnterGame;

    internal Button CharRefreshButtonForTest => _btnCharRefresh;

    internal Panel CharacterPanelForTest => _panelCharacter;

    /// <summary>Affiche la page perso sans TCP et termine la mise en page avant tout focus.</summary>
    internal void ShowCharacterSelectForTest()
    {
        SetPhase(ClientUiPhase.CharacterSelect);
        _panelCharacter.Visible = true;
        _panelCharacter.BringToFront();
        PerformLayout();
        LayoutTree(_panelCharacter);
        if (_panelCharacter.Visible)
        {
            _panelCharacter.CreateControl();
        }

        if (_appearancePicker.Visible)
        {
            _appearancePicker.CreateControl();
        }

        System.Windows.Forms.Application.DoEvents();
    }

    private static void LayoutTree(Control root)
    {
        root.PerformLayout();
        foreach (Control child in root.Controls)
        {
            LayoutTree(child);
        }
    }

    internal TextBox NewCharNameTextBoxForTest => _txtNewCharName;

    internal ComboBox CharactersComboForTest => _cmbCharacters;

    internal InventoryPanel InventoryPanelForTest => _inventoryPanel;

    internal byte? SelectedInventorySlotForTest => _inventoryPanel.SelectedInventorySlotForTest;

    internal EquipmentPanel EquipmentPanelForTest => _equipmentPanel;

    internal CharacterSheetPanel CharacterSheetForTest => _characterSheet;

    internal bool IsCharacterSheetTabSelectedForTest => _gameplayTabs.SelectedTab == _tabCharacter;

    internal void ShowPlayingHudForTest()
    {
        if (string.IsNullOrEmpty(_username))
        {
            _username = "Netsun";
        }

        SetPhase(ClientUiPhase.Playing);
    }

    /// <summary>Ouvre l'inventaire en jeu pour que « Porter/Retirer la tunique » puisse recevoir un clic.</summary>
    internal void ShowInventoryEquipmentForTest()
    {
        ShowPlayingHudForTest();
        OnHudMenuCommand(HudMenuCommand.Inventory);
        LayoutTree(_panelGame);
        if (_panelGame.Visible)
        {
            _panelGame.CreateControl();
        }

        if (_equipmentPanel.Visible)
        {
            _equipmentPanel.CreateControl();
        }

        System.Windows.Forms.Application.DoEvents();
    }

    internal void PressCharacterSheetKeyForTest() => MainShell_KeyDown(this, new KeyEventArgs(Keys.C));

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

    internal Button ShopToggleButtonForTest => _btnShopToggle;

    internal string ShopListingLabelForTest => _lblShopListing.Text;

    internal bool ShopOpenForTest => _shopBank.ShopOpen;

    internal void OpenShopForTest()
    {
        if (_cmbShop.SelectedItem is ShopPickRow shop)
        {
            OpenShopWindow(shop.Id, showForm: false);
            return;
        }

        if (_cmbShop.Items.Count > 0 && _cmbShop.Items[0] is ShopPickRow first)
        {
            OpenShopWindow(first.Id, showForm: false);
        }
    }

    internal void ConfirmShopBuyForTest()
    {
        OpenShopForTest();
        _btnShopBuy.PerformClick();
        _btnShopBuy.PerformClick();
    }

    internal void ConfirmShopSellForTest()
    {
        OpenShopForTest();
        _btnShopSell.PerformClick();
        _btnShopSell.PerformClick();
    }

    internal void ConfirmBankDepositItemForTest()
    {
        _btnBankDepositItem.PerformClick();
        _btnBankDepositItem.PerformClick();
    }

    internal void ConfirmBankWithdrawItemForTest()
    {
        _btnBankWithdrawItem.PerformClick();
        _btnBankWithdrawItem.PerformClick();
    }

    internal void ConfirmBankDepositGoldForTest()
    {
        _btnBankDepositGold.PerformClick();
        _btnBankDepositGold.PerformClick();
    }

    internal void ConfirmBankWithdrawGoldForTest()
    {
        _btnBankWithdrawGold.PerformClick();
        _btnBankWithdrawGold.PerformClick();
    }

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

    internal int? CombatGoldForTest => _lastCombatState?.Gold;

    internal int? CombatMaxHpForTest => _lastCombatState?.MaxHp;

    internal bool CombatIsDeadForTest => _lastCombatState?.IsDead ?? false;

    internal void OnInventorySnapshotForTest(InventorySnapshotWire snapshot) => OnInventorySnapshot(snapshot);

    internal Button RespawnButtonForTest => _btnRespawn;

    internal void SelectGameplayTabForTest()
    {
        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabGameplay;
    }

    internal void SelectChatTabForTest()
    {
        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabChat;
    }

    internal void SelectChatChannelForTest(int channelIndex) => _cmbChannel.SelectedIndex = channelIndex;

    internal bool LogContainsForTest(string fragment) =>
        _txtLog.Text.Contains(fragment, StringComparison.Ordinal);

    internal string LogTextForTest => _txtLog.Text;

    internal TabControl GameplayTabsForTest => _gameplayTabs;

    internal HudWindowChrome WindowChromeForTest => _windowChrome;

    internal bool IsPhase8TabSelectedForTest => _gameplayTabs.SelectedTab == _tabPhase8;

    internal void SelectPhase8TabForTest()
    {
        SetWindowLayerVisible(true);
        _gameplayTabs.SelectedTab = _tabPhase8;
        _tabPhase8.PerformLayout();
        LayoutGameHud();
        Update();
    }

    internal SocialHubPanel SocialHubForTest => _socialHub;

    internal ClientSocialRoster SocialRosterForTest => _socialRoster;

    internal bool IsSocialTabSelectedForTest => _gameplayTabs.SelectedTab == _tabSocial;

    internal void OpenSocialPanelForTest(SocialKind kind) => OpenSocialPanel(kind);

    internal void OpenEconomyPanelForTest(EconomyHubKind kind) => OpenEconomyPanel(kind);

    internal ClientEconomyHub EconomyHubForTest => _economyHub;

    internal void OnEconomyHubSnapshotForTest(EconomyHubSnapshotWire snap) => OnEconomyHubSnapshot(snap);

    internal void OpenInstancePanelForTest() => OpenInstancePanel();

    internal ClientInstanceHub InstanceHubForTest => _instanceHub;

    internal ClientCombatHud CombatHudForTest => _combatHud;

    internal void OnDamageEventForTest(DamageEvent ev) => OnDamageEvent(ev);

    internal void OnStatusEffectForTest(StatusEffectEvent ev, DamageEvent? damage) => OnStatusEffect(ev, damage);

    internal void OnInstanceHubSnapshotForTest(InstanceHubSnapshotWire snap) => OnInstanceHubSnapshot(snap);

    internal void OnSocialSnapshotForTest(SocialSnapshotWire snap) => OnSocialSnapshot(snap);

    internal void OnSocialEventForTest(SocialEventWire ev) => OnSocialEvent(ev);

    internal void OnSocialResultForTest(SocialResultWire result) => OnSocialResult(result);

    internal DialoguePanel DialoguePanelForTest => _dialoguePanel;

    internal QuestJournalPanel QuestJournalPanelForTest => _questJournalPanel;

    internal CraftPanel CraftPanelForTest => _craftPanel;

    internal EnvironmentPanel EnvironmentPanelForTest => _environmentPanel;

    internal void AcquireProfessionForTest(Guid professionId) => _ = AcquireProfessionAsync(professionId);

    internal Button HelpButtonForTest => _btnHelp;

    internal Button OptionsButtonForTest => _btnOptions;

    internal Button CopyDiagnosticsButtonForTest => _btnCopyDiagnostics;

    internal string VersionBadgeTextForTest => _lblVersion.Text;

    internal string PlayerStatusTextForTest => _lblPlayerStatus.Text;

    internal string MoveHintTextForTest => _lblMoveHint.Text;

    internal UserSettings SettingsForTest => _settings.Clone();

    internal int UiScalePercentForTest => _uiScalePercent;

    internal InputService InputServiceForTest => _input;

    internal SoundService SoundServiceForTest => _sound;

    internal void OpenHelpForTest() => OpenHelp();

    internal void OpenOptionsForTest() => OpenOptions();

    internal void InvokeHudMenuCommandForTest(HudMenuCommand command) => OnHudMenuCommand(command);

    internal string CopyDiagnosticsForTest()
    {
        var report = BuildDiagnosticsText();
        try
        {
            Clipboard.SetText(report);
        }
        catch
        {
            // CI / headless : le texte suffit.
        }

        return report;
    }

    internal HelpForm? HelpFormForTest => _helpForm is { IsDisposed: false } ? _helpForm : null;

    internal void ShowPlayerStatusForTest(string message) => ShowPlayerStatus(message);

    internal void ApplyKeyboardPresetForTest(KeyboardLayoutPreset preset)
    {
        _settings.ApplyPreset(preset);
        _input.Apply(_settings);
        _settingsStore.Save(_settings);
        RefreshMoveHint();
    }

    internal void ProcessF1ForTest()
    {
        MainShell_KeyDown(this, new KeyEventArgs(Keys.F1));
    }

    internal void ProcessF8ForTest()
    {
        MainShell_KeyDown(this, new KeyEventArgs(Keys.F8));
    }

    internal WeatherOverlayPlan WeatherPlanForTest => _weatherPlan;

    internal WeatherDebugOverride WeatherDebugForTest => _weatherDebug;

    internal void ApplyEnvironmentStateForTest(EnvironmentStateWire state) => OnEnvironmentStatePush(state);

    internal Panel WorldHostForTest => _worldHost;

    internal Panel MapScrollForTest => _mapScroll;

    internal PictureBox MapPictureForTest => _picMap;

    internal bool MapScrollUsesAutoScrollForTest => _mapScroll.AutoScroll;

    internal Point MapPictureLocationForTest => _picMap.Location;

    internal void ShowOfflineMapViewportForTest(Map map, float? focusWorldXPx, float? focusWorldYPx)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        if (focusWorldXPx is { } fx && focusWorldYPx is { } fy)
        {
            _localVisualInitialized = true;
            _visLocalCx = fx;
            _visLocalCy = fy;
            _srvPixelX = (int)Math.Round(fx);
            _srvPixelY = (int)Math.Round(fy);
            SnapCameraToLocalVisual();
        }
        else
        {
            _localVisualInitialized = false;
            _camFocusInitialized = false;
        }

        SetPhase(ClientUiPhase.Playing);
        RedrawMap();
        LayoutGameHud();
    }

    internal HudStatusModule StatusHudForTest => _hudStatus;

    internal HudMinimapModule MinimapForTest => _hudMinimap;

    internal HudQuestTrackerModule QuestTrackerForTest => _hudQuest;

    internal HudChatDock ChatDockForTest => _hudChat;

    internal HudHotbar HotbarForTest => _hudHotbar;

    internal HudMenuRing MenuRingForTest => _hudMenu;

    internal bool WindowLayerVisibleForTest => _windowLayerVisible;

    internal bool GameToolbarOnWorldForTest => _gameToolbar.Parent == _worldHost;

    internal int SmoothTimerIntervalForTest => _smoothTimer.Interval;

    internal MovementMeasureProbe MovementMeasureForTest => _movementMeasure;

    internal void SetWindowLayerVisibleForTest(bool visible) => SetWindowLayerVisible(visible);

    internal void LayoutGameHudForTest() => LayoutGameHud();

    internal bool PresentInteractForTest(bool success, string message) => TryPresentEventMessage(success, message);

    internal void ApplyInteractResultForTest(bool ok, string message) =>
        OnInteractResult(ok, message, Guid.Empty);

    internal int EventPictureCountForTest => _eventPictures.Count;

    internal bool TryGetEventPictureForTest(int pictureId, out int x, out int y, out int opacity, out string blend)
    {
        if (!_eventPictures.TryGetValue(pictureId, out var picture))
        {
            x = 0;
            y = 0;
            opacity = 0;
            blend = string.Empty;
            return false;
        }

        x = picture.X;
        y = picture.Y;
        opacity = picture.Opacity;
        blend = picture.Blend;
        return true;
    }

    internal bool EventMessageOpenForTest => _eventMessage.IsOpen;

    internal string EventMessageTextForTest => _eventMessage.BodyText;

    internal Rectangle EventMessageBoundsForTest => _eventMessage.Bounds;

    internal void PressPlayingKeyForTest(Keys key) => MainShell_KeyDown(this, new KeyEventArgs(key));

    internal void ClickWorldForTest() => OnWorldSurfaceClick();

    internal bool InteractHintVisibleForTest => _interactHint.Visible;

    internal string InteractHintTextForTest => _interactHint.Visible ? _interactHint.Text : string.Empty;

    internal Rectangle InteractHintBoundsForTest => _interactHint.Bounds;

    internal void SetMapEventsForTest(IReadOnlyList<MapEventWireEntry> events)
    {
        _mapEvents.Clear();
        if (events is { Count: > 0 })
        {
            _mapEvents.AddRange(events);
        }

        RefreshInteractHint();
    }

    internal void PushDialogueForTest(DialogueStateWire state) => OnDialogueStatePush(state);

    internal void SetInteractBindingForTest(string keyName)
    {
        _settings.Bindings.Interact = keyName;
        _settings.Bindings.Normalize(_settings.KeyboardPreset);
        _input.Apply(_settings);
        RefreshMoveHint();
    }

    internal void RefreshInteractHintForTest() => RefreshInteractHint();

    internal void FocusMapSurfaceForTest()
    {
        _picMap.TabStop = true;
        ActiveControl = _picMap;
        RefreshInteractHint();
    }
}
