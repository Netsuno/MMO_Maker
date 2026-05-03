using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Controls;
using Frog.Editor.Dialogs;

namespace Frog.Editor.Forms;

public sealed class MainForm : Form
{
    private readonly ToolStrip _tool;
    private readonly ToolStripButton _btnUndo;
    private readonly ToolStripButton _btnRedo;
    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _lblPos;
    private readonly SplitContainer _splitLeft;
    private readonly SplitContainer _splitRight;
    private readonly PaletteView _palette;
    private readonly ListBox _layersList;
    private readonly PropertyGrid _propGrid;
    private readonly MapCanvas _canvas;
    private readonly TileTypePalette _tileTypePalette;
    private readonly ToolPalette _toolPalette;

    public MainForm()
    {
        Text = "FROG Map Editor";
        MinimumSize = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;

        Shown += (_, _) => ApplyLayoutPercentages();
        ResizeEnd += (_, _) => ApplyLayoutPercentages();
        KeyDown += MainForm_KeyDown;

        _tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        var btnNewMap = new ToolStripButton("Nouvelle carte");
        var btnOpenTileset = new ToolStripButton("Ouvrir tileset");
        var btnSave = new ToolStripButton("Sauvegarder (.fmap)");
        var btnLoad = new ToolStripButton("Charger (.fmap)");
        _btnUndo = new ToolStripButton("Annuler") { Enabled = false };
        _btnRedo = new ToolStripButton("Rétablir") { Enabled = false };

        btnNewMap.Click += (_, _) => CreateNewMap();
        btnOpenTileset.Click += (_, _) => OpenTileset();
        btnSave.Click += (_, _) => SaveMap();
        btnLoad.Click += (_, _) => LoadMap();
        _btnUndo.Click += (_, _) => DoUndo();
        _btnRedo.Click += (_, _) => DoRedo();

        _tool.Items.AddRange(new ToolStripItem[]
        {
            btnNewMap, new ToolStripSeparator(),
            btnOpenTileset, new ToolStripSeparator(),
            btnSave, btnLoad, new ToolStripSeparator(),
            _btnUndo, _btnRedo
        });

        _status = new StatusStrip();
        _lblPos = new ToolStripStatusLabel("x=0, y=0");
        _status.Items.Add(_lblPos);

        _splitLeft = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 260,
            FixedPanel = FixedPanel.Panel1
        };
        _splitRight = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
            FixedPanel = FixedPanel.Panel2
        };

        _canvas = new MapCanvas { Dock = DockStyle.Fill };
        _canvas.HoveredTileChanged += p => _lblPos.Text = $"x={p.X}, y={p.Y}";
        _canvas.TileClicked += OnTileClicked;
        _canvas.MapReplaced += OnMapReplaced;
        _canvas.UndoHistoryChanged += UpdateUndoRedoButtons;

        _palette = new PaletteView { TileSize = 32, Dock = DockStyle.Fill };
        _palette.SelectedTileChanged += pt => _canvas.SelectedSrc = pt;

        _toolPalette = new ToolPalette { Dock = DockStyle.Top };
        _toolPalette.ToolChanged += tool => _canvas.ActiveTool = tool;

        _tileTypePalette = new TileTypePalette { Dock = DockStyle.Top };
        _tileTypePalette.SelectedTileTypeChanged += type => _canvas.SelectedTileType = type;

        _splitLeft.Panel1.Controls.Add(_palette);
        _splitLeft.Panel1.Controls.Add(_toolPalette);
        _splitLeft.Panel1.Controls.Add(_tileTypePalette);
        _splitRight.Panel1.Controls.Add(_canvas);

        var rightPanel = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 280
        };
        _layersList = new ListBox { Dock = DockStyle.Fill };
        _layersList.SelectedIndexChanged += (_, _) => _canvas.ActiveLayerIndex = _layersList.SelectedIndex;
        rightPanel.Panel1.Controls.Add(_layersList);

        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Ajouter couche", null, (_, _) => AddLayer());
        ctx.Items.Add("Supprimer couche", null, (_, _) => RemoveLayer());
        ctx.Items.Add("Renommer couche", null, (_, _) => RenameLayer());
        _layersList.ContextMenuStrip = ctx;

        _propGrid = new PropertyGrid { Dock = DockStyle.Fill };
        rightPanel.Panel2.Controls.Add(_propGrid);

        _splitRight.Panel2.Controls.Add(rightPanel);
        _splitLeft.Panel2.Controls.Add(_splitRight);
        Controls.AddRange(new Control[] { _splitLeft, _tool, _status });

        var map = new Map { Width = 20, Height = 15, Name = "Nouvelle carte" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        _canvas.Map = map;
        _propGrid.SelectedObject = _canvas.Map;
        RefreshLayersUi();
        UpdateUndoRedoButtons();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control)
        {
            return;
        }

        if (e.KeyCode == Keys.Z)
        {
            DoUndo();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Y)
        {
            DoRedo();
            e.Handled = true;
        }
    }

    private void OnTileClicked(Tile? tile)
    {
        _propGrid.SelectedObject = tile ?? (object?)_canvas.Map;
    }

    private void OnMapReplaced()
    {
        RefreshLayersUi();
        _propGrid.SelectedObject = _canvas.Map;
        UpdateUndoRedoButtons();
    }

    private void DoUndo()
    {
        _canvas.PerformUndo();
        UpdateUndoRedoButtons();
    }

    private void DoRedo()
    {
        _canvas.PerformRedo();
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        _btnUndo.Enabled = _canvas.History.CanUndo;
        _btnRedo.Enabled = _canvas.History.CanRedo;
    }

    private void RefreshLayersUi()
    {
        _layersList.Items.Clear();
        if (_canvas.Map is null)
        {
            return;
        }

        for (var i = 0; i < _canvas.Map.Layers.Count; i++)
        {
            var l = _canvas.Map.Layers[i];
            _layersList.Items.Add($"{i}: {l.LayerType}");
        }

        if (_layersList.Items.Count > 0)
        {
            _layersList.SelectedIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, _layersList.Items.Count - 1);
        }
    }

    private void AddLayer()
    {
        if (_canvas.Map is null)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        _canvas.Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void RemoveLayer()
    {
        if (_canvas.Map is null || _layersList.SelectedIndex < 0)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        _canvas.Map.Layers.RemoveAt(_layersList.SelectedIndex);
        _canvas.ActiveLayerIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, Math.Max(0, _canvas.Map.Layers.Count - 1));
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void RenameLayer()
    {
        if (_layersList.SelectedIndex < 0 || _canvas.Map is null)
        {
            return;
        }

        var current = _canvas.Map.Layers[_layersList.SelectedIndex].LayerType.ToString();
        var input = SimpleInputDialog.Show(this, "Renommer la couche", "Nom (LayerType enum, ex. Ground, Attributes) :", current);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (Enum.TryParse(input, true, out LayerType type))
        {
            _canvas.History.PushBeforeChange(_canvas.Map);
            _canvas.Map.Layers[_layersList.SelectedIndex].LayerType = type;
            RefreshLayersUi();
            UpdateUndoRedoButtons();
        }
        else
        {
            MessageBox.Show(this, "Valeur d’énumération non reconnue.", "Renommer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CreateNewMap()
    {
        using var dlg = new NewMapDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var map = new Map { Width = dlg.MapWidth, Height = dlg.MapHeight, Name = dlg.MapName };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        _canvas.ClearHistory();
        _canvas.Map = map;
        _propGrid.SelectedObject = map;
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void OpenTileset()
    {
        using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (ofd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var id = TilesetCache.LoadFromFile(ofd.FileName);
        _canvas.ActiveTilesetId = id;
        _palette.SetTileset(id);
    }

    private void SaveMap()
    {
        if (_canvas.Map is null)
        {
            return;
        }

        using var sfd = new SaveFileDialog { Filter = "Frog Map|*.fmap" };
        if (sfd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var serializer = new MapSerializer();
        var bytes = serializer.Serialize(_canvas.Map);
        File.WriteAllBytes(sfd.FileName, bytes);
        MessageBox.Show(this, "Carte sauvegardée.", "Succès");
    }

    private void LoadMap()
    {
        using var ofd = new OpenFileDialog { Filter = "Frog Map|*.fmap" };
        if (ofd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var data = File.ReadAllBytes(ofd.FileName);
        var serializer = new MapSerializer();
        var map = serializer.Deserialize(data);
        _canvas.ClearHistory();
        _canvas.Map = map;
        _propGrid.SelectedObject = map;
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void ApplyLayoutPercentages()
    {
        var totalW = ClientSize.Width;
        if (totalW <= 0)
        {
            return;
        }

        var leftW = (int)(totalW * 0.20f);
        leftW = Math.Max(300, leftW);
        _splitLeft.SplitterDistance = leftW;

        var rightContainerW = _splitRight.Width;
        if (rightContainerW <= 0)
        {
            rightContainerW = totalW - leftW;
        }

        var propsW = (int)(rightContainerW * 0.25f);
        propsW = Math.Max(340, propsW);
        _splitRight.SplitterDistance = Math.Max(200, _splitRight.Width - propsW);
    }

    internal sealed class NewMapDialog : Form
    {
        private readonly NumericUpDown _numW;
        private readonly NumericUpDown _numH;
        private readonly TextBox _txtName;

        public int MapWidth => (int)_numW.Value;
        public int MapHeight => (int)_numH.Value;
        public string MapName => _txtName.Text.Trim();

        public NewMapDialog()
        {
            Text = "Nouvelle carte";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 160);

            _numW = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 20, Location = new Point(140, 20), Width = 120 };
            _numH = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 15, Location = new Point(140, 55), Width = 120 };
            _txtName = new TextBox { Text = "Nouvelle carte", Location = new Point(140, 90), Width = 160 };

            Controls.Add(new Label { Text = "Largeur (tuiles):", AutoSize = true, Location = new Point(20, 22) });
            Controls.Add(_numW);
            Controls.Add(new Label { Text = "Hauteur (tuiles):", AutoSize = true, Location = new Point(20, 57) });
            Controls.Add(_numH);
            Controls.Add(new Label { Text = "Nom:", AutoSize = true, Location = new Point(20, 93) });
            Controls.Add(_txtName);

            var btnOk = new Button { Text = "Créer", DialogResult = DialogResult.OK, Location = new Point(140, 125), Width = 80 };
            var btnCancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(230, 125), Width = 70 };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
