using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Core.Models;
using Frog.Editor.Controls;

namespace Frog.Editor.Forms
{
    public sealed class MainForm : Form
    {
        private readonly ToolStrip _tool;
        private readonly StatusStrip _status;
        private readonly ToolStripStatusLabel _lblPos;
        private readonly SplitContainer _splitLeft;
        private readonly SplitContainer _splitRight;
        private readonly Panel _tilesetPanel;
        private readonly ListBox _layersList;
        private readonly PropertyGrid _propGrid;
        private readonly MapCanvas _canvas;

        public MainForm()
        {
            Text = "FROG Map Editor (v1 UI)";
            MinimumSize = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;

            // ToolStrip
            _tool = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20),
                Dock = DockStyle.Top
            };
            var btnNewMap = new ToolStripButton("Nouvelle carte");
            var btnOpenTileset = new ToolStripButton("Ouvrir tileset");
            btnNewMap.Click += (s, e) => CreateNewMap();
            btnOpenTileset.Click += (s, e) => MessageBox.Show("Palette/tileset arrive à l’étape suivante.");
            _tool.Items.AddRange(new ToolStripItem[] { btnNewMap, new ToolStripSeparator(), btnOpenTileset });

            // StatusStrip
            _status = new StatusStrip();
            _lblPos = new ToolStripStatusLabel("x=0, y=0");
            _status.Items.Add(_lblPos);

            // Split gauche (palette) / droite (canvas + props)
            _splitLeft = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 250,
                FixedPanel = FixedPanel.Panel1
            };

            _tilesetPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            _tilesetPanel.Controls.Add(new Label
            {
                Text = "Palette (à venir)",
                Dock = DockStyle.Top,
                ForeColor = Color.Gainsboro,
                Padding = new Padding(8)
            });

            _splitLeft.Panel1.Controls.Add(_tilesetPanel);

            _splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700,
                FixedPanel = FixedPanel.Panel2
            };

            _canvas = new MapCanvas();
            _canvas.HoveredTileChanged += p => _lblPos.Text = $"x={p.X}, y={p.Y}";
            _splitRight.Panel1.Controls.Add(_canvas);

            var rightPanel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 250
            };

            _layersList = new ListBox { Dock = DockStyle.Fill };
            _layersList.Items.Add("Layers (à venir)");
            rightPanel.Panel1.Controls.Add(_layersList);

            _propGrid = new PropertyGrid { Dock = DockStyle.Fill };
            rightPanel.Panel2.Controls.Add(_propGrid);

            _splitRight.Panel2.Controls.Add(rightPanel);
            _splitLeft.Panel2.Controls.Add(_splitRight);

            Controls.AddRange(new Control[] { _splitLeft, _tool, _status });

            // Map par défaut (10x10)
            _canvas.Map = new Map { Width = 10, Height = 10, Name = "Nouvelle carte" };
            _propGrid.SelectedObject = _canvas.Map;
        }

        private void CreateNewMap()
        {
            using var dlg = new NewMapDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _canvas.Map = new Map
                {
                    Width = dlg.MapWidth,
                    Height = dlg.MapHeight,
                    Name = dlg.MapName
                };
                _propGrid.SelectedObject = _canvas.Map;
                _canvas.Invalidate();
            }
        }
    }

    /// <summary>Boîte de dialogue simple “Nouvelle carte”.</summary>
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
