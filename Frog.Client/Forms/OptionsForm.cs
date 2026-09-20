using System.Drawing;
using System.Windows.Forms;
using Frog.Client.Config;
using Frog.Client.UI;

namespace Frog.Client.Forms;

/// <summary>Fenêtre, volume / mute / musique, disposition clavier et rebind — enregistrement atomique JSON côté appelant.</summary>
public sealed class OptionsForm : Form
{
    private readonly ComboBox _cmbLayout = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly TrackBar _volume = new()
    {
        Minimum = 0,
        Maximum = 100,
        TickFrequency = 10,
        Width = 240,
        SmallChange = 1,
        LargeChange = 10,
    };
    private readonly Label _lblVolume = new() { AutoSize = true };
    private readonly CheckBox _chkMute = new() { Text = "Muet (SFX + musique)", AutoSize = true };
    private readonly CheckBox _chkMusic = new() { Text = "Musique (boucle placeholder)", AutoSize = true };
    private readonly NumericUpDown _numWidth = new() { Minimum = 980, Maximum = 7680, Width = 80 };
    private readonly NumericUpDown _numHeight = new() { Minimum = 640, Maximum = 4320, Width = 80 };
    private readonly CheckBox _chkMaximized = new() { Text = "Fenêtre maximisée", AutoSize = true };
    private readonly CheckBox _chkFullScreen = new() { Text = "Plein écran", AutoSize = true };
    private readonly Button _btnUp = new() { AutoSize = true, MinimumSize = new Size(88, 28) };
    private readonly Button _btnDown = new() { AutoSize = true, MinimumSize = new Size(88, 28) };
    private readonly Button _btnLeft = new() { AutoSize = true, MinimumSize = new Size(88, 28) };
    private readonly Button _btnRight = new() { AutoSize = true, MinimumSize = new Size(88, 28) };
    private readonly Button _btnInteract = new() { AutoSize = true, MinimumSize = new Size(88, 28) };
    private readonly Label _lblCapture = new() { AutoSize = true, Text = "Cliquez une action puis appuyez sur une touche." };
    private readonly TextBox _txtHost = new() { Width = 160 };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private ListBox? _nav;

    private UserSettings _draft;
    private string? _capturing;

    public OptionsForm(UserSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);
        _draft = current.Clone();
        Text = "Options — FRoG";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(620, 540);

        _cmbLayout.Items.AddRange(new object[] { "AZERTY (ZQSD)", "QWERTY (WASD)" });
        _cmbLayout.SelectedIndex = _draft.KeyboardPreset == KeyboardLayoutPreset.Qwerty ? 1 : 0;
        _volume.Value = _draft.VolumePercent;
        _chkMute.Checked = _draft.AudioMuted;
        _chkMusic.Checked = _draft.MusicEnabled;
        _numWidth.Value = _draft.Window.Width;
        _numHeight.Value = _draft.Window.Height;
        _chkMaximized.Checked = _draft.Window.Maximized;
        _chkFullScreen.Checked = _draft.Window.FullScreen;
        _txtHost.Text = _draft.LastHost;
        _numPort.Value = _draft.LastPort;
        RefreshVolumeLabel();
        RefreshBindButtons();

        var nav = new ListBox
        {
            Dock = DockStyle.Left,
            Width = 140,
            IntegralHeight = false,
        };
        nav.Items.AddRange(new object[] { "Graphisme", "Son", "Contrôles", "Interface", "Réseau" });

