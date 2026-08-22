using Frog.Application.Maps;

namespace Frog.Editor.Dialogs;

/// <summary>Configuration destination d’une tuile warp (carte cible + coordonnées).</summary>
internal sealed class WarpDestinationDialog : Form
{
    private readonly ComboBox _cmbTargetMap;
    private readonly NumericUpDown _numX;
    private readonly NumericUpDown _numY;

    public Guid TargetMapId { get; private set; }

    public int TargetX => (int)_numX.Value;

    public int TargetY => (int)_numY.Value;

    public WarpDestinationDialog(
        IReadOnlyList<MapCatalogEntry> catalog,
        Guid initialTargetMapId,
        int initialX,
        int initialY,
        int mapWidth,
        int mapHeight)
    {
        Text = "Destination warp";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 210);

        _cmbTargetMap = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 280,
            Location = new Point(140, 16),
        };
        foreach (var entry in catalog.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            _cmbTargetMap.Items.Add(new MapComboItem(entry.MapId, entry.Name));
        }

        if (_cmbTargetMap.Items.Count == 0)
        {
            _cmbTargetMap.Items.Add(new MapComboItem(Guid.Empty, "(aucune carte catalogue)"));
        }

        SelectMap(initialTargetMapId);

        _numX = new NumericUpDown
        {
            Minimum = 0,
            Maximum = Math.Max(0, mapWidth - 1),
            Value = Math.Clamp(initialX, 0, Math.Max(0, mapWidth - 1)),
            Width = 80,
            Location = new Point(140, 60),
        };
        _numY = new NumericUpDown
        {
            Minimum = 0,
            Maximum = Math.Max(0, mapHeight - 1),
            Value = Math.Clamp(initialY, 0, Math.Max(0, mapHeight - 1)),
            Width = 80,
            Location = new Point(140, 96),
        };

        Controls.Add(MkLabel("Carte cible", 16, 20));
        Controls.Add(_cmbTargetMap);
        Controls.Add(MkLabel("X destination", 16, 64));
        Controls.Add(_numX);
        Controls.Add(MkLabel("Y destination", 16, 100));
        Controls.Add(_numY);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(260, 150), AutoSize = true };
        var btnCancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(350, 150), AutoSize = true };
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    public bool TryValidate(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_cmbTargetMap.SelectedItem is not MapComboItem item || item.MapId == Guid.Empty)
        {
            errorMessage = "Sélectionnez une carte cible valide dans le catalogue.";
            return false;
        }

        TargetMapId = item.MapId;
        return true;
    }

    private void SelectMap(Guid mapId)
    {
        for (var i = 0; i < _cmbTargetMap.Items.Count; i++)
        {
            if (_cmbTargetMap.Items[i] is MapComboItem item && item.MapId == mapId)
            {
                _cmbTargetMap.SelectedIndex = i;
                return;
            }
        }

        _cmbTargetMap.SelectedIndex = _cmbTargetMap.Items.Count > 0 ? 0 : -1;
    }

    private static Label MkLabel(string text, int x, int y)
        => new() { Text = text, AutoSize = true, Location = new Point(x, y) };

    private sealed record MapComboItem(Guid MapId, string Name)
    {
        public override string ToString() => $"{Name} ({MapId.ToString("N")[..8]})";
    }
}
