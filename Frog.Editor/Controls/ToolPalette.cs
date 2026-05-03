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
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = EditorChrome.SidebarBg,
        };

        var title = EditorChrome.BuildSectionCaption("OUTIL");
        title.Margin = new Padding(0, 0, 0, 8);

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = EditorChrome.SidebarBg,
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
        if (sender is not RadioButton { Checked: true } rb)
        {
            return;
        }

        SelectedTool = rb == _rbBrush ? EditorTool.Brush
            : rb == _rbEraser ? EditorTool.Eraser
            : rb == _rbCursor ? EditorTool.Cursor
            : rb == _rbFill ? EditorTool.Fill
            : rb == _rbRectangle ? EditorTool.Rectangle
            : rb == _rbSelection ? EditorTool.Selection
            : SelectedTool;

        ToolChanged?.Invoke(SelectedTool);
    }
}
