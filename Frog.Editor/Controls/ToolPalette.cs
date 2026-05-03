using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Editor.Enums;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

public sealed class ToolPalette : UserControl
{
    private readonly RadioButton _rbBrush;
    private readonly RadioButton _rbEraser;
    private readonly RadioButton _rbCursor;
    private readonly RadioButton _rbFill;
    private readonly RadioButton _rbRectangle;
    private readonly RadioButton _rbSelection;

    public event Action<EditorTool>? ToolChanged;

    public EditorTool SelectedTool { get; private set; } = EditorTool.Brush;

    public ToolPalette()
    {
        AutoSize = true;
        Dock = DockStyle.Top;
        BackColor = EditorChrome.SidebarBg;
        Padding = new Padding(10, 14, 10, 8);

        var col = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
        };

        var title = EditorChrome.BuildSectionCaption("OUTIL");
        title.Margin = new Padding(0, 0, 0, 8);

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
        };

        _rbBrush = new RadioButton { Text = "  Pinceau" };
        _rbEraser = new RadioButton { Text = "  Gomme" };
        _rbCursor = new RadioButton { Text = "  Curseur" };
        _rbFill = new RadioButton { Text = "  Pot" };
        _rbRectangle = new RadioButton { Text = "  Rectangle" };
        _rbSelection = new RadioButton { Text = "  Sélection" };

        foreach (RadioButton rb in new RadioButton[]
                 { _rbBrush, _rbEraser, _rbCursor, _rbFill, _rbRectangle, _rbSelection })
        {
            EditorChrome.StyleSidebarRadio(rb);
            rb.CheckedChanged += OnCheckedChanged;
            row.Controls.Add(rb);
        }

        _rbBrush.Checked = true;

        col.Controls.Add(title);
        col.Controls.Add(row);
        Controls.Add(col);
    }

    private void OnCheckedChanged(object? sender, EventArgs e)
    {
        if (_rbBrush.Checked)
        {
            SelectedTool = EditorTool.Brush;
        }
        else if (_rbEraser.Checked)
        {
            SelectedTool = EditorTool.Eraser;
        }
        else if (_rbCursor.Checked)
        {
            SelectedTool = EditorTool.Cursor;
        }
        else if (_rbFill.Checked)
        {
            SelectedTool = EditorTool.Fill;
        }
        else if (_rbRectangle.Checked)
        {
            SelectedTool = EditorTool.Rectangle;
        }
        else if (_rbSelection.Checked)
        {
            SelectedTool = EditorTool.Selection;
        }

        ToolChanged?.Invoke(SelectedTool);
    }
}
