using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Shell « Données de jeu » : navigation catégories + éditeurs Phase 6.
/// Accès DB uniquement via ports Application.
/// </summary>
public sealed class GameDataForm : Form
{
    private readonly ListBox _categoryList = new() { Dock = DockStyle.Fill };
    private readonly Panel _host = new() { Dock = DockStyle.Fill };
    private readonly TilesetEditorPanel _tilesets;
    private readonly NpcEditorPanel _npcs;
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };

    public GameDataForm()
    {
        Text = "Données de jeu";
        Width = 960;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        var tilesetBundle = EditorTilesetRepositoryFactory.CreateBundle();
        _tilesets = new TilesetEditorPanel(
            new TilesetWorkspaceSession(tilesetBundle.Repository),
            tilesetBundle.Capabilities);
        _tilesets.StatusChanged += msg => _status.Text = msg;
        var npcBundle = EditorNpcRepositoryFactory.CreateBundle();
        _npcs = new NpcEditorPanel(
            new NpcWorkspaceSession(npcBundle.Repository),
            npcBundle.Capabilities);
        _npcs.StatusChanged += msg => _status.Text = msg;

        _categoryList.Items.AddRange(new object[]
        {
            "Tilesets",
            "NPCs / monstres",
            "Objets (Phase 6 — à venir)",
            "Sorts / compétences (Phase 6 — à venir)",
            "Classes (Phase 6 — à venir)",
            "Boutiques (Phase 6 — à venir)",
            "Ressources / spawns (Phase 6 — à venir)",
        });
        _categoryList.SelectedIndex = 0;
        _categoryList.SelectedIndexChanged += (_, _) => ShowCategory();

        var left = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(4) };
        left.Controls.Add(_categoryList);
        Controls.Add(_host);
        Controls.Add(_status);
        Controls.Add(left);

        ShowCategory();
        FormClosing += GameDataForm_FormClosing;
        Load += async (_, _) =>
        {
            await _tilesets.InitializeAsync().ConfigureAwait(true);
            await _npcs.InitializeAsync().ConfigureAwait(true);
        };
    }

    private void ShowCategory()
    {
        _host.Controls.Clear();
        if (_categoryList.SelectedIndex == 0)
        {
            _tilesets.Dock = DockStyle.Fill;
            _host.Controls.Add(_tilesets);
        }
        else if (_categoryList.SelectedIndex == 1)
        {
            _npcs.Dock = DockStyle.Fill;
            _host.Controls.Add(_npcs);
        }
        else
        {
            _host.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Cette catégorie sera livrée dans une prochaine tranche Phase 6.",
            });
        }
    }

    private void GameDataForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_tilesets.IsDirty || _npcs.IsDirty)
        {
            var r = MessageBox.Show(
                this,
                "Modifications non enregistrées. Fermer quand même ?",
                "Données de jeu",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (r != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        }
    }
}

