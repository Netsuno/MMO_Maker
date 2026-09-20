#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>
/// DA v2 step 5 — écran login immersif (pré-jeu). Carte centrée <c>bg.panel</c>,
/// filet or seulement ; champs compte/mdp crème ; hôte/port hors carte joueur (F9).
/// </summary>
public sealed class LoginShell : Panel
{
    public const int CardWidth = 400;
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
            Text = "Serveur : Options → Réseau  ·  F9 = hôte / port",
            AutoSize = true,
            Font = UiTheme.UiFont(8f),
            ForeColor = UiTheme.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0),
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
        Label authStatus)
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

        login.Text = "Connexion";
        login.MinimumSize = new Size(FieldWidth, 34);
        register.MinimumSize = new Size(120, 30);
        reconnect.MinimumSize = new Size(160, 30);
        connect.MinimumSize = new Size(120, 30);
        disconnect.MinimumSize = new Size(120, 30);

        StyleAccountField(user);
        StyleAccountField(pass);
        StylePrimaryCta(login);
        StyleSecondaryCta(register);
        StyleSecondaryCta(reconnect);
        StyleSecondaryCta(connect);
        StyleSecondaryCta(disconnect);

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

        var serverRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };
        connect.Margin = new Padding(0, 0, 8, 4);
        disconnect.Margin = new Padding(0, 0, 0, 4);
        serverRow.Controls.Add(connect);
        serverRow.Controls.Add(disconnect);
        body.Controls.Add(serverRow);

        authStatus.ForeColor = UiTheme.TextSecondary;
        authStatus.BackColor = Color.Transparent;
        authStatus.Margin = new Padding(0, 8, 0, 0);
        body.Controls.Add(authStatus);
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
        opsFields.Controls.Add(FieldLabel("Hôte", muted: true));
        opsFields.Controls.Add(host);
        opsFields.Controls.Add(FieldLabel("Port", muted: true));
        opsFields.Controls.Add(port);
        _opsStrip.Controls.Add(_opsHint);
        _opsStrip.Controls.Add(opsFields);
        body.Controls.Add(_opsStrip);

        _card.Controls.Clear();
        _card.Controls.Add(body);
        _card.Size = new Size(CardWidth, Math.Max(420, body.PreferredSize.Height + 8));
        _opsVisible = false;
        _opsStrip.Visible = false;
        CenterCard();
    }

    public void ToggleOps() => SetOpsVisible(!_opsVisible);

    public void SetOpsVisible(bool visible)
    {
        _opsVisible = visible;
        _opsStrip.Visible = visible;
        _card.PerformLayout();
        CenterCard();
        Invalidate(true);
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
        _opsHint.ForeColor = UiTheme.TextSecondary;
        _opsStrip.BackColor = Color.Transparent;
    }

    internal bool OpsVisibleForTest => _opsVisible && _opsStrip.Visible;

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
        button.Font = UiTheme.UiFont(10f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    internal static void StyleSecondaryCta(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        UiTheme.StyleContrastHudButton(button, button.Enabled);
        button.Font = UiTheme.UiFont(8.5f);
        button.Cursor = Cursors.Hand;
    }

    internal static void StyleAccountField(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        box.Width = FieldWidth;
        box.MinimumSize = new Size(FieldWidth, 24);
        UiTheme.StyleInput(box);
    }

    /// <summary>Page 2 (sélection perso) : même carte DA, pas un second thème.</summary>
    public static void HostCenteredCard(Panel page, params Control[] sections)
    {
        ArgumentNullException.ThrowIfNull(page);
        var card = new LoginCard { Width = 520 };
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(CardPadding),
            BackColor = Color.Transparent,
            Width = 520,
        };
        foreach (var section in sections)
        {
            section.Margin = new Padding(0, 0, 0, 10);
            body.Controls.Add(section);
        }

        card.Controls.Add(body);
        card.Height = Math.Max(280, body.PreferredSize.Height + 16);

        var host = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.BgApp };
        host.Controls.Add(card);
        void Center()
        {
            card.Location = new Point(
                Math.Max(12, (host.Width - card.Width) / 2),
                Math.Max(24, (host.Height - card.Height) / 3));
        }

        host.Resize += (_, _) => Center();
        page.Controls.Clear();
        page.BackColor = UiTheme.BgApp;
        page.Controls.Add(host);
        Center();
    }

    private void CenterCard()
    {
        if (_card.Width <= 0 || Width <= 0)
        {
            return;
        }

        _card.Location = new Point(
            Math.Max(12, (Width - _card.Width) / 2),
            Math.Max(20, (Height - _card.Height) / 3));
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
                UiTheme.UiFont(16f, FontStyle.Bold),
                ClientRectangle,
                UiTheme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
