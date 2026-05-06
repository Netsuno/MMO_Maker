using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>Consultation lecture seule du catalogue d’événements et des placements sur une <c>frog_map</c>.</summary>
internal sealed class MapEventsBrowseDialog : Form
{
    private readonly string _connectionString;
    private readonly NumericUpDown _numMapId = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 100 };
    private readonly Button _btnReload = new() { Text = "Charger", AutoSize = true };
    private readonly ListView _lvCatalog = new()
    {
        View = View.Details,
        FullRowSelect = true,
        Dock = DockStyle.Fill,
    };

    private readonly ListView _lvPlacements = new()
    {
        View = View.Details,
        FullRowSelect = true,
        Dock = DockStyle.Fill,
    };

    public MapEventsBrowseDialog(string connectionString, int initialMapId = 1)
    {
        _connectionString = connectionString;
        Text = "Événements carte (MariaDB)";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(880, 520);
        _numMapId.Value = Math.Clamp(initialMapId, 1, int.MaxValue);

        _lvCatalog.Columns.Add("id", 50);
        _lvCatalog.Columns.Add("slug", 160);
        _lvCatalog.Columns.Add("display_name", 420);

        _lvPlacements.Columns.Add("id", 70);
        _lvPlacements.Columns.Add("map_id", 60);
        _lvPlacements.Columns.Add("catalog_id", 70);
        _lvPlacements.Columns.Add("tile_x", 60);
        _lvPlacements.Columns.Add("tile_y", 60);
        _lvPlacements.Columns.Add("slug", 140);
        _lvPlacements.Columns.Add("display_name", 220);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = false,
        };
        top.Controls.Add(new Label { Text = "frog_map.id", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        top.Controls.Add(_numMapId);
        top.Controls.Add(_btnReload);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 240,
        };

        var catPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        catPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        catPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        catPanel.Controls.Add(new Label { Text = "frog_event_catalog", Dock = DockStyle.Fill, AutoSize = true }, 0, 0);
        catPanel.Controls.Add(_lvCatalog, 0, 1);
        split.Panel1.Controls.Add(catPanel);

        var placePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        placePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        placePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        placePanel.Controls.Add(new Label { Text = "frog_map_event (filtre map_id)", Dock = DockStyle.Fill, AutoSize = true }, 0, 0);
        placePanel.Controls.Add(_lvPlacements, 0, 1);
        split.Panel2.Controls.Add(placePanel);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var btnClose = new Button { Text = "Fermer", DialogResult = DialogResult.Cancel, AutoSize = true };
        bottom.Controls.Add(btnClose);

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(bottom);
        CancelButton = btnClose;

        _btnReload.Click += (_, _) => ReloadSafe();
        Shown += (_, _) => ReloadSafe();
    }

    private void ReloadSafe()
    {
        try
        {
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Reload()
    {
        _lvCatalog.BeginUpdate();
        _lvPlacements.BeginUpdate();
        try
        {
            _lvCatalog.Items.Clear();
            foreach (var row in MapEventsMariaDbReader.LoadCatalog(_connectionString))
            {
                var item = new ListViewItem(row.Id.ToString());
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.DisplayName);
                _lvCatalog.Items.Add(item);
            }

            _lvPlacements.Items.Clear();
            var mapId = (int)_numMapId.Value;
            foreach (var row in MapEventsMariaDbReader.LoadPlacementsForMap(_connectionString, mapId))
            {
                var item = new ListViewItem(row.Id.ToString());
                item.SubItems.Add(row.MapId.ToString());
                item.SubItems.Add(row.EventCatalogId.ToString());
                item.SubItems.Add(row.TileX.ToString());
                item.SubItems.Add(row.TileY.ToString());
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.DisplayName);
                _lvPlacements.Items.Add(item);
            }
        }
        finally
        {
            _lvCatalog.EndUpdate();
            _lvPlacements.EndUpdate();
        }
    }
}