/// <summary>Liste + formulaire tileset (brouillon / publication).</summary>
public sealed class TilesetEditorPanel : UserControl
{
    private readonly TilesetWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _path = new() { Width = 280 };
    private readonly NumericUpDown _tileSize = new() { Minimum = 8, Maximum = 256, Value = 32, Width = 80 };
    private readonly NumericUpDown _width = new() { Minimum = 8, Maximum = 8192, Value = 32, Width = 80 };
    private readonly NumericUpDown _height = new() { Minimum = 8, Maximum = 8192, Value = 32, Width = 80 };
    private readonly TextBox _sha = new() { Width = 280 };
    private readonly NumericUpDown _palette = new() { Minimum = 0, Maximum = 99999, Value = 0, Width = 80 };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private bool _suppressList;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    public TilesetEditorPanel(TilesetWorkspaceSession session, ContentRepositoryCapabilities capabilities)
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
        Row("Taille tuile", _tileSize);
        Row("Largeur px", _width);
        Row("Hauteur px", _height);
        Row("SHA-256", _sha);
        Row("Palette éditeur", _palette);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

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
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync().ConfigureAwait(true);
        };
        _list.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (_session.IsDirty)
            {
                var r = MessageBox.Show(
                    this,
                    "Modifications non enregistrées. Continuer ?",
                    "Tilesets",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (r != DialogResult.Yes)
                {
                    return;
                }
            }

            await _session.OpenAsync(item.Id).ConfigureAwait(true);
            BindForm();
        };

        void Mark()
        {
            ApplyFormToSession();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        _name.TextChanged += (_, _) => Mark();
        _path.TextChanged += (_, _) => Mark();
        _tileSize.ValueChanged += (_, _) => Mark();
        _width.ValueChanged += (_, _) => Mark();
        _height.ValueChanged += (_, _) => Mark();
        _sha.TextChanged += (_, _) => Mark();
        _palette.ValueChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            var def = new TilesetDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau tileset",
                LogicalPath = $"tiles/new_{Guid.NewGuid():N}.png",
                TileSizePixels = 32,
                WidthPixels = 32,
                HeightPixels = 32,
                Sha256Hex = new string('0', 64),
            };
            _session.AdoptNewDraft(def);
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
        _btnSave.Click += async (_, _) => await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true);
        _btnPublish.Click += async (_, _) => await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true);
        _btnDelete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);

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

    private async Task RefreshListAsync()
    {
        await _session.RefreshCatalogAsync().ConfigureAwait(true);
        _suppressList = true;
        _list.Items.Clear();
        foreach (var e in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(e.TilesetId, $"{e.Name} [{e.Status}]"));
        }

        _suppressList = false;
    }

    private void BindForm()
    {
        var d = _session.Current;
        if (d is null)
        {
            return;
        }

        _name.Text = d.Name;
        _path.Text = d.LogicalPath;
        _tileSize.Value = Math.Clamp(d.TileSizePixels, 8, 256);
        _width.Value = Math.Clamp(d.WidthPixels, 8, 8192);
        _height.Value = Math.Clamp(d.HeightPixels, 8, 8192);
        _sha.Text = d.Sha256Hex;
        _palette.Value = d.EditorPaletteId ?? 0;
        _meta.Text =
            $"Id={d.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
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
        _session.Current.TileSizePixels = (int)_tileSize.Value;
        _session.Current.WidthPixels = (int)_width.Value;
        _session.Current.HeightPixels = (int)_height.Value;
        _session.Current.Sha256Hex = _sha.Text.Trim();
        _session.Current.EditorPaletteId = _palette.Value <= 0 ? null : (int)_palette.Value;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = _session.Current.Validate(out var err) ? string.Empty : err;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveTilesetResult.Success s:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={s.PublishedRevision}"
                        : $"Brouillon enregistré rev={s.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveTilesetResult.ValidationFailed v:
                MessageBox.Show(this, v.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveTilesetResult.Conflict c:
                MessageBox.Show(this, $"Conflit de révision (courante={c.CurrentRevision}).", "Conflit");
                break;
            case SaveTilesetResult.NotDurable n:
                MessageBox.Show(this, n.Message, "Persistance");
                break;
            case SaveTilesetResult.PersistenceFailed p:
                MessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteTilesetResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteTilesetResult.Referenced r:
                MessageBox.Show(this, r.Error, "Référence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case DeleteTilesetResult.NotFound:
                MessageBox.Show(this, "Tileset introuvable.");
                break;
            case DeleteTilesetResult.PersistenceFailed p:
                MessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}

/// <summary>Liste + formulaire NPC/monstre (brouillon / publication).</summary>
public sealed class NpcEditorPanel : UserControl
{
    private readonly NpcWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly ComboBox _kind = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _spritePath = new() { Width = 280 };
    private readonly NumericUpDown _level = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 80 };
    private readonly TextBox _notes = new() { Width = 360, Height = 90, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly NumericUpDown _alias = new() { Minimum = 0, Maximum = 99999, Value = 0, Width = 80 };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private bool _suppressList;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    public NpcEditorPanel(NpcWorkspaceSession session, ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _kind.Items.AddRange(new object[] { NpcKind.Npc, NpcKind.Monster });
        _kind.SelectedIndex = 0;

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
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("Type", _kind);
        Row("Chemin sprite", _spritePath);
        Row("Niveau", _level);
        Row("Notes", _notes);
        Row("Alias éditeur", _alias);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

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
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync().ConfigureAwait(true);
        };
        _list.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (_session.IsDirty)
            {
                var result = MessageBox.Show(
                    this,
                    "Modifications non enregistrées. Continuer ?",
                    "NPCs / monstres",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            await _session.OpenAsync(item.Id).ConfigureAwait(true);
            BindForm();
        };

        void Mark()
        {
            ApplyFormToSession();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        _name.TextChanged += (_, _) => Mark();
        _kind.SelectedIndexChanged += (_, _) => Mark();
        _spritePath.TextChanged += (_, _) => Mark();
        _level.ValueChanged += (_, _) => Mark();
        _notes.TextChanged += (_, _) => Mark();
        _alias.ValueChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new NpcDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau NPC",
                Kind = NpcKind.Npc,
                SpriteLogicalPath = $"sprites/npcs/new_{Guid.NewGuid():N}.png",
                Level = 1,
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
        _btnSave.Click += async (_, _) => await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true);
        _btnPublish.Click += async (_, _) => await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true);
        _btnDelete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend NPC : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshListAsync()
    {
        await _session.RefreshCatalogAsync().ConfigureAwait(true);
        _suppressList = true;
        _list.Items.Clear();
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.NpcId,
                $"{entry.Name} ({entry.Kind}, niv. {entry.Level}) [{entry.Status}]"));
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

        _name.Text = definition.Name;
        _kind.SelectedItem = definition.Kind;
        _spritePath.Text = definition.SpriteLogicalPath;
        _level.Value = Math.Clamp(definition.Level, 1, 99);
        _notes.Text = definition.Notes ?? string.Empty;
        _alias.Value = definition.EditorAliasId ?? 0;
        _meta.Text =
            $"Id={definition.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
        LiveValidate();
    }

    private void ApplyFormToSession()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.Name = _name.Text.Trim();
        _session.Current.Kind = _kind.SelectedItem is NpcKind kind ? kind : NpcKind.Npc;
        _session.Current.SpriteLogicalPath = _spritePath.Text.Trim().Replace('\\', '/');
        _session.Current.Level = (int)_level.Value;
        _session.Current.Notes = string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim();
        _session.Current.EditorAliasId = _alias.Value <= 0 ? null : (int)_alias.Value;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = _session.Current.Validate(out var error) ? string.Empty : error;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveNpcResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveNpcResult.ValidationFailed validation:
                MessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveNpcResult.Conflict conflict:
                MessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveNpcResult.NotDurable notDurable:
                MessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveNpcResult.PersistenceFailed persistence:
                MessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteNpcResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteNpcResult.Referenced referenced:
                MessageBox.Show(
                    this,
                    referenced.Error,
                    "Référence",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case DeleteNpcResult.NotFound:
                MessageBox.Show(this, "NPC introuvable.");
                break;
            case DeleteNpcResult.PersistenceFailed persistence:
                MessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
