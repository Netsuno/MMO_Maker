#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Frog.Client.Config;
using Frog.Core.Constants;

namespace Frog.Client.UI;

/// <summary>
/// DA v2 step 5 — écran login immersif (pré-jeu). Carte centrée <c>bg.panel</c>,
/// filet or seulement ; champs compte/mdp crème ; hôte/port hors carte joueur (F9).
/// </summary>
public sealed class LoginShell : Panel
{
    public const int CardWidth = 400;
    public const int CharacterCardWidth = 520;
    public const int CardPadding = 12;
    public const int FieldWidth = 280;
    public const int LogoEmblemSize = 48;

    private readonly LoginCard _card = new();
    private readonly FlowLayoutPanel _opsStrip;
    private readonly Label _logoWordmark;
    private readonly Label _logoSub;
    private readonly LogoEmblem _emblem = new();
    private readonly CheckBox _remember;
    private readonly Label _opsHint;
    private readonly Label _networkHint;
    private bool _opsVisible;
    private FlowLayoutPanel? _body;
    private TextBox? _accountUser;
    private TextBox? _accountPass;
    private Button? _loginButton;
    private Button? _registerButton;
    private Button? _reconnectButton;
    private Button? _connectButton;
    private Button? _disconnectButton;
    private TextBox? _hostBox;
    private NumericUpDown? _portBox;
    private ComboBox? _serverList;
    private TextBox? _serverName;
    private Button? _addServerButton;
    private Button? _retryButton;
    private readonly Label _connectDiag;
    private int _appliedUiScalePercent = ClientUiScale.DefaultPercent;

