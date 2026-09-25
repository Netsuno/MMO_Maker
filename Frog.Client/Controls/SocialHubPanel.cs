#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Economy;
using Frog.Core.Enums;
using Frog.Core.Instances;
using Frog.Core.Social;

namespace Frog.Client.Controls;

/// <summary>
/// Fenêtre overlay Amis / Groupe / Guilde — chrome DA v2 (tabs or) + boutons contraste Kenney (#16).
/// </summary>
public sealed class SocialHubPanel : UserControl
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _tabFriends = new("Amis") { Padding = new Padding(4) };
    private readonly TabPage _tabParty = new("Groupe") { Padding = new Padding(4) };
    private readonly TabPage _tabGuild = new("Guilde") { Padding = new Padding(4) };
    private readonly TabPage _tabMail = new("Courrier") { Padding = new Padding(4) };
    private readonly TabPage _tabAuction = new("HdV") { Padding = new Padding(4) };
    private readonly TabPage _tabGuildBank = new("Coffre") { Padding = new Padding(4) };
    private readonly TabPage _tabInstance = new("Instance") { Padding = new Padding(4) };
    private readonly SocialKindSurface _friends;
    private readonly SocialKindSurface _party;
    private readonly SocialKindSurface _guild;
    private readonly EconomyHubSurface _mail = new(EconomyHubKind.Mail);
    private readonly EconomyHubSurface _auction = new(EconomyHubKind.Auction);
    private readonly EconomyHubSurface _guildBank = new(EconomyHubKind.GuildBank);
    private readonly InstanceHubSurface _instance = new();
    private ClientSocialRoster _roster = new();
    private ClientEconomyHub _economy = new();
    private ClientInstanceHub _instanceHub = new();

    public event Action<SocialClientRequest>? ActionRequested;

    public event Action<EconomyHubKind>? EconomyQueryRequested;

    public event Action<InstanceHubKind>? InstanceQueryRequested;

    public event Action<InstanceHubKind, Guid>? InstanceEnterRequested;

    public event Action<InstanceHubKind>? InstanceLeaveRequested;

    public event Action? SurfaceChanged;

    public SocialHubPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        _friends = new SocialKindSurface(SocialKind.Friend, this);
        _party = new SocialKindSurface(SocialKind.Party, this);
        _guild = new SocialKindSurface(SocialKind.Guild, this);
        _tabFriends.Controls.Add(_friends);
        _tabParty.Controls.Add(_party);
        _tabGuild.Controls.Add(_guild);
        _tabMail.Controls.Add(_mail);
        _tabAuction.Controls.Add(_auction);
        _tabGuildBank.Controls.Add(_guildBank);
        _tabInstance.Controls.Add(_instance);
        _mail.QueryRequested += kind => EconomyQueryRequested?.Invoke(kind);
        _auction.QueryRequested += kind => EconomyQueryRequested?.Invoke(kind);
        _guildBank.QueryRequested += kind => EconomyQueryRequested?.Invoke(kind);
        _instance.QueryRequested += kind => InstanceQueryRequested?.Invoke(kind);
        _instance.EnterRequested += (kind, id) => InstanceEnterRequested?.Invoke(kind, id);
        _instance.LeaveRequested += kind => InstanceLeaveRequested?.Invoke(kind);
        _tabs.TabPages.Add(_tabFriends);
        _tabs.TabPages.Add(_tabParty);
        _tabs.TabPages.Add(_tabGuild);
        _tabs.TabPages.Add(_tabMail);
        _tabs.TabPages.Add(_tabAuction);
        _tabs.TabPages.Add(_tabGuildBank);
        _tabs.TabPages.Add(_tabInstance);
        UiTheme.StyleGoldTabs(_tabs);
        Controls.Add(_tabs);
        Paint += (s, e) => UiTheme.PaintDoubleGoldFrame(this, e);
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            SyncActions();
            SurfaceChanged?.Invoke();
            if (TryGetSelectedEconomy(out var economyKind))
            {
                EconomyQueryRequested?.Invoke(economyKind);
            }
            else if (IsInstanceTabSelected)
            {
                InstanceQueryRequested?.Invoke(InstanceHubKind.Dungeon);
                InstanceQueryRequested?.Invoke(InstanceHubKind.Raid);
            }
        };
        ApplyRoster(_roster);
        ApplyEconomy(_economy);
        ApplyInstance(_instanceHub);
    }

    public SocialKind SelectedKind
    {
        get
        {
            if (_tabs.SelectedTab == _tabParty)
            {
                return SocialKind.Party;
            }

            if (_tabs.SelectedTab == _tabGuild)
            {
                return SocialKind.Guild;
            }

            return SocialKind.Friend;
        }
    }

    public void SelectKind(SocialKind kind)
    {
        _tabs.SelectedTab = kind switch
        {
            SocialKind.Party => _tabParty,
            SocialKind.Guild => _tabGuild,
            _ => _tabFriends
        };
        SyncActions();
        SurfaceChanged?.Invoke();
    }

    public void SelectEconomy(EconomyHubKind kind)
    {
        _tabs.SelectedTab = kind switch
        {
            EconomyHubKind.Auction => _tabAuction,
            EconomyHubKind.GuildBank => _tabGuildBank,
            _ => _tabMail
        };
        SyncActions();
        SurfaceChanged?.Invoke();
        EconomyQueryRequested?.Invoke(kind);
    }

    public bool TryGetSelectedEconomy(out EconomyHubKind kind)
    {
        if (_tabs.SelectedTab == _tabAuction)
        {
            kind = EconomyHubKind.Auction;
            return true;
        }

        if (_tabs.SelectedTab == _tabMail)
        {
            kind = EconomyHubKind.Mail;
            return true;
        }

        if (_tabs.SelectedTab == _tabGuildBank)
        {
            kind = EconomyHubKind.GuildBank;
            return true;
        }

        kind = default;
        return false;
    }

    public bool IsInstanceTabSelected => _tabs.SelectedTab == _tabInstance;

    /// <summary>Nom affiché du membre sélectionné (ami, groupe ou guilde), pas une invitation.</summary>
    public bool TryGetSelectedWhisperName(out string displayName)
    {
        displayName = string.Empty;
        if (TryGetSelectedEconomy(out _) || IsInstanceTabSelected)
        {
            return false;
        }

        return Surface(SelectedKind).TryGetSelectedName(out displayName);
    }

    public void SelectInstance()
    {
        _tabs.SelectedTab = _tabInstance;
        SyncActions();
        SurfaceChanged?.Invoke();
        InstanceQueryRequested?.Invoke(InstanceHubKind.Dungeon);
        InstanceQueryRequested?.Invoke(InstanceHubKind.Raid);
    }

    public void ApplyRoster(ClientSocialRoster roster)
    {
        _roster = roster ?? new ClientSocialRoster();
        _friends.Bind(_roster);
        _party.Bind(_roster);
        _guild.Bind(_roster);
        SyncActions();
    }

    public void ApplyEconomy(ClientEconomyHub state)
    {
        _economy = state ?? new ClientEconomyHub();
        _mail.Bind(_economy);
        _auction.Bind(_economy);
        _guildBank.Bind(_economy);
    }

    public void ApplyInstance(ClientInstanceHub state)
    {
        _instanceHub = state ?? new ClientInstanceHub();
        _instance.Bind(_instanceHub);
    }

    public string ChromeTitle => IsInstanceTabSelected
        ? (string.IsNullOrEmpty(_instanceHub.CurrentInstanceName) ? "Instance" : _instanceHub.CurrentInstanceName)
        : TryGetSelectedEconomy(out var economy)
            ? ClientEconomyHub.KindLabel(economy)
            : ClientSocialRoster.KindLabel(SelectedKind);

    internal TabControl TabsForTest => _tabs;

    internal int VisibleRowCountForTest(SocialKind kind) => Surface(kind).RowCountForTest;

    internal string EmptyHintForTest(SocialKind kind) => Surface(kind).EmptyHintForTest;

    internal string StatusForTest(SocialKind kind) => Surface(kind).StatusForTest;

    internal void SelectFirstRowForTest(SocialKind kind) => Surface(kind).SelectFirstForTest();

    internal void SetInputForTest(SocialKind kind, string text) => Surface(kind).SetInputForTest(text);

    internal void ClickActionForTest(SocialKind kind, string buttonText) =>
        Surface(kind).ClickActionForTest(buttonText);

    internal bool ActionEnabledForTest(SocialKind kind, string buttonText) =>
        Surface(kind).ActionEnabledForTest(buttonText);

    internal void Raise(SocialClientRequest request) => ActionRequested?.Invoke(request);

    internal ClientSocialRoster RosterForTest => _roster;

    internal ClientEconomyHub EconomyForTest => _economy;

    internal int EconomyRowCountForTest(EconomyHubKind kind) => EconomySurface(kind).RowCountForTest;

    internal string EconomyEmptyHintForTest(EconomyHubKind kind) => EconomySurface(kind).EmptyHintForTest;

    internal void ClickEconomyRefreshForTest(EconomyHubKind kind) => EconomySurface(kind).ClickRefreshForTest();

    internal ClientInstanceHub InstanceForTest => _instanceHub;

    internal int InstanceRowCountForTest => _instance.RowCountForTest;

    internal string InstanceEmptyHintForTest => _instance.EmptyHintForTest;

    internal string InstanceNameForTest => _instance.InstanceNameForTest;

    internal void ClickInstanceRefreshForTest() => _instance.ClickRefreshForTest();

    internal void ClickInstanceEnterForTest() => _instance.ClickEnterForTest();

    internal void ClickInstanceLeaveForTest() => _instance.ClickLeaveForTest();

    private EconomyHubSurface EconomySurface(EconomyHubKind kind) => kind switch
    {
        EconomyHubKind.Auction => _auction,
        EconomyHubKind.GuildBank => _guildBank,
        _ => _mail
    };

    private SocialKindSurface Surface(SocialKind kind) => kind switch
    {
        SocialKind.Party => _party,
        SocialKind.Guild => _guild,
        _ => _friends
    };

    private void SyncActions()
    {
        _friends.RefreshActions();
        _party.RefreshActions();
        _guild.RefreshActions();
    }

    private sealed class SocialKindSurface : UserControl
    {
        private readonly SocialKind _kind;
        private readonly SocialHubPanel _hub;
        private readonly Label _motd = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        private readonly Label _empty = new()
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(8),
        };
        private readonly ListBox _list = new()
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Visible = false,
        };
        private readonly Label _status = new()
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        private readonly FlowLayoutPanel _actions = new()
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        private readonly TextBox _input = new()
        {
            Width = 220,
            PlaceholderText = "Guid personnage / nom guilde / MOTD",
        };
        private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
        private ClientSocialRoster _roster = new();

        public SocialKindSurface(SocialKind kind, SocialHubPanel hub)
        {
            _kind = kind;
            _hub = hub;
            Dock = DockStyle.Fill;
            BackColor = UiTheme.BgPanel;
            ForeColor = UiTheme.TextPrimary;
            _motd.ForeColor = UiTheme.TextGold;
            _motd.BackColor = UiTheme.BgPanelHeader;
            _empty.ForeColor = UiTheme.TextSecondary;
            _empty.BackColor = UiTheme.BgPanel;
            _status.ForeColor = UiTheme.TextMuted;
            _status.BackColor = UiTheme.BgPanel;
            _list.BackColor = UiTheme.BgInput;
            _list.ForeColor = UiTheme.TextPrimary;
            _list.BorderStyle = BorderStyle.FixedSingle;
            UiTheme.StyleInput(_input);

            var listHost = new Panel { Dock = DockStyle.Fill };
            listHost.Controls.Add(_empty);
            listHost.Controls.Add(_list);

            BuildButtons();
            _actions.Controls.Add(_input);
            Controls.Add(listHost);
            Controls.Add(_actions);
            Controls.Add(_status);
            Controls.Add(_motd);
            _list.SelectedIndexChanged += (_, _) => RefreshActions();
            _input.TextChanged += (_, _) => RefreshActions();
        }

        public bool TryGetSelectedName(out string displayName)
        {
            displayName = string.Empty;
            if (_list.SelectedItem is not SocialRow row || row.Item.IsPendingInvite)
            {
                return false;
            }

            var name = row.Item.DisplayName.Trim();
            if (name.Length == 0)
            {
                return false;
            }

            displayName = name;
            return true;
        }

        public int RowCountForTest => _list.Items.Count;

        public string EmptyHintForTest => _empty.Text;

        public string StatusForTest => _status.Text;

        public void SelectFirstForTest()
        {
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }

        public void SetInputForTest(string text) => _input.Text = text;

        public void ClickActionForTest(string buttonText)
        {
            if (_buttons.TryGetValue(buttonText, out var btn) && btn.Enabled)
            {
                btn.PerformClick();
            }
        }

        public bool ActionEnabledForTest(string buttonText) =>
            _buttons.TryGetValue(buttonText, out var btn) && btn.Enabled;

        public void Bind(ClientSocialRoster roster)
        {
            _roster = roster;
            var selected = (_list.SelectedItem as SocialRow)?.Item.CharacterId;
            var rows = roster.BuildRows(_kind);
            _list.Items.Clear();
            foreach (var row in rows)
            {
                _list.Items.Add(new SocialRow(row));
            }

            if (_list.Items.Count > 0)
            {
                var restore = selected is Guid prev
                    ? _list.Items.Cast<SocialRow>().ToList().FindIndex(r => r.Item.CharacterId == prev)
                    : -1;
                _list.SelectedIndex = restore >= 0 ? restore : 0;
            }

            var empty = roster.EmptyHint(_kind);
            _empty.Text = empty;
            _empty.Visible = rows.Count == 0;
            _list.Visible = rows.Count > 0;
            _status.Text = roster.StatusLine;
            var motd = roster.MotdText(_kind);
            _motd.Text = string.IsNullOrEmpty(motd)
                ? ClientSocialRoster.KindLabel(_kind)
                : "MOTD : " + motd;
            RefreshActions();
        }

        public void RefreshActions()
        {
            var selected = _list.SelectedItem as SocialRow;
            var pending = selected is { Item.IsPendingInvite: true }
                          || selected is { Item.Role: ClientSocialRoster.FriendRoleIncoming };
            var hasMember = selected is { Item.IsPendingInvite: false };
            var hasGuid = ClientSocialRoster.TryParseTargetGuid(_input.Text, out _, out _);
            switch (_kind)
            {
                case SocialKind.Friend:
                    SetEnabled("Ajouter", hasGuid);
                    SetEnabled("Accepter", pending);
                    SetEnabled("Refuser", pending);
                    SetEnabled("Retirer", hasMember && selected!.Item.Role != ClientSocialRoster.FriendRoleIncoming);
                    break;
                case SocialKind.Party:
                    SetEnabled("Inviter", hasGuid);
                    SetEnabled("Accepter", pending);
                    SetEnabled("Refuser", pending);
                    SetEnabled("Quitter", _roster.HasParty);
                    SetEnabled("Expulser", _roster.HasParty && hasMember);
                    SetEnabled("Chef", _roster.HasParty && hasMember);
                    SetEnabled("Dissoudre", _roster.HasParty);
                    break;
                case SocialKind.Guild:
                    SetEnabled("Créer", !string.IsNullOrWhiteSpace(_input.Text) && !_roster.HasGuild);
                    SetEnabled("Inviter", _roster.HasGuild && hasGuid);
                    SetEnabled("Accepter", pending);
                    SetEnabled("Refuser", pending);
                    SetEnabled("Quitter", _roster.HasGuild);
                    SetEnabled("Expulser", _roster.HasGuild && hasMember);
                    SetEnabled("Chef", _roster.HasGuild && hasMember);
                    SetEnabled("Dissoudre", _roster.HasGuild);
                    SetEnabled("MOTD", _roster.HasGuild && !string.IsNullOrWhiteSpace(_input.Text));
                    break;
            }
        }

        private void BuildButtons()
        {
            string[] labels = _kind switch
            {
                SocialKind.Friend => ["Ajouter", "Accepter", "Refuser", "Retirer"],
                SocialKind.Party => ["Inviter", "Accepter", "Refuser", "Quitter", "Expulser", "Chef", "Dissoudre"],
                _ => ["Créer", "Inviter", "Accepter", "Refuser", "Quitter", "Expulser", "Chef", "Dissoudre", "MOTD"]
            };
            foreach (var label in labels)
            {
                var btn = new Button
                {
                    Text = label,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 4, 4),
                };
                UiTheme.StyleContrastHudButton(btn, enabled: true);
                btn.Click += (_, _) => OnAction(label);
                _buttons[label] = btn;
                _actions.Controls.Add(btn);
            }
        }

        private void SetEnabled(string label, bool enabled)
        {
            if (!_buttons.TryGetValue(label, out var btn))
            {
                return;
            }

            btn.Enabled = enabled;
            UiTheme.StyleContrastHudButton(btn, enabled);
        }

        private void OnAction(string label)
        {
            var selected = _list.SelectedItem as SocialRow;
            SocialClientRequest request;
            string? error = null;
            switch (label)
            {
                case "Ajouter" when ClientSocialRoster.TryParseTargetGuid(_input.Text, out var add, out error):
                    request = ClientSocialRoster.Invite(SocialKind.Friend, add);
                    break;
                case "Inviter" when ClientSocialRoster.TryParseTargetGuid(_input.Text, out var inv, out error):
                    request = ClientSocialRoster.Invite(_kind, inv);
                    break;
                case "Accepter" when selected is not null:
                    request = ClientSocialRoster.Accept(_kind, ClientSocialRoster.AcceptTarget(selected.Item));
                    break;
                case "Refuser" when selected is not null:
                    request = ClientSocialRoster.Decline(_kind, ClientSocialRoster.AcceptTarget(selected.Item));
                    break;
                case "Retirer" when selected is not null:
                    request = ClientSocialRoster.FriendRemove(selected.Item.CharacterId);
                    break;
                case "Quitter":
                    request = ClientSocialRoster.Leave(_kind);
                    break;
                case "Expulser" when selected is not null:
                    request = ClientSocialRoster.Kick(_kind, selected.Item.CharacterId);
                    break;
                case "Chef" when selected is not null:
                    request = ClientSocialRoster.TransferLeader(_kind, selected.Item.CharacterId);
                    break;
                case "Dissoudre":
                    request = ClientSocialRoster.Disband(_kind);
                    break;
                case "Créer":
                    if (!ClientSocialRoster.TryGuildCreate(_input.Text, out request, out error))
                    {
                        _status.Text = error;
                        return;
                    }

                    break;
                case "MOTD":
                    if (!ClientSocialRoster.TryGuildSetMotd(_input.Text, out request, out error))
                    {
                        _status.Text = error;
                        return;
                    }

                    break;
                default:
                    if (!string.IsNullOrEmpty(error))
                    {
                        _status.Text = error;
                    }

                    return;
            }

            _hub.Raise(request);
        }

        private sealed class SocialRow
        {
            public SocialListItem Item { get; }

            public SocialRow(SocialListItem item)
            {
                Item = item;
            }

            public override string ToString() => Item.Text;
        }
    }
}
