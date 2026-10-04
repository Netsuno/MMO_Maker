using System.IO;
using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Catalogue des objets de carte (props placables). Distinct de la page Objets (inventaire).
/// </summary>
public sealed class MapObjectEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly MapObjectWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _path = new() { Width = 280 };
    private readonly TextBox _placementId = new() { Width = 280 };
    private readonly NumericUpDown _footprintW = new() { Minimum = 1, Maximum = 32, Value = 1, Width = 80 };
    private readonly NumericUpDown _footprintH = new() { Minimum = 1, Maximum = 32, Value = 1, Width = 80 };
    private readonly NumericUpDown _width = new() { Minimum = 1, Maximum = 8192, Value = 32, Width = 80 };
    private readonly NumericUpDown _height = new() { Minimum = 1, Maximum = 8192, Value = 32, Width = 80 };
    private readonly TextBox _sha = new() { Width = 280 };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly Button _btnImport = new() { Text = "Importer…", AutoSize = true };
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
    private bool _suppressList;
    private bool _binding;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal long CurrentRevisionForTest => _session.CurrentRevision;

    internal long? PublishedRevisionForTest => _session.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => _session.CurrentStatus;

    internal string? PlacementIdForTest => _session.Current?.PlacementId;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal string CapabilitiesLabelForTest => _capabilities.DisplayLabel;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal Button BtnImportForTest => _btnImport;

    internal TextBox NameForTest => _name;

    internal TextBox PathForTest => _path;

    internal TextBox PlacementIdBoxForTest => _placementId;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal AssetPreviewControl PreviewForTest => _preview;

    public MapObjectEditorPanel(MapObjectWorkspaceSession session, ContentRepositoryCapabilities capabilities)
    {
        _session = session;
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
            AutoSize = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control c)
        {
            var r = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
            form.Controls.Add(c, 1, r);
        }

        Row("Nom", _name);
        Row("Chemin logique", _path);
        Row("Identifiant", _placementId);
        Row("Aperçu", _preview);
        Row("Largeur tuiles", _footprintW);
        Row("Hauteur tuiles", _footprintH);
        Row("Largeur px", _width);
        Row("Hauteur px", _height);
        Row("SHA-256", _sha);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { _btnNew, _btnDup, _btnImport, _btnSave, _btnPublish, _btnDelete });

        Controls.Add(form);
        Controls.Add(buttons);
        Controls.Add(left);

        _search.TextChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _session.SearchFilter = _search.Text;
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _statusFilter.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _list.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async _ =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Objets de carte", _session.IsDirty))
            {
                GameDataListNavigation.RevertListSelection(
                    _list,
                    ref _suppressList,
                    _session.CurrentId,
                    listItem => ((CatalogItem)listItem).Id);
                return;
            }

            await _session.OpenAsync(item.Id).ConfigureAwait(true);
            BindForm();
        }, "refresh");

        void Mark()
        {
            if (_binding)
            {
                return;
            }

            ApplyFormToSession();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        _name.TextChanged += (_, _) => Mark();
        _path.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _path.Text.Trim();
        };
        _placementId.TextChanged += (_, _) => Mark();
        _footprintW.ValueChanged += (_, _) => Mark();
        _footprintH.ValueChanged += (_, _) => Mark();
        _width.ValueChanged += (_, _) => Mark();
        _height.ValueChanged += (_, _) => Mark();
        _sha.TextChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            var id = Guid.NewGuid();
            _session.AdoptNewDraft(CreateBlank(id));
            BindForm();
            StatusChanged?.Invoke("Nouveau brouillon");
        };
        _btnDup.Click += (_, _) =>
        {
            if (_session.Current is null)
            {
                return;
            }

            _session.DuplicateCurrent();
            BindForm();
            StatusChanged?.Invoke("Copie créée");
        };
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true), "publish");
        _btnDelete.Click += (_, _) => _ = _lifecycle.RunAsync(async _ => await DeleteAsync().ConfigureAwait(true), "delete");
        _btnImport.Click += (_, _) => ImportAsset();

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
    }

    internal void ImportAssetFromPathForTest(string sourcePath)
    {
        EditorTestHooks.OverrideImportSourcePath = sourcePath;
        try
        {
            ImportAsset();
        }
        finally
        {
            EditorTestHooks.OverrideImportSourcePath = null;
        }
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend : {_capabilities.DisplayLabel}");
    }

    private static MapObjectDefinition CreateBlank(Guid id) => new()
    {
        Id = id,
        Name = "Nouvel objet",
        LogicalPath = $"prefabs/{MapObjectDefinition.CreatePlacementId(id)}.png",
        PlacementId = MapObjectDefinition.CreatePlacementId(id),
        FootprintWidthTiles = 1,
        FootprintHeightTiles = 1,
        WidthPixels = 32,
        HeightPixels = 32,
        Sha256Hex = new string('0', 64),
    };

    private void ImportAsset()
    {
        if (_session.Current is null)
        {
            _session.AdoptNewDraft(CreateBlank(Guid.NewGuid()));
            BindForm();
        }

        if (!GameDataAssetImport.TryPickAndImport(this, ProjectAssetKind.Prefabs, out var imported))
        {
            if (!string.IsNullOrWhiteSpace(imported.Error) && imported.Error != "Annulé.")
            {
                GameDataUiMessageBox.Show(this, imported.Error, "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        _path.Text = imported.LogicalPath ?? _path.Text;
        if (!string.IsNullOrWhiteSpace(imported.Sha256Hex))
        {
            _sha.Text = imported.Sha256Hex;
        }

        if (imported.WidthPixels > 0)
        {
            _width.Value = Math.Clamp(imported.WidthPixels, _width.Minimum, _width.Maximum);
        }

        if (imported.HeightPixels > 0)
        {
            _height.Value = Math.Clamp(imported.HeightPixels, _height.Minimum, _height.Maximum);
        }

        _preview.LogicalPath = _path.Text.Trim();
        ApplyFormToSession();
        if (!string.IsNullOrWhiteSpace(imported.AbsolutePath))
        {
            TryAttachPngBytes(_session.Current, imported.AbsolutePath);
        }

        _session.MarkDirty();
        LiveValidate();
        StatusChanged?.Invoke("Asset importé (non enregistré)");
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        try
        {
            await _session.RefreshCatalogAsync(ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            _suppressList = true;
            _list.Items.Clear();
            foreach (var e in _session.Catalog)
            {
                _list.Items.Add(new CatalogItem(e.MapObjectId, $"{e.Name} [{e.Status}]"));
            }

            _suppressList = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private void BindForm()
    {
        var d = _session.Current;
        if (d is null)
        {
            return;
        }

        _binding = true;
        try
        {
            _name.Text = d.Name;
            _path.Text = d.LogicalPath;
            _placementId.Text = d.PlacementId;
            _footprintW.Value = Math.Clamp(d.FootprintWidthTiles, (int)_footprintW.Minimum, (int)_footprintW.Maximum);
            _footprintH.Value = Math.Clamp(d.FootprintHeightTiles, (int)_footprintH.Minimum, (int)_footprintH.Maximum);
            _width.Value = Math.Clamp(d.WidthPixels, (int)_width.Minimum, (int)_width.Maximum);
            _height.Value = Math.Clamp(d.HeightPixels, (int)_height.Minimum, (int)_height.Maximum);
            _sha.Text = d.Sha256Hex;
            _preview.SetLogicalPathSilently(d.LogicalPath);
            _meta.Text =
                $"Id={d.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private void ApplyFormToSession()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.Name = _name.Text.Trim();
        _session.Current.LogicalPath = _path.Text.Trim().Replace('\\', '/');
        _session.Current.PlacementId = _placementId.Text.Trim();
        _session.Current.FootprintWidthTiles = (int)_footprintW.Value;
        _session.Current.FootprintHeightTiles = (int)_footprintH.Value;
        _session.Current.WidthPixels = (int)_width.Value;
        _session.Current.HeightPixels = (int)_height.Value;
        _session.Current.Sha256Hex = _sha.Text.Trim();
    }

    private static void TryAttachPngBytesFromAssetRoot(MapObjectDefinition? definition)
    {
        if (definition is null || string.IsNullOrWhiteSpace(definition.LogicalPath))
        {
            return;
        }

        var root = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();
        var resolved = ProjectAssetPathResolver.TryResolve(root, definition.LogicalPath);
        if (resolved.Status != ProjectAssetPathResolver.ResolveStatus.Success
            || string.IsNullOrWhiteSpace(resolved.AbsolutePath))
        {
            return;
        }

        TryAttachPngBytes(definition, resolved.AbsolutePath);
    }

    private static void TryAttachPngBytes(MapObjectDefinition? definition, string absolutePath)
    {
        if (definition is null)
        {
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(absolutePath);
            if (bytes.Length == 0)
            {
                return;
            }

            definition.PngBytes = bytes;
            definition.Sha256Hex = MapObjectDefinition.ComputeSha256Hex(bytes);
        }
        catch
        {
            // fichier optionnel
        }
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = _session.Current.Validate(out var err) ? string.Empty : err ?? string.Empty;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        TryAttachPngBytesFromAssetRoot(_session.Current);
        if (_session.Current is not null && !string.IsNullOrWhiteSpace(_session.Current.Sha256Hex))
        {
            _sha.Text = _session.Current.Sha256Hex;
        }

        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveMapObjectResult.Success s:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={s.PublishedRevision}"
                        : $"Brouillon enregistré rev={s.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveMapObjectResult.ValidationFailed v:
                GameDataUiMessageBox.Show(this, v.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveMapObjectResult.Conflict c:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={c.CurrentRevision}).", "Conflit");
                break;
            case SaveMapObjectResult.NotDurable n:
                GameDataUiMessageBox.Show(this, n.Message, "Persistance");
                break;
            case SaveMapObjectResult.PersistenceFailed p:
                GameDataUiMessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteMapObjectResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteMapObjectResult.NotFound:
                GameDataUiMessageBox.Show(this, "Objet de carte introuvable.");
                break;
            case DeleteMapObjectResult.PersistenceFailed p:
                GameDataUiMessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
