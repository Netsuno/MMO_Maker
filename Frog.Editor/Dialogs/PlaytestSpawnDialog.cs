using System.Drawing;
using System.Windows.Forms;

namespace Frog.Editor.Dialogs;

/// <summary>Dialogue minimal pour choisir la tuile de spawn playtest.</summary>
internal sealed class PlaytestSpawnDialog : Form
{
    private readonly NumericUpDown _numX;
    private readonly NumericUpDown _numY;

    public int TileX => (int)_numX.Value;
    public int TileY => (int)_numY.Value;

    public PlaytestSpawnDialog(int mapWidth, int mapHeight, int initialX, int initialY)
    {
        Text = "Playtest — position de départ";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 160);
        var maxX = Math.Max(0, mapWidth - 1);
        var maxY = Math.Max(0, mapHeight - 1);
        initialX = Math.Clamp(initialX, 0, maxX);
        initialY = Math.Clamp(initialY, 0, maxY);

        var lbl = new Label
        {
            AutoSize = true,
            Location = new Point(12, 12),
            Text = $"Tuile de spawn (carte {mapWidth}×{mapHeight}) :",
        };
        Controls.Add(lbl);

        Controls.Add(new Label { Text = "X", AutoSize = true, Location = new Point(12, 48) });
        _numX = new NumericUpDown
        {
            Minimum = 0,
            Maximum = maxX,
            Value = initialX,
            Location = new Point(40, 44),
            Width = 80,
        };
        Controls.Add(_numX);

        Controls.Add(new Label { Text = "Y", AutoSize = true, Location = new Point(140, 48) });
        _numY = new NumericUpDown
        {
            Minimum = 0,
            Maximum = maxY,
            Value = initialY,
            Location = new Point(168, 44),
            Width = 80,
        };
        Controls.Add(_numY);

        var ok = new Button { Text = "Playtest", DialogResult = DialogResult.OK, Location = new Point(160, 110) };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(250, 110) };
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
