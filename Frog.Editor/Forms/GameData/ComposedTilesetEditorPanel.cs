using System.IO;

using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Tileset composé de tuiles choisies. Une feuille n’est pas importée ni découpée ici.
/// Les tuiles de contenu restent 48×48.
/// </summary>
public sealed class ComposedTilesetEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly ComposedTilesetWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _path = new() { Width = 280 };
    private readonly ListBox _tiles = new() { Width = 360, Height = 180, IntegralHeight = false };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly Button _btnAdd = new() { Text = "Ajouter une tuile…", AutoSize = true };
    private readonly Button _btnUp = new() { Text = "Monter", AutoSize = true };
    private readonly Button _btnDown = new() { Text = "Descendre", AutoSize = true };
    private readonly Button _btnRemove = new() { Text = "Retirer", AutoSize = true };
    private bool _suppressList;
    private bool _binding;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal long CurrentRevisionForTest => _session.CurrentRevision;

    internal long? PublishedRevisionForTest => _session.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => _session.CurrentStatus;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal string CapabilitiesLabelForTest => _capabilities.DisplayLabel;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal ListBox ListForTest => _list;

    internal ListBox TilesForTest => _tiles;

    internal Label ValidationForTest => _validation;

    internal int ChosenTileCountForTest => _session.Current?.Tiles.Count ?? 0;

    public ComposedTilesetEditorPanel(ComposedTilesetWorkspaceSession session, ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
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
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control c)
        {
            var r = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
            form.Controls.Add(c, 1, r);
        }

        var tileActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        tileActions.Controls.AddRange(new Control[] { _btnAdd, _btnUp, _btnDown, _btnRemove });
        var tileHost = new Panel { Width = 380, Height = 220 };
        _tiles.Dock = DockStyle.Fill;
        tileHost.Controls.Add(_tiles);

        Row("Nom", _name);
        Row("Chemin logique", _path);
        Row("Tuiles choisies", tileHost);
        Row("", tileActions);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Tuiles composées", _session.IsDirty))
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
        _path.TextChanged += (_, _) => Mark();
        _btnNew.Click += (_, _) =>
        {
            var id = Guid.NewGuid();
            _session.AdoptNewDraft(new ComposedTilesetDefinition
            {
                Id = id,
                Name = "Nouveau tileset",
                LogicalPath = $"tiles/composed/{id:N}.tileset",
            });
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
        _btnAdd.Click += (_, _) => AddTileFromDialog();
        _btnUp.Click += (_, _) => MoveSelected(-1);
        _btnDown.Click += (_, _) => MoveSelected(1);
        _btnRemove.Click += (_, _) => RemoveSelected();

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend : {_capabilities.DisplayLabel}");
    }

    /// <summary>Ajoute une tuile 48×48 déjà décodée (RGBA droit). Une feuille plus grande est refusée.</summary>
    internal bool TryAddStraightTileForTest(byte[] straightRgba, int width, int height, string? displayName, out string? error)
    {
        error = null;
        EnsureDraft();
        if (!TryBuildTile(straightRgba, width, height, displayName, out var tile, out error))
        {
            _validation.Text = error ?? string.Empty;
            return false;
        }

        _session.Current!.Tiles.Add(tile);
        _session.MarkDirty();
        BindTiles();
        LiveValidate();
        StatusChanged?.Invoke("Tuile ajoutée (non enregistré)");
        return true;
    }

    private void AddTileFromDialog()
    {
        EnsureDraft();
        var path = EditorTestHooks.OverrideImportSourcePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "PNG (*.png)|*.png",
                Title = "Choisir une tuile 48×48",
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            path = dialog.FileName;
        }

        byte[] png;
        try
        {
            png = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GameDataUiMessageBox.Show(this, ex.Message, "Tuile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TileAssetPngCodec.TryDecode(png, out var width, out var height, out var rgba))
        {
            GameDataUiMessageBox.Show(
                this,
                "PNG illisible. Choisissez une tuile 48×48, pas une feuille.",
                "Tuile",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var display = Path.GetFileNameWithoutExtension(path);
        if (!TryAddStraightTileForTest(rgba, width, height, display, out var error))
        {
            GameDataUiMessageBox.Show(this, error ?? "Tuile refusée.", "Tuile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool TryBuildTile(
        byte[] straightRgba,
        int width,
        int height,
        string? displayName,
        out ComposedTileRef tile,
        out string? error)
    {
        tile = new ComposedTileRef();
        if (width != TileAssetMetrics.TargetTileSizePixels || height != TileAssetMetrics.TargetTileSizePixels)
        {
            error = "Une tuile de contenu fait 48×48. Cette page ne découpe pas une feuille.";
            return false;
        }

        TileAsset asset;
        try
        {
            asset = TileAsset.FromStraightRgba(straightRgba);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            error = ex.Message;
            return false;
        }

        tile = new ComposedTileRef
        {
            TileAssetId = asset.Id.ToHex(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            NormalizedRgba = asset.NormalizedRgba,
        };
        error = null;
        return true;
    }

    private void EnsureDraft()
    {
        if (_session.Current is not null)
        {
            return;
        }

        var id = Guid.NewGuid();
        _session.AdoptNewDraft(new ComposedTilesetDefinition
        {
            Id = id,
            Name = "Nouveau tileset",
            LogicalPath = $"tiles/composed/{id:N}.tileset",
        });
        BindForm();
    }

    private void MoveSelected(int delta)
    {
        if (_session.Current is null)
        {
            return;
        }

        var index = _tiles.SelectedIndex;
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _session.Current.Tiles.Count)
        {
            return;
        }

        var tiles = _session.Current.Tiles;
        (tiles[index], tiles[target]) = (tiles[target], tiles[index]);
        _session.MarkDirty();
        BindTiles();
        _tiles.SelectedIndex = target;
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void RemoveSelected()
    {
        if (_session.Current is null || _tiles.SelectedIndex < 0 || _tiles.SelectedIndex >= _session.Current.Tiles.Count)
        {
            return;
        }

        _session.Current.Tiles.RemoveAt(_tiles.SelectedIndex);
        _session.MarkDirty();
        BindTiles();
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
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
            foreach (var entry in _session.Catalog)
            {
                _list.Items.Add(new CatalogItem(entry.TilesetId, $"{entry.Name} [{entry.Status}]"));
            }

            _suppressList = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
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
            _path.Text = definition.LogicalPath;
            _meta.Text =
                $"Id={definition.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
            BindTiles();
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private void BindTiles()
    {
        var selected = _tiles.SelectedIndex;
        _tiles.Items.Clear();
        if (_session.Current is null)
        {
            return;
        }

        for (var i = 0; i < _session.Current.Tiles.Count; i++)
        {
            var tile = _session.Current.Tiles[i];
            var label = string.IsNullOrWhiteSpace(tile.DisplayName) ? tile.TileAssetId : tile.DisplayName.Trim();
            if (label.Length > 48)
            {
                label = label[..48];
            }

            _tiles.Items.Add($"{i + 1}. {label}");
        }

        if (selected >= 0 && selected < _tiles.Items.Count)
        {
            _tiles.SelectedIndex = selected;
        }
    }

    private void ApplyFormToSession()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.Name = _name.Text.Trim();
        _session.Current.LogicalPath = _path.Text.Trim().Replace('\\', '/');
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
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveComposedTilesetResult.Success s:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={s.PublishedRevision}"
                        : $"Brouillon enregistré rev={s.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveComposedTilesetResult.ValidationFailed v:
                GameDataUiMessageBox.Show(this, v.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveComposedTilesetResult.Conflict c:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={c.CurrentRevision}).", "Conflit");
                break;
            case SaveComposedTilesetResult.NotDurable n:
                GameDataUiMessageBox.Show(this, n.Message, "Persistance");
                break;
            case SaveComposedTilesetResult.PersistenceFailed p:
                GameDataUiMessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteComposedTilesetResult.Success:
                StatusChanged?.Invoke("Supprimé");
                _tiles.Items.Clear();
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteComposedTilesetResult.NotFound:
                GameDataUiMessageBox.Show(this, "Tileset introuvable.");
                break;
            case DeleteComposedTilesetResult.PersistenceFailed p:
                GameDataUiMessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