        var graphics = Page(
            Heading("Graphisme"),
            Row(Lbl("Largeur"), _numWidth, Lbl("Hauteur"), _numHeight),
            _chkMaximized,
            _chkFullScreen);
        var sound = Page(
            Heading("Son"),
            _volume,
            _lblVolume,
            _chkMute,
            _chkMusic,
            Note("Clic UI : ui-click.wav. Musique : music-loop.wav (générés CC0, dans le dépôt)."));
        var controls = Page(
            Heading("Contrôles"),
            Row(Lbl("Disposition"), _cmbLayout),
            Row(Lbl("Haut"), _btnUp, Lbl("Bas"), _btnDown),
            Row(Lbl("Gauche"), _btnLeft, Lbl("Droite"), _btnRight),
            Row(Lbl("Interagir"), _btnInteract),
            _lblCapture);
        var ui = Page(
            Heading("Interface"),
            Note("DPI Windows 100 / 125 / 150 % : layout DIP (AutoScaleMode.Font)."),
            Note("Hitboxes et cadres restent en pixels logiques — pas de chiffre FPS inventé."),
            Note("Le HUD n’est pas repeint par le timer mouvement 16 ms."));
        var network = Page(
            Heading("Réseau"),
            Row(Lbl("Hôte"), _txtHost, Lbl("Port"), _numPort),
            Note("TLS : géré par le serveur — pas de handshake ajouté ici."));
        var pages = new[] { graphics, sound, controls, ui, network };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        foreach (var page in pages)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            content.Controls.Add(page);
        }

        void ShowPage(int index)
        {
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == index;
            }
        }

        nav.SelectedIndexChanged += (_, _) => ShowPage(Math.Max(0, nav.SelectedIndex));
        nav.SelectedIndex = 0;
        ShowPage(0);

        var save = new Button { Text = "Enregistrer", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        AcceptButton = save;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(content);
        Controls.Add(nav);
        Controls.Add(buttons);
        UiTheme.Apply(this);
        _nav = nav;

        _cmbLayout.SelectedIndexChanged += (_, _) =>
        {
            _draft.ApplyPreset(_cmbLayout.SelectedIndex == 1 ? KeyboardLayoutPreset.Qwerty : KeyboardLayoutPreset.Azerty);
            _capturing = null;
            RefreshBindButtons();
        };
        _volume.ValueChanged += (_, _) =>
        {
            _draft.VolumePercent = _volume.Value;
            RefreshVolumeLabel();
        };
        _chkMute.CheckedChanged += (_, _) => _draft.AudioMuted = _chkMute.Checked;
        _chkMusic.CheckedChanged += (_, _) => _draft.MusicEnabled = _chkMusic.Checked;
        _numWidth.ValueChanged += (_, _) => _draft.Window.Width = (int)_numWidth.Value;
        _numHeight.ValueChanged += (_, _) => _draft.Window.Height = (int)_numHeight.Value;
        _chkMaximized.CheckedChanged += (_, _) => _draft.Window.Maximized = _chkMaximized.Checked;
        _chkFullScreen.CheckedChanged += (_, _) => _draft.Window.FullScreen = _chkFullScreen.Checked;
        _txtHost.TextChanged += (_, _) => _draft.LastHost = _txtHost.Text.Trim();
        _numPort.ValueChanged += (_, _) => _draft.LastPort = (int)_numPort.Value;

        _btnUp.Click += (_, _) => BeginCapture("MoveUp");
        _btnDown.Click += (_, _) => BeginCapture("MoveDown");
        _btnLeft.Click += (_, _) => BeginCapture("MoveLeft");
        _btnRight.Click += (_, _) => BeginCapture("MoveRight");
        _btnInteract.Click += (_, _) => BeginCapture("Interact");

        KeyDown += OptionsForm_KeyDown;
        save.Click += (_, _) => CommitSave();
    }

    public UserSettings Settings { get; private set; } = new();

    internal ComboBox LayoutComboForTest => _cmbLayout;

    internal TrackBar VolumeTrackForTest => _volume;

    internal CheckBox MuteCheckBoxForTest => _chkMute;

    internal CheckBox MusicCheckBoxForTest => _chkMusic;

    internal Button SaveButtonForTest => AcceptButton as Button ?? throw new InvalidOperationException("Save");

    internal Button MoveUpButtonForTest => _btnUp;

    internal TextBox HostTextBoxForTest => _txtHost;

    internal NumericUpDown PortNumericForTest => _numPort;

    internal ListBox SectionNavForTest => _nav ?? throw new InvalidOperationException("nav");

    internal void CommitSave()
    {
        _draft.Normalize();
        Settings = _draft.Clone();
        DialogResult = DialogResult.OK;
    }

    private void RefreshVolumeLabel() => _lblVolume.Text = $"Volume : {_draft.VolumePercent} %";

    private void RefreshBindButtons()
    {
        _btnUp.Text = _draft.Bindings.MoveUp;
        _btnDown.Text = _draft.Bindings.MoveDown;
        _btnLeft.Text = _draft.Bindings.MoveLeft;
        _btnRight.Text = _draft.Bindings.MoveRight;
        _btnInteract.Text = _draft.Bindings.Interact;
    }

    private void BeginCapture(string action)
    {
        _capturing = action;
        _lblCapture.Text = "Appuyez sur une touche pour « " + ActionLabel(action) + " »…";
    }

    private void OptionsForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturing is null)
        {
            return;
        }

        if (e.KeyCode is Keys.Escape or Keys.F1 or Keys.Tab or Keys.Enter)
        {
            _capturing = null;
            _lblCapture.Text = "Rebind annulé.";
            e.Handled = true;
            return;
        }

        var name = e.KeyCode.ToString();
        switch (_capturing)
        {
            case "MoveUp":
                _draft.Bindings.MoveUp = name;
                break;
            case "MoveDown":
                _draft.Bindings.MoveDown = name;
                break;
            case "MoveLeft":
                _draft.Bindings.MoveLeft = name;
                break;
            case "MoveRight":
                _draft.Bindings.MoveRight = name;
                break;
            case "Interact":
                _draft.Bindings.Interact = name;
                break;
        }

        _capturing = null;
        _lblCapture.Text = "Touche enregistrée (pas encore sauvegardée).";
        RefreshBindButtons();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private static string ActionLabel(string action) => action switch
    {
        "MoveUp" => "haut",
        "MoveDown" => "bas",
        "MoveLeft" => "gauche",
        "MoveRight" => "droite",
        "Interact" => "interagir",
        _ => action,
    };

    private static FlowLayoutPanel Page(params Control[] controls)
    {
        var page = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
        };
        foreach (var c in controls)
        {
            page.Controls.Add(c);
        }

        return page;
    }

    private static Label Note(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(420, 0),
        ForeColor = Color.FromArgb(0xA8, 0xB0, 0xC0),
        Margin = new Padding(0, 4, 0, 4),
    };

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, 11f, FontStyle.Bold),
        Margin = new Padding(0, 12, 0, 6),
    };

    private static Label Lbl(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 8, 8, 4),
    };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        foreach (var c in controls)
        {
            row.Controls.Add(c);
        }

        return row;
    }
}
