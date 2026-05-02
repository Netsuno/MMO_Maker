using System.Collections.Concurrent;
using Frog.Client.Network;
using Frog.Client.UI;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Client;

public partial class Form1 : Form
{
    private FrogGameClient? _client;
    private Map? _map;
    private string? _username;
    private int _tileX;
    private int _tileY;
    private readonly ConcurrentDictionary<string, (int X, int Y)> _others = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastMoveUtc = DateTime.MinValue;
    private readonly TextBox _txtHost = new() { Text = "127.0.0.1", Width = 120 };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 6000, Width = 70 };
    private readonly TextBox _txtUser = new() { Text = "demo", Width = 100 };
    private readonly TextBox _txtPass = new() { Text = "demo", Width = 100, UseSystemPasswordChar = true };
    private readonly Button _btnConnect = new() { Text = "Connecter" };
    private readonly Button _btnDisconnect = new() { Text = "Déconnecter", Enabled = false };
    private readonly Button _btnLogin = new() { Text = "Login", Enabled = false };
    private readonly Button _btnRegister = new() { Text = "Inscription", Enabled = false };
    private readonly Button _btnMap = new() { Text = "Demander map", Enabled = false };
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 72, Dock = DockStyle.Bottom };
    private readonly Panel _mapScroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly PictureBox _picMap = new() { Location = new Point(0, 0), SizeMode = PictureBoxSizeMode.AutoSize };
    private readonly TextBox _txtChat = new() { Dock = DockStyle.Fill };
    private readonly Button _btnSendChat = new() { Text = "Envoyer chat", Dock = DockStyle.Bottom, Height = 28 };
    private readonly ComboBox _cmbChannel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly TextBox _txtWhisperTo = new() { PlaceholderText = "Cible whisper", Width = 120 };
    private readonly TextBox _txtMeleeTarget = new() { PlaceholderText = "Cible mêlée", Width = 100 };
    private readonly Button _btnMelee = new() { Text = "Mêlée", Enabled = false };
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

    private void BuildLayout()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(6)
        };
        top.Controls.AddRange(new Control[]
        {
            new Label { Text = "Hôte", AutoSize = true, Margin = new Padding(0, 8, 0, 0) },
            _txtHost,
            new Label { Text = "Port", AutoSize = true, Margin = new Padding(0, 8, 0, 0) },
            _numPort,
            new Label { Text = "User", AutoSize = true, Margin = new Padding(0, 8, 0, 0) },
            _txtUser,
            new Label { Text = "Pass", AutoSize = true, Margin = new Padding(0, 8, 0, 0) },
            _txtPass,
            _btnConnect,
            _btnDisconnect,
            _btnLogin,
            _btnRegister,
            _btnMap,
            _txtMeleeTarget,
            _btnMelee
        });

        _mapScroll.Controls.Add(_picMap);
        var rightChat = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 320,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4)
        };
        rightChat.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        rightChat.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightChat.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var chatTop = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        chatTop.Controls.Add(_cmbChannel);
        chatTop.Controls.Add(_txtWhisperTo);
        rightChat.Controls.Add(chatTop, 0, 0);
        _txtChat.Dock = DockStyle.Fill;
        rightChat.Controls.Add(_txtChat, 0, 1);
        rightChat.Controls.Add(_btnSendChat, 0, 2);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        center.Controls.Add(_mapScroll, 0, 0);
        center.Controls.Add(rightChat, 1, 0);

        Controls.Add(center);
        Controls.Add(_txtLog);
        Controls.Add(top);
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
        _btnSendChat.Click += async (_, _) => await SendChatAsync();
        _btnMelee.Click += async (_, _) => await MeleeAsync();

        _client.HelloReceived += msg => AppendLog("Hello: " + msg);
        _client.LoginResultReceived += OnLoginResult;
        _client.RegisterResultReceived += (ok, msg) => AppendLog(ok ? "Inscription OK: " + msg : "Inscription: " + msg);
        _client.MapDataReceived += OnMapData;
        _client.PositionUpdateReceived += OnPositionUpdate;
        _client.PlayerLeaveReceived += OnPlayerLeave;
        _client.ErrorReceived += err => AppendLog("Erreur: " + err);
        _client.HeartbeatAckReceived += () => { };
        _client.LogoutAckReceived += () => AppendLog("Logout ACK");
        _client.ChatMessageReceived += OnChatMessage;
        _client.MeleeAttackResultReceived += (hit, tgt, msg) =>
            AppendLog($"Mêlée → {tgt}: {(hit ? "touche" : "rate")} — {msg}");
        _client.ConnectionClosed += OnConnectionClosed;
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
        _map = null;
        _username = null;
        _others.Clear();
        ClearMapImage();
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
        _heartbeatTimer.Start();
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

    private void OnMapData(int mapId, Map map)
    {
        AppendLog($"Map reçue id={mapId} {map.Name} {map.Width}x{map.Height}");
        _map = map;
        RedrawMap();
    }

    private void OnPositionUpdate(string user, int x, int y)
    {
        if (_username is not null && string.Equals(user, _username, StringComparison.OrdinalIgnoreCase))
        {
            _tileX = x;
            _tileY = y;
        }
        else
        {
            _others[user] = (x, y);
        }

        RedrawMap();
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

        var bmp = MapViewRenderer.Render(_map, _others, _username, _tileX, _tileY);
        ClearMapImage();
        _picMap.Image = bmp;
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
}
