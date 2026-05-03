using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
    private readonly MenuStrip _menuStrip;
    private readonly ToolStripMenuItem _mnuUndo;
    private readonly ToolStripMenuItem _mnuRedo;
    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _lblPos;
    private readonly SplitContainer _splitLeft;
    private readonly SplitContainer _splitRight;
    private readonly PaletteView _palette;
    private readonly ListView _layersList;
    private bool _suspendLayerListEvents;
    private readonly PropertyGrid _propGrid;
    private readonly MapCanvas _canvas;
    private readonly MapMinimapControl _minimap;
    private readonly TileTypePalette _tileTypePalette;
    private readonly ToolPalette _toolPalette;
    private readonly TableLayoutPanel _leftLayout;
    private readonly ListBox _lstTilesets;
    private readonly Button _btnAddTileset;
    /// <summary>Horizontal : panneau haut = couches, bas = PropertyGrid.</summary>
    private readonly SplitContainer _splitLayersProps;
    private readonly SplitContainer _splitRightTileset;
    private readonly TabControl _tabTilesets;
    private readonly TreeView _mapsTree;
    private readonly Panel _mapWorkbench;
    private readonly Panel _mapHeader;
    private readonly Label _lblMapWorkspaceTitle;
    private bool _suspendTilesetTabSync;
    private bool _suspendTilesetListSync;

    /// <param name="embedAsWpfChild">Si vrai, la fenêtre est hébergée dans un <c>WindowsFormsHost</c> WPF (pas de chrome fenêtre).</param>
    public MainForm(bool embedAsWpfChild = false)
    {
        Text = "Frog — Éditeur de cartes";
        MinimumSize = new Size(1100, 720);
        if (embedAsWpfChild)
        {
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            TopLevel = false;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
        }

        KeyPreview = true;
        EditorChrome.ApplyFormChrome(this);

        FormClosed += (_, _) => TilesetCache.Clear();

        Shown += (_, _) =>
        {
            ApplyLayoutPercentages();
            PositionMinimap();
        };
        ResizeEnd += (_, _) => ApplyLayoutPercentages();

        _menuStrip = new MenuStrip();
        EditorChrome.StyleMainMenu(_menuStrip);

        var mFile = new ToolStripMenuItem("Fichier");
        mFile.DropDownItems.Add(new ToolStripMenuItem("Nouvelle carte…", null, (_, _) => CreateNewMap())
        {
            ShortcutKeys = Keys.Control | Keys.N,
            ShowShortcutKeys = true,
        });
        mFile.DropDownItems.Add(new ToolStripMenuItem("Ouvrir…", null, (_, _) => LoadMap())
        {
            ShortcutKeys = Keys.Control | Keys.O,
            ShowShortcutKeys = true,
        });
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add(new ToolStripMenuItem("Enregistrer", null, (_, _) => SaveMap())
        {
            ShortcutKeys = Keys.Control | Keys.S,
            ShowShortcutKeys = true,
        });
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add("Quitter", null, (_, _) =>
        {
            if (TopLevel)
            {
                Close();
            }
            else
            {
                System.Windows.Application.Current.Shutdown();
            }
        });

        _mnuUndo = new ToolStripMenuItem("Annuler", null, (_, _) => DoUndo())
        {
            Enabled = false,
            ShortcutKeys = Keys.Control | Keys.Z,
            ShowShortcutKeys = true,
        };
        _mnuRedo = new ToolStripMenuItem("Rétablir", null, (_, _) => DoRedo())
        {
            Enabled = false,
            ShortcutKeys = Keys.Control | Keys.Y,
            ShowShortcutKeys = true,
        };
        var mEdit = new ToolStripMenuItem("Édition");
        mEdit.DropDownItems.Add(_mnuUndo);
        mEdit.DropDownItems.Add(_mnuRedo);

        var mResources = new ToolStripMenuItem("Ressources");
        mResources.DropDownItems.Add("Charger une image tuiles…", null, (_, _) => OpenTileset());

        var mMap = new ToolStripMenuItem("Carte");
        mMap.DropDownItems.Add("Valider la carte…", null, (_, _) => ValidateMap());

        var mView = new ToolStripMenuItem("Affichage");
        mView.DropDownItems.Add("Réinitialiser la vue (zoom 100 %)", null, (_, _) => ResetMapView());

        _menuStrip.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mResources, mMap, mView });
        MainMenuStrip = _menuStrip;

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
        _splitRight.Panel1.BackColor = EditorChrome.WorkspaceCenter;
        _splitRight.Panel2.BackColor = EditorChrome.SidebarBg;

        _canvas = new MapCanvas { Dock = DockStyle.Fill };
        _canvas.HoveredTileChanged += p => _lblPos.Text = $"Tuile · x = {p.X}, y = {p.Y}";
        _canvas.TileClicked += OnTileClicked;
        _canvas.MapReplaced += OnMapReplaced;
        _canvas.UndoHistoryChanged += UpdateUndoRedoButtons;

        _minimap = new MapMinimapControl
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _minimap.Attach(_canvas);

        _palette = new PaletteView { TileSize = 32, Dock = DockStyle.Fill, Margin = new Padding(6, 2, 6, 8) };
        _palette.SelectedTileChanged += pt => _canvas.SelectedSrc = pt;

        _toolPalette = new ToolPalette { Dock = DockStyle.Top };
        _toolPalette.ToolChanged += tool =>
        {
            _canvas.ActiveTool = tool;
            _canvas.Invalidate();
        };

        _tileTypePalette = new TileTypePalette { Dock = DockStyle.Top };
        _tileTypePalette.SelectedTileTypeChanged += type => _canvas.SelectedTileType = type;

        _mapsTree = new TreeView { Dock = DockStyle.Fill };
        EditorChrome.StyleMapsTree(_mapsTree);

        var tilesetBand = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 10, 10, 10),
            Margin = new Padding(6, 12, 6, 4),
            BackColor = EditorChrome.SidebarElevated,
        };
        tilesetBand.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        tilesetBand.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _btnAddTileset = new Button { Text = "Charger image tuiles…", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4) };
        EditorChrome.StylePrimaryButton(_btnAddTileset);
        _btnAddTileset.Click += (_, _) => OpenTileset();
        _lstTilesets = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        EditorChrome.StyleSidebarList(_lstTilesets);
        _lstTilesets.Font = EditorChrome.CaptionFont;
        _lstTilesets.SelectedIndexChanged += TilesetsList_SelectedIndexChanged;
        tilesetBand.Controls.Add(_btnAddTileset, 0, 0);
        tilesetBand.Controls.Add(_lstTilesets, 0, 1);

        _tabTilesets = new TabControl { Dock = DockStyle.Top, Height = 34, Margin = new Padding(6, 4, 6, 0) };
        EditorChrome.StyleTabControlMaps(_tabTilesets);
        foreach (var letter in new[] { "A", "B", "C", "D" })
        {
            _tabTilesets.TabPages.Add(new TabPage(letter) { BackColor = EditorChrome.SidebarElevated });
        }

        _tabTilesets.SelectedIndexChanged += TabTilesets_SelectedIndexChanged;

        var tilesetStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4, 4, 4, 6),
            BackColor = EditorChrome.SidebarBg,
        };
        tilesetStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
        tilesetStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        tilesetStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 152f));
        tilesetStack.Controls.Add(_tabTilesets, 0, 0);
        tilesetStack.Controls.Add(_palette, 0, 1);
        tilesetStack.Controls.Add(tilesetBand, 0, 2);

        var tilesBanner = EditorChrome.BuildZoneBanner("TUILES — sélection graphique");
        var tilesHost = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg, Padding = new Padding(0) };
        tilesHost.Controls.Add(tilesBanner);
        tilesHost.Controls.Add(tilesetStack);

        _leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(2, 10, 2, 12),
            BackColor = EditorChrome.SidebarBg,
        };
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var mapsBanner = EditorChrome.BuildZoneBanner("CARTES — projet");
        var mapsHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10), BackColor = EditorChrome.SidebarBg };
        mapsHost.Controls.Add(mapsBanner);
        mapsHost.Controls.Add(_mapsTree);

        _leftLayout.Controls.Add(_toolPalette, 0, 0);
        _leftLayout.Controls.Add(_tileTypePalette, 0, 1);
        _leftLayout.Controls.Add(mapsHost, 0, 2);

        _splitLeft.Panel1.Controls.Add(_leftLayout);

        _mapHeader = new Panel { Dock = DockStyle.Top, Height = 33, BackColor = EditorChrome.RibbonBg };
        _lblMapWorkspaceTitle = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 8, 0),
            ForeColor = EditorChrome.LabelPrimary,
            Font = EditorChrome.SectionFont,
            BackColor = Color.Transparent,
        };
        var mapAccent = new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = EditorChrome.RibbonAccent };
        _mapHeader.Controls.Add(_lblMapWorkspaceTitle);
        _mapHeader.Controls.Add(mapAccent);
        _mapWorkbench = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.CanvasInset,
            Padding = new Padding(10, 0, 10, 12),
        };
        // Ordre de docking : d’abord le bandeau (Top), puis le canevas (Fill), sinon le Fill « mange » tout et le bandeau bleu se superpose mal.
        _mapWorkbench.Controls.Add(_mapHeader);
        _mapWorkbench.Controls.Add(_canvas);
        _mapWorkbench.Controls.Add(_minimap);
        _minimap.BringToFront();
        _mapWorkbench.Resize += (_, _) => PositionMinimap();
        _splitRight.Panel1.Controls.Add(_mapWorkbench);
        PositionMinimap();

        _splitRightTileset = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.None,
            // Minimums bas : au 1er layout la hauteur du split peut être petite ; SplitterDistance est appliquée plus tard.
            Panel1MinSize = 48,
            Panel2MinSize = 48,
            BackColor = EditorChrome.CanvasInset,
        };
        _splitRightTileset.Panel1.BackColor = EditorChrome.SidebarBg;
        _splitRightTileset.Panel2.BackColor = EditorChrome.SidebarBg;
        _splitRightTileset.Panel1.Controls.Add(tilesHost);

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
        layersHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        layersHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var layersBanner = EditorChrome.BuildZoneBanner("COUCHES — ordre de dessin");
        layersHost.Controls.Add(layersBanner, 0, 0);

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
        _propGrid.PropertyValueChanged += (_, _) =>
        {
            if (_propGrid.SelectedObject is Map)
            {
                UpdateMapChromeLabels();
            }
        };
        _mapsTree.AfterSelect += (_, _) =>
        {
            if (_mapsTree.SelectedNode?.Tag as string == "current"
                && _propGrid.SelectedObject is not Map
                && _canvas.Map is not null)
            {
                _propGrid.SelectedObject = _canvas.Map;
            }
        };
        _splitLayersProps.Panel2.Controls.Add(_propGrid);

        _splitRightTileset.Panel2.Controls.Add(_splitLayersProps);
        _splitRightTileset.HandleCreated += (_, _) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(ApplyRightTilesetSplitDistance));
            }
        };
        _splitRight.Panel2.Controls.Add(_splitRightTileset);
        _splitLeft.Panel2.Controls.Add(_splitRight);
        // Ordre d’ancrage WinForms : bas (status), milieu (fill), haut (menu) pour réserver correctement l’espace sous le MenuStrip.
        _menuStrip.Dock = DockStyle.Top;
        _splitLeft.Dock = DockStyle.Fill;
        _status.Dock = DockStyle.Bottom;
        Controls.Add(_status);
        Controls.Add(_splitLeft);
        Controls.Add(_menuStrip);

        var map = new Map { Width = 20, Height = 15, Name = "Nouvelle carte" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        _canvas.Map = map;
        _propGrid.SelectedObject = _canvas.Map;
        RefreshLayersUi();
        UpdateUndoRedoButtons();
        RefreshTilesetList();
        SyncMapsTree();
        UpdateMapChromeLabels();
    }

    private void ResetMapView() => _canvas.ResetViewTransform();

    private void TabTilesets_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suspendTilesetTabSync)
        {
            return;
        }

        var ix = _tabTilesets.SelectedIndex;
        if (ix < 0 || ix >= _lstTilesets.Items.Count)
        {
            return;
        }

        _suspendTilesetListSync = true;
        try
        {
            _lstTilesets.SelectedIndex = ix;
        }
        finally
        {
            _suspendTilesetListSync = false;
        }
    }

    private void SyncMapsTree()
    {
        _mapsTree.BeginUpdate();
        try
        {
            _mapsTree.Nodes.Clear();
            var root = _mapsTree.Nodes.Add("Cartes du projet");
            root.ForeColor = EditorChrome.LabelPrimary;
            if (_canvas.Map is not null)
            {
                var n = root.Nodes.Add($"001  {_canvas.Map.Name}");
                n.ForeColor = EditorChrome.RibbonAccent;
                n.Tag = "current";
            }

            root.Expand();
            _mapsTree.SelectedNode = root.Nodes.Count > 0 ? root.Nodes[0] : root;
        }
        finally
        {
            _mapsTree.EndUpdate();
        }
    }

    private void UpdateMapChromeLabels()
    {
        if (_canvas.Map is null)
        {
            _lblMapWorkspaceTitle.Text = "Carte : —";
            return;
        }

        _lblMapWorkspaceTitle.Text =
            $"Carte : {_canvas.Map.Name}    ({_canvas.Map.Width} × {_canvas.Map.Height} tuiles)";
        if (_mapsTree.Nodes.Count > 0 && _mapsTree.Nodes[0].Nodes.Count > 0)
        {
            _mapsTree.Nodes[0].Nodes[0].Text = $"001  {_canvas.Map.Name}";
        }
    }

    private void TilesetsList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suspendTilesetListSync)
        {
            return;
        }

        if (_lstTilesets.SelectedItem is not TilesetEntry te)
        {
            return;
        }

        _canvas.ActiveTilesetId = te.Id;
        _palette.SetTileset(te.Id);
        SyncTilesetTabFromList();
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
                    SyncTilesetTabFromList();
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

            SyncTilesetTabFromList();
        }
        finally
        {
            _lstTilesets.EndUpdate();
        }
    }

    private void SyncTilesetTabFromList()
    {
        _suspendTilesetTabSync = true;
        try
        {
            if (_lstTilesets.Items.Count == 0)
            {
                _tabTilesets.SelectedIndex = 0;
                return;
            }

            var ix = Math.Max(0, _lstTilesets.SelectedIndex);
            _tabTilesets.SelectedIndex = Math.Min(ix, _tabTilesets.TabCount - 1);
        }
        finally
        {
            _suspendTilesetTabSync = false;
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

        // Annuler / Rétablir : raccourcis gérés par le MenuStrip (Édition).

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnTileClicked(Tile? tile)
    {
        _propGrid.SelectedObject = tile ?? (object?)_canvas.Map;
    }

    private void ValidateMap()
    {
        if (_canvas.Map is null)
        {
            MessageBox.Show(this, "Aucune carte chargée.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_canvas.Map.Validate(out var err))
        {
            MessageBox.Show(this, "Carte valide (dimensions, couches, tuiles, warps).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(this, err ?? "Erreur inconnue.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnMapReplaced()
    {
        RefreshLayersUi();
        _propGrid.SelectedObject = _canvas.Map;
        UpdateUndoRedoButtons();
        UpdateMapChromeLabels();
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
        _mnuUndo.Enabled = _canvas.History.CanUndo;
        _mnuRedo.Enabled = _canvas.History.CanRedo;
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
        SyncMapsTree();
        UpdateMapChromeLabels();
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
        SaveTilesetManifestNextToMap(sfd.FileName);
        MessageBox.Show(this, "Carte et manifeste tilesets (.tilesets.json) sauvegardés.", "Succès");
    }

    private static void SaveTilesetManifestNextToMap(string mapFilePath)
    {
        var dir = Path.GetDirectoryName(mapFilePath);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(stem))
        {
            return;
        }

        var manifest = new TilesetManifest();
        foreach (var (id, label) in TilesetCache.ListRegistered())
        {
            manifest.Entries.Add(new TilesetManifestEntry { Id = id, FileName = label });
        }

        var manifestPath = Path.Combine(dir, stem + ".tilesets.json");
        File.WriteAllBytes(manifestPath, TilesetManifestJson.Serialize(manifest));
    }

    private void LoadMap()
    {
        using var ofd = new OpenFileDialog { Filter = "Frog Map|*.fmap" };
        if (ofd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var mapPath = ofd.FileName;
        var data = File.ReadAllBytes(mapPath);
        var serializer = new MapSerializer();
        var map = serializer.Deserialize(data);

        TilesetCache.Clear();
        var manifestOutcome = TryApplyTilesetManifestFromMapPath(mapPath);

        _canvas.ClearHistory();
        _canvas.Map = map;
        _canvas.ActiveTilesetId = 0;
        if (TilesetCache.ListRegistered().Count > 0)
        {
            _canvas.ActiveTilesetId = TilesetCache.ListRegistered()[0].Id;
        }

        _propGrid.SelectedObject = map;
        RefreshLayersUi();
        RefreshTilesetList();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        SyncMapsTree();
        UpdateMapChromeLabels();

        if (manifestOutcome.HadManifest && manifestOutcome.MissingFiles.Count > 0)
        {
            var list = string.Join(Environment.NewLine, manifestOutcome.MissingFiles.Take(12));
            var tail = manifestOutcome.MissingFiles.Count > 12 ? Environment.NewLine + "…" : string.Empty;
            MessageBox.Show(
                this,
                "Fichiers PNG introuvables ou illisibles (manifeste à côté du .fmap) :" + Environment.NewLine + list + tail,
                "Tilesets",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Lit <c>{stem}.tilesets.json</c> à côté du fichier carte et réinjecte les bitmaps avec les mêmes <see cref="TilesetManifestEntry.Id"/> que lors de l’enregistrement.
    /// </summary>
    private static (bool HadManifest, List<string> MissingFiles) TryApplyTilesetManifestFromMapPath(string mapFilePath)
    {
        var missing = new List<string>();
        var dir = Path.GetDirectoryName(mapFilePath);
        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            return (false, missing);
        }

        var manifestPath = Path.Combine(dir, stem + ".tilesets.json");
        var manifest = TilesetManifestJson.TryDeserializeFromFile(manifestPath);
        if (manifest is null)
        {
            return (false, missing);
        }

        foreach (var entry in manifest.Entries.OrderBy(e => e.Id))
        {
            if (entry.Id < 1)
            {
                continue;
            }

            var nameOnly = Path.GetFileName(entry.FileName);
            if (string.IsNullOrEmpty(nameOnly))
            {
                missing.Add($"id {entry.Id} (nom vide)");
                continue;
            }

            var full = Path.Combine(dir, nameOnly);
            if (!File.Exists(full))
            {
                missing.Add(nameOnly);
                continue;
            }

            try
            {
                TilesetCache.LoadFromFileAtId(full, entry.Id);
            }
            catch
            {
                missing.Add(nameOnly);
            }
        }

        return (true, missing);
    }

    private void PositionMinimap()
    {
        var pad = _mapWorkbench.Padding;
        _minimap.Location = new Point(
            _mapWorkbench.ClientSize.Width - _minimap.Width - pad.Right,
            pad.Top);
    }

    private void ApplyLayoutPercentages()
    {
        var totalW = ClientSize.Width;
        if (totalW <= 0)
        {
            return;
        }

        var leftW = (int)(totalW * 0.20f);
        leftW = Math.Max(328, leftW);
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
        PositionMinimap();
        ApplyRightTilesetSplitDistance();
    }

    /// <summary>
    /// Règle la barre tuiles / couches uniquement quand la hauteur du split le permet (évite InvalidOperationException au démarrage).
    /// </summary>
    private void ApplyRightTilesetSplitDistance()
    {
        var sc = _splitRightTileset;
        var h = sc.Height;
        var sw = sc.SplitterWidth;
        if (h <= sw + 12)
        {
            return;
        }

        var minD = sc.Panel1MinSize;
        var maxD = h - sc.Panel2MinSize - sw;
        if (maxD <= minD)
        {
            return;
        }

        var want = Math.Clamp((int)(h * 0.48f), minD, maxD);
        sc.SplitterDistance = want;
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
            MinimumSize = new Size(440, 260);
            ClientSize = new Size(480, 240);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);
            EditorChrome.ApplyFormChrome(this);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(20, 18, 20, 16),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));

            var lblW = new Label
            {
                Text = "Largeur (tuiles)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = EditorChrome.LabelPrimary,
                AutoSize = false,
            };
            _numW = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 512,
                Value = 20,
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
                TextAlign = HorizontalAlignment.Right,
            };
            var lblH = new Label
            {
                Text = "Hauteur (tuiles)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = EditorChrome.LabelPrimary,
                AutoSize = false,
            };
            _numH = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 512,
                Value = 15,
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
                TextAlign = HorizontalAlignment.Right,
            };
            var lblN = new Label
            {
                Text = "Nom de la carte",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = EditorChrome.LabelPrimary,
                AutoSize = false,
            };
            _txtName = new TextBox
            {
                Text = "Nouvelle carte",
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
            };

            root.Controls.Add(lblW, 0, 0);
            root.Controls.Add(_numW, 1, 0);
            root.Controls.Add(lblH, 0, 1);
            root.Controls.Add(_numH, 1, 1);
            root.Controls.Add(lblN, 0, 2);
            root.Controls.Add(_txtName, 1, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoSize = false,
                Padding = new Padding(0, 8, 0, 0),
                Margin = new Padding(0),
            };
            var btnOk = new Button { Text = "Créer", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(108, 34), Margin = new Padding(10, 0, 0, 0) };
            var btnCancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(108, 34) };
            EditorChrome.StyleDialogButton(btnOk, primary: true);
            EditorChrome.StyleDialogButton(btnCancel, primary: false);
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);
            root.SetColumnSpan(buttons, 2);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
