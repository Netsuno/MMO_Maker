using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Frog.Client.Assets;
using Frog.Client.Network;
using Frog.Client.UI;
using Frog.Core.Character;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client;

public partial class Form1 : Form
{
    private FrogGameClient? _client;
    private Map? _map;
    private string? _username;
    private int _tileX;
    private int _tileY;
    private readonly ConcurrentDictionary<string, (int X, int Y)> _others = new(StringComparer.OrdinalIgnoreCase);
    private int _sessionDisplayedMapId;
    private DateTime _lastAutoMapRequestUtc = DateTime.MinValue;
    private static readonly TimeSpan AutoMapRequestDebounce = TimeSpan.FromMilliseconds(300);
    private readonly Dictionary<int, Bitmap> _tilesetBitmaps = new();
    private DateTime _lastMoveUtc = DateTime.MinValue;
    private DateTime _lastInteractUtc = DateTime.MinValue;
    private readonly List<MapEventWireEntry> _mapEvents = new();
    private readonly TextBox _txtHost = new() { Text = "127.0.0.1", Width = 120 };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 6000, Width = 70 };
    private readonly TextBox _txtUser = new() { Text = "demo", Width = 100 };
    private readonly TextBox _txtPass = new() { Text = "demo", Width = 100, UseSystemPasswordChar = true };
    private readonly Button _btnConnect = new() { Text = "Connecter" };
    private readonly Button _btnDisconnect = new() { Text = "Déconnecter", Enabled = false };
    private readonly Button _btnLogin = new() { Text = "Login", Enabled = false };
    private readonly Button _btnRegister = new() { Text = "Inscription", Enabled = false };
    private readonly Button _btnMap = new() { Text = "Demander map", Enabled = false };
    private readonly Button _btnLogout = new() { Text = "Logout", Enabled = false };
    private readonly ComboBox _cmbCharacters = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Enabled = false };
    private readonly Button _btnCharRefresh = new() { Text = "Liste persos", Width = 95, Enabled = false };
    private readonly Button _btnCharApply = new() { Text = "Activer", Width = 72, Enabled = false };
    private readonly TextBox _txtNewCharName = new() { Width = 100, PlaceholderText = "Nouveau perso" };
    private readonly Button _btnCharCreate = new() { Text = "Créer perso", Width = 95, Enabled = false };
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 72, Dock = DockStyle.Bottom };
    private readonly Panel _mapScroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly PictureBox _picMap = new() { Location = new Point(0, 0), SizeMode = PictureBoxSizeMode.AutoSize };
    private readonly TextBox _txtChat = new() { Dock = DockStyle.Fill };
    private readonly Button _btnSendChat = new() { Text = "Envoyer chat", Dock = DockStyle.Bottom, Height = 28 };
    private readonly ComboBox _cmbChannel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly TextBox _txtWhisperTo = new() { PlaceholderText = "Cible whisper", Width = 120 };
    private readonly TextBox _txtMeleeTarget = new() { PlaceholderText = "Cible mêlée", Width = 100 };
    private readonly Button _btnMelee = new() { Text = "Mêlée", Enabled = false };
    private readonly NumericUpDown[] _numStats = new NumericUpDown[CharacterStatsWire.PackedByteCount];
    private readonly Button _btnStatsApply = new() { Text = "Appliquer stats", AutoSize = true, Enabled = false };
    private readonly System.Windows.Forms.Timer _heartbeatTimer = new() { Interval = 45_000 };

    public Form1()
    {
        InitializeComponent();
        Text = "FRoG Client (WinForms)";
        ClientSize = new Size(1024, 720);
        KeyPreview = true;
        BuildLayout();
        _cmbChannel.Items.AddRange(new object[] { "Global", "Map", "Whisper" });
        _cmbChannel.SelectedIndex = 1;
        _heartbeatTimer.Tick += async (_, _) => await SendHeartbeatSafeAsync();
        Load += Form1_Load;
        FormClosing += async (_, _) => await Form1_FormClosingAsync();
        KeyDown += Form1_KeyDown;
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        var ctx = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _client = new FrogGameClient(ctx);
        WireClient();
    }

    private async Task Form1_FormClosingAsync()
    {
        _heartbeatTimer.Stop();
        if (_client is not null)
        {
            await _client.DisconnectAsync().ConfigureAwait(true);
            _client.Dispose();
        }
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

    private void BuildLayout()
    {
        StyleToolbarButton(_btnConnect);
        StyleToolbarButton(_btnDisconnect);
        StyleToolbarButton(_btnLogin);
        StyleToolbarButton(_btnRegister);
        StyleToolbarButton(_btnMap);
        StyleToolbarButton(_btnLogout);
        StyleToolbarButton(_btnCharRefresh);
        StyleToolbarButton(_btnCharApply);
        StyleToolbarButton(_btnCharCreate);
        StyleToolbarButton(_btnMelee);
        StyleToolbarButton(_btnStatsApply);
        _cmbCharacters.MinimumSize = new Size(220, 0);
        _cmbCharacters.Width = Math.Max(_cmbCharacters.Width, 280);
        _txtNewCharName.MinimumSize = new Size(120, 0);
        _txtNewCharName.Width = Math.Max(_txtNewCharName.Width, 140);
        foreach (Control c in new Control[] { _txtHost, _txtUser, _txtPass })
        {
            c.Margin = new Padding(2, 4, 12, 4);
        }

        _numPort.Margin = new Padding(2, 4, 12, 4);
        _txtMeleeTarget.Margin = new Padding(2, 4, 8, 4);

        var rowConn = CreateToolbarRow();
        rowConn.Controls.Add(Lbl("Hôte"));
        rowConn.Controls.Add(_txtHost);
        rowConn.Controls.Add(Lbl("Port"));
        rowConn.Controls.Add(_numPort);
        rowConn.Controls.Add(Lbl("User"));
        rowConn.Controls.Add(_txtUser);
        rowConn.Controls.Add(Lbl("Pass"));
        rowConn.Controls.Add(_txtPass);
        rowConn.Controls.Add(_btnConnect);
        rowConn.Controls.Add(_btnDisconnect);
        rowConn.Controls.Add(_btnLogin);
        rowConn.Controls.Add(_btnRegister);
        rowConn.Controls.Add(_btnMap);
        rowConn.Controls.Add(_btnLogout);

        var rowChar = CreateToolbarRow();
        rowChar.Controls.Add(Lbl("Perso"));
        rowChar.Controls.Add(_cmbCharacters);
        rowChar.Controls.Add(_btnCharRefresh);
        rowChar.Controls.Add(_btnCharApply);
        rowChar.Controls.Add(Lbl("Nouveau"));
        rowChar.Controls.Add(_txtNewCharName);
        rowChar.Controls.Add(_btnCharCreate);
        rowChar.Controls.Add(Lbl("Mêlée"));
        rowChar.Controls.Add(_txtMeleeTarget);
        rowChar.Controls.Add(_btnMelee);

        var rowStats = CreateToolbarRow();
        var statLabels = new[] { "STR", "AGI", "DEX", "INT", "VIT", "LUCK" };
        rowStats.Controls.Add(Lbl("Stats"));
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
            rowStats.Controls.Add(Lbl(statLabels[i], topPad: 8));
            rowStats.Controls.Add(_numStats[i]);
        }

        rowStats.Controls.Add(_btnStatsApply);
        rowStats.Controls.Add(Lbl("(flèches = bouger · E = interagir)", topPad: 10));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 8, 8, 6),
            BackColor = SystemColors.Control,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.Controls.Add(rowConn, 0, 0);
        toolbar.Controls.Add(rowChar, 0, 1);
        toolbar.Controls.Add(rowStats, 0, 2);

        _mapScroll.Controls.Add(_picMap);
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

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        center.Controls.Add(_mapScroll, 0, 0);
        center.Controls.Add(rightChat, 1, 0);

        _txtLog.MinimumSize = new Size(120, 88);
        _txtLog.Height = 100;

        MinimumSize = new Size(980, 640);
        ClientSize = new Size(Math.Max(ClientSize.Width, 1040), Math.Max(ClientSize.Height, 720));

        Controls.Add(center);
        Controls.Add(_txtLog);
        Controls.Add(toolbar);
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
        _btnMap.Click += async (_, _) => await MapRequestAsync();
        _btnLogout.Click += async (_, _) => await LogoutAsync();
        _btnSendChat.Click += async (_, _) => await SendChatAsync();
        _btnMelee.Click += async (_, _) => await MeleeAsync();

        _client.HelloReceived += msg => AppendLog("Hello: " + msg);
        _client.LoginResultReceived += OnLoginResult;
        _client.RegisterResultReceived += (ok, msg) => AppendLog(ok ? "Inscription OK: " + msg : "Inscription: " + msg);
        _client.MapDataReceived += OnMapData;
        _client.MapAlreadySyncedReceived += OnMapAlreadySynced;
        _client.CharacterPayloadReceived += OnCharacterPayload;
        _client.PositionUpdateReceived += OnPositionUpdate;
        _client.PlayerLeaveReceived += OnPlayerLeave;
        _client.ErrorReceived += err => AppendLog("Erreur: " + err);
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
        _client.ConnectionClosed += OnConnectionClosed;
        _btnCharRefresh.Click += async (_, _) => await RefreshCharacterListAsync();
        _btnCharApply.Click += async (_, _) => await ApplySelectedCharacterAsync();
        _btnCharCreate.Click += async (_, _) => await CreateCharacterAsync();
        _btnStatsApply.Click += async (_, _) => await ApplyCharacterStatsAsync();
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
        _btnLogout.Enabled = false;
        ResetCharacterPickUi();
        _map = null;
        _username = null;
        _sessionDisplayedMapId = 0;
        _others.Clear();
        ClearMapImage();
        DisposeTilesetBitmaps();
        _mapEvents.Clear();
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
        AppendLog(ok ? "Login OK: " + message : "Login refusé: " + message);
        if (!ok)
        {
            return;
        }

        _username = _txtUser.Text.Trim();
        _btnMap.Enabled = true;
        _btnMelee.Enabled = true;
        _btnLogout.Enabled = true;
        _cmbCharacters.Enabled = true;
        _btnCharRefresh.Enabled = true;
        _btnCharApply.Enabled = true;
        _txtNewCharName.Enabled = true;
        _btnCharCreate.Enabled = true;
        SetStatsControlsEnabled(true);
        _heartbeatTimer.Start();
        _ = RefreshCharacterListAsync();
        _ = MapRequestAsync();
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
        _sessionDisplayedMapId = 0;
        _others.Clear();
        ClearMapImage();
        DisposeTilesetBitmaps();
        _mapEvents.Clear();
        _btnMap.Enabled = false;
        _btnMelee.Enabled = false;
        _btnLogout.Enabled = false;
        ResetCharacterPickUi();
    }

    private void ResetCharacterPickUi()
    {
        _cmbCharacters.Items.Clear();
        _cmbCharacters.Enabled = false;
        _btnCharRefresh.Enabled = false;
        _btnCharApply.Enabled = false;
        _txtNewCharName.Enabled = false;
        _btnCharCreate.Enabled = false;
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
        _others.Clear();
        ReloadTilesetBitmaps();
        RedrawMap();
        _ = RequestMapEventsFromServerAsync();
    }

    private void OnMapAlreadySynced(int mapId, long revision)
    {
        AppendLog($"Carte id={mapId} déjà à jour (révision serveur {revision}).");
        _ = RequestMapEventsFromServerAsync();
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
            _ = MapRequestAsync();
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
            await _client.SendCharacterCreateAsync(name).ConfigureAwait(true);
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
        if (!isLocal && _sessionDisplayedMapId != 0 && mapId != _sessionDisplayedMapId)
        {
            return;
        }

        if (isLocal)
        {
            _tileX = x;
            _tileY = y;
            if (_sessionDisplayedMapId != 0 && mapId != _sessionDisplayedMapId)
            {
                TryScheduleMapRequestAfterWarp(mapId);
            }
        }
        else
        {
            _others[user] = (x, y);
        }

        RedrawMap();
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

        var t = _txtMeleeTarget.Text.Trim();
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

    private async void Form1_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_client is null || !_client.IsConnected || string.IsNullOrEmpty(_username) || _map is null)
        {
            return;
        }

        if (e.KeyCode == Keys.E)
        {
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

        sbyte dx = 0, dy = 0;
        switch (e.KeyCode)
        {
            case Keys.Left: dx = -1; break;
            case Keys.Right: dx = 1; break;
            case Keys.Up: dy = -1; break;
            case Keys.Down: dy = 1; break;
            default: return;
        }

        e.Handled = true;
        var now = DateTime.UtcNow;
        if ((now - _lastMoveUtc).TotalMilliseconds < 120)
        {
            return;
        }

        _lastMoveUtc = now;
        try
        {
            await _client.SendMoveAsync(dx, dy).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Move: " + ex.Message);
        }
    }

    private void RedrawMap()
    {
        if (_map is null)
        {
            return;
        }

        var bmp = MapViewRenderer.Render(_map, _others, _username, _tileX, _tileY, _tilesetBitmaps, _mapEvents);
        ClearMapImage();
        _picMap.Image = bmp;
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

        var t = DateTime.Now.ToString("HH:mm:ss");
        _txtLog.AppendText($"[{t}] {line}{Environment.NewLine}");
    }

    private sealed class CharacterPickRow(string id, string displayName)
    {
        public string Id { get; } = id;

        public string DisplayName { get; } = displayName;

        public override string ToString() => $"{DisplayName} — {Id}";
    }
}
