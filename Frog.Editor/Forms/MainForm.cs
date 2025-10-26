using System;
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Models;
using Frog.Core.Enums;
using Frog.Editor.Controls;
using Frog.Editor.Assets;

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

        public MainForm()
        {
            Text = "FROG Map Editor";
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;

            // ToolStrip
            _tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(20, 20), Dock = DockStyle.Top };
            var btnNewMap = new ToolStripButton("Nouvelle carte");
            var btnOpenTileset = new ToolStripButton("Ouvrir tileset (PNG)");
            btnNewMap.Click += (s, e) => CreateNewMap();
            btnOpenTileset.Click += (s, e) => OpenTileset();
            _tool.Items.AddRange(new ToolStripItem[] { btnNewMap, new ToolStripSeparator(), btnOpenTileset });

            // Status
            _status = new StatusStrip();
            _lblPos = new ToolStripStatusLabel("x=0, y=0");
            _status.Items.Add(_lblPos);

            // Split global
            _splitLeft = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 260, FixedPanel = FixedPanel.Panel1 };
            _splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 760, FixedPanel = FixedPanel.Panel2 };

            // Palette (gauche)
            _palette = new PaletteView { TileSize = 32 };
            _palette.SelectedTileChanged += pt =>
            {
                _canvas.SelectedSrc = pt;
            };
            _splitLeft.Panel1.Controls.Add(_palette);

            // Canvas (centre)
            _canvas = new MapCanvas();
            _canvas.HoveredTileChanged += p => _lblPos.Text = $"x={p.X}, y={p.Y}";
            _splitRight.Panel1.Controls.Add(_canvas);

            // Panneau droit : Layers + Propriétés
            var rightPanel = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 280 };
            _layersList = new ListBox { Dock = DockStyle.Fill };
            _layersList.SelectedIndexChanged += (s, e) => _canvas.ActiveLayerIndex = _layersList.SelectedIndex;
            rightPanel.Panel1.Controls.Add(_layersList);

            _propGrid = new PropertyGrid { Dock = DockStyle.Fill };
            rightPanel.Panel2.Controls.Add(_propGrid);

            _splitRight.Panel2.Controls.Add(rightPanel);
            _splitLeft.Panel2.Controls.Add(_splitRight);

            Controls.AddRange(new Control[] { _splitLeft, _tool, _status });

            // Map par défaut + couche Ground
            var map = new Map { Width = 20, Height = 15, Name = "Nouvelle carte" };
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            _canvas.Map = map;
            _propGrid.SelectedObject = _canvas.Map;
            RefreshLayersUi();
        }

        private void RefreshLayersUi()
        {
            _layersList.Items.Clear();
            if (_canvas.Map is null) return;
            for (int i = 0; i < _canvas.Map.Layers.Count; i++)
            {
                var lt = _canvas.Map.Layers[i].LayerType;
                _layersList.Items.Add($"{i}: {lt}");
            }
            if (_layersList.Items.Count > 0)
            {
                _layersList.SelectedIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, _layersList.Items.Count - 1);
            }
        }

        private void CreateNewMap()
        {
            using var dlg = new NewMapDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var map = new Map { Width = dlg.MapWidth, Height = dlg.MapHeight, Name = dlg.MapName };
                map.Layers.Add(new Layer { LayerType = LayerType.Ground });
                _canvas.Map = map;
                _propGrid.SelectedObject = _canvas.Map;
                _canvas.Invalidate();
                RefreshLayersUi();
            }
        }

        private void OpenTileset()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Choisir un tileset (PNG/JPG)",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = false
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            int id = TilesetCache.LoadFromFile(ofd.FileName);
            _canvas.ActiveTilesetId = id;
            _palette.SetTileset(id);
        }
    }

    // même dialog que précédemment (inchangé)
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
