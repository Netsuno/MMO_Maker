using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Frog.Core.Models;
using Frog.Core.Enums;
using Frog.Editor.Controls;
using Frog.Editor.Assets;
using Frog.Core.IO;


namespace Frog.Editor.Forms
{
    public sealed class MainForm : Form
    {
        private readonly ToolStrip _tool;
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

            // Barre d’outils principale
            _tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            var btnNewMap = new ToolStripButton("Nouvelle carte");
            var btnOpenTileset = new ToolStripButton("Ouvrir tileset");
            var btnSave = new ToolStripButton("Sauvegarder (.fmap)");
            var btnLoad = new ToolStripButton("Charger (.fmap)");

            btnNewMap.Click += (s, e) => CreateNewMap();
            btnOpenTileset.Click += (s, e) => OpenTileset();
            btnSave.Click += (s, e) => SaveMap();
            btnLoad.Click += (s, e) => LoadMap();

            _tool.Items.AddRange(new ToolStripItem[]
            {
                btnNewMap, new ToolStripSeparator(),
                btnOpenTileset, new ToolStripSeparator(),
                btnSave, btnLoad
            });

            // Barre de statut
            _status = new StatusStrip();
            _lblPos = new ToolStripStatusLabel("x=0, y=0");
            _status.Items.Add(_lblPos);

            // Split global
            _splitLeft = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 260, FixedPanel = FixedPanel.Panel1 };
            _splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 760, FixedPanel = FixedPanel.Panel2 };

            // Palette gauche
            _palette = new PaletteView { TileSize = 32 };
            _palette.SelectedTileChanged += pt => _canvas.SelectedSrc = pt;
            _splitLeft.Panel1.Controls.Add(_palette);

            // Palette de sélection du TileType
            _tileTypePalette = new TileTypePalette
            {
                Dock = DockStyle.Top
            };

            _toolPalette = new ToolPalette
            {
                Dock = DockStyle.Top
            };

            _toolPalette.ToolChanged += tool =>
            {
                _canvas.ActiveTool = tool;
            };

            // Ajouter la palette des outils dans le panel gauche
            _splitLeft.Panel1.Controls.Add(_toolPalette);


            // On ajoute la palette des outils au panel gauche
            _splitLeft.Panel1.Controls.Add(_toolPalette);
            // Quand on change Ground / Block / Warp / Resource,
            // on met à jour directement le MapCanvas
            _tileTypePalette.SelectedTileTypeChanged += type =>
            {
                _canvas.SelectedTileType = type;
            };

            // On place la palette SOUS la palette des tiles dans le panel de gauche
            _splitLeft.Panel1.Controls.Add(_tileTypePalette);

            // Canvas central
            _canvas = new MapCanvas();
            _canvas.HoveredTileChanged += p => _lblPos.Text = $"x={p.X}, y={p.Y}";
            _canvas.TileClicked += tile =>
            {
                // Si on clique sur une tuile, on montre ses propriétés
                // Si null (en dehors / rien trouvé), on revient à la map
                _propGrid.SelectedObject = tile ?? (object?)_canvas.Map;
            };
            _splitRight.Panel1.Controls.Add(_canvas);

            // Côté droit : couches + propriétés
            var rightPanel = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 280 };
            _layersList = new ListBox { Dock = DockStyle.Fill };
            _layersList.SelectedIndexChanged += (s, e) => _canvas.ActiveLayerIndex = _layersList.SelectedIndex;
            rightPanel.Panel1.Controls.Add(_layersList);

            // Menu contextuel des couches
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Ajouter couche", null, (s, e) => AddLayer());
            ctx.Items.Add("Supprimer couche", null, (s, e) => RemoveLayer());
            ctx.Items.Add("Renommer couche", null, (s, e) => RenameLayer());
            _layersList.ContextMenuStrip = ctx;

            _propGrid = new PropertyGrid { Dock = DockStyle.Fill };
            rightPanel.Panel2.Controls.Add(_propGrid);

            _splitRight.Panel2.Controls.Add(rightPanel);
            _splitLeft.Panel2.Controls.Add(_splitRight);
            Controls.AddRange(new Control[] { _splitLeft, _tool, _status });

            // Map de départ
            var map = new Map { Width = 20, Height = 15, Name = "Nouvelle carte" };
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            _canvas.Map = map;
            _propGrid.SelectedObject = _canvas.Map;
            RefreshLayersUi();
        }

        private void RefreshLayersUi()
        {
            _layersList.Items.Clear();
            if (_canvas.Map == null) return;
            for (int i = 0; i < _canvas.Map.Layers.Count; i++)
            {
                var l = _canvas.Map.Layers[i];
                _layersList.Items.Add($"{i}: {l.LayerType}");
            }
            if (_layersList.Items.Count > 0)
                _layersList.SelectedIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, _layersList.Items.Count - 1);
        }

        private void AddLayer()
        {
            if (_canvas.Map == null) return;
            _canvas.Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            RefreshLayersUi();
            _canvas.Invalidate();
        }

        private void RemoveLayer()
        {
            if (_canvas.Map == null) return;
            if (_layersList.SelectedIndex < 0) return;
            _canvas.Map.Layers.RemoveAt(_layersList.SelectedIndex);
            _canvas.ActiveLayerIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, _canvas.Map.Layers.Count - 1);
            RefreshLayersUi();
            _canvas.Invalidate();
        }

        private void RenameLayer()
        {
            if (_layersList.SelectedIndex < 0 || _canvas.Map == null) return;
            string? input = Microsoft.VisualBasic.Interaction.InputBox("Nom de la couche:", "Renommer", _canvas.Map.Layers[_layersList.SelectedIndex].LayerType.ToString());
            if (!string.IsNullOrWhiteSpace(input))
            {
                _canvas.Map.Layers[_layersList.SelectedIndex].LayerType = Enum.TryParse(input, true, out LayerType type)
                    ? type : LayerType.Ground;
                RefreshLayersUi();
            }
        }

        private void CreateNewMap()
        {
            using var dlg = new NewMapDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var map = new Map { Width = dlg.MapWidth, Height = dlg.MapHeight, Name = dlg.MapName };
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            _canvas.Map = map;
            _propGrid.SelectedObject = map;
            RefreshLayersUi();
            _canvas.Invalidate();
        }

        private void OpenTileset()
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            int id = TilesetCache.LoadFromFile(ofd.FileName);
            _canvas.ActiveTilesetId = id;
            _palette.SetTileset(id);
        }

        private void SaveMap()
        {
            if (_canvas.Map == null) return;
            using var sfd = new SaveFileDialog { Filter = "Frog Map|*.fmap" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var serializer = new MapSerializer();
            var bytes = serializer.Serialize(_canvas.Map);
            File.WriteAllBytes(sfd.FileName, bytes);
            MessageBox.Show("Carte sauvegardée.", "Succès");
        }

        private void LoadMap()
        {
            using var ofd = new OpenFileDialog { Filter = "Frog Map|*.fmap" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var data = File.ReadAllBytes(ofd.FileName);
            var serializer = new MapSerializer();
            var map = serializer.Deserialize(data);
            _canvas.Map = map;
            _propGrid.SelectedObject = map;
            RefreshLayersUi();
            _canvas.Invalidate();
        }
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
            MaximizeBox = false; MinimizeBox = false;
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
            Controls.Add(btnOk); Controls.Add(btnCancel);
            AcceptButton = btnOk; CancelButton = btnCancel;
        }


    }

}

