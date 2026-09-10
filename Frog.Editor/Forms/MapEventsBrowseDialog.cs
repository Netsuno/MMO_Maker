using System.Collections.Generic;
using Frog.Core.Events;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>Consultation et écriture MVP des événements carte / catalogue (PostgreSQL Phase 8).</summary>
internal sealed class MapEventsBrowseDialog : Form
{
    private readonly MapEventsPostgreSqlService _service;
    private readonly Label _lblMapId = new() { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
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
    private readonly Button _btnAddCatalog = new() { Text = "Ajouter au catalogue", AutoSize = true };
    private readonly Button _btnDeleteCatalogRow = new() { Text = "Supprimer entrée catalogue", AutoSize = true };
    private readonly Button _btnEditPages = new() { Text = "Éditer pages…", AutoSize = true };
    private readonly TextBox _txtFilterCatalog = new() { Width = 220, PlaceholderText = "Filtrer catalogue…" };
    private readonly TextBox _txtFilterPlacements = new() { Width = 220, PlaceholderText = "Filtrer placements…" };
    private readonly List<PgEventCatalogRow> _catalogRows = new();
    private readonly List<PgMapEventPlacementRow> _placementRows = new();
    private Guid _mapId;

    public MapEventsBrowseDialog(MapEventsPostgreSqlService service, Guid mapId, int defaultTileX = 0, int defaultTileY = 0)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _mapId = mapId;
        Text = "Événements carte (PostgreSQL)";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(920, 600);
        _lblMapId.Text = FormatMapId(mapId);
        _numTileX.Value = defaultTileX;
        _numTileY.Value = defaultTileY;

        _lvCatalog.Columns.Add("event_id", 220);
        _lvCatalog.Columns.Add("slug", 160);
        _lvCatalog.Columns.Add("name", 220);
        _lvCatalog.Columns.Add("status", 80);
        _lvCatalog.Columns.Add("pages", 50);

        _lvPlacements.Columns.Add("placement_id", 220);
        _lvPlacements.Columns.Add("map_id", 220);
        _lvPlacements.Columns.Add("event_id", 220);
        _lvPlacements.Columns.Add("tile_x", 60);
        _lvPlacements.Columns.Add("tile_y", 60);
        _lvPlacements.Columns.Add("slug", 140);
        _lvPlacements.Columns.Add("name", 160);
        _lvPlacements.Columns.Add("trigger_kind", 120);

        _cbTrigger.Items.AddRange(new object[]
        {
            "action — interaction joueur (touche E)",
            "player_contact — contact sur la tuile (marche)",
            "autorun — exécution automatique à l'activation",
            "parallel — exécution parallèle tant que la page est active",
        });
        _cbTrigger.SelectedIndex = 0;

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = false,
        };
        top.Controls.Add(new Label { Text = "Carte", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        top.Controls.Add(_lblMapId);
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
        catPanel.Controls.Add(new Label { Text = "Catalogue événements (sélection = type à placer sur la carte)", Dock = DockStyle.Fill, AutoSize = true }, 0, 0);
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
        catNewRow.Controls.Add(_btnAddCatalog);
        catNewRow.Controls.Add(_btnEditPages);
        catNewRow.Controls.Add(_btnDeleteCatalogRow);
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
        placeMid.Controls.Add(new Label { Text = "Placements carte · ", AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
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
        _btnEditPages.Click += (_, _) => EditPagesSafe();
        _btnDeleteCatalogRow.Click += (_, _) => DeleteCatalogRowSafe();
        _txtFilterCatalog.TextChanged += (_, _) => RefreshFilteredLists();
        _txtFilterPlacements.TextChanged += (_, _) => RefreshFilteredLists();
        Shown += (_, _) => ReloadSafe();
    }

    public void SetMapId(Guid mapId)
    {
        _mapId = mapId;
        _lblMapId.Text = FormatMapId(mapId);
    }

    private static string FormatMapId(Guid mapId) =>
        mapId == Guid.Empty ? "(aucune carte catalogue)" : mapId.ToString("D");

    private void EditPagesSafe()
    {
        try
        {
            if (!TryGetSingleSelectedGuid(_lvCatalog, out var eventId))
            {
                MessageBox.Show(this, "Sélectionnez une entrée catalogue.", "Pages", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = _catalogRows.FirstOrDefault(r => r.EventId == eventId);
            var name = row.DisplayName ?? eventId.ToString("D");
            using var dlg = new MapEventPageEditorDialog(_service, eventId, name);
            dlg.ShowDialog(this);
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Pages", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddCatalogSafe()
    {
        try
        {
            if (!_service.TryInsertCatalog(_txtNewSlug.Text, _txtNewDisplay.Text, out var newId, out var err))
            {
                MessageBox.Show(this, err, "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(this, $"Entrée catalogue créée ({newId:D}).", "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNewSlug.Clear();
            _txtNewDisplay.Clear();
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteCatalogRowSafe()
    {
        try
        {
            if (!TryGetSingleSelectedGuid(_lvCatalog, out var eventId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne du catalogue (colonne event_id).", "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ok = MessageBox.Show(
                this,
                $"Supprimer l'entrée catalogue {eventId:D} ?\nLes placements liés seront supprimés (cascade).",
                "Confirmer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (ok != DialogResult.Yes)
            {
                return;
            }

            if (!_service.TryDeleteCatalogById(eventId, out var err))
            {
                MessageBox.Show(this, err, "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PlaceSafe()
    {
        try
        {
            if (_mapId == Guid.Empty)
            {
                MessageBox.Show(this, "Ouvrez ou enregistrez une carte dans le catalogue PostgreSQL.", "Placer événement", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryGetSingleSelectedGuid(_lvCatalog, out var eventId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne dans le catalogue (slug/type).", "Placer événement", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tx = (int)_numTileX.Value;
            var ty = (int)_numTileY.Value;
            if (!_service.TryInsertPlacement(_mapId, eventId, tx, ty, GetTriggerKindFromUi(), out var err))
            {
                MessageBox.Show(this, err, "Placer événement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyTriggerSafe()
    {
        try
        {
            if (!TryGetSingleSelectedGuid(_lvPlacements, out var placementId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne de placement.", "Déclencheur", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_mapId == Guid.Empty)
            {
                MessageBox.Show(this, "Carte catalogue invalide.", "Déclencheur", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!_service.TryUpdatePlacementTriggerKind(placementId, _mapId, GetTriggerKindFromUi(), out var err))
            {
                MessageBox.Show(this, err, "Déclencheur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string GetTriggerKindFromUi() =>
        _cbTrigger.SelectedIndex switch
        {
            1 => Phase8MapEventTriggerKinds.PlayerContact,
            2 => Phase8MapEventTriggerKinds.Autorun,
            3 => Phase8MapEventTriggerKinds.Parallel,
            _ => Phase8MapEventTriggerKinds.Action,
        };

    private void DeleteSelectedSafe()
    {
        try
        {
            if (!TryGetSingleSelectedGuid(_lvPlacements, out var placementId))
            {
                MessageBox.Show(this, "Sélectionnez une ligne de placement.", "Supprimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_mapId == Guid.Empty)
            {
                MessageBox.Show(this, "Carte catalogue invalide.", "Supprimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ok = MessageBox.Show(
                this,
                $"Supprimer le placement {placementId:D} sur la carte {_mapId:D} ?",
                "Confirmer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (ok != DialogResult.Yes)
            {
                return;
            }

            if (!_service.TryDeletePlacement(placementId, _mapId, out var err))
            {
                MessageBox.Show(this, err, "Supprimer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(this, ex.Message, "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Reload()
    {
        _catalogRows.Clear();
        foreach (var row in _service.LoadCatalog())
        {
            _catalogRows.Add(row);
        }

        _placementRows.Clear();
        if (_mapId != Guid.Empty)
        {
            foreach (var row in _service.LoadPlacementsForMap(_mapId))
            {
                _placementRows.Add(row);
            }
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
                    if (!row.Slug.Contains(cf, o) && !row.DisplayName.Contains(cf, o) && !row.EventId.ToString("D").Contains(cf, o))
                    {
                        continue;
                    }
                }

                var item = new ListViewItem(row.EventId.ToString("D"));
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.DisplayName);
                item.SubItems.Add(row.Status.ToString());
                item.SubItems.Add(row.PageCount.ToString());
                _lvCatalog.Items.Add(item);
            }

            _lvPlacements.Items.Clear();
            foreach (var row in _placementRows)
            {
                if (pf.Length > 0)
                {
                    var blob =
                        $"{row.Id} {row.MapId} {row.EventDefinitionId} {row.TileX} {row.TileY} {row.Slug} {row.DisplayName} {row.TriggerKind}";
                    if (!blob.Contains(pf, o))
                    {
                        continue;
                    }
                }

                var item = new ListViewItem(row.Id.ToString("D"));
                item.SubItems.Add(row.MapId.ToString("D"));
                item.SubItems.Add(row.EventDefinitionId.ToString("D"));
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

    private static bool TryGetSingleSelectedGuid(ListView lv, out Guid value)
    {
        value = Guid.Empty;
        if (lv.SelectedItems.Count != 1)
        {
            return false;
        }

        return Guid.TryParse(lv.SelectedItems[0].Text, out value) && value != Guid.Empty;
    }
}
