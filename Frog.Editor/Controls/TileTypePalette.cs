using System;
using System.Windows.Forms;

using Frog.Core.Enums;

namespace Frog.Editor.Controls
{
    /// <summary>
    /// Palette de sélection du TileType (Ground, Block, Warp, Resource).
    /// </summary>
    public sealed class TileTypePalette : UserControl
    {
        private readonly RadioButton _rbGround;
        private readonly RadioButton _rbBlock;
        private readonly RadioButton _rbWarp;
        private readonly RadioButton _rbResource;

        public event Action<TileType>? SelectedTileTypeChanged;

        public TileType SelectedTileType { get; private set; } = TileType.Ground;

        public TileTypePalette()
        {
            AutoSize = true;

            var group = new GroupBox
            {
                Text = "Type de tuile",
                AutoSize = true,
                Dock = DockStyle.Top
            };

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown
            };

            _rbGround = new RadioButton { Text = "Ground", AutoSize = true };
            _rbBlock = new RadioButton { Text = "Block", AutoSize = true };
            _rbWarp = new RadioButton { Text = "Warp", AutoSize = true };
            _rbResource = new RadioButton { Text = "Resource", AutoSize = true };

            _rbGround.CheckedChanged += OnRadioCheckedChanged;
            _rbBlock.CheckedChanged += OnRadioCheckedChanged;
            _rbWarp.CheckedChanged += OnRadioCheckedChanged;
            _rbResource.CheckedChanged += OnRadioCheckedChanged;

            _rbGround.Checked = true;

            panel.Controls.Add(_rbGround);
            panel.Controls.Add(_rbBlock);
            panel.Controls.Add(_rbWarp);
            panel.Controls.Add(_rbResource);

            group.Controls.Add(panel);
            Controls.Add(group);
        }

        private void OnRadioCheckedChanged(object? sender, EventArgs e)
        {
            if (_rbGround.Checked)
                SelectedTileType = TileType.Ground;
            else if (_rbBlock.Checked)
                SelectedTileType = TileType.Block;
            else if (_rbWarp.Checked)
                SelectedTileType = TileType.Warp;
            else if (_rbResource.Checked)
                SelectedTileType = TileType.Resource;

            SelectedTileTypeChanged?.Invoke(SelectedTileType);
        }
    }
}
