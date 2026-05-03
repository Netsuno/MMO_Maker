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
using Frog.Editor.Ui;

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
    private readonly ListView _layersList;
    private bool _suspendLayerListEvents;
    private readonly PropertyGrid _propGrid;
    private readonly MapCanvas _canvas;
    private readonly TileTypePalette _tileTypePalette;
    private readonly ToolPalette _toolPalette;
    private readonly TableLayoutPanel _leftLayout;
    private readonly ListBox _lstTilesets;
    private readonly Button _btnAddTileset;
    /// <summary>Horizontal : panneau haut = couches, bas = PropertyGrid.</summary>
    private readonly SplitContainer _splitLayersProps;

    public MainForm()
    {
        Text = "Frog — Éditeur de cartes";
        MinimumSize = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        EditorChrome.ApplyFormChrome(this);

        FormClosed += (_, _) => TilesetCache.Clear();

        Shown += (_, _) => ApplyLayoutPercentages();
        ResizeEnd += (_, _) => ApplyLayoutPercentages();

        _tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        var btnNewMap = new ToolStripButton("Nouvelle carte");
        var btnOpenTileset = new ToolStripButton("Tileset…");
        var btnSave = new ToolStripButton("Enregistrer");
        var btnLoad = new ToolStripButton("Ouvrir…");
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
        EditorChrome.StripToolbar(_tool);
        foreach (ToolStripItem ti in _tool.Items)
        {
            if (ti is ToolStripButton tsb)
            {
                tsb.ForeColor = EditorChrome.LabelPrimary;
            }
        }

        _status = new StatusStrip { SizingGrip = false, GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Bottom };
        _status.BackColor = EditorChrome.RibbonBg;
        _status.Padding = new Padding(8, 4, 8, 4);
        _lblPos = new ToolStripStatusLabel("x = 0, y = 0") { BorderSides = ToolStripStatusLabelBorderSides.None };
        _lblPos.ForeColor = EditorChrome.LabelMuted;
        _status.Items.Add(_lblPos);

        _splitLeft = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 260,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6,
            BackColor = EditorChrome.CanvasInset,
        };
        _splitLeft.Panel1.BackColor = EditorChrome.SidebarBg;
        _splitLeft.Panel2.BackColor = EditorChrome.WorkspaceBg;

        _splitRight = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 6,
            BackColor = EditorChrome.CanvasInset,
        };
        _splitRight.Panel1.BackColor = EditorChrome.WorkspaceBg;
        _splitRight.Panel2.BackColor = EditorChrome.SidebarBg;

        _canvas = new MapCanvas { Dock = DockStyle.Fill };
        _canvas.HoveredTileChanged += p => _lblPos.Text = $"Tuile · x = {p.X}, y = {p.Y}";
        _canvas.TileClicked += OnTileClicked;
        _canvas.MapReplaced += OnMapReplaced;
        _canvas.UndoHistoryChanged += UpdateUndoRedoButtons;

        _palette = new PaletteView { TileSize = 32, Dock = DockStyle.Fill };
        _palette.SelectedTileChanged += pt => _canvas.SelectedSrc = pt;

        _toolPalette = new ToolPalette { Dock = DockStyle.Top };
        _toolPalette.ToolChanged += tool =>
        {
            _canvas.ActiveTool = tool;
            _canvas.Invalidate();
        };

        _tileTypePalette = new TileTypePalette { Dock = DockStyle.Fill };
        _tileTypePalette.SelectedTileTypeChanged += type => _canvas.SelectedTileType = type;

        var tilesetBand = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 14, 10, 14),
            BackColor = EditorChrome.SidebarElevated,
        };
        tilesetBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        tilesetBand.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _btnAddTileset = new Button { Text = "Charger une image tuiles…", Dock = DockStyle.Fill };
        EditorChrome.StylePrimaryButton(_btnAddTileset);
        _btnAddTileset.Click += (_, _) => OpenTileset();
        _lstTilesets = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        EditorChrome.StyleSidebarList(_lstTilesets);
        _lstTilesets.Font = EditorChrome.CaptionFont;
        _lstTilesets.SelectedIndexChanged += TilesetsList_SelectedIndexChanged;
        tilesetBand.Controls.Add(_btnAddTileset, 0, 0);
        tilesetBand.Controls.Add(_lstTilesets, 0, 1);

        _leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0, 6, 0, 10),
            BackColor = EditorChrome.SidebarBg,
        };
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148f));

        _leftLayout.Controls.Add(_toolPalette, 0, 0);
        _leftLayout.Controls.Add(_tileTypePalette, 0, 1);
        _leftLayout.Controls.Add(_palette, 0, 2);
        _leftLayout.Controls.Add(tilesetBand, 0, 3);
        _palette.Dock = DockStyle.Fill;

        _splitLeft.Panel1.Controls.Add(_leftLayout);
        var mapWorkbench = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.CanvasInset,
            Padding = new Padding(10, 12, 10, 14),
        };
        mapWorkbench.Controls.Add(_canvas);
        _splitRight.Panel1.Controls.Add(mapWorkbench);

        _splitLayersProps = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = EditorChrome.CanvasInset,
        };
        _splitLayersProps.Panel2.BackColor = EditorChrome.SidebarBg;
        var layersHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 14, 10, 12),
            BackColor = EditorChrome.SidebarBg,
        };
        layersHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layersHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var lblLayersHint = new Label
        {
            Text = "COUCHES\r\n(case = visible comme dans RPG Maker)",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = EditorChrome.LabelMuted,
            Font = EditorChrome.CaptionFont,
            BackColor = Color.Transparent,
        };
        layersHost.Controls.Add(lblLayersHint, 0, 0);

        _layersList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            CheckBoxes = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
            Font = EditorChrome.BodyFont,
        };
        EditorChrome.StyleSidebarListView(_layersList);
        _layersList.Columns.Add("Affichage", 130);
        _layersList.Columns.Add("Type moteur", 95);
        _layersList.Columns.Add("Verrou", 52);
        _layersList.SelectedIndexChanged += LayersList_SelectedIndexChanged;
        _layersList.ItemChecked += LayersList_ItemChecked;
        _layersList.MouseDoubleClick += (_, _) =>
        {
            if (GetSelectedLayerIndex() >= 0)
            {
                RenameLayerDisplay();
            }
        };

        layersHost.Controls.Add(_layersList, 0, 1);

        _splitLayersProps.Panel1.Controls.Add(layersHost);

        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Ajouter couche", null, (_, _) => AddLayer());
        ctx.Items.Add("Supprimer couche", null, (_, _) => RemoveLayer());
        ctx.Items.Add("Renommer l’affichage…", null, (_, _) => RenameLayerDisplay());
        ctx.Items.Add("Type moteur (Ground, Mask…)…", null, (_, _) => ChangeLayerEngineType());
        ctx.Items.Add("Verrouiller / déverrouiller", null, (_, _) => ToggleLayerLock());
        _layersList.ContextMenuStrip = ctx;

        _propGrid = new PropertyGrid { Dock = DockStyle.Fill, HelpVisible = false };
        EditorChrome.StylePropertyGrid(_propGrid);
        _propGrid.Font = EditorChrome.BodyFont;
        _splitLayersProps.Panel2.Controls.Add(_propGrid);

        _splitRight.Panel2.Controls.Add(_splitLayersProps);
        _splitLeft.Panel2.Controls.Add(_splitRight);
        Controls.AddRange(new Control[] { _splitLeft, _tool, _status });

        var map = new Map { Width = 20, Height = 15, Name = "Nouvelle carte" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        _canvas.Map = map;
        _propGrid.SelectedObject = _canvas.Map;
        RefreshLayersUi();
        UpdateUndoRedoButtons();
        RefreshTilesetList();
    }

    private void TilesetsList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lstTilesets.SelectedItem is not TilesetEntry te)
        {
            return;
        }

        _canvas.ActiveTilesetId = te.Id;
        _palette.SetTileset(te.Id);
    }

    private void LayersList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_layersList.SelectedIndices.Count == 0)
        {
            return;
        }

        _canvas.ActiveLayerIndex = _layersList.SelectedIndices[0];
        _canvas.Invalidate();
    }

    private void LayersList_ItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_suspendLayerListEvents || _canvas.Map is null)
        {
            return;
        }

        var idx = e.Item.Index;
        if (idx < 0 || idx >= _canvas.Map.Layers.Count)
        {
            return;
        }

        _canvas.Map.Layers[idx].Visible = e.Item.Checked;
        _canvas.Invalidate();
    }

    private int GetSelectedLayerIndex() =>
        _layersList.SelectedIndices.Count > 0 ? _layersList.SelectedIndices[0] : -1;

    private void RefreshTilesetList()
    {
        var selId = GetSelectedTilesetId();
        _lstTilesets.BeginUpdate();
        try
        {
            _lstTilesets.Items.Clear();
            foreach (var (id, label) in TilesetCache.ListRegistered())
            {
                _lstTilesets.Items.Add(new TilesetEntry(id, label));
            }

            for (var i = 0; i < _lstTilesets.Items.Count; i++)
            {
                if (((TilesetEntry)_lstTilesets.Items[i]).Id == selId)
                {
                    _lstTilesets.SelectedIndex = i;
                    return;
                }
            }

            if (_lstTilesets.Items.Count > 0)
            {
                _lstTilesets.SelectedIndex = _lstTilesets.Items.Count - 1;
                var last = (TilesetEntry)_lstTilesets.SelectedItem!;
                _canvas.ActiveTilesetId = last.Id;
                _palette.SetTileset(last.Id);
            }
        }
        finally
        {
            _lstTilesets.EndUpdate();
        }
    }

    private int GetSelectedTilesetId()
    {
        if (_lstTilesets.SelectedItem is TilesetEntry te)
        {
            return te.Id;
        }

        return _canvas.ActiveTilesetId > 0 ? _canvas.ActiveTilesetId : 0;
    }

    private sealed record TilesetEntry(int Id, string Label)
    {
        public override string ToString() => $"{Id}: {Label}";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        var ctrl = (keyData & Keys.Control) == Keys.Control;

        if (ActiveControl is TextBoxBase)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (!ctrl && code == Keys.Escape)
        {
            _canvas.ClearSelection();
            return true;
        }

        if (_canvas.HandleEditorShortcuts(keyData))
        {
            return true;
        }

        if (ctrl && code == Keys.Z)
        {
            DoUndo();
            return true;
        }

        if (ctrl && code == Keys.Y)
        {
            DoRedo();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
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
        _suspendLayerListEvents = true;
        try
        {
            _layersList.Items.Clear();
            if (_canvas.Map is null)
            {
                return;
            }

            for (var i = 0; i < _canvas.Map.Layers.Count; i++)
            {
                var l = _canvas.Map.Layers[i];
                var it = new ListViewItem(l.GetDisplayLabel()) { Tag = i, Checked = l.Visible };
                it.SubItems.Add(l.LayerType.ToString());
                it.SubItems.Add(l.Locked ? "Oui" : "—");
                _layersList.Items.Add(it);
            }

            if (_layersList.Items.Count > 0)
            {
                var want = Math.Clamp(_canvas.ActiveLayerIndex, 0, _layersList.Items.Count - 1);
                _layersList.Items[want].Selected = true;
                _layersList.Items[want].Focused = true;
            }
        }
        finally
        {
            _suspendLayerListEvents = false;
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
        var ix = GetSelectedLayerIndex();
        if (_canvas.Map is null || ix < 0)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        _canvas.Map.Layers.RemoveAt(ix);
        _canvas.ActiveLayerIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, Math.Max(0, _canvas.Map.Layers.Count - 1));
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void RenameLayerDisplay()
    {
        var ix = GetSelectedLayerIndex();
        if (ix < 0 || _canvas.Map is null)
        {
            return;
        }

        var layer = _canvas.Map.Layers[ix];
        var current = string.IsNullOrWhiteSpace(layer.DisplayName) ? layer.GetDisplayLabel() : layer.DisplayName;
        var input = SimpleInputDialog.Show(this, "Nom affiché", "Libellé dans la liste (vide = nom du type moteur) :", current);
        if (input is null)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        layer.DisplayName = input.Trim();
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void ChangeLayerEngineType()
    {
        var ix = GetSelectedLayerIndex();
        if (ix < 0 || _canvas.Map is null)
        {
            return;
        }

        var layer = _canvas.Map.Layers[ix];
        var input = SimpleInputDialog.Show(
            this,
            "Type moteur",
            "LayerType (Ground, Mask, Mask2, Fringe, Fringe2, Attributes) :",
            layer.LayerType.ToString());
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (!Enum.TryParse(input, true, out LayerType type))
        {
            MessageBox.Show(this, "Valeur d’énumération non reconnue.", "Type moteur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        layer.LayerType = type;
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
    }

    private void ToggleLayerLock()
    {
        var ix = GetSelectedLayerIndex();
        if (ix < 0 || _canvas.Map is null)
        {
            return;
        }

        _canvas.Map.Layers[ix].Locked = !_canvas.Map.Layers[ix].Locked;
        RefreshLayersUi();
        _canvas.Invalidate();
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
        RefreshTilesetList();
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

        ApplyLayersPropertySplitDistance();
    }

    /// <summary>
    /// WinForms valide SplitterDistance dès l’assignation : au constructeur la hauteur du split est souvent 0,
    /// d’où l’impossibilité de fixer 280 + Panel2MinSize ici. On applique après layout.
    /// </summary>
    private void ApplyLayersPropertySplitDistance()
    {
        var h = _splitLayersProps.ClientSize.Height;
        if (h <= _splitLayersProps.SplitterWidth + 8)
        {
            return;
        }

        _splitLayersProps.Panel1MinSize = 100;
        _splitLayersProps.Panel2MinSize = 160;
        var sw = _splitLayersProps.SplitterWidth;
        var max = h - _splitLayersProps.Panel2MinSize - sw;
        var min = _splitLayersProps.Panel1MinSize;
        if (max < min)
        {
            _splitLayersProps.Panel2MinSize = Math.Max(80, h - min - sw - 1);
            max = h - _splitLayersProps.Panel2MinSize - sw;
        }

        if (max < min)
        {
            return;
        }

        _splitLayersProps.SplitterDistance = Math.Clamp(280, min, max);
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
            ClientSize = new Size(348, 168);
            EditorChrome.ApplyFormChrome(this);

            _numW = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 20, Location = new Point(160, 24), Width = 120 };
            _numH = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 15, Location = new Point(160, 54), Width = 120 };
            _txtName = new TextBox { Text = "Nouvelle carte", Location = new Point(160, 84), Width = 160 };

            Controls.Add(new Label { Text = "Largeur (tuiles)", AutoSize = true, Location = new Point(22, 26), ForeColor = EditorChrome.LabelMuted });
            Controls.Add(_numW);
            Controls.Add(new Label { Text = "Hauteur (tuiles)", AutoSize = true, Location = new Point(22, 56), ForeColor = EditorChrome.LabelMuted });
            Controls.Add(_numH);
            Controls.Add(new Label { Text = "Nom de la carte", AutoSize = true, Location = new Point(22, 86), ForeColor = EditorChrome.LabelMuted });
            Controls.Add(_txtName);

            var btnOk = new Button { Text = "Créer", DialogResult = DialogResult.OK, Location = new Point(160, 122), Width = 88 };
            var btnCancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(256, 122), Width = 80 };
            EditorChrome.StyleDialogButton(btnOk, primary: true);
            EditorChrome.StyleDialogButton(btnCancel, primary: false);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
