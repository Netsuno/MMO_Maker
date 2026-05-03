using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Core.Enums;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>Palette TileType façon MZ : groupe compact lisible.</summary>
public sealed class TileTypePalette : UserControl
{
    private readonly RadioButton _rbGround;
    private readonly RadioButton _rbBlock;
    private readonly RadioButton _rbWarp;
    private readonly RadioButton _rbResource;
    private readonly RadioButton _rbScript;

    public event Action<TileType>? SelectedTileTypeChanged;

    public TileType SelectedTileType { get; private set; } = TileType.Ground;

    public TileTypePalette()
    {
        AutoSize = true;
        Dock = DockStyle.Top;
        BackColor = EditorChrome.SidebarBg;
        Padding = new Padding(10, 0, 10, 8);

        var col = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
        };

        var title = EditorChrome.BuildSectionCaption("TYPE DE TUILE");
        title.Margin = new Padding(0, 0, 0, 8);

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
        };

        _rbGround = new RadioButton { Text = "  Terrain" };
        _rbBlock = new RadioButton { Text = "  Blocage" };
        _rbWarp = new RadioButton { Text = "  Warp" };
        _rbResource = new RadioButton { Text = "  Ressource" };
        _rbScript = new RadioButton { Text = "  Script" };

        foreach (RadioButton rb in new RadioButton[] { _rbGround, _rbBlock, _rbWarp, _rbResource, _rbScript })
        {
            EditorChrome.StyleSidebarRadio(rb);
            rb.CheckedChanged += OnRadioCheckedChanged;
            row.Controls.Add(rb);
        }

        _rbGround.Checked = true;

        col.Controls.Add(title);
        col.Controls.Add(row);
        Controls.Add(col);
    }

    private void OnRadioCheckedChanged(object? sender, EventArgs e)
    {
        if (_rbGround.Checked)
        {
            SelectedTileType = TileType.Ground;
        }
        else if (_rbBlock.Checked)
        {
            SelectedTileType = TileType.Block;
        }
        else if (_rbWarp.Checked)
        {
            SelectedTileType = TileType.Warp;
        }
        else if (_rbResource.Checked)
        {
            SelectedTileType = TileType.Resource;
        }
        else if (_rbScript.Checked)
        {
            SelectedTileType = TileType.Script;
        }

        SelectedTileTypeChanged?.Invoke(SelectedTileType);
    }
}
