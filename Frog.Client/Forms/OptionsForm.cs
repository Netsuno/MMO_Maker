using System.Windows.Forms;
using Frog.Client.Config;
using Frog.Client.UI;

namespace Frog.Client.Forms;

/// <summary>Fenêtre, volume, disposition clavier et rebind — enregistrement atomique JSON côté appelant.</summary>
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
        ClientSize = new Size(500, 520);

        _cmbLayout.Items.AddRange(new object[] { "AZERTY (ZQSD)", "QWERTY (WASD)" });
        _cmbLayout.SelectedIndex = _draft.KeyboardPreset == KeyboardLayoutPreset.Qwerty ? 1 : 0;
        _volume.Value = _draft.VolumePercent;
        _numWidth.Value = _draft.Window.Width;
        _numHeight.Value = _draft.Window.Height;
        _chkMaximized.Checked = _draft.Window.Maximized;
        _chkFullScreen.Checked = _draft.Window.FullScreen;
        _txtHost.Text = _draft.LastHost;
        _numPort.Value = _draft.LastPort;
        RefreshVolumeLabel();
        RefreshBindButtons();

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            AutoScroll = true,
        };

        root.Controls.Add(Heading("Affichage"));
        root.Controls.Add(Row(Lbl("Largeur"), _numWidth, Lbl("Hauteur"), _numHeight));
        root.Controls.Add(_chkMaximized);
        root.Controls.Add(_chkFullScreen);

        root.Controls.Add(Heading("Réseau"));
        root.Controls.Add(Row(Lbl("Hôte"), _txtHost, Lbl("Port"), _numPort));

        root.Controls.Add(Heading("Volume"));
        root.Controls.Add(_volume);
        root.Controls.Add(_lblVolume);

        root.Controls.Add(Heading("Clavier"));
        root.Controls.Add(Row(Lbl("Disposition"), _cmbLayout));
        root.Controls.Add(Row(Lbl("Haut"), _btnUp, Lbl("Bas"), _btnDown));
        root.Controls.Add(Row(Lbl("Gauche"), _btnLeft, Lbl("Droite"), _btnRight));
        root.Controls.Add(Row(Lbl("Interagir"), _btnInteract));
        root.Controls.Add(_lblCapture);

        var save = new Button { Text = "Enregistrer", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        AcceptButton = save;
        CancelButton = cancel;
        root.Controls.Add(Row(save, cancel));

        Controls.Add(root);
        UiTheme.Apply(this);

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

    internal Button SaveButtonForTest => AcceptButton as Button ?? throw new InvalidOperationException("Save");

    internal Button MoveUpButtonForTest => _btnUp;

    internal TextBox HostTextBoxForTest => _txtHost;

    internal NumericUpDown PortNumericForTest => _numPort;

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
