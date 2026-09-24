using System.Collections.Generic;
using System.Drawing;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Editor.Services;
using Frog.Editor.Ui;

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
        MultiSelect = false,
        HideSelection = false,
        Dock = DockStyle.Fill,
    };

    private readonly CheckBox _chkShowNames = new()
    {
        Text = "Afficher noms des événements",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(16, 2, 0, 0),
    };

    private readonly TextBox _txtNewSlug = new() { Width = 160, PlaceholderText = "ex. pnj_marchand" };
    private readonly TextBox _txtNewDisplay = new() { Width = 260, PlaceholderText = "Nom affiché" };
    private readonly Button _btnAddCatalog = new() { Text = "Ajouter au catalogue", AutoSize = true };
    private readonly Button _btnDeleteCatalogRow = new() { Text = "Supprimer entrée catalogue", AutoSize = true };
    private readonly Button _btnEditPages = new() { Text = "Éditer pages…", AutoSize = true };
    private readonly Button _btnQuickNpc = new() { Text = "PNJ rapide…", AutoSize = true };
    private readonly TextBox _txtFilterCatalog = new() { Width = 220, PlaceholderText = "Filtrer le catalogue…" };
    private readonly TextBox _txtFilterPlacements = new() { Width = 220, PlaceholderText = "Filtrer les événements…" };
    private readonly Label _lblMarkerLegend = new()
    {
        Text = "Losanges : A action · C contact · ! automatique · P parallèle",
        AutoSize = true,
        Margin = new Padding(12, 4, 0, 0),
    };
    private readonly List<PgEventCatalogRow> _catalogRows = new();
    private readonly List<PgMapEventPlacementRow> _placementRows = new();
    private Guid _mapId;
    private bool _suppressHighlight;
    private bool _suppressNamesEvent;

    /// <summary>Placements rechargés (pose, suppression, déclencheur, changement de carte).</summary>
    internal event Action? PlacementsChanged;

    /// <summary>L’utilisateur a sélectionné une ligne de placement dans la liste.</summary>
    internal event Action<PgMapEventPlacementRow>? PlacementHighlighted;

    /// <summary>Case « Afficher noms des événements ».</summary>
    internal event Action<bool>? ShowEventNamesChanged;

    public MapEventsBrowseDialog(MapEventsPostgreSqlService service, Guid mapId, int defaultTileX = 0, int defaultTileY = 0)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _mapId = mapId;
        Text = "Événements de la carte";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(760, 480);
        _lblMapId.Text = FormatMapId(mapId);
        _numTileX.Value = defaultTileX;
        _numTileY.Value = defaultTileY;

        _lvCatalog.ShowItemToolTips = true;
        _lvCatalog.Columns.Add("Nom", 240);
        _lvCatalog.Columns.Add("Identifiant", 180);
        _lvCatalog.Columns.Add("Pages", 64);
        _lvCatalog.Columns.Add("Statut", 90);
        _lvCatalog.Columns.Add("Id", 90);

        _lvPlacements.ShowItemToolTips = true;
        _lvPlacements.Columns.Add("Nom", 200);
        _lvPlacements.Columns.Add("Déclencheur", 120);
        _lvPlacements.Columns.Add("X", 48);
        _lvPlacements.Columns.Add("Y", 48);
        _lvPlacements.Columns.Add("Identifiant", 150);
        _lvPlacements.Columns.Add("Id", 88);

        _cbTrigger.Items.AddRange(new object[]
        {
            "A · Action — interaction (touche)",
            "C · Contact — marche sur la tuile",
            "! · Automatique — une fois à l'activation",
            "P · Parallèle — tant que la page est active",
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
        catPanel.Controls.Add(new Label { Text = "Catalogue — choisissez le type, puis placez-le sur une tuile", Dock = DockStyle.Fill, AutoSize = true }, 0, 0);
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
        catNewRow.Controls.Add(_btnQuickNpc);
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
        placeMid.Controls.Add(new Label { Text = "Sur la carte", AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
        placeMid.Controls.Add(new Label { Text = "Filtre", AutoSize = true, Margin = new Padding(8, 4, 4, 0) });
        placeMid.Controls.Add(_txtFilterPlacements);
        placeMid.Controls.Add(_chkShowNames);
        placeMid.Controls.Add(_lblMarkerLegend);
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
        _btnQuickNpc.Click += (_, _) =>
        {
            QuickNpcRequested = true;
            Close();
        };
        _btnEditPages.Click += (_, _) => EditPagesSafe();
        _btnDeleteCatalogRow.Click += (_, _) => DeleteCatalogRowSafe();
        _txtFilterCatalog.TextChanged += (_, _) => RefreshFilteredLists();
        _txtFilterPlacements.TextChanged += (_, _) => RefreshFilteredLists();
        _lvPlacements.SelectedIndexChanged += (_, _) => NotifyPlacementHighlighted();
        _chkShowNames.CheckedChanged += (_, _) =>
        {
            if (_suppressNamesEvent)
            {
                return;
            }

            ShowEventNamesChanged?.Invoke(_chkShowNames.Checked);
        };
        Shown += (_, _) => ReloadSafe();
        ApplyEditorChrome();
    }

    private void ApplyEditorChrome()
    {
        EditorChrome.ApplyFormChrome(this);
        StyleChromeTree(this);
        _lblMarkerLegend.ForeColor = EditorChrome.LabelMuted;
    }

    private void StyleChromeTree(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            switch (child)
            {
                case ListView list:
                    EditorChrome.StyleSidebarListView(list);
                    list.Font = EditorChrome.BodyFont;
                    break;
                case Button button:
                    EditorChrome.StyleDialogButton(button, primary: ReferenceEquals(button, _btnPlace));
                    break;
                case CheckBox check:
                    check.ForeColor = EditorChrome.LabelPrimary;
                    check.BackColor = EditorChrome.SidebarBg;
                    break;
                case Label label:
                    label.ForeColor = EditorChrome.LabelPrimary;
                    if (label.BackColor == SystemColors.Control)
                    {
                        label.BackColor = Color.Transparent;
                    }

                    break;
                case TextBox textBox:
                    textBox.BackColor = EditorChrome.SidebarElevated;
                    textBox.ForeColor = EditorChrome.LabelPrimary;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox combo:
                    EditorChrome.StyleSidebarComboBox(combo);
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = EditorChrome.SidebarElevated;
                    numeric.ForeColor = EditorChrome.LabelPrimary;
                    break;
                case SplitContainer split:
                    split.BackColor = EditorChrome.CanvasInset;
                    split.Panel1.BackColor = EditorChrome.SidebarBg;
                    split.Panel2.BackColor = EditorChrome.SidebarBg;
                    break;
                case Panel panel:
                    panel.BackColor = EditorChrome.SidebarBg;
                    break;
            }

            if (child.HasChildren)
            {
                StyleChromeTree(child);
            }
        }
    }

    internal void SetShowEventNames(bool value)
    {
        if (_chkShowNames.Checked == value)
        {
            return;
        }

        _suppressNamesEvent = true;
        try
        {
            _chkShowNames.Checked = value;
        }
        finally
        {
            _suppressNamesEvent = false;
        }
    }

    /// <summary>Recharge la liste si la carte catalogue a changé.</summary>
    internal void EnsureMap(Guid mapId)
    {
        if (_mapId == mapId)
        {
            return;
        }

        SetMapId(mapId);
        ReloadSafe();
    }

    internal void SetMapContext(Guid mapId, int defaultTileX, int defaultTileY)
    {
        _numTileX.Value = defaultTileX;
        _numTileY.Value = defaultTileY;
        EnsureMap(mapId);
    }

    /// <summary>Sélectionne le placement correspondant au marqueur cliqué sur le canevas.</summary>
    internal bool TrySelectPlacement(int tileX, int tileY, string? placementKey)
    {
        if (TrySelectVisiblePlacement(tileX, tileY, placementKey))
        {
            return true;
        }

        if (_txtFilterPlacements.TextLength == 0)
        {
            return false;
        }

        _txtFilterPlacements.Clear();
        return TrySelectVisiblePlacement(tileX, tileY, placementKey);
    }

    private bool TrySelectVisiblePlacement(int tileX, int tileY, string? placementKey)
    {
        var keys = new List<MapEventMarkerLayout.MapEventPlacementListKey>(_lvPlacements.Items.Count);
        foreach (ListViewItem item in _lvPlacements.Items)
        {
            if (item.Tag is PlacementTag tag)
            {
                keys.Add(new MapEventMarkerLayout.MapEventPlacementListKey(tag.Key, tag.TileX, tag.TileY));
                continue;
            }

            var x = item.SubItems.Count > 2 && int.TryParse(item.SubItems[2].Text, out var tx) ? tx : int.MinValue;
            var y = item.SubItems.Count > 3 && int.TryParse(item.SubItems[3].Text, out var ty) ? ty : int.MinValue;
            keys.Add(new MapEventMarkerLayout.MapEventPlacementListKey(item.Text, x, y));
        }

        var index = MapEventMarkerLayout.FindPlacementIndex(keys, placementKey, tileX, tileY);
        if (index < 0 || index >= _lvPlacements.Items.Count)
        {
            return false;
        }

        _suppressHighlight = true;
        try
        {
            _lvPlacements.SelectedItems.Clear();
            var item = _lvPlacements.Items[index];
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            _lvPlacements.Select();
        }
        finally
        {
            _suppressHighlight = false;
        }

        return true;
    }

    private void NotifyPlacementHighlighted()
    {
        if (_suppressHighlight || !TryGetSingleSelectedGuid(_lvPlacements, out var id))
        {
            return;
        }

        foreach (var row in _placementRows)
        {
            if (row.Id != id)
            {
                continue;
            }

            PlacementHighlighted?.Invoke(row);
            return;
        }
    }
    /// <summary>L'utilisateur a demandé le raccourci PNJ rapide (la boîte se ferme).</summary>
    internal bool QuickNpcRequested { get; private set; }


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
                MessageBox.Show(this, "Sélectionnez une entrée du catalogue.", "Catalogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        PlacementsChanged?.Invoke();
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

                var title = MapEventMarkerLayout.FormatListTitle(row.DisplayName, row.Slug);
                var item = new ListViewItem(title)
                {
                    Tag = row.EventId,
                    ToolTipText = row.EventId.ToString("D"),
                };
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.PageCount.ToString());
                item.SubItems.Add(FormatPublishStatus(row.Status));
                item.SubItems.Add(row.EventId.ToString("N")[..8]);
                _lvCatalog.Items.Add(item);
            }

            _lvPlacements.Items.Clear();
            foreach (var row in _placementRows
                         .OrderBy(r => r.TileY)
                         .ThenBy(r => r.TileX)
                         .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var trigger = MapEventMarkerLayout.TriggerLabel(row.TriggerKind);
                if (pf.Length > 0)
                {
                    var blob =
                        $"{row.Id} {row.MapId} {row.EventDefinitionId} {row.TileX} {row.TileY} {row.Slug} {row.DisplayName} {row.TriggerKind} {trigger}";
                    if (!blob.Contains(pf, o))
                    {
                        continue;
                    }
                }

                var title = MapEventMarkerLayout.FormatListTitle(row.DisplayName, row.Slug);
                var item = new ListViewItem(title)
                {
                    Tag = new PlacementTag(row.Id, row.Id.ToString("D"), row.TileX, row.TileY),
                    ToolTipText = $"{title} · {trigger} ({row.TileX}, {row.TileY}){Environment.NewLine}{row.Id:D}",
                    ForeColor = MapEventMarkerColors.TriggerAccent(row.TriggerKind),
                };
                item.SubItems.Add(MapEventMarkerLayout.TriggerGlyph(row.TriggerKind) + "  " + trigger);
                item.SubItems.Add(row.TileX.ToString());
                item.SubItems.Add(row.TileY.ToString());
                item.SubItems.Add(row.Slug);
                item.SubItems.Add(row.Id.ToString("N")[..8]);
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

        var item = lv.SelectedItems[0];
        if (item.Tag is Guid id)
        {
            value = id;
            return id != Guid.Empty;
        }

        if (item.Tag is PlacementTag placement)
        {
            value = placement.Id;
            return value != Guid.Empty;
        }

        return Guid.TryParse(item.Text, out value) && value != Guid.Empty;
    }

    private static string FormatPublishStatus(ContentPublishStatus status) =>
        status == ContentPublishStatus.Published ? "Publié" : "Brouillon";

    private sealed class PlacementTag
    {
        public PlacementTag(Guid id, string key, int tileX, int tileY)
        {
            Id = id;
            Key = key;
            TileX = tileX;
            TileY = tileY;
        }

        public Guid Id { get; }

        public string Key { get; }

        public int TileX { get; }

        public int TileY { get; }
    }
}
