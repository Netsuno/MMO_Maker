using System;
using System.Windows.Forms;

using Frog.Editor.Enums;

namespace Frog.Editor.Controls
{
    public sealed class ToolPalette : UserControl
    {
        private readonly RadioButton _rbBrush;
        private readonly RadioButton _rbEraser;
        private readonly RadioButton _rbCursor;

        public event Action<EditorTool>? ToolChanged;

        public EditorTool SelectedTool { get; private set; } = EditorTool.Brush;

        public ToolPalette()
        {
            AutoSize = true;

            var group = new GroupBox
            {
                Text = "Outil",
                AutoSize = true,
                Dock = DockStyle.Top
            };

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown
            };

            _rbBrush = new RadioButton { Text = "Pinceau", AutoSize = true };
            _rbEraser = new RadioButton { Text = "Efface", AutoSize = true };
            _rbCursor = new RadioButton { Text = "Curseur", AutoSize = true };

            _rbBrush.CheckedChanged += OnCheckedChanged;
            _rbEraser.CheckedChanged += OnCheckedChanged;
            _rbCursor.CheckedChanged += OnCheckedChanged;

            _rbBrush.Checked = true;

            panel.Controls.Add(_rbBrush);
            panel.Controls.Add(_rbEraser);
            panel.Controls.Add(_rbCursor);

            group.Controls.Add(panel);
            Controls.Add(group);
        }

        private void OnCheckedChanged(object? sender, EventArgs e)
        {
            if (_rbBrush.Checked)
                SelectedTool = EditorTool.Brush;
            else if (_rbEraser.Checked)
                SelectedTool = EditorTool.Eraser;
            else if (_rbCursor.Checked)
                SelectedTool = EditorTool.Cursor;

            ToolChanged?.Invoke(SelectedTool);
        }
    }
}

