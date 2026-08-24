using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>Deux éditeurs liés : définitions de ressources et placements sur carte.</summary>
public sealed class ResourceAndSpawnEditorPanel : UserControl
{
    private readonly ResourceEditorPanel _resources;
    private readonly ResourceSpawnEditorPanel _spawns;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    public ResourceAndSpawnEditorPanel(
        ResourceWorkspaceSession resourceSession,
        ResourceSpawnWorkspaceSession spawnSession,
        IPublishedItemCatalog itemCatalog,
        IPublishedResourceCatalog resourceCatalog,
        IMapRepository mapRepository,
        ContentRepositoryCapabilities resourceCapabilities,
        ContentRepositoryCapabilities spawnCapabilities)
    {
        _resources = new ResourceEditorPanel(
            resourceSession,
            itemCatalog,
            resourceCapabilities)
        {
            Dock = DockStyle.Fill,
        };
        _spawns = new ResourceSpawnEditorPanel(
            spawnSession,
            mapRepository,
            resourceCatalog,
            spawnCapabilities)
        {
            Dock = DockStyle.Fill,
        };
        _resources.StatusChanged += message => StatusChanged?.Invoke(message);
        _spawns.StatusChanged += message => StatusChanged?.Invoke(message);

        var resourcesPage = new TabPage("Ressources");
        resourcesPage.Controls.Add(_resources);
        var spawnsPage = new TabPage("Spawns");
        spawnsPage.Controls.Add(_spawns);
        _tabs.TabPages.Add(resourcesPage);
        _tabs.TabPages.Add(spawnsPage);
        _tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (_tabs.SelectedIndex == 1)
            {
                await _spawns.InitializeAsync().ConfigureAwait(true);
            }
        };
        Controls.Add(_tabs);
    }

    public event Action<string>? StatusChanged;

    public bool IsDirty => _resources.IsDirty || _spawns.IsDirty;

    internal ResourceEditorPanel ResourcesPanelForTest => _resources;

    internal ResourceSpawnEditorPanel SpawnsPanelForTest => _spawns;

    internal TabControl TabsForTest => _tabs;

    public async Task InitializeAsync()
    {
        await _resources.InitializeAsync().ConfigureAwait(true);
        await _spawns.InitializeAsync().ConfigureAwait(true);
    }
}

public sealed class ResourceEditorPanel : UserControl
{
    private readonly ResourceWorkspaceSession _session;
    private readonly IPublishedItemCatalog _itemCatalog;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _description = new()
    {
        Width = 360,
        Height = 70,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly TextBox _spritePath = new() { Width = 300 };
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
    private readonly NumericUpDown _respawn = new()
    {
        Minimum = 0,
        Maximum = int.MaxValue,
        Width = 120,
    };
    private readonly ComboBox _tool = ChoiceCombo();
    private readonly ComboBox _yieldItem = ChoiceCombo();
    private readonly NumericUpDown _yieldQuantity = new()
    {
        Minimum = ResourceDefinition.MinYieldQuantity,
        Maximum = ResourceDefinition.MaxYieldQuantity,
        Value = 1,
        Width = 80,
    };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private bool _suppressList;
    private bool _binding;

    public bool IsDirty => _session.IsDirty;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal TextBox NameForTest => _name;

    internal TextBox SpritePathForTest => _spritePath;

    internal ListBox ListForTest => _list;

    internal AssetPreviewControl PreviewForTest => _preview;

    public ResourceEditorPanel(
        ResourceWorkspaceSession session,
        IPublishedItemCatalog itemCatalog,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _itemCatalog = itemCatalog;
        _capabilities = capabilities;
        _preview.AssetRoot = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();
        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(
                new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left },
                0,
                row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("Description", _description);
        Row("Chemin sprite", _spritePath);
        Row("Aperçu", _preview);
        Row("Réapparition (s)", _respawn);
        Row("Outil publié", _tool);
        Row("Objet produit", _yieldItem);
        Row("Quantité produite", _yieldQuantity);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
        buttons.Controls.AddRange(
            new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });
        Controls.Add(form);
        Controls.Add(buttons);
        Controls.Add(left);

        _search.TextChanged += async (_, _) =>
        {
            _session.SearchFilter = _search.Text;
            await RefreshListAsync().ConfigureAwait(true);
        };
        _statusFilter.SelectedIndexChanged += async (_, _) =>
        {
            _session.StatusFilter = StatusFromIndex(_statusFilter.SelectedIndex);
            await RefreshListAsync().ConfigureAwait(true);
        };
        _list.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogChoice choice)
            {
                return;
            }

