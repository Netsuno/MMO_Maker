using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Dialogs;

/// <summary>
/// Propriétés de la carte : nom, taille, chevauchement, musique (BGM) et ambiance (SE).
/// L’identité graphique est affichée, pas éditée. Le départ playtest est une mémo locale.
/// </summary>
internal sealed class MapPropertiesDialog : Form
{
    private readonly TextBox _txtName;
    private readonly NumericUpDown _numW;
    private readonly NumericUpDown _numH;
    private readonly CheckBox _overlap;
    private readonly Label _identity;
    private readonly Label _spawn;
    private readonly TextBox _bgmPath;
    private readonly NumericUpDown _bgmVolume;
    private readonly NumericUpDown _bgmFade;
    private readonly Button _bgmBrowse;
    private readonly Button _bgmClear;
    private readonly TextBox _sePath;
    private readonly NumericUpDown _seVolume;
    private readonly NumericUpDown _seFade;
    private readonly Button _seBrowse;
    private readonly Button _seClear;

    public MapPropertiesDialog(Map map, Point? playtestSpawn)
    {
        ArgumentNullException.ThrowIfNull(map);
        Text = "Propriétés de la carte";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 640);
        ClientSize = new Size(620, 600);
        AutoScaleMode = AutoScaleMode.Dpi;
        EditorChrome.ApplyFormChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(20, 16, 20, 12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

        _txtName = new TextBox
        {
            Text = map.Name,
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 6, 0, 6),
        };
        _numW = new NumericUpDown
        {
            Minimum = MapEditOperations.MinDimensionTiles,
            Maximum = MapEditOperations.MaxDimensionTiles,
            Value = Math.Clamp(map.Width, MapEditOperations.MinDimensionTiles, MapEditOperations.MaxDimensionTiles),
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 6, 0, 6),
            TextAlign = HorizontalAlignment.Right,
        };
        _numH = new NumericUpDown
        {
            Minimum = MapEditOperations.MinDimensionTiles,
            Maximum = MapEditOperations.MaxDimensionTiles,
            Value = Math.Clamp(map.Height, MapEditOperations.MinDimensionTiles, MapEditOperations.MaxDimensionTiles),
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 6, 0, 6),
            TextAlign = HorizontalAlignment.Right,
        };
        _overlap = new CheckBox
        {
            Text = "Autoriser plusieurs joueurs sur la même tuile",
            Checked = map.AllowPlayerOverlap,
            AutoSize = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(10, 10, 0, 0),
        };
        _identity = new Label
        {
            Text = MapEditOperations.FormatGraphicIdentity(map),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Margin = new Padding(10, 4, 0, 0),
        };
        _spawn = new Label
        {
            Text = MapEditOperations.FormatSpawnMemo(playtestSpawn?.X, playtestSpawn?.Y)
                   + " — mémo locale (outil D), absente du fichier carte.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Margin = new Padding(10, 0, 0, 0),
        };

        var bgm = BuildTrackEditor(map.Bgm, out _bgmPath, out _bgmVolume, out _bgmFade, out _bgmBrowse, out _bgmClear);
        var se = BuildTrackEditor(map.Se, out _sePath, out _seVolume, out _seFade, out _seBrowse, out _seClear);
        _bgmBrowse.Click += (_, _) => BrowseBgm();
        _bgmClear.Click += (_, _) => ClearBgm();
        _seBrowse.Click += (_, _) => BrowseSe();
        _seClear.Click += (_, _) => ClearSe();

        AddRow(root, 0, "Nom", _txtName);
        AddRow(root, 1, "Largeur (tuiles)", _numW);
        AddRow(root, 2, "Hauteur (tuiles)", _numH);
        var overlapLabel = MakeLabel("Chevauchement");
        root.Controls.Add(overlapLabel, 0, 3);
        root.Controls.Add(_overlap, 1, 3);
        var bgmLabel = MakeLabel("Musique (BGM)");
        root.Controls.Add(bgmLabel, 0, 4);
        root.Controls.Add(bgm, 1, 4);
        var seLabel = MakeLabel("Ambiance (SE)");
        root.Controls.Add(seLabel, 0, 5);
        root.Controls.Add(se, 1, 5);
        var identityLabel = MakeLabel("Identité");
        root.Controls.Add(identityLabel, 0, 6);
        root.Controls.Add(_identity, 1, 6);
        var spawnLabel = MakeLabel("Départ");
        root.Controls.Add(spawnLabel, 0, 7);
        root.Controls.Add(_spawn, 1, 7);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            Margin = new Padding(0),
        };
        var ok = new Button
        {
            Text = "Appliquer",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(108, 34),
            Margin = new Padding(10, 0, 0, 0),
        };
        var cancel = new Button
        {
            Text = "Annuler",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(108, 34),
        };
        EditorChrome.StyleDialogButton(ok, primary: true);
        EditorChrome.StyleDialogButton(cancel, primary: false);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.SetColumnSpan(buttons, 2);
        root.Controls.Add(buttons, 0, 8);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public MapPropertiesEdit PendingEdit => new()
    {
        Name = _txtName.Text,
        Width = (int)_numW.Value,
        Height = (int)_numH.Value,
        AllowPlayerOverlap = _overlap.Checked,
        Bgm = ReadTrack(_bgmPath, _bgmVolume, _bgmFade),
        Se = ReadTrack(_sePath, _seVolume, _seFade),
    };

    internal string NameTextForTest
    {
        get => _txtName.Text;
        set => _txtName.Text = value;
    }

    internal int WidthForTest
    {
        get => (int)_numW.Value;
        set => _numW.Value = value;
    }

    internal int HeightForTest
    {
        get => (int)_numH.Value;
        set => _numH.Value = value;
    }

    internal bool OverlapForTest
    {
        get => _overlap.Checked;
        set => _overlap.Checked = value;
    }

    internal string IdentityTextForTest => _identity.Text;

    internal string SpawnTextForTest => _spawn.Text;

    internal string BgmAssetForTest
    {
        get => _bgmPath.Text;
        set => _bgmPath.Text = value;
    }

    internal int BgmVolumeForTest
    {
        get => (int)_bgmVolume.Value;
        set => _bgmVolume.Value = value;
    }

    internal int BgmFadeForTest
    {
        get => (int)_bgmFade.Value;
        set => _bgmFade.Value = value;
    }

    internal string SeAssetForTest
    {
        get => _sePath.Text;
        set => _sePath.Text = value;
    }

    internal int SeVolumeForTest
    {
        get => (int)_seVolume.Value;
        set => _seVolume.Value = value;
    }

    internal int SeFadeForTest
    {
        get => (int)_seFade.Value;
        set => _seFade.Value = value;
    }

    // PerformClick est un no-op tant que le dialogue n’est pas visible (CanSelect).
    // Les smokes appellent donc le même chemin que Parcourir… / Effacer.
    internal void ClickBgmBrowseForTest() => BrowseBgm();

    internal void ClickSeBrowseForTest() => BrowseSe();

    internal void ClickBgmClearForTest() => ClearBgm();

    internal void ClickSeClearForTest() => ClearSe();

    private void BrowseBgm() => PickAudio("Choisir la musique (BGM)", _bgmPath);

    private void BrowseSe() => PickAudio("Choisir l’ambiance (SE)", _sePath);

    private void ClearBgm() => ClearTrack(_bgmPath, _bgmVolume, _bgmFade);

    private void ClearSe() => ClearTrack(_sePath, _seVolume, _seFade);

    internal string JoinedLabelsForTest
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            CollectText(this, parts);
            return string.Join('\n', parts);
        }
    }

    private void PickAudio(string title, TextBox target)
    {
        var picked = EditorTestHooks.OverrideMapAudioPickPath;
        if (string.IsNullOrWhiteSpace(picked))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Audio WAV (*.wav)|*.wav|Tous les fichiers (*.*)|*.*",
                Title = title,
                CheckFileExists = true,
                RestoreDirectory = true,
            };
            var initial = FindAudioFolder();
            if (initial is not null)
            {
                dialog.InitialDirectory = initial;
            }

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            picked = dialog.FileName;
        }

        if (!MapAudioTrack.TryFromPickedFile(picked, FindRepositoryRoot(), out var stored, out var error))
        {
            MessageBox.Show(this, error ?? "Fichier audio refusé.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        target.Text = stored;
    }

    private static void ClearTrack(TextBox path, NumericUpDown volume, NumericUpDown fade)
    {
        path.Text = string.Empty;
        volume.Value = MapAudioTrack.DefaultVolume;
        fade.Value = MapAudioTrack.MinFadeMs;
    }

    private static MapAudioTrack ReadTrack(TextBox path, NumericUpDown volume, NumericUpDown fade)
        => new()
        {
            Asset = path.Text,
            Volume = (int)volume.Value,
            FadeMs = (int)fade.Value,
        };

    private static Control BuildTrackEditor(
        MapAudioTrack track,
        out TextBox path,
        out NumericUpDown volume,
        out NumericUpDown fade,
        out Button browse,
        out Button clear)
    {
        path = new TextBox
        {
            Text = track.Asset ?? string.Empty,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 6, 2),
        };
        browse = new Button
        {
            Text = "Parcourir…",
            AutoSize = true,
            MinimumSize = new Size(108, 28),
            Margin = new Padding(0, 2, 6, 0),
        };
        clear = new Button
        {
            Text = "Effacer",
            AutoSize = true,
            MinimumSize = new Size(84, 28),
            Margin = new Padding(0, 2, 0, 0),
        };
        EditorChrome.StyleDialogButton(browse, primary: false);
        EditorChrome.StyleDialogButton(clear, primary: false);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        buttons.Controls.Add(browse);
        buttons.Controls.Add(clear);

        var fileRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
        };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fileRow.Controls.Add(path, 0, 0);
        fileRow.Controls.Add(buttons, 1, 0);

        volume = new NumericUpDown
        {
            Minimum = MapAudioTrack.MinVolume,
            Maximum = MapAudioTrack.MaxVolume,
            Value = Math.Clamp(track.Volume, MapAudioTrack.MinVolume, MapAudioTrack.MaxVolume),
            Width = 64,
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(4, 2, 12, 0),
        };
        fade = new NumericUpDown
        {
            Minimum = MapAudioTrack.MinFadeMs,
            Maximum = MapAudioTrack.MaxFadeMs,
            Value = Math.Clamp(track.FadeMs, MapAudioTrack.MinFadeMs, MapAudioTrack.MaxFadeMs),
            Width = 78,
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(4, 2, 0, 0),
        };
        var knobs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        knobs.Controls.Add(MakeInlineLabel("Volume"));
        knobs.Controls.Add(volume);
        knobs.Controls.Add(MakeInlineLabel("Fondu (ms)"));
        knobs.Controls.Add(fade);

        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(10, 2, 0, 0),
        };
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        host.Controls.Add(fileRow, 0, 0);
        host.Controls.Add(knobs, 0, 1);
        return host;
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static string? FindAudioFolder()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return null;
        }

        var folder = Path.Combine(root, "Frog.Client", "Assets", "Audio");
        return Directory.Exists(folder) ? folder : null;
    }

    private static void AddRow(TableLayoutPanel root, int row, string caption, Control editor)
    {
        root.Controls.Add(MakeLabel(caption), 0, row);
        root.Controls.Add(editor, 1, row);
    }

    private static Label MakeLabel(string caption) => new()
    {
        Text = caption,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = EditorChrome.LabelPrimary,
        AutoSize = false,
    };

    private static Label MakeInlineLabel(string caption) => new()
    {
        Text = caption,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = EditorChrome.LabelMuted,
        Margin = new Padding(0, 6, 0, 0),
    };

    private static void CollectText(Control parent, System.Collections.Generic.List<string> parts)
    {
        foreach (Control child in parent.Controls)
        {
            if (!string.IsNullOrWhiteSpace(child.Text))
            {
                parts.Add(child.Text);
            }

            if (child.HasChildren)
            {
                CollectText(child, parts);
            }
        }
    }
}