    public LoginShell()
    {
        SetStyle(
            ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        Dock = DockStyle.Fill;
        BackColor = UiTheme.BgApp;
        ForeColor = UiTheme.TextPrimary;
        Padding = new Padding(0);

        _logoWordmark = new Label
        {
            Text = "FRoG",
            AutoSize = true,
            Font = UiTheme.UiFont(22f, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };
        _logoSub = new Label
        {
            Text = "Frog Isle",
            AutoSize = true,
            Font = UiTheme.UiFont(10f),
            ForeColor = UiTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8),
        };
        _remember = new CheckBox
        {
            Text = "Souvenir",
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 8),
        };
        _networkHint = new Label
        {
            Text = "Liste ci-dessus · F9 = ajouter · Options → Réseau",
            AutoSize = true,
            Font = UiTheme.UiFont(8f),
            ForeColor = UiTheme.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };
        _connectDiag = new Label
        {
            Text = "Protocole " + FrogWireProtocol.Version + " · prêt.",
            AutoSize = true,
            MaximumSize = new Size(FieldWidth, 0),
            Font = UiTheme.UiFont(8f),
            ForeColor = UiTheme.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };
        _opsHint = new Label
        {
            Text = "Hôte / port (F9)",
            AutoSize = true,
            Font = UiTheme.UiFont(8f, FontStyle.Bold),
            ForeColor = UiTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 2),
        };
        _opsStrip = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Visible = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };

        Controls.Add(_card);
        Resize += (_, _) => CenterCard();
        Layout += (_, _) => CenterCard();
    }

    public void Attach(
        TextBox user,
        TextBox pass,
        Button login,
        Button register,
        Button reconnect,
        Button connect,
        Button disconnect,
        TextBox host,
        NumericUpDown port,
        Label authStatus,
        ComboBox servers,
        TextBox serverName,
        Button addServer,
        Button retry)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(reconnect);
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(disconnect);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(authStatus);
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(addServer);
        ArgumentNullException.ThrowIfNull(retry);

        _accountUser = user;
        _accountPass = pass;
        _loginButton = login;
        _registerButton = register;
        _reconnectButton = reconnect;
        _connectButton = connect;
        _disconnectButton = disconnect;
        _hostBox = host;
        _portBox = port;
        _serverList = servers;
        _serverName = serverName;
        _addServerButton = addServer;
        _retryButton = retry;

        login.Text = "Connexion";
        login.MinimumSize = new Size(FieldWidth, 34);
        register.MinimumSize = new Size(120, 30);
        reconnect.MinimumSize = new Size(160, 30);
        connect.MinimumSize = new Size(120, 30);
        disconnect.MinimumSize = new Size(120, 30);
        retry.Text = "Réessayer";
        retry.MinimumSize = new Size(120, 30);
        addServer.Text = "Ajouter";
        addServer.MinimumSize = new Size(88, 30);

        StyleAccountField(user);
        StyleAccountField(pass);
        StylePrimaryCta(login);
        StyleSecondaryCta(register);
        StyleSecondaryCta(reconnect);
        StyleSecondaryCta(connect);
        StyleSecondaryCta(disconnect);
        StyleSecondaryCta(retry);
        StyleSecondaryCta(addServer);
        UiTheme.StyleInput(servers);
        UiTheme.StyleInput(serverName);

        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(CardPadding),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            Width = CardWidth,
        };
        _body = body;

        var logoRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding((CardWidth - CardPadding * 2 - LogoEmblemSize) / 2, 4, 0, 0),
        };
        logoRow.Controls.Add(_emblem);
        body.Controls.Add(logoRow);
        CenterLabel(body, _logoWordmark);
        CenterLabel(body, _logoSub);

        body.Controls.Add(FieldLabel("Compte"));
        user.Margin = new Padding(0, 0, 0, 8);
        body.Controls.Add(user);
        body.Controls.Add(FieldLabel("Mot de passe"));
        pass.Margin = new Padding(0, 0, 0, 4);
        body.Controls.Add(pass);
        body.Controls.Add(_remember);

        login.Margin = new Padding(0, 4, 0, 8);
        body.Controls.Add(login);

        var secondary = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4),
        };
        register.Margin = new Padding(0, 0, 8, 4);
        reconnect.Margin = new Padding(0, 0, 0, 4);
        secondary.Controls.Add(register);
        secondary.Controls.Add(reconnect);
        body.Controls.Add(secondary);

        body.Controls.Add(FieldLabel("Serveur"));
        servers.Width = FieldWidth;
        servers.DropDownStyle = ComboBoxStyle.DropDownList;
        servers.Margin = new Padding(0, 0, 0, 8);
        body.Controls.Add(servers);

        var serverRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };
        connect.Margin = new Padding(0, 0, 8, 4);
        disconnect.Margin = new Padding(0, 0, 8, 4);
        retry.Margin = new Padding(0, 0, 0, 4);
        serverRow.Controls.Add(connect);
        serverRow.Controls.Add(disconnect);
        serverRow.Controls.Add(retry);
        body.Controls.Add(serverRow);

        authStatus.ForeColor = UiTheme.TextSecondary;
        authStatus.BackColor = Color.Transparent;
        authStatus.Margin = new Padding(0, 8, 0, 0);
        authStatus.MaximumSize = new Size(FieldWidth, 0);
        body.Controls.Add(authStatus);
        body.Controls.Add(_connectDiag);
        body.Controls.Add(_networkHint);

        host.Width = 160;
        host.Margin = new Padding(0, 0, 8, 4);
        port.Width = 70;
        port.Margin = new Padding(0, 0, 0, 4);
        var opsFields = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        serverName.Width = 120;
        serverName.Margin = new Padding(0, 0, 8, 4);
        addServer.Margin = new Padding(0, 0, 0, 4);
        opsFields.Controls.Add(FieldLabel("Hôte", muted: true));
        opsFields.Controls.Add(host);
        opsFields.Controls.Add(FieldLabel("Port", muted: true));
        opsFields.Controls.Add(port);
        opsFields.Controls.Add(FieldLabel("Nom", muted: true));
        opsFields.Controls.Add(serverName);
        opsFields.Controls.Add(addServer);
        _opsStrip.Controls.Add(_opsHint);
        _opsStrip.Controls.Add(opsFields);
        body.Controls.Add(_opsStrip);

        _card.Controls.Clear();
        _card.Controls.Add(body);
        _opsVisible = false;
        _opsStrip.Visible = false;
        FitCardToBody();
    }

    /// <summary>Bandeau non modal : protocole, adresse, cause courte. Le statut joueur garde la phrase complète.</summary>
    public void SetConnectDiagnostic(string text, bool failure)
    {
        var color = failure ? UiTheme.TextDanger : UiTheme.TextMuted;
        if (string.Equals(_connectDiag.Text, text, StringComparison.Ordinal)
            && _connectDiag.ForeColor.ToArgb() == color.ToArgb())
        {
            return;
        }

        _connectDiag.Text = text;
        _connectDiag.ForeColor = color;
        FitCardToBody();
    }

    internal string ConnectDiagnosticTextForTest => _connectDiag.Text;

    public void ToggleOps() => SetOpsVisible(!_opsVisible);

    public void SetOpsVisible(bool visible)
    {
        _opsVisible = visible;
        _opsStrip.Visible = visible;
        FitCardToBody();
        Invalidate(true);
    }

    /// <summary>
    /// Échelle DIP de la carte login. À 100 % ne relit pas la mise en page déjà construite.
    /// </summary>
    public void ApplyUiScale(int percent)
    {
        var clamped = ClientUiScale.ClampPercent(percent);
        if (_body is null)
        {
            _appliedUiScalePercent = clamped;
            return;
        }

        if (clamped == _appliedUiScalePercent)
        {
            return;
        }

        _appliedUiScalePercent = clamped;
        var cardW = ClientUiScale.ScaleDip(CardWidth, clamped);
        var pad = ClientUiScale.ScaleDip(CardPadding, clamped);
        var fieldW = ClientUiScale.ScaleDip(FieldWidth, clamped);
        var emblem = ClientUiScale.ScaleDip(LogoEmblemSize, clamped);
        var inner = Math.Max(1, cardW - (pad * 2));

        _body.Padding = new Padding(pad);
        _body.Width = cardW;
        _card.Width = cardW;
        _emblem.MinimumSize = new Size(emblem, emblem);
        _emblem.MaximumSize = new Size(emblem, emblem);
        _emblem.Size = new Size(emblem, emblem);

        if (_body.Controls.Count > 0 && _body.Controls[0] is FlowLayoutPanel logoRow)
        {
            logoRow.Margin = new Padding(Math.Max(0, (inner - emblem) / 2), ClientUiScale.ScaleDip(4, clamped), 0, 0);
        }

        SizeField(_accountUser, fieldW, 24, clamped);
        SizeField(_accountPass, fieldW, 24, clamped);
        SizeButton(_loginButton, fieldW, 34, clamped);
        SizeButton(_registerButton, 120, 30, clamped);
        SizeButton(_reconnectButton, 160, 30, clamped);
        SizeButton(_connectButton, 120, 30, clamped);
        SizeButton(_disconnectButton, 120, 30, clamped);
        SizeButton(_retryButton, 120, 30, clamped);
        SizeButton(_addServerButton, 88, 30, clamped);
        if (_serverList is not null)
        {
            _serverList.Width = fieldW;
        }

        if (_hostBox is not null)
        {
            _hostBox.Width = ClientUiScale.ScaleDip(160, clamped);
        }

        if (_portBox is not null)
        {
            _portBox.Width = ClientUiScale.ScaleDip(70, clamped);
        }

        if (_serverName is not null)
        {
            _serverName.Width = ClientUiScale.ScaleDip(120, clamped);
        }

        _connectDiag.MaximumSize = new Size(fieldW, 0);

        RecenterLabel(_logoWordmark, inner);
        RecenterLabel(_logoSub, inner);
        FitCardToBody();
    }

    private static void SizeField(TextBox? box, int width, int designHeight, int percent)
    {
        if (box is null)
        {
            return;
        }

        box.Width = width;
        box.MinimumSize = new Size(width, ClientUiScale.ScaleDip(designHeight, percent));
    }

    private static void SizeButton(Button? button, int designWidth, int designHeight, int percent)
    {
        if (button is null)
        {
            return;
        }

        button.MinimumSize = new Size(
            ClientUiScale.ScaleDip(designWidth, percent),
            ClientUiScale.ScaleDip(designHeight, percent));
    }

    private static void RecenterLabel(Label label, int innerWidth)
    {
        var textW = TextRenderer.MeasureText(label.Text, label.Font).Width;
        label.Margin = new Padding(Math.Max(0, (innerWidth - textW) / 2), label.Margin.Top, 0, label.Margin.Bottom);
    }

    public void ApplyTheme()
    {
        BackColor = UiTheme.BgApp;
        ForeColor = UiTheme.TextPrimary;
        _card.ApplyTheme();
        _emblem.Invalidate();
        _logoWordmark.ForeColor = UiTheme.TextPrimary;
        _logoWordmark.BackColor = Color.Transparent;
        _logoSub.ForeColor = UiTheme.TextSecondary;
        _logoSub.BackColor = Color.Transparent;
        _remember.ForeColor = UiTheme.TextPrimary;
        _remember.BackColor = Color.Transparent;
        _networkHint.ForeColor = UiTheme.TextMuted;
        if (_connectDiag.ForeColor != UiTheme.TextDanger)
        {
            _connectDiag.ForeColor = UiTheme.TextMuted;
        }

        _connectDiag.BackColor = Color.Transparent;
        _opsHint.ForeColor = UiTheme.TextSecondary;
        _opsStrip.BackColor = Color.Transparent;
    }

    internal bool OpsVisibleForTest => _opsVisible && _opsStrip.Visible;

    private void FitCardToBody()
    {
        if (_body is null)
        {
            return;
        }

        var cardW = ClientUiScale.ScaleDip(CardWidth, _appliedUiScalePercent);
        _body.Width = cardW;
        _body.PerformLayout();
        _body.Width = cardW;
        _card.Width = cardW;
        var minH = ClientUiScale.ScaleDip(420, _appliedUiScalePercent);
        _card.Height = Math.Max(minH, _body.PreferredSize.Height + 8);
        CenterCard();
    }

    internal CheckBox RememberCheckBoxForTest => _remember;

    internal LoginCard CardForTest => _card;

    internal Label LogoWordmarkForTest => _logoWordmark;

    internal bool HasGoldRingEmblemForTest => _emblem.UsesGoldRing;

    internal int CardWidthForTest => _card.Width;

    internal int CardPaddingForTest => CardPadding;

    internal static void StylePrimaryCta(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        UiTheme.StyleContrastHudButton(button, button.Enabled);
        UiScaleApplicator.ApplyDesignFont(button, 10f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    internal static void StyleSecondaryCta(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        UiTheme.StyleContrastHudButton(button, button.Enabled);
        UiScaleApplicator.ApplyDesignFont(button, 8.5f, FontStyle.Regular);
        button.Cursor = Cursors.Hand;
    }

    internal static void StyleAccountField(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        box.Width = FieldWidth;
        box.MinimumSize = new Size(FieldWidth, 24);
        UiTheme.StyleInput(box);
    }

    /// <summary>
    /// Page 2 (sélection perso) : même carte DA, pas un second thème.
    /// Rangées contraintes à la largeur interne pour que <see cref="FlowLayoutPanel.WrapContents"/>
    /// wrap réellement (sinon AutoSize élargit la rangée et les CTA
    /// sortent du clip 520 px). <see cref="StackRow"/> reste en colonne.
    /// <paramref name="fitInnerWidth"/> aligne champs et CTA sur cette largeur.
    /// AutoScroll vertical si la carte dépasse la hauteur utile.
    /// </summary>
    public static void HostCenteredCard(Panel page, Action<int>? fitInnerWidth, params Control[] sections)
    {
        ArgumentNullException.ThrowIfNull(page);
        var state = new CardScaleState();
        CharacterPageScales.Add(page, state);
        var cardWidth = ClientUiScale.ScaleDip(CharacterCardWidth, state.Percent);
        var pad = ClientUiScale.ScaleDip(CardPadding, state.Percent);
        var innerWidth = cardWidth - (pad * 2);

        var card = new LoginCard { Width = cardWidth, AutoScroll = true };
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(pad),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            Width = cardWidth,
            MaximumSize = new Size(cardWidth, 0),
        };
        fitInnerWidth?.Invoke(innerWidth);
        foreach (var section in sections)
        {
            ArgumentNullException.ThrowIfNull(section);
            ConstrainCardSection(section, innerWidth);
            body.Controls.Add(section);
        }

        card.Controls.Add(body);

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.BgApp,
            AutoScroll = true,
        };
        host.Controls.Add(card);

        var layingOut = false;
        void LayoutCard()
        {
            if (layingOut || host.ClientSize.Width <= 0 || host.ClientSize.Height <= 0)
            {
                return;
            }

            layingOut = true;
            try
            {
                var scaledCard = ClientUiScale.ScaleDip(CharacterCardWidth, state.Percent);
                var scaledPad = ClientUiScale.ScaleDip(CardPadding, state.Percent);
                var scaledInner = Math.Max(1, scaledCard - (scaledPad * 2));
                body.Padding = new Padding(scaledPad);
                body.Width = scaledCard;
                body.MaximumSize = new Size(scaledCard, 0);
                fitInnerWidth?.Invoke(scaledInner);
                foreach (Control section in body.Controls)
                {
                    ConstrainCardSection(section, scaledInner);
                }

                body.PerformLayout();
                cardWidth = scaledCard;

                var contentH = Math.Max(8, body.PreferredSize.Height + 8);
                var availH = Math.Max(80, host.ClientSize.Height - 16);
                var needsScroll = contentH > availH;
                var scrollPad = needsScroll ? SystemInformation.VerticalScrollBarWidth : 0;

                card.AutoScroll = needsScroll;
                card.Width = cardWidth + scrollPad;
                card.Height = needsScroll ? availH : contentH;
                card.AutoScrollMinSize = needsScroll ? new Size(0, contentH) : Size.Empty;
                card.Location = new Point(
                    Math.Max(8, (host.ClientSize.Width - card.Width) / 2),
                    needsScroll ? 8 : Math.Max(12, (host.ClientSize.Height - card.Height) / 3));
            }
            finally
            {
                layingOut = false;
            }
        }

        host.Resize += (_, _) => LayoutCard();
        page.VisibleChanged += (_, _) =>
        {
            if (page.Visible)
            {
                LayoutCard();
            }
        };
        page.Controls.Clear();
        page.BackColor = UiTheme.BgApp;
        page.Controls.Add(host);
        state.Relayout = LayoutCard;
        LayoutCard();
    }

    private static void ConstrainCardSection(Control section, int innerWidth)
    {
        section.Margin = new Padding(0, 0, 0, 6);
        section.Dock = DockStyle.None;
        if (section is not FlowLayoutPanel row)
        {
            return;
        }

        var stack = row is StackRow;
        row.WrapContents = !stack;
        row.AutoSize = true;
        row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        row.Width = innerWidth;
        row.MaximumSize = new Size(innerWidth, 0);
        if (!stack)
        {
            row.FlowDirection = FlowDirection.LeftToRight;
        }
    }

    /// <summary>Colonne de la carte perso (titre, nom, classe). Ne wrap pas en rangées horizontales.</summary>
    internal sealed class StackRow : FlowLayoutPanel
    {
        public StackRow()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = Color.Transparent;
        }
    }

    /// <summary>Carte « choisir un personnage ». 100 % laisse la largeur 520 déjà posée.</summary>
    public static void ApplyCharacterPageScale(Panel page, int percent)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!CharacterPageScales.TryGetValue(page, out var state))
        {
            return;
        }

        var clamped = ClientUiScale.ClampPercent(percent);
        if (clamped == state.Percent)
        {
            return;
        }

        state.Percent = clamped;
        state.Relayout?.Invoke();
    }

    private static readonly ConditionalWeakTable<Panel, CardScaleState> CharacterPageScales = new();

    private sealed class CardScaleState
    {
        public int Percent = ClientUiScale.DefaultPercent;

        public Action? Relayout;
    }

    private bool _centeringCard;

    private void CenterCard()
    {
        if (_centeringCard || _card.Width <= 0 || Width <= 0)
        {
            return;
        }

        _centeringCard = true;
        try
        {
            _card.Location = new Point(
                Math.Max(12, (Width - _card.Width) / 2),
                Math.Max(20, (Height - _card.Height) / 3));
            var fits = ClientSize.Width <= 0
                || ClientSize.Height <= 0
                || (_card.Width + 24 <= ClientSize.Width && _card.Height + 40 <= ClientSize.Height);
            AutoScroll = !fits;
            AutoScrollMinSize = fits ? Size.Empty : new Size(_card.Width + 24, _card.Height + 40);
        }
        finally
        {
            _centeringCard = false;
        }
    }

    private static void CenterLabel(FlowLayoutPanel body, Label label)
    {
        label.Margin = new Padding(Math.Max(0, (CardWidth - CardPadding * 2 - TextRenderer.MeasureText(label.Text, label.Font).Width) / 2), 0, 0, 0);
        body.Controls.Add(label);
    }

    private static Label FieldLabel(string text, bool muted = false) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = UiTheme.UiFont(8.5f),
            ForeColor = muted ? UiTheme.TextMuted : UiTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 8, 2),
        };

    /// <summary>Carte login / perso : <c>bg.panel</c> + double filet or (jetons E0).</summary>
    public sealed class LoginCard : Panel
    {
        public LoginCard()
        {
            SetStyle(
                ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer,
                true);
            Width = CardWidth;
            BackColor = UiTheme.BgPanel;
            ForeColor = UiTheme.TextPrimary;
            Padding = new Padding(1);
            Paint += DrawCard;
        }

        public void ApplyTheme()
        {
            BackColor = UiTheme.BgPanel;
            ForeColor = UiTheme.TextPrimary;
            Invalidate();
        }

        internal Color BackColorForTest => BackColor;

        private void DrawCard(object? sender, PaintEventArgs e)
        {
            UiTheme.PaintDoubleGoldFrame(this, e);
        }
    }

    /// <summary>Emblème : disque <c>bg.slot</c> + filet or + initiale crème (pas d’art commercial).</summary>
    internal sealed class LogoEmblem : Control
    {
        public LogoEmblem()
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor,
                true);
            Size = new Size(LogoEmblemSize, LogoEmblemSize);
            MinimumSize = Size;
            MaximumSize = Size;
            Font = UiTheme.UiFont(16f, FontStyle.Bold);
            BackColor = Color.Transparent;
        }

        internal bool UsesGoldRing => true;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var circle = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var fill = new SolidBrush(UiTheme.BgSlot))
            {
                g.FillEllipse(fill, circle);
            }

            using (var ring = new Pen(UiTheme.AccentGold, 1f))
            {
                g.DrawEllipse(ring, circle);
            }

            TextRenderer.DrawText(
                g,
                "F",
                Font,
                ClientRectangle,
                UiTheme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
