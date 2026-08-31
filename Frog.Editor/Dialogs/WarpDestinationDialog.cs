using Frog.Application.Maps;

namespace Frog.Editor.Dialogs;

/// <summary>Configuration destination d’une tuile warp (carte cible + coordonnées).</summary>
internal sealed class WarpDestinationDialog : Form
{
    private readonly IReadOnlyList<MapCatalogEntry> _catalog;
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
        _catalog = catalog;
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
            _cmbTargetMap.Items.Add(new MapComboItem(entry.MapId, entry.Name, entry.Width, entry.Height));
        }

        if (_cmbTargetMap.Items.Count == 0)
        {
            _cmbTargetMap.Items.Add(new MapComboItem(Guid.Empty, "(aucune carte catalogue)", 0, 0));
        }

        _cmbTargetMap.SelectedIndexChanged += (_, _) => ApplyBoundsForSelectedTarget(initialX, initialY);

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

        SelectMap(initialTargetMapId);
        ApplyBoundsForSelectedTarget(initialX, initialY);
    }

    public bool TryValidate(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_cmbTargetMap.SelectedItem is not MapComboItem item || item.MapId == Guid.Empty)
        {
            errorMessage = "Sélectionnez une carte cible valide dans le catalogue.";
            return false;
        }

        if (!_catalog.Any(e => e.MapId == item.MapId))
        {
            errorMessage = "La carte cible sélectionnée n’existe plus dans le catalogue.";
            return false;
        }

        if (TargetX < 0 || TargetY < 0 || TargetX >= item.Width || TargetY >= item.Height)
        {
            errorMessage =
                $"Destination ({TargetX}, {TargetY}) hors limites de la carte cible ({item.Width}×{item.Height}).";
            return false;
        }

        TargetMapId = item.MapId;
        return true;
    }

    private void ApplyBoundsForSelectedTarget(int preferredX, int preferredY)
    {
        if (_cmbTargetMap.SelectedItem is not MapComboItem item || item.MapId == Guid.Empty || item.Width <= 0 || item.Height <= 0)
        {
            _numX.Maximum = 0;
            _numY.Maximum = 0;
            _numX.Value = 0;
            _numY.Value = 0;
            return;
        }

        _numX.Maximum = Math.Max(0, item.Width - 1);
        _numY.Maximum = Math.Max(0, item.Height - 1);
        _numX.Value = Math.Clamp(preferredX, 0, (int)_numX.Maximum);
        _numY.Value = Math.Clamp(preferredY, 0, (int)_numY.Maximum);
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

    private sealed record MapComboItem(Guid MapId, string Name, int Width, int Height)
    {
        public override string ToString() => $"{Name} ({MapId.ToString("N")[..8]}) {Width}×{Height}";
    }
}
