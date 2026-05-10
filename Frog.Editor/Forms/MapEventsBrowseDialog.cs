using System.Collections.Generic;
using Frog.Core.Protocol;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>Consultation et écriture MVP des événements <c>frog_map_event</c> / catalogue.</summary>
internal sealed class MapEventsBrowseDialog : Form
{
    private readonly string _connectionString;
    private readonly NumericUpDown _numMapId = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 100 };
    private readonly NumericUpDown _numTileX = new() { Minimum = int.MinValue, Maximum = int.MaxValue, Width = 90 };
    private readonly NumericUpDown _numTileY = new() { Minimum = int.MinValue, Maximum = int.MaxValue, Width = 90 };
    private readonly Button _btnReload = new() { Text = "Charger", AutoSize = true };
    private readonly Button _btnPlace = new() { Text = "Placer sur carte", AutoSize = true };
    private readonly Button _btnDeleteSelected = new() { Text = "Supprimer ligne", AutoSize = true };
    private readonly ComboBox _cbTrigger = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 480 };
    private readonly Button _btnApplyTrigger = new() { Text = "Appliquer déclencheur à la ligne", AutoSize = true };
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

    private readonly TextBox _txtNewSlug = new() { Width = 160, PlaceholderText = "ex. pnj_marchand" };
    private readonly TextBox _txtNewDisplay = new() { Width = 260, PlaceholderText = "Nom affiché" };
    private readonly TextBox _txtNewScriptKey = new() { Width = 160, PlaceholderText = "script_key (opt.)" };
    private readonly Button _btnAddCatalog = new() { Text = "Ajouter au catalogue", AutoSize = true };
    private readonly Button _btnDeleteCatalogRow = new() { Text = "Supprimer entrée catalogue", AutoSize = true };
    private readonly Button _btnApplyScriptKey = new() { Text = "Appliquer script_key (ligne catalogue)", AutoSize = true };
    private readonly TextBox _txtFilterCatalog = new() { Width = 220, PlaceholderText = "Filtrer catalogue…" };
    private readonly TextBox _txtFilterPlacements = new() { Width = 220, PlaceholderText = "Filtrer placements…" };
    private readonly List<EventCatalogRow> _catalogRows = new();
    private readonly List<MapEventPlacementRow> _placementRows = new();

    public MapEventsBrowseDialog(string connectionString, int initialMapId = 1, int defaultTileX = 0, int defaultTileY = 0)
    {
        _connectionString = connectionString;
        Text = "Événements carte (MariaDB)";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(920, 600);
        _numMapId.Value = Math.Clamp(initialMapId, 1, int.MaxValue);
        _numTileX.Value = defaultTileX;
        _numTileY.Value = defaultTileY;

        _lvCatalog.Columns.Add("id", 50);
        _lvCatalog.Columns.Add("slug", 160);
        _lvCatalog.Columns.Add("display_name", 280);
        _lvCatalog.Columns.Add("script_key", 200);

        _lvPlacements.Columns.Add("id", 70);
        _lvPlacements.Columns.Add("map_id", 60);
        _lvPlacements.Columns.Add("catalog_id", 70);
        _lvPlacements.Columns.Add("tile_x", 60);
        _lvPlacements.Columns.Add("tile_y", 60);
        _lvPlacements.Columns.Add("slug", 140);
        _lvPlacements.Columns.Add("display_name", 200);
        _lvPlacements.Columns.Add("trigger_kind", 110);

        _cbTrigger.Items.AddRange(new object[]
        {
            "interact — action « Interagir » (touche E)",
            "step_on — à l’arrivée sur la tuile (marche)",
            "page — une fois par entrée sur la carte (tuile d’arrivée)",
            "auto_tile — sur place : Heartbeat serveur (cooldown par placement)",
        });
        _cbTrigger.SelectedIndex = 0;

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

        var catPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        catPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        catPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        catPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        catPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        catPanel.Controls.Add(new Label { Text = "frog_event_catalog (sélection = type à placer sur la carte)", Dock = DockStyle.Fill, AutoSize = true }, 0, 0);
        var catFilterRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 4),
        };
        catFilterRow.Controls.Add(new Label { Text = "Filtre", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        catFilterRow.Controls.Add(_txtFilterCatalog);
        catPanel.Controls.Add(catFilterRow, 0, 1);
        var catNewRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 2, 0, 6),
        };
        catNewRow.Controls.Add(new Label { Text = "Nouveau type", AutoSize = true, Margin = new Padding(0, 10, 4, 0) });
        catNewRow.Controls.Add(_txtNewSlug);
        catNewRow.Controls.Add(new Label { Text = "Nom", AutoSize = true, Margin = new Padding(8, 10, 4, 0) });
        catNewRow.Controls.Add(_txtNewDisplay);
        catNewRow.Controls.Add(new Label { Text = "script_key", AutoSize = true, Margin = new Padding(8, 10, 4, 0) });
        catNewRow.Controls.Add(_txtNewScriptKey);
        catNewRow.Controls.Add(_btnAddCatalog);
        catNewRow.Controls.Add(_btnDeleteCatalogRow);
        catNewRow.Controls.Add(_btnApplyScriptKey);
        catPanel.Controls.Add(catNewRow, 0, 2);
        catPanel.Controls.Add(_lvCatalog, 0, 3);
        split.Panel1.Controls.Add(catPanel);

        var placeOuter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        placeOuter.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        placeOuter.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        placeOuter.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var placeToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0),
        };
        placeToolbar.Controls.Add(new Label { Text = "Tuile X", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        placeToolbar.Controls.Add(_numTileX);
        placeToolbar.Controls.Add(new Label { Text = "Y", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
        placeToolbar.Controls.Add(_numTileY);
        placeToolbar.Controls.Add(new Label { Text = "Déclencheur", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        placeToolbar.Controls.Add(_cbTrigger);
        placeToolbar.Controls.Add(_btnPlace);
        placeToolbar.Controls.Add(_btnDeleteSelected);
        placeToolbar.Controls.Add(_btnApplyTrigger);
        placeOuter.Controls.Add(placeToolbar, 0, 0);
        var placeMid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 4),
        };
        placeMid.Controls.Add(new Label { Text = "frog_map_event · ", AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
        placeMid.Controls.Add(new Label { Text = "Filtre", AutoSize = true, Margin = new Padding(8, 4, 4, 0) });
        placeMid.Controls.Add(_txtFilterPlacements);
        placeOuter.Controls.Add(placeMid, 0, 1);
        placeOuter.Controls.Add(_lvPlacements, 0, 2);
        split.Panel2.Controls.Add(placeOuter);

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
        _btnPlace.Click += (_, _) => PlaceSafe();
        _btnDeleteSelected.Click += (_, _) => DeleteSelectedSafe();
        _btnApplyTrigger.Click += (_, _) => ApplyTriggerSafe();
        _btnAddCatalog.Click += (_, _) => AddCatalogSafe();
        _btnDeleteCatalogRow.Click += (_, _) => DeleteCatalogRowSafe();
        _btnApplyScriptKey.Click += (_, _) => ApplyScriptKeySafe();
        _txtFilterCatalog.TextChanged += (_, _) => RefreshFilteredLists();
        _txtFilterPlacements.TextChanged += (_, _) => RefreshFilteredLists();
        Shown += (_, _) => ReloadSafe();
    }

    private void AddCatalogSafe()
    {
        try
        {
            if (!MapEventsMariaDbWriter.TryInsertCatalog(
                    _connectionString,
                    _txtNewSlug.Text,
                    _txtNewDisplay.Text,
                    _txtNewScriptKey.Text,
                    out var newId,
                    out var err))
            {
                MessageBox.Show(this, err, "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(this, $"Entrée catalogue créée (id={newId}).", "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNewSlug.Clear();
            _txtNewDisplay.Clear();
            _txtNewScriptKey.Clear();
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteCatalogRowSafe()
    {
        try
        {
            if (!TryGetSingleSelectedFirstColumnInt(_lvCatalog, out var catalogId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne du catalogue (colonne id).", "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ok = MessageBox.Show(
                this,
                $"Supprimer l’entrée catalogue id={catalogId} ?\nLes placements frog_map_event liés seront supprimés (cascade).",
                "Confirmer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (ok != DialogResult.Yes)
            {
                return;
            }

            if (!MapEventsMariaDbWriter.TryDeleteCatalogById(_connectionString, catalogId, out var err))
            {
                MessageBox.Show(this, err, "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PlaceSafe()
    {
        try
        {
            if (!TryGetSingleSelectedFirstColumnInt(_lvCatalog, out var catalogId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne dans le catalogue (slug/type).", "Placer événement", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var mapId = (int)_numMapId.Value;
            var tx = (int)_numTileX.Value;
            var ty = (int)_numTileY.Value;
            if (!MapEventsMariaDbWriter.TryInsertPlacement(_connectionString, mapId, catalogId, tx, ty, GetTriggerKindFromUi(), out var err))
            {
                MessageBox.Show(this, string.IsNullOrEmpty(err) ? "Placement déjà présent pour cette carte, tuile et type (INSERT IGNORE)." : err, "Placer événement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyTriggerSafe()
    {
        try
        {
            if (!TryGetSingleSelectedFirstColumnLong(_lvPlacements, out var rowId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne de placement.", "Déclencheur", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var mapId = (int)_numMapId.Value;
            if (!MapEventsMariaDbWriter.TryUpdatePlacementTriggerKind(_connectionString, rowId, mapId, GetTriggerKindFromUi(), out var err))
            {
                MessageBox.Show(this, err, "Déclencheur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string GetTriggerKindFromUi() =>
        _cbTrigger.SelectedIndex switch
        {
            1 => MapEventTriggerKinds.StepOn,
            2 => MapEventTriggerKinds.Page,
            3 => MapEventTriggerKinds.AutoTile,
            _ => MapEventTriggerKinds.Interact,
        };

    private void DeleteSelectedSafe()
    {
        try
        {
            if (!TryGetSingleSelectedFirstColumnLong(_lvPlacements, out var rowId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne de placement.", "Supprimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var mapId = (int)_numMapId.Value;
            var ok = MessageBox.Show(
                this,
                $"Supprimer l’événement placement id={rowId} sur frog_map.id={mapId} ?",
                "Confirmer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (ok != DialogResult.Yes)
            {
                return;
            }

            if (!MapEventsMariaDbWriter.TryDeletePlacement(_connectionString, rowId, mapId, out var err))
            {
                MessageBox.Show(this, err, "Supprimer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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

    private void ApplyScriptKeySafe()
    {
        try
        {
            if (!TryGetSingleSelectedFirstColumnInt(_lvCatalog, out var catalogId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne du catalogue (colonne id).", "script_key", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!MapEventsMariaDbWriter.TryUpdateCatalogScriptKey(_connectionString, catalogId, _txtNewScriptKey.Text, out var err))
            {
                MessageBox.Show(this, err, "script_key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Reload()
    {
        _catalogRows.Clear();
        foreach (var row in MapEventsMariaDbReader.LoadCatalog(_connectionString))
        {
            _catalogRows.Add(row);
        }

        _placementRows.Clear();
        var mapId = (int)_numMapId.Value;
        foreach (var row in MapEventsMariaDbReader.LoadPlacementsForMap(_connectionString, mapId))
        {
            _placementRows.Add(row);
        }

        RefreshFilteredLists();
    }

    private void RefreshFilteredLists()
    {
        var cf = _txtFilterCatalog.Text.Trim();
        var pf = _txtFilterPlacements.Text.Trim();
        var o = StringComparison.OrdinalIgnoreCase;
        _lvCatalog.BeginUpdate();
        _lvPlacements.BeginUpdate();
        try
        {
            _lvCatalog.Items.Clear();
            foreach (var row in _catalogRows)
            {
                if (cf.Length > 0)
                {
                    var sk = row.ScriptKey ?? string.Empty;
                    if (!row.Slug.Contains(cf, o) && !row.DisplayName.Contains(cf, o) && !sk.Contains(cf, o))
                    {
                        continue;
                    }
                }

                var item = new ListViewItem(row.Id.ToString());
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.DisplayName);
                item.SubItems.Add(row.ScriptKey ?? string.Empty);
                _lvCatalog.Items.Add(item);
            }

            _lvPlacements.Items.Clear();
            foreach (var row in _placementRows)
            {
                if (pf.Length > 0)
                {
                    var blob =
                        $"{row.Id} {row.MapId} {row.EventCatalogId} {row.TileX} {row.TileY} {row.Slug} {row.DisplayName} {row.TriggerKind}";
                    if (!blob.Contains(pf, o))
                    {
                        continue;
                    }
                }

                var item = new ListViewItem(row.Id.ToString());
                item.SubItems.Add(row.MapId.ToString());
                item.SubItems.Add(row.EventCatalogId.ToString());
                item.SubItems.Add(row.TileX.ToString());
                item.SubItems.Add(row.TileY.ToString());
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.DisplayName);
                item.SubItems.Add(row.TriggerKind);
                _lvPlacements.Items.Add(item);
            }
        }
        finally
        {
            _lvCatalog.EndUpdate();
            _lvPlacements.EndUpdate();
        }
    }

    private static bool TryGetSingleSelectedFirstColumnInt(ListView lv, out int value)
    {
        value = 0;
        if (lv.SelectedItems.Count != 1)
        {
            return false;
        }

        var t = lv.SelectedItems[0].Text;
        return int.TryParse(t, out value) && value > 0;
    }

    private static bool TryGetSingleSelectedFirstColumnLong(ListView lv, out long value)
    {
        value = 0;
        if (lv.SelectedItems.Count != 1)
        {
            return false;
        }

        var t = lv.SelectedItems[0].Text;
        return long.TryParse(t, out value) && value > 0;
    }
}