            if (_session.IsDirty
                && MessageBox.Show(
                    this,
                    "Modifications non enregistrées. Continuer ?",
                    "Ressources",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            await _session.OpenAsync(choice.Id).ConfigureAwait(true);
            BindForm();
        };

        void Mark()
        {
            if (_binding)
            {
                return;
            }

            ApplyForm();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Ressource modifiée (non enregistrée)");
        }

        _name.TextChanged += (_, _) => Mark();
        _description.TextChanged += (_, _) => Mark();
        _spritePath.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _spritePath.Text.Trim();
        };
        _respawn.ValueChanged += (_, _) => Mark();
        _tool.SelectedIndexChanged += (_, _) => Mark();
        _yieldItem.SelectedIndexChanged += (_, _) => Mark();
        _yieldQuantity.ValueChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new ResourceDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouvelle ressource",
                SpriteLogicalPath = $"sprites/resources/new_{Guid.NewGuid():N}.png",
                YieldItemId = (_yieldItem.Items.Cast<ItemChoice>().FirstOrDefault())?.Id
                              ?? Guid.Empty,
                YieldQuantity = 1,
            });
            BindForm();
            StatusChanged?.Invoke("Nouveau brouillon de ressource");
        };
        _btnDup.Click += (_, _) =>
        {
            if (_session.Current is null)
            {
                return;
            }

            _session.DuplicateCurrent();
            BindForm();
            StatusChanged?.Invoke("Copie de ressource créée");
        };
        _btnSave.Click += async (_, _) =>
            await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true);
        _btnPublish.Click += async (_, _) =>
            await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true);
        _btnDelete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);

        _btnSave.Enabled = capabilities.AllowsSave;
        _btnPublish.Enabled = capabilities.AllowsSave;
        _btnDelete.Enabled = capabilities.AllowsSave;
    }

    public event Action<string>? StatusChanged;

    public async Task InitializeAsync()
    {
        await RefreshItemsAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend ressources : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshItemsAsync()
    {
        var items = await _itemCatalog.ListPublishedAsync().ConfigureAwait(true);
        var selectedTool = (_tool.SelectedItem as ItemChoice)?.Id;
        var selectedYield = (_yieldItem.SelectedItem as ItemChoice)?.Id;
        _binding = true;
        try
        {
            _tool.Items.Clear();
            _tool.Items.Add(new ItemChoice(null, "Aucun"));
            _yieldItem.Items.Clear();
            foreach (var item in items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                _tool.Items.Add(new ItemChoice(item.Id, item.Name));
                _yieldItem.Items.Add(new ItemChoice(item.Id, item.Name));
            }

            _tool.SelectedItem = _tool.Items.Cast<ItemChoice>()
                .FirstOrDefault(choice => choice.Id == selectedTool)
                ?? _tool.Items.Cast<ItemChoice>().First();
            _yieldItem.SelectedItem = _yieldItem.Items.Cast<ItemChoice>()
                .FirstOrDefault(choice => choice.Id == selectedYield)
                ?? _yieldItem.Items.Cast<ItemChoice>().FirstOrDefault();
        }
        finally
        {
            _binding = false;
        }
    }

    private async Task RefreshListAsync()
    {
        await _session.RefreshCatalogAsync().ConfigureAwait(true);
        _suppressList = true;
        _list.Items.Clear();
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogChoice(
                entry.ResourceId,
                $"{entry.Name} (respawn {entry.RespawnSeconds}s) [{entry.Status}]"));
        }

        _suppressList = false;
    }

    private void BindForm()
    {
        var definition = _session.Current;
        if (definition is null)
        {
            return;
        }

        _binding = true;
        try
        {
            _name.Text = definition.Name;
            _description.Text = definition.Description ?? string.Empty;
            _spritePath.Text = definition.SpriteLogicalPath;
            _preview.SetLogicalPathSilently(definition.SpriteLogicalPath);
            _respawn.Value = Math.Clamp(definition.RespawnSeconds, 0, int.MaxValue);
            SelectItem(_tool, definition.ToolItemId, optional: true);
            SelectItem(_yieldItem, definition.YieldItemId, optional: false);
            _yieldQuantity.Value = Math.Clamp(
                definition.YieldQuantity,
                ResourceDefinition.MinYieldQuantity,
                ResourceDefinition.MaxYieldQuantity);
            _meta.Text =
                $"Id={definition.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private void SelectItem(ComboBox combo, Guid? id, bool optional)
    {
        var choice = combo.Items.Cast<ItemChoice>().FirstOrDefault(item => item.Id == id);
        if (choice is null && id is Guid missing)
        {
            choice = new ItemChoice(missing, $"[introuvable] {missing:N}");
            combo.Items.Add(choice);
        }

        combo.SelectedItem = choice
                             ?? (optional
                                 ? combo.Items.Cast<ItemChoice>().FirstOrDefault(item => item.Id is null)
                                 : combo.Items.Cast<ItemChoice>().FirstOrDefault());
    }

    private void ApplyForm()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.Name = _name.Text.Trim();
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
        _session.Current.SpriteLogicalPath = _spritePath.Text.Trim().Replace('\\', '/');
        _session.Current.RespawnSeconds = (int)_respawn.Value;
        _session.Current.ToolItemId = (_tool.SelectedItem as ItemChoice)?.Id;
        _session.Current.YieldItemId = (_yieldItem.SelectedItem as ItemChoice)?.Id ?? Guid.Empty;
        _session.Current.YieldQuantity = (int)_yieldQuantity.Value;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyForm();
        _validation.Text = _session.Current.Validate(out var error) ? string.Empty : error;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyForm();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveResourceResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Ressource publiée rev={success.PublishedRevision}"
                        : $"Brouillon ressource enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveResourceResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveResourceResult.Conflict conflict:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={conflict.CurrentRevision}).", "Conflit");
                break;
            case SaveResourceResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveResourceResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteResourceResult.Success:
                StatusChanged?.Invoke("Ressource supprimée");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteResourceResult.Referenced referenced:
                GameDataUiMessageBox.Show(this, referenced.Error, "Référence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case DeleteResourceResult.NotFound:
                GameDataUiMessageBox.Show(this, "Ressource introuvable.");
                break;
            case DeleteResourceResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private static ComboBox ChoiceCombo() => new()
    {
        Width = 300,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private static ContentPublishStatus? StatusFromIndex(int index) => index switch
    {
        1 => ContentPublishStatus.Draft,
        2 => ContentPublishStatus.Published,
        _ => null,
    };

    private sealed record CatalogChoice(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ItemChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}

public sealed class ResourceSpawnEditorPanel : UserControl
{
    private readonly ResourceSpawnWorkspaceSession _session;
    private readonly IMapRepository _maps;
    private readonly IPublishedResourceCatalog _resources;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _mapFilter = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly ComboBox _resourceFilter = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly ComboBox _statusFilter = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly ComboBox _map = ChoiceCombo();
    private readonly ComboBox _resource = ChoiceCombo();
    private readonly NumericUpDown _tileX = CoordinateControl();
    private readonly NumericUpDown _tileY = CoordinateControl();
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private bool _suppressList;
    private bool _binding;

    public ResourceSpawnEditorPanel(
        ResourceSpawnWorkspaceSession session,
        IMapRepository maps,
        IPublishedResourceCatalog resources,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _maps = maps;
        _resources = resources;
        _capabilities = capabilities;
        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 320, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_mapFilter);
        left.Controls.Add(_resourceFilter);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(
                new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left },
                0,
                row);
            form.Controls.Add(control, 1, row);
        }

        Row("Carte", _map);
        Row("Ressource publiée", _resource);
        Row("Tuile X", _tileX);
        Row("Tuile Y", _tileY);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
        buttons.Controls.AddRange(
            new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });
        Controls.Add(form);
        Controls.Add(buttons);
        Controls.Add(left);

        _statusFilter.SelectedIndexChanged += async (_, _) =>
        {
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync().ConfigureAwait(true);
        };
        _mapFilter.SelectedIndexChanged += async (_, _) =>
        {
            _session.MapFilter = (_mapFilter.SelectedItem as EntityChoice)?.Id;
            await RefreshListAsync().ConfigureAwait(true);
        };
        _resourceFilter.SelectedIndexChanged += async (_, _) =>
        {
            _session.ResourceFilter = (_resourceFilter.SelectedItem as EntityChoice)?.Id;
            await RefreshListAsync().ConfigureAwait(true);
        };
        _list.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressList || _list.SelectedItem is not SpawnChoice choice)
            {
                return;
            }

            if (_session.IsDirty
                && MessageBox.Show(
                    this,
                    "Modifications non enregistrées. Continuer ?",
                    "Spawns de ressources",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            await _session.OpenAsync(choice.Id).ConfigureAwait(true);
            BindForm();
        };

        void Mark()
        {
            if (_binding)
            {
                return;
            }

            ApplyForm();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Spawn de ressource modifié (non enregistré)");
        }

        _map.SelectedIndexChanged += (_, _) => Mark();
        _resource.SelectedIndexChanged += (_, _) => Mark();
        _tileX.ValueChanged += (_, _) => Mark();
        _tileY.ValueChanged += (_, _) => Mark();
        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new ResourceSpawnDefinition
            {
                Id = Guid.NewGuid(),
                MapId = (_map.SelectedItem as EntityChoice)?.Id ?? Guid.Empty,
                ResourceId = (_resource.SelectedItem as EntityChoice)?.Id ?? Guid.Empty,
            });
            BindForm();
            StatusChanged?.Invoke("Nouveau brouillon de spawn");
        };
        _btnDup.Click += (_, _) =>
        {
            if (_session.Current is null)
            {
                return;
            }

            _session.DuplicateCurrent();
            BindForm();
            StatusChanged?.Invoke("Copie de spawn créée");
        };
        _btnSave.Click += async (_, _) =>
            await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true);
        _btnPublish.Click += async (_, _) =>
            await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true);
        _btnDelete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);

        _btnSave.Enabled = capabilities.AllowsSave;
        _btnPublish.Enabled = capabilities.AllowsSave;
        _btnDelete.Enabled = capabilities.AllowsSave;
    }

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal ComboBox MapFilterForTest => _mapFilter;

    internal ComboBox ResourceFilterForTest => _resourceFilter;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal ListBox ListForTest => _list;

    public async Task InitializeAsync()
    {
        await RefreshReferencesAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend spawns de ressources : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshReferencesAsync()
    {
        var mapId = (_map.SelectedItem as EntityChoice)?.Id;
        var resourceId = (_resource.SelectedItem as EntityChoice)?.Id;
        var mapFilterId = (_mapFilter.SelectedItem as EntityChoice)?.Id;
        var resourceFilterId = (_resourceFilter.SelectedItem as EntityChoice)?.Id;
        var maps = await _maps.ListSummariesAsync().ConfigureAwait(true);
        var resources = await _resources.ListPublishedAsync().ConfigureAwait(true);
        _binding = true;
        try
        {
            _map.Items.Clear();
            foreach (var entry in maps.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                _map.Items.Add(new EntityChoice(entry.MapId, entry.Name));
            }

            _resource.Items.Clear();
            foreach (var definition in resources.OrderBy(
                         definition => definition.Name,
                         StringComparer.OrdinalIgnoreCase))
            {
                _resource.Items.Add(new EntityChoice(definition.Id, definition.Name));
            }

            _mapFilter.Items.Clear();
            _mapFilter.Items.Add(new EntityChoice(Guid.Empty, "Toutes les cartes"));
            foreach (var entry in maps.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                _mapFilter.Items.Add(new EntityChoice(entry.MapId, entry.Name));
            }

            _resourceFilter.Items.Clear();
            _resourceFilter.Items.Add(new EntityChoice(Guid.Empty, "Toutes les ressources"));
            foreach (var definition in resources.OrderBy(
                         definition => definition.Name,
                         StringComparer.OrdinalIgnoreCase))
            {
                _resourceFilter.Items.Add(new EntityChoice(definition.Id, definition.Name));
            }

            _mapFilter.SelectedItem = _mapFilter.Items.Cast<EntityChoice>()
                .FirstOrDefault(choice => choice.Id == mapFilterId)
                ?? _mapFilter.Items[0];
            _resourceFilter.SelectedItem = _resourceFilter.Items.Cast<EntityChoice>()
                .FirstOrDefault(choice => choice.Id == resourceFilterId)
                ?? _resourceFilter.Items[0];
            _session.MapFilter = (_mapFilter.SelectedItem as EntityChoice)?.Id is Guid mapGuid
                                 && mapGuid != Guid.Empty
                ? mapGuid
                : null;
            _session.ResourceFilter = (_resourceFilter.SelectedItem as EntityChoice)?.Id is Guid resourceGuid
                                      && resourceGuid != Guid.Empty
                ? resourceGuid
                : null;

            _map.SelectedItem = _map.Items.Cast<EntityChoice>()
                .FirstOrDefault(choice => choice.Id == mapId)
                ?? _map.Items.Cast<EntityChoice>().FirstOrDefault();
            _resource.SelectedItem = _resource.Items.Cast<EntityChoice>()
                .FirstOrDefault(choice => choice.Id == resourceId)
                ?? _resource.Items.Cast<EntityChoice>().FirstOrDefault();
        }
        finally
        {
            _binding = false;
        }
    }

    private async Task RefreshListAsync()
    {
        await _session.RefreshCatalogAsync().ConfigureAwait(true);
        _suppressList = true;
        _list.Items.Clear();
        foreach (var entry in _session.Catalog)
        {
            var mapName = _map.Items.Cast<EntityChoice>()
                .FirstOrDefault(choice => choice.Id == entry.MapId)?.Label
                ?? entry.MapId.ToString("N");
            var resourceName = _resource.Items.Cast<EntityChoice>()
                .FirstOrDefault(choice => choice.Id == entry.ResourceId)?.Label
                ?? entry.ResourceId.ToString("N");
            _list.Items.Add(new SpawnChoice(
                entry.SpawnId,
                $"{mapName} ({entry.TileX},{entry.TileY}) — {resourceName} [{entry.Status}]"));
        }

        _suppressList = false;
    }

    private void BindForm()
    {
        var definition = _session.Current;
        if (definition is null)
        {
            return;
        }

        _binding = true;
        try
        {
            SelectEntity(_map, definition.MapId);
            SelectEntity(_resource, definition.ResourceId);
            _tileX.Value = Math.Clamp(definition.TileX, 0, int.MaxValue);
            _tileY.Value = Math.Clamp(definition.TileY, 0, int.MaxValue);
            _meta.Text =
                $"Id={definition.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private static void SelectEntity(ComboBox combo, Guid id)
    {
        var choice = combo.Items.Cast<EntityChoice>().FirstOrDefault(item => item.Id == id);
        if (choice is null && id != Guid.Empty)
        {
            choice = new EntityChoice(id, $"[introuvable] {id:N}");
            combo.Items.Add(choice);
        }

        combo.SelectedItem = choice ?? combo.Items.Cast<EntityChoice>().FirstOrDefault();
    }

    private void ApplyForm()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.MapId = (_map.SelectedItem as EntityChoice)?.Id ?? Guid.Empty;
        _session.Current.ResourceId = (_resource.SelectedItem as EntityChoice)?.Id ?? Guid.Empty;
        _session.Current.TileX = (int)_tileX.Value;
        _session.Current.TileY = (int)_tileY.Value;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyForm();
        _validation.Text = _session.Current.Validate(out var error) ? string.Empty : error;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyForm();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveResourceSpawnResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Spawn publié rev={success.PublishedRevision}"
                        : $"Brouillon spawn enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveResourceSpawnResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveResourceSpawnResult.Conflict conflict:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={conflict.CurrentRevision}).", "Conflit");
                break;
            case SaveResourceSpawnResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveResourceSpawnResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteResourceSpawnResult.Success:
                StatusChanged?.Invoke("Spawn supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteResourceSpawnResult.NotFound:
                GameDataUiMessageBox.Show(this, "Spawn de ressource introuvable.");
                break;
            case DeleteResourceSpawnResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private static ComboBox ChoiceCombo() => new()
    {
        Width = 300,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private static NumericUpDown CoordinateControl() => new()
    {
        Minimum = 0,
        Maximum = int.MaxValue,
        Width = 120,
    };

    private sealed record SpawnChoice(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record EntityChoice(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
